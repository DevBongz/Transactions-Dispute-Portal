# TDP-INFRA-01 — Repository Structure & Solution Scaffold

**Jira summary:** Establish the canonical repository layout and .NET 8 solution scaffold for the Transactions Dispute Portal so that every subsequent ticket (backend APIs, Kafka, AI, frontend, tests, CI) has a consistent, agreed home. This ticket produces the empty-but-buildable skeleton: the `.sln`, the ASP.NET Core Web API project at `src/DisputePortal.Api`, the React/TypeScript placeholder at `src/dispute-portal-ui`, a test project, and the shared repo hygiene files (`.gitignore`, `.editorconfig`, `.gitattributes`). It unblocks the entire Day 1 Foundation milestone.

## 1. Context & Motivation

- **Background:** The repository (`dmc-fin-motion_topicproducer`) currently contains only `SPEC.md` and a single initial commit. There is no solution file, no project structure, and no agreed convention for where backend, frontend, tests, and infrastructure artefacts live. Every downstream ticket assumes the source paths `src/DisputePortal.Api` and `src/dispute-portal-ui` (see brief and SPEC §3.1), so those must exist and compile before anyone writes a feature.
- **Business Impact:** A clean, conventional scaffold is the difference between `docker compose up` working on Day 7 and a scramble of mismatched paths. It directly supports the objective "Runnable from a single command" (SPEC §1.1) by locking the folder contract that the Docker Compose stack (TDP-INFRA-02) and CI pipeline (TDP-CICD-01) depend on.
- **User Story:** As the sole developer (Bongani), I want a consistent, buildable solution scaffold with agreed source paths and repo hygiene files so that I can add backend, AI, and frontend features without reorganising the tree or fighting inconsistent formatting.
- **Dependencies:** None. This is the root of the dependency graph — TDP-INFRA-02, TDP-DATA-01, and TDP-OBS-01 all depend on it. Maps to **Milestone Day 1 — Foundation** (SPEC §4.1).

## 2. Detailed Description

### 2.1 Target directory tree

Create the following structure at the repository root. Leaf files listed here are the deliverables of *this* ticket; feature code is added by later tickets.

```
dmc-fin-motion_topicproducer/
├── DisputePortal.sln
├── .gitignore
├── .editorconfig
├── .gitattributes
├── Directory.Build.props
├── global.json
├── README.md                        # stub only; full content in TDP-DOC-02
├── SPEC.md                          # already present
├── docs/
│   └── tickets/                     # this ticket set
├── docker-compose.yml               # placeholder; authored in TDP-INFRA-02
├── .env.example                     # placeholder; authored in TDP-INFRA-02
└── src/
    ├── DisputePortal.Api/
    │   ├── DisputePortal.Api.csproj
    │   ├── Program.cs
    │   ├── appsettings.json
    │   ├── appsettings.Development.json
    │   ├── Dockerfile               # placeholder; authored in TDP-INFRA-02
    │   ├── Controllers/             # .gitkeep
    │   ├── Services/                # .gitkeep
    │   ├── Repositories/            # .gitkeep
    │   ├── Data/                    # DbContext lands here (TDP-DATA-01)
    │   ├── Domain/                  # entities land here (TDP-DATA-01)
    │   ├── Contracts/               # DTOs / request-response records
    │   ├── Messaging/               # Kafka producer/consumer (TDP-KAFKA-01)
    │   ├── Infrastructure/          # cross-cutting: auth, logging, config
    │   └── Migrations/              # EF Core migrations (TDP-DATA-02)
    ├── DisputePortal.Api.Tests/
    │   ├── DisputePortal.Api.Tests.csproj
    │   └── .gitkeep
    └── dispute-portal-ui/
        ├── package.json             # placeholder; full scaffold in TDP-FE-01
        ├── .gitignore
        └── src/                     # .gitkeep
```

> Empty folders are committed with a `.gitkeep` file so the contract is visible in Git. Folders that receive code in a *named* later ticket are annotated above.

### 2.2 Solution file & framework pinning

Create `DisputePortal.sln` referencing the API and test projects. Pin the SDK with `global.json` so all machines and CI (TDP-CICD-01) use the same toolchain:

```json
// global.json
{
  "sdk": {
    "version": "8.0.400",
    "rollForward": "latestFeature"
  }
}
```

Solution wiring (via `dotnet sln`):

```bash
dotnet new sln -n DisputePortal
dotnet new webapi -n DisputePortal.Api -o src/DisputePortal.Api --framework net8.0 --use-controllers
dotnet new xunit -n DisputePortal.Api.Tests -o src/DisputePortal.Api.Tests --framework net8.0
dotnet sln add src/DisputePortal.Api/DisputePortal.Api.csproj
dotnet sln add src/DisputePortal.Api.Tests/DisputePortal.Api.Tests.csproj
dotnet add src/DisputePortal.Api.Tests reference src/DisputePortal.Api/DisputePortal.Api.csproj
```

### 2.3 API project file

`src/DisputePortal.Api/DisputePortal.Api.csproj` establishes the target framework and reserves the NuGet packages the Foundation tickets will consume. Package versions are pinned to the .NET 8 line.

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <InvariantGlobalization>true</InvariantGlobalization>
    <!-- Program must be public for WebApplicationFactory<Program> in TDP-TEST-01 -->
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <NoWarn>$(NoWarn);1591</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="8.0.*" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.*" />
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="8.0.*" />
    <PackageReference Include="BCrypt.Net-Next" Version="4.0.3" />
    <PackageReference Include="Confluent.Kafka" Version="2.5.*" />
    <PackageReference Include="Serilog.AspNetCore" Version="8.0.*" />
    <PackageReference Include="Swashbuckle.AspNetCore" Version="6.6.*" />
  </ItemGroup>

</Project>
```

> The listed packages are *referenced* here so the solution restores cleanly. Their actual wiring is done in the owning tickets: EF Core/Npgsql → TDP-DATA-01/02, JwtBearer + BCrypt → TDP-AUTH-01, Confluent.Kafka → TDP-KAFKA-01, Serilog → TDP-OBS-01, Swashbuckle → TDP-DOC-01. It is acceptable to add them incrementally, but reserving them now avoids churning the csproj.

### 2.4 Minimal `Program.cs`

The scaffold must build and expose a trivial startup. It uses the minimal-hosting model but keeps `Program` reachable as a public partial class for integration tests (`WebApplicationFactory<Program>`, SPEC §4.4).

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.Run();

// Exposed for WebApplicationFactory<Program> integration tests (TDP-TEST-01).
public partial class Program { }
```

### 2.5 `Directory.Build.props` (shared MSBuild settings)

Centralise nullable, analysers, and warnings-as-errors so every project inherits them:

```xml
<Project>
  <PropertyGroup>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
  </PropertyGroup>
</Project>
```

### 2.6 `.gitignore`

Cover .NET, Node, IDE, and secrets. Critically, ignore `.env` (real secrets) while keeping `.env.example` tracked (TDP-INFRA-02 relies on this).

```gitignore
# .NET
bin/
obj/
*.user
artifacts/

# Node / frontend
node_modules/
dist/
.vite/
*.local

# IDE
.vs/
.idea/
.vscode/*
!.vscode/extensions.json

# Secrets & env — NEVER commit real keys (ANTHROPIC_API_KEY, JWT_SECRET)
.env
.env.*.local
appsettings.*.local.json

# OS
.DS_Store

# Tooling scratch
.claude-flow/
```

### 2.7 `.editorconfig`

Enforce consistent formatting for C#, TS, JSON, and YAML. Abridged, key rules:

```ini
root = true

[*]
charset = utf-8
end_of_line = lf
insert_final_newline = true
trim_trailing_whitespace = true
indent_style = space

[*.cs]
indent_size = 4
csharp_new_line_before_open_brace = all
dotnet_sort_system_directives_first = true
csharp_style_namespace_declarations = file_scoped:warning
dotnet_style_require_accessibility_modifiers = always:warning

[*.{ts,tsx,js,jsx,json,yml,yaml,css}]
indent_size = 2

[*.{csproj,props,targets}]
indent_size = 2
```

### 2.8 `.gitattributes`

Normalise line endings (LF) to avoid CRLF churn between Windows/Rider and macOS/VS Code (SPEC §4.2 lists both IDE families):

```gitattributes
* text=auto eol=lf
*.png binary
*.jpg binary
*.ico binary
```

### 2.9 Frontend placeholder

`src/dispute-portal-ui` gets a minimal `package.json` and `.gitignore` only. The full Vite + React + TypeScript + shadcn/ui scaffold is TDP-FE-01's responsibility; this ticket just reserves the path so TDP-INFRA-02 can wire the Docker build context.

```json
// src/dispute-portal-ui/package.json (placeholder)
{
  "name": "dispute-portal-ui",
  "private": true,
  "version": "0.0.0",
  "scripts": {
    "dev": "echo \"scaffolded in TDP-FE-01\" && exit 0"
  }
}
```

## 3. Acceptance Criteria

- `dotnet build DisputePortal.sln` succeeds from the repo root with zero warnings and zero errors (warnings-as-errors is on).
- `dotnet run --project src/DisputePortal.Api` starts and serves Swagger UI at `/swagger` in Development.
- The solution contains exactly two projects — `DisputePortal.Api` and `DisputePortal.Api.Tests` — and the test project references the API project.
- The directory tree in §2.1 exists exactly, with `.gitkeep` in every otherwise-empty folder.
- `git status` shows `.env` would be ignored while `.env.example` (once added in TDP-INFRA-02) would be tracked.
- `global.json` pins the .NET 8 SDK; `dotnet --version` on the build machine satisfies the pin.
- `.editorconfig` and `.gitattributes` are present at the root and applied (formatting a `.cs` file yields file-scoped namespaces, 4-space indent).
- The source paths `src/DisputePortal.Api` and `src/dispute-portal-ui` match the values referenced by the Docker Compose stack in SPEC §3.1 exactly.

## 4. Technical Notes

- **`Program` visibility:** Do not delete the `public partial class Program { }` line — TDP-TEST-01's `WebApplicationFactory<Program>` integration tests will not compile without it.
- **Package restore vs. wiring:** Referencing a NuGet package in the csproj without calling its `AddXxx()` in `Program.cs` is fine and intentional; it keeps the scaffold restore-clean while leaving wiring to owning tickets.
- **Path casing:** The frontend folder is `dispute-portal-ui` (kebab-case, lowercase) and the API is `DisputePortal.Api` (PascalCase). These casings are load-bearing for the Docker `build:` contexts in TDP-INFRA-02 — do not "tidy" them.
- **`.claude-flow/`** already appears in the working tree; add it to `.gitignore` so tooling scratch is never committed.
- **SDK version:** `8.0.400` is a safe floor for the .NET 8 line; `rollForward: latestFeature` lets CI use any 8.0.4xx patch.
- **Frontend deferral:** Resist scaffolding the real Vite app here — duplicating TDP-FE-01 causes merge friction. Placeholder only.

## 5. Definition of Done

- [ ] Directory tree from §2.1 created and committed (including `.gitkeep` markers).
- [ ] `DisputePortal.sln`, `global.json`, `Directory.Build.props` present and building.
- [ ] `.gitignore`, `.editorconfig`, `.gitattributes` present at repo root with the content above.
- [ ] `dotnet build DisputePortal.sln` is green (0 warnings, 0 errors).
- [ ] `dotnet run --project src/DisputePortal.Api` serves `/swagger` locally.
- [ ] API and test projects both added to the solution; test → API project reference in place.
- [ ] Frontend and API source paths confirmed to match SPEC §3.1.
- [ ] Stub `README.md` present (full content deferred to TDP-DOC-02).
- [ ] Reviewed and merged to `main`; branch protection / CI (once TDP-CICD-01 lands) can build the scaffold.
