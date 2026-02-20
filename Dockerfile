# =========================
# Build stage: restore & publish .NET API
# =========================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
ARG CI
ENV CI=${CI}

# Copy only csproj and restore first
COPY src/Arcus.ClamAV/Arcus.ClamAV.csproj Arcus.ClamAV/
RUN dotnet restore Arcus.ClamAV/Arcus.ClamAV.csproj

# Copy everything else and publish
COPY src/Arcus.ClamAV/ Arcus.ClamAV/
RUN dotnet publish Arcus.ClamAV/Arcus.ClamAV.csproj -c Release -p:CI=true -o /app/publish /p:UseAppHost=false

# =========================
# Final stage: ASP.NET runtime + ClamAV
# =========================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
ENV ASPNETCORE_URLS=http://0.0.0.0:8080 \
CLAMD_HOST=127.0.0.1 \
CLAMD_PORT=3310 \
MAX_FILE_SIZE_MB=200 \
FRESHCLAM_DELAY_SECS=0


# Install ClamAV packages
RUN apt-get update \
 && apt-get upgrade -y \
 && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
    clamav clamav-daemon clamav-freshclam ca-certificates curl tini netcat-openbsd sudo \
 && apt-get install --reinstall -y ca-certificates \
 && update-ca-certificates --fresh \
 && apt-get autoremove -y \
 && rm -rf /var/lib/apt/lists/*

# Create app user and configure permissions
RUN mkdir -p /var/log/clamav /app && \
    chown -R clamav:clamav /var/log/clamav && \
    useradd -m -u 1001 appuser && \
    chown -R appuser:appuser /app && \
    usermod -aG clamav appuser && \
    chmod -R g+w /var/log/clamav /var/lib/clamav 2>/dev/null || true && \
    echo 'appuser ALL=(clamav) NOPASSWD: /usr/sbin/clamd, /usr/bin/freshclam' >> /etc/sudoers.d/clamav && \
    chmod 0440 /etc/sudoers.d/clamav

# Copy published API
WORKDIR /app
COPY --from=build --chown=appuser:appuser /app/publish ./


# Copy configs & scripts
WORKDIR /
COPY conf/clamd.conf /etc/clamav/clamd.conf
COPY conf/freshclam.conf /etc/clamav/freshclam.conf
COPY scripts/start.sh /start.sh
# Strip Windows line endings from all copied files
RUN sed -i 's/\r$//' /etc/clamav/clamd.conf /etc/clamav/freshclam.conf /start.sh && \
    chmod +x /start.sh && \
    chmod 644 /etc/clamav/clamd.conf /etc/clamav/freshclam.conf


# Persist virus DB across restarts (mount a volume in production)
VOLUME ["/var/lib/clamav"]


# Expose HTTP API
EXPOSE 8080


# Healthcheck: ensure clamd is up and the API responds
HEALTHCHECK --interval=30s --timeout=5s --start-period=60s --retries=5 \
CMD bash -lc 'echo PING | nc -w 2 127.0.0.1 3310 >/dev/null 2>&1 && curl -sf http://127.0.0.1:8080/healthz >/dev/null'
ENTRYPOINT ["/usr/bin/tini", "--"]
CMD ["/start.sh"]
