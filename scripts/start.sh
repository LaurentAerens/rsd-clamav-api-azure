#!/usr/bin/env bash
set -euo pipefail

# Optional delay before first update (useful in constrained networks)
: "${FRESHCLAM_DELAY_SECS:=0}"

# Update CA bundle (sometimes needed on slim images)
update-ca-certificates || true

# Ensure required directories and permissions
mkdir -p /var/lib/clamav /var/log/clamav
chown -R clamav:clamav /var/lib/clamav /var/log/clamav
chmod 755 /var/lib/clamav /var/log/clamav

# Prepare log file for clamd
touch /var/log/clamav/clamd.log
chown clamav:clamav /var/log/clamav/clamd.log

# Optional delay before initial DB update
if [[ "${FRESHCLAM_DELAY_SECS}" -gt 0 ]]; then
  echo "[start.sh] Sleeping ${FRESHCLAM_DELAY_SECS}s before initial freshclam..."
  sleep "${FRESHCLAM_DELAY_SECS}"
fi

# Run one foreground update to ensure databases exist
echo "[start.sh] Running initial freshclam update..."
su -s /bin/sh -c 'freshclam --stdout --verbose' clamav || true

# Start clamd in the foreground
echo "[start.sh] Starting clamd..."
su -s /bin/sh -c 'clamd --foreground=true --config-file=/etc/clamav/clamd.conf' clamav &
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

