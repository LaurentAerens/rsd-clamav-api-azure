#!/usr/bin/env bash
set -euo pipefail

# Optional delay before first update (useful in constrained networks)
: "${FRESHCLAM_DELAY_SECS:=0}"

# Update CA bundle (skip if not root, already done at build time)
if [[ $EUID -eq 0 ]]; then
  update-ca-certificates || true
else
  echo "[start.sh] Running as non-root, skipping CA certificate update"
fi

# Ensure required directories exist (may fail if not root, that's ok)
mkdir -p /var/lib/clamav /var/log/clamav 2>/dev/null || true

# Try to fix permissions if root, skip otherwise
if [[ $EUID -eq 0 ]]; then
  chown -R clamav:clamav /var/lib/clamav /var/log/clamav
  chmod 755 /var/lib/clamav /var/log/clamav
fi

# Optional delay before initial DB update
if [[ "${FRESHCLAM_DELAY_SECS}" -gt 0 ]]; then
  echo "[start.sh] Sleeping ${FRESHCLAM_DELAY_SECS}s before initial freshclam..."
  sleep "${FRESHCLAM_DELAY_SECS}"
fi

# Run one foreground update to ensure databases exist
echo "[start.sh] Running initial freshclam update..."
if [[ $EUID -eq 0 ]]; then
  su -s /bin/sh -c 'freshclam --stdout --verbose' clamav || true
else
  freshclam --stdout --verbose || true
fi

# Start clamd in the foreground
echo "[start.sh] Starting clamd..."
if [[ $EUID -eq 0 ]]; then
  su -s /bin/sh -c 'clamd --foreground=true --config-file=/etc/clamav/clamd.conf' clamav &
else
  clamd --foreground=true --config-file=/etc/clamav/clamd.conf &
fi
CLAMD_PID=$!

# Wait for clamd TCP socket to become ready
for i in {1..60}; do
  if echo PING | nc -w 2 127.0.0.1 3310 | grep -q PONG; then
    echo "[start.sh] clamd is ready."
    break
  fi
  echo "[start.sh] Waiting for clamd (attempt $i)..."
  sleep 2
  if ! kill -0 "$CLAMD_PID" 2>/dev/null; then
    echo "[start.sh] clamd process exited unexpectedly" >&2
    exit 1
  fi
done

# Start the .NET API
export ASPNETCORE_URLS
export CLAMD_HOST
export CLAMD_PORT
export MAX_FILE_SIZE_MB

cd /app
echo "[start.sh] Starting .NET API on ${ASPNETCORE_URLS}..."
exec dotnet Arcus.ClamAV.dll

