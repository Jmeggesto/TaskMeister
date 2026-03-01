# Setup

## Running with Docker (recommended)

No .NET SDK or Node required — just [Docker Desktop](https://www.docker.com/products/docker-desktop/).

```bash
cp .env.example .env
# Open .env and set JWT_SECRET_KEY to a random string (32+ chars)
# e.g.  openssl rand -base64 32

docker compose up --build
```

The app is available at **http://localhost** once both containers are healthy.

To stop and remove containers (data in the SQLite volume is preserved):

```bash
docker compose down
```

---

## Running locally (development)

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10)
- [Node.js 22+](https://nodejs.org)

### Backend

```bash
cd backend/src/TaskMeisterAPI
dotnet user-secrets set "Jwt:SecretKey" "your-secret-key-here"
dotnet run
```

The API starts on **http://localhost:5276**. SQLite creates `todos.db` automatically on first run. Swagger UI is at **http://localhost:5276/swagger**.

### Frontend

```bash
cd frontend
npm install
npm run dev
```

The app opens on **http://localhost:5173** and proxies `/api` requests to the backend automatically (configured in `vite.config.ts`).

---

## Running tests

```bash
cd backend
dotnet test
```

---

## Environment variables

| Variable          | Required | Default            | Description                         |
|-------------------|-----------|--------------------|-------------------------------------|
| `JWT_SECRET_KEY`  | Yes       | —                  | JWT signing key (32+ chars)         |
| `FRONTEND_PORT`   | No        | `80`               | Host port to serve the frontend on  |
| `FRONTEND_ORIGIN` | No        | `http://localhost` | Origin added to the CORS allow-list |
