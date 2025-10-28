# 🛡️ GovUK DfE ClamAV API Container

A self-contained Dockerised antivirus scanning service built on **ClamAV** with a lightweight **.NET 8 HTTP API** and **Swagger UI**.

This container runs the ClamAV engine and exposes a simple REST API for uploading and scanning files.  
It’s designed for local development, testing, and service integration — all without needing to install ClamAV manually.

---

## 🚀 Features

- 🧩 **All-in-one container** – ClamAV + REST API + Swagger.
- 🔄 **Automatic virus database updates** at start-up.
- 🧠 **Swagger UI** for easy manual testing (`/swagger`).
- 💬 **Endpoints** for scanning, health checks, and ClamAV version info.
- 💾 **Persistent database volume** so virus definitions are reused between restarts.
- 🔒 **Stateless HTTP interface** – ideal for CI pipelines or microservices.

---

## 📁 Project Structure

```
.
├── Dockerfile                # Builds .NET API + installs ClamAV
├── docker-compose.yml        # Runs the container locally
├── scripts/
│   └── start.sh              # Starts ClamAV & the API
├── conf/
│   ├── clamd.conf            # ClamAV daemon configuration
│   └── freshclam.conf        # Freshclam configuration
└── src/
    └── GovUK.Dfe.ClamAV/     # .NET 8 API project
```

---

## 🧰 Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (or Docker Engine + Compose plugin)
- Optional: `curl` for testing from the command line

---

## 🧱 Build and Run Locally

From the project root, run:

```bash
docker compose up -d --build
```

This will:
1. Build the image from the local `Dockerfile`
2. Start the container
3. Expose the API on **http://localhost:8080**

---

## 🌐 API Endpoints

| Method | Endpoint | Description |
|:-------|:----------|:-------------|
| `GET` | `/healthz` | Health check endpoint |
| `GET` | `/version` | Returns ClamAV engine & database version |
| `POST` | `/scan` | Upload a file to scan for viruses |
| `GET` | `/swagger` | OpenAPI documentation & interactive UI |

---

## 🔍 Test Examples

### 🧪 Via Swagger UI
Open **[http://localhost:8080/swagger](http://localhost:8080/swagger)** in your browser.  
You’ll see interactive endpoints — you can upload files directly under `/scan`.

---

### 🧾 Via `curl`

#### 1️⃣ Clean file
```bash
echo "hello" > clean.txt
curl -F "file=@clean.txt" http://localhost:8080/scan
```

Expected response:
```json
{ "status": "clean", "file": "clean.txt", "size": 6 }
```

#### 2️⃣ EICAR test virus
```bash
echo "X5O!P%@AP[4\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*" > eicar.txt
curl -F "file=@eicar.txt" http://localhost:8080/scan
```

Expected response:
```json
{ "status": "infected", "malware": "Eicar-Test-Signature", "file": "eicar.txt" }
```

💡 *Note: Your local antivirus may delete the EICAR test file immediately – that’s normal.*

---

## 🧩 ClamAV Version Endpoint

To check the currently loaded ClamAV engine and database version:

```bash
curl http://localhost:8080/version
```

Example:
```json
{
  "engine": "0.103.10",
  "database": "27806",
  "databaseDate": "Wed Oct 28 10:00:00 2025"
}
```

---

## 💾 Persistent Virus Database

The container uses a named Docker volume (`clamav-db`) to persist the ClamAV signature database.  
This prevents full re-downloads every time the container starts.

To clear it manually:
```bash
docker compose down -v
```

---

## ⚙️ Configuration

Environment variables can be overridden in `docker-compose.yml`:

| Variable | Default | Description |
|-----------|----------|-------------|
| `CLAMD_HOST` | `127.0.0.1` | ClamAV daemon host |
| `CLAMD_PORT` | `3310` | ClamAV daemon port |
| `MAX_FILE_SIZE_MB` | `200` | Max upload size |
| `ASPNETCORE_ENVIRONMENT` | `Production` | .NET environment name |

---

## 🧹 Stop and Clean Up

```bash
docker compose down
```

To also remove the virus DB volume:
```bash
docker compose down -v
```

---

## 🧠 Useful Notes

- Database updates happen automatically on container start.
- Logs for ClamAV and the API are visible via:
  ```bash
  docker logs -f clamav-api
  ```
- You can safely integrate this service with other apps via HTTP (no local ClamAV needed).

---

## 🧑‍💻 Contributing

1. Fork the repo and make changes in a new branch.  
2. Run `docker compose up -d --build` to test locally.  
3. Submit a PR with a clear description of your change.

---

## 📜 Licence

This project is provided under the MIT Licence.  
ClamAV is licensed separately under the [GNU General Public License (GPL)](https://www.clamav.net/about).
