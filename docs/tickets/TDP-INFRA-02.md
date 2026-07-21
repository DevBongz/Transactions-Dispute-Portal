# TDP-INFRA-02 — Docker Compose Stack (postgres, zookeeper, kafka, api, frontend)

**Jira summary:** Deliver the complete Docker Compose stack that brings the entire Transactions Dispute Portal online with a single `docker compose up --build`. This includes the `docker-compose.yml` with five services (postgres, zookeeper, kafka, api, frontend), multi-stage Dockerfiles for the API and frontend, an nginx config for the SPA, health checks and correct `depends_on` ordering, and `.env`-based injection of the two secrets (`ANTHROPIC_API_KEY`, `JWT_SECRET`). This is the backbone of the SPEC objective "Runnable from a single command" and the `AC-NFR: Docker` acceptance criteria.

## 1. Context & Motivation

- **Background:** The scaffold from TDP-INFRA-01 builds locally but there is no orchestration. SPEC §3.1 sketches the compose services and SPEC §2.3 `AC-NFR: Docker` sets hard exit criteria: `docker compose up --build` must start api, frontend, postgres, kafka, and zookeeper with no manual config, frontend on `:3000`, API on `:5000`, Swagger on `:5000/swagger`.
- **Business Impact:** This is the single most-graded deliverable — a reviewer clones the public repo and runs one command (SPEC §1.1: "brings the full stack online in under 2 minutes"). If compose fails, nothing else is assessable. It also de-risks the two highest-scoring risks in SPEC §4.3 (Kafka setup complexity; JWT secret misconfiguration).
- **User Story:** As a reviewer or new developer, I want to run `docker compose up --build` from the repo root and have the whole portal come up healthy, so that I can exercise the app without installing .NET, Node, Postgres, or Kafka locally.
- **Dependencies:** **TDP-INFRA-01** (repo structure, source paths, Dockerfile placeholders). Consumed by every runtime ticket; the Kafka topics it hosts are used by TDP-KAFKA-01, TDP-AI-02. Maps to **Milestone Day 1 — Foundation** (SPEC §4.1), finalised on Day 7 (SPEC §4.1 Day 7 "Dockerfile finalised").

## 2. Detailed Description

### 2.1 `docker-compose.yml` (repository root)

Builds on the sketch in SPEC §3.1, adding health checks, `depends_on` conditions, named volumes, a shared network, and secret injection. The API listens on container port `8080` (ASP.NET Core default in the .NET 8 image) and is published as host `5000`; the frontend nginx listens on `80`, published as host `3000`.

```yaml
name: dispute-portal

services:
  postgres:
    image: postgres:16-alpine
    container_name: dp-postgres
    environment:
      POSTGRES_DB: disputeportal
      POSTGRES_USER: dp_user
      POSTGRES_PASSWORD: dp_pass
    ports:
      - "5432:5432"
    volumes:
      - pgdata:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U dp_user -d disputeportal"]
      interval: 5s
      timeout: 5s
      retries: 10
    networks: [dpnet]

  zookeeper:
    image: confluentinc/cp-zookeeper:7.6.0
    container_name: dp-zookeeper
    environment:
      ZOOKEEPER_CLIENT_PORT: 2181
      ZOOKEEPER_TICK_TIME: 2000
    ports:
      - "2181:2181"
    healthcheck:
      test: ["CMD-SHELL", "echo ruok | nc localhost 2181 | grep imok"]
      interval: 10s
      timeout: 5s
      retries: 10
    networks: [dpnet]

  kafka:
    image: confluentinc/cp-kafka:7.6.0
    container_name: dp-kafka
    depends_on:
      zookeeper:
        condition: service_healthy
    ports:
      - "9092:9092"
    environment:
      KAFKA_BROKER_ID: 1
      KAFKA_ZOOKEEPER_CONNECT: zookeeper:2181
      # Two listeners: internal (container network) and external (host debugging)
      KAFKA_LISTENER_SECURITY_PROTOCOL_MAP: PLAINTEXT:PLAINTEXT,PLAINTEXT_HOST:PLAINTEXT
      KAFKA_ADVERTISED_LISTENERS: PLAINTEXT://kafka:29092,PLAINTEXT_HOST://localhost:9092
      KAFKA_LISTENERS: PLAINTEXT://0.0.0.0:29092,PLAINTEXT_HOST://0.0.0.0:9092
      KAFKA_INTER_BROKER_LISTENER_NAME: PLAINTEXT
      KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR: 1
      KAFKA_TRANSACTION_STATE_LOG_REPLICATION_FACTOR: 1
      KAFKA_TRANSACTION_STATE_LOG_MIN_ISR: 1
      KAFKA_AUTO_CREATE_TOPICS_ENABLE: "true"
    healthcheck:
      test: ["CMD-SHELL", "kafka-broker-api-versions --bootstrap-server localhost:9092 || exit 1"]
      interval: 10s
      timeout: 10s
      retries: 12
    networks: [dpnet]

  api:
    build:
      context: ./src/DisputePortal.Api
      dockerfile: Dockerfile
    container_name: dp-api
    depends_on:
      postgres:
        condition: service_healthy
      kafka:
        condition: service_healthy
    ports:
      - "5000:8080"
    environment:
      ASPNETCORE_ENVIRONMENT: Docker
      ASPNETCORE_URLS: "http://+:8080"
      ConnectionStrings__Default: "Host=postgres;Database=disputeportal;Username=dp_user;Password=dp_pass"
      Kafka__BootstrapServers: "kafka:29092"
      Anthropic__ApiKey: "${ANTHROPIC_API_KEY}"
      Jwt__Secret: "${JWT_SECRET}"
      Cors__AllowedOrigins__0: "http://localhost:3000"
    healthcheck:
      test: ["CMD-SHELL", "curl -fsS http://localhost:8080/health/ready || exit 1"]
      interval: 10s
      timeout: 5s
      retries: 12
      start_period: 30s
    networks: [dpnet]

  frontend:
    build:
      context: ./src/dispute-portal-ui
      dockerfile: Dockerfile
      args:
        VITE_API_BASE_URL: "http://localhost:5000/api/v1"
    container_name: dp-frontend
    depends_on:
      api:
        condition: service_healthy
    ports:
      - "3000:80"
    networks: [dpnet]

volumes:
  pgdata:

networks:
  dpnet:
    driver: bridge
```

> **Note on Kafka listeners:** the API connects over the internal network via `kafka:29092` (the `PLAINTEXT` listener), while `localhost:9092` (the `PLAINTEXT_HOST` listener) is exposed for host-side debugging/tools. This dual-listener pattern is required for `confluentinc/cp-kafka` to be reachable both inside and outside the compose network. The `Kafka__BootstrapServers` for the API is therefore `kafka:29092`, refining the `kafka:9092` value shown in the SPEC §3.1 sketch.

### 2.2 API Dockerfile — `src/DisputePortal.Api/Dockerfile`

Multi-stage: SDK image restores/publishes, runtime image runs. `curl` is added to the runtime image so the compose health check works. The published app runs the EF Core `MigrateAsync()` on startup (TDP-DATA-02), so no separate migration step is needed.

```dockerfile
# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["DisputePortal.Api.csproj", "./"]
RUN dotnet restore "DisputePortal.Api.csproj"
COPY . .
RUN dotnet publish "DisputePortal.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
# curl is required for the compose healthcheck against /health/ready
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "DisputePortal.Api.dll"]
```

> The `context:` is `./src/DisputePortal.Api`, so `COPY` paths are relative to that folder (hence `COPY ["DisputePortal.Api.csproj", "./"]`, not a `src/...` prefix).

### 2.3 Frontend Dockerfile — `src/dispute-portal-ui/Dockerfile`

Multi-stage: Node build produces static assets, nginx serves them. The API base URL is baked at build time via a Vite build arg (Vite inlines `VITE_*` vars at build).

```dockerfile
# ---- build ----
FROM node:20-alpine AS build
WORKDIR /app
ARG VITE_API_BASE_URL
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

# ---- serve ----
FROM nginx:1.27-alpine AS final
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
```

> The frontend build (`npm run build`, `dist/` output) is fully implemented in TDP-FE-01. This ticket owns the Dockerfile and nginx config; the placeholder `package.json` from TDP-INFRA-01 is replaced by the real one there. Coordinate so the Docker build is not merged before a real `npm run build` exists — until then this Dockerfile is committed but the `frontend` service may be built on Day 4+.

### 2.4 nginx config — `src/dispute-portal-ui/nginx.conf`

SPA fallback so client-side routing (React Router in TDP-FE-01) works on refresh:

```nginx
server {
    listen       80;
    server_name  localhost;
    root         /usr/share/nginx/html;
    index        index.html;

    location / {
        try_files $uri $uri/ /index.html;
    }

    location ~* \.(?:js|css|woff2?|png|svg|ico)$ {
        expires 7d;
        add_header Cache-Control "public";
    }
}
```

### 2.5 Secret handling — `.env` and `.env.example`

The two secrets are injected from a root `.env` file that compose reads automatically. `.env` is git-ignored (TDP-INFRA-01 §2.6); `.env.example` is committed as the template.

`.env.example` (committed):

```dotenv
# Copy to .env and fill in before running `docker compose up --build`.
# Anthropic Claude API key — server-side only, NEVER exposed to the frontend.
ANTHROPIC_API_KEY=sk-ant-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
# JWT signing secret — MUST be >= 32 chars. Override in every environment.
JWT_SECRET=change-me-to-a-long-random-string-min-32-bytes
```

Compose substitutes `${ANTHROPIC_API_KEY}` and `${JWT_SECRET}` into the `api` service environment. If a variable is unset, compose warns and passes an empty string; the API's startup validation (TDP-AUTH-01) fails fast on an empty/short `JWT_SECRET`, and AI features degrade gracefully on a missing `ANTHROPIC_API_KEY` per SPEC §4.3.

### 2.6 Startup ordering & readiness

`depends_on` with `condition: service_healthy` enforces: zookeeper → kafka; postgres + kafka healthy → api; api healthy → frontend. This prevents the classic race where the API dials Kafka/Postgres before they accept connections. The API's own `/health/ready` (owned by TDP-OBS-01) is what the compose health check probes; `start_period: 30s` gives EF Core `MigrateAsync()` time to run on a cold database.

## 3. Acceptance Criteria

- `docker compose up --build` from the repo root starts all five services (postgres, zookeeper, kafka, api, frontend) with no manual configuration (SPEC AC-NFR).
- Frontend is reachable at `http://localhost:3000`, API at `http://localhost:5000`, Swagger UI at `http://localhost:5000/swagger` (SPEC AC-NFR).
- The full stack reaches a healthy state in under 2 minutes on a clean machine (SPEC §1.1 objective).
- `ANTHROPIC_API_KEY` and `JWT_SECRET` are injected only into the `api` service environment and are absent from any frontend build output or image (SPEC §3.6 Security: "API key never exposed to frontend").
- `docker compose down -v && docker compose up --build` reproduces a working stack from empty state (SPEC §4.4 manual QA), with a fresh migrated + seeded database.
- Kafka auto-creates the `dispute.submitted`, `dispute.classified`, and `dispute.resolved` topics on first publish (`KAFKA_AUTO_CREATE_TOPICS_ENABLE=true`), mitigating the SPEC §4.3 Kafka risk.
- The `api` service does not report healthy until Postgres and Kafka are healthy and `/health/ready` returns 200.
- `.env.example` is committed; `.env` is git-ignored and never appears in `git status` as tracked.

## 4. Technical Notes

- **Port mapping:** host `5000` → container `8080` for the API. `ASPNETCORE_URLS=http://+:8080` must match, or the container listens on the wrong port and the health check fails silently.
- **Kafka dual listeners** (§2.1): getting `KAFKA_ADVERTISED_LISTENERS` wrong is the #1 cause of "Kafka works from host but not from the API container" or vice versa. The API uses `kafka:29092`.
- **`curl` in the runtime image:** the `aspnet:8.0` base image has no `curl`; it must be installed for the compose health check, or use a `wget`/dotnet-based probe instead.
- **Replication factors = 1:** single-broker dev cluster; `KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR=1` etc. are mandatory or Kafka refuses to create internal topics.
- **Vite build-time env:** `VITE_API_BASE_URL` is inlined at *build* time, not runtime — it is passed as a Docker `build.args`, not a container `environment` entry. Changing it requires a rebuild.
- **`ASPNETCORE_ENVIRONMENT=Docker`:** enables Swagger in the container (SPEC §3.6 Documentation: "Swagger UI enabled in Development and Docker environments"). `Program.cs` must treat `Docker` like `Development` for Swagger exposure.
- **Frontend build coupling:** the `frontend` service cannot build until TDP-FE-01 provides a real `npm run build`. Until then, either comment the service or expect its build to no-op. Do not block the api/postgres/kafka bring-up on it.
- **CORS:** `Cors__AllowedOrigins__0` is passed to the API and consumed by TDP-AUTH-01's CORS policy; keep it aligned with the published frontend origin `http://localhost:3000`.

## 5. Definition of Done

- [ ] `docker-compose.yml` at repo root with all five services, health checks, `depends_on` conditions, named `pgdata` volume, and `dpnet` network.
- [ ] `src/DisputePortal.Api/Dockerfile` (multi-stage, curl-enabled runtime) builds and runs.
- [ ] `src/dispute-portal-ui/Dockerfile` + `nginx.conf` present (activated once TDP-FE-01 lands).
- [ ] `.env.example` committed; `.env` git-ignored; secrets injected only into `api`.
- [ ] `docker compose up --build` brings the stack healthy in < 2 min on a clean machine.
- [ ] Frontend `:3000`, API `:5000`, Swagger `:5000/swagger` all verified reachable.
- [ ] `docker compose down -v && docker compose up --build` reproduces a clean, working, seeded stack.
- [ ] Kafka topics auto-create on first publish; API connects via `kafka:29092`.
- [ ] Reviewed, README run-instructions cross-linked (full runbook in TDP-DOC-02), merged to `main`.
