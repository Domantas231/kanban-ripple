# Kanban Ripple

A full-stack Kanban application.

- **Backend**: ASP.NET Core 8 Web API (PostgreSQL, SignalR, JWT auth)
- **Frontend**: React 18 + Vite + TypeScript (MUI, TanStack Query/Router, Zustand)
- **Infrastructure locally**: PostgreSQL + MinIO via Docker Compose
- **E2E**: Playwright

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/) and npm
- [Docker](https://www.docker.com/) (for PostgreSQL and MinIO)

## 1. Start infrastructure (PostgreSQL + MinIO)

From the project root:

```bash
docker compose up -d
```

This starts:

- PostgreSQL on `localhost:5432` (db: `kanban_db`, user: `kanban_user`, password: `kanban_password`)
- MinIO on `localhost:9000` (console at `localhost:9001`, user/pass: `minioadmin`/`minioadmin`)

## 2. Run the backend

```bash
cd backend/Kanban.Api/
dotnet restore
dotnet run
```

The API will be available at <http://localhost:5231>. 

## 3. Run the frontend

In a new terminal:

```bash
cd frontend
npm install
npm run dev
```

The app will be available at <http://localhost:5173>.