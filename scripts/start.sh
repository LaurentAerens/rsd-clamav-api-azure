#!/usr/bin/env bash
set -euo pipefail

# Optional delay before first update (useful in constrained networks)
: "${FRESHCLAM_DELAY_SECS:=0}"

run_as_clamav() {
  local cmd="$1"

  if [[ $EUID -ne 0 ]]; then
    /bin/sh -c "$cmd"
    return
  fi

  if command -v su >/dev/null 2>&1; then
    su -s /bin/sh -c "$cmd" clamav
  elif command -v runuser >/dev/null 2>&1; then
    runuser -u clamav -- /bin/sh -c "$cmd"
  else
    echo "[start.sh] Warning: no 'su' or 'runuser' found; running command as root"
    /bin/sh -c "$cmd"
  fi
}

# Update CA bundle (skip if not root, already done at build time)
if [[ $EUID -eq 0 ]]; then
  if command -v update-ca-certificates >/dev/null 2>&1; then
    update-ca-certificates || true
  elif command -v update-ca-trust >/dev/null 2>&1; then
    update-ca-trust extract || true
  else
    echo "[start.sh] No CA update command found, skipping"
  fi
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
if ! run_as_clamav '/usr/bin/freshclam --stdout --verbose'; then
  echo "[start.sh] Warning: freshclam update failed (including optional mirrors). Continuing startup with available signatures."
fi

# Start clamd in the foreground
echo "[start.sh] Starting clamd..."
run_as_clamav '/usr/sbin/clamd --foreground=true --config-file=/etc/clamav/clamd.conf' &
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

