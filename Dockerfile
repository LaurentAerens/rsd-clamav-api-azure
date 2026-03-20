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
# Final stage: ASP.NET runtime + ClamAV (Alpine)
# =========================
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final

# Build arg for Swagger configuration (default: disabled)
ARG ENABLE_SWAGGER=false

ENV ASPNETCORE_URLS=http://0.0.0.0:8080 \
CLAMD_HOST=127.0.0.1 \
CLAMD_PORT=3310 \
MAX_FILE_SIZE_MB=200 \
FRESHCLAM_DELAY_SECS=0 \
FRESHCLAM_BACKGROUND_UPDATE=true \
FRESHCLAM_BLOCKING_ON_EMPTY_DB=true \
UPDATE_CA_CERTS_ON_START=false \
Swagger__Enabled=${ENABLE_SWAGGER}

# Install ClamAV packages
RUN apk update \
    && apk add --no-cache \
       bash \
       ca-certificates \
       clamav \
       clamav-daemon \
       netcat-openbsd \
       shadow \
       tini \
    && update-ca-certificates

# Preload virus databases at build time to speed up first container start.
# Ignore transient network failures; runtime startup logic still handles updates.
RUN freshclam --stdout --verbose || true

# Create app user and configure permissions
RUN mkdir -p /var/log/clamav /app \
    && useradd -m -u 1001 appuser \
    && chown -R appuser:appuser /app /var/log/clamav /var/lib/clamav \
    && chmod 755 /var/log/clamav /var/lib/clamav

# Copy published API
WORKDIR /app
COPY --from=build --chown=appuser:appuser /app/publish ./

# Copy configs & scripts
WORKDIR /
COPY conf/clamd.conf /etc/clamav/clamd.conf
COPY conf/freshclam.conf /etc/clamav/freshclam.conf
COPY scripts/start.sh /start.sh
# Strip Windows line endings from all copied files
RUN sed -i 's/\r$//' /etc/clamav/clamd.conf /etc/clamav/freshclam.conf /start.sh \
    && chmod +x /start.sh \
    && chmod 644 /etc/clamav/clamd.conf /etc/clamav/freshclam.conf

# Persist virus DB across restarts (mount a volume in production)
VOLUME ["/var/lib/clamav"]

# Expose HTTP API
EXPOSE 8080

# Healthcheck: ensure clamd is up and the API responds
HEALTHCHECK --interval=30s --timeout=5s --start-period=60s --retries=5 \
CMD bash -lc 'echo PING | nc -w 2 127.0.0.1 3310 >/dev/null 2>&1 && printf "GET /healthz HTTP/1.1\r\nHost: 127.0.0.1\r\nConnection: close\r\n\r\n" | nc -w 2 127.0.0.1 8080 | grep -q "200"'
USER appuser
ENTRYPOINT ["/sbin/tini", "--"]
CMD ["/start.sh"]
