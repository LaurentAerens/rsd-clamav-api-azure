#!/usr/bin/env bash
set -euo pipefail

# Optional delay before first update (useful in constrained networks)
: "${FRESHCLAM_DELAY_SECS:=0}"
: "${FRESHCLAM_BACKGROUND_UPDATE:=true}"
: "${FRESHCLAM_BLOCKING_ON_EMPTY_DB:=true}"
: "${UPDATE_CA_CERTS_ON_START:=false}"

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

has_local_db() {
  shopt -s nullglob
  local db_files=(/var/lib/clamav/*.cvd /var/lib/clamav/*.cld /var/lib/clamav/*.cud)
  shopt -u nullglob
  (( ${#db_files[@]} > 0 ))
}

# Update CA bundle only when explicitly requested (already done at build time)
if [[ "${UPDATE_CA_CERTS_ON_START,,}" == "true" ]]; then
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
fi

# Ensure required directories exist (may fail if not root, that's ok)
mkdir -p /var/lib/clamav /var/log/clamav 2>/dev/null || true

# Try to fix permissions if root, skip otherwise
if [[ $EUID -eq 0 ]]; then
  chown clamav:clamav /var/lib/clamav /var/log/clamav 2>/dev/null || true
  chmod 755 /var/lib/clamav /var/log/clamav
fi

# Fail fast with a clear message when the mounted database volume is not writable.
if ! touch /var/lib/clamav/.write_test 2>/dev/null; then
  echo "[start.sh] ERROR: Cannot write to /var/lib/clamav (running as UID $EUID)." >&2
  echo "[start.sh] For Azure Container Apps + Azure Files, verify volume mount permissions." >&2
  exit 1
fi
rm -f /var/lib/clamav/.write_test 2>/dev/null || true

# Optional delay before initial DB update
if [[ "${FRESHCLAM_DELAY_SECS}" -gt 0 ]]; then
  echo "[start.sh] Sleeping ${FRESHCLAM_DELAY_SECS}s before initial freshclam..."
  sleep "${FRESHCLAM_DELAY_SECS}"
fi

# Ensure signatures are present, but avoid blocking startup unnecessarily.
if has_local_db; then
  echo "[start.sh] Found existing ClamAV databases; skipping blocking freshclam."
  if [[ "${FRESHCLAM_BACKGROUND_UPDATE,,}" == "true" ]]; then
    echo "[start.sh] Starting background freshclam update..."
    (
      if ! run_as_clamav '/usr/bin/freshclam --stdout --verbose'; then
        echo "[start.sh] Warning: background freshclam update failed. Continuing with existing signatures."
      fi
    ) &
  fi
else
  if [[ "${FRESHCLAM_BLOCKING_ON_EMPTY_DB,,}" == "true" ]]; then
    echo "[start.sh] No local ClamAV databases found; running blocking freshclam update..."
    if ! run_as_clamav '/usr/bin/freshclam --stdout --verbose'; then
      echo "[start.sh] Warning: freshclam update failed on empty database state; clamd may fail to start."
    fi
  else
    echo "[start.sh] No local ClamAV databases found; blocking update disabled."
  fi
fi

# Start clamd in the foreground
CLAMD_START_TIME=$(date +%s%3N)
echo "[start.sh] Starting clamd..."
run_as_clamav '/usr/sbin/clamd --foreground=true --config-file=/etc/clamav/clamd.conf' &
CLAMD_PID=$!

# Wait for clamd TCP socket to become ready (optimized: faster retries)
# Reduced from 60x2s to 30x100ms = 3 second timeout, typically responds in <500ms
CLAMD_READY=false
for i in {1..30}; do
  if echo PING | nc -w 1 127.0.0.1 3310 >/dev/null 2>&1 && echo PING | nc -w 1 127.0.0.1 3310 | grep -q PONG; then
    CLAMD_READY=true
    CLAMD_END_TIME=$(date +%s%3N)
    CLAMD_ELAPSED=$((CLAMD_END_TIME - CLAMD_START_TIME))
    echo "[start.sh] clamd ready in ${CLAMD_ELAPSED}ms"
    break
  fi
  sleep 0.1
  if ! kill -0 "$CLAMD_PID" 2>/dev/null; then
    echo "[start.sh] clamd process exited unexpectedly" >&2
    exit 1
  fi
done

if ! $CLAMD_READY; then
  echo "[start.sh] Warning: clamd not responding after 3s, but starting .NET API anyway (will retry connections)"
fi

# Start the .NET API
export ASPNETCORE_URLS
export CLAMD_HOST
export CLAMD_PORT
export MAX_FILE_SIZE_MB

cd /app
echo "[start.sh] Starting .NET API on ${ASPNETCORE_URLS}..."
exec dotnet Arcus.ClamAV.dll

