# TDP-CICD-01 — GitHub Actions CI Pipeline

**Jira summary:** Add a GitHub Actions continuous-integration pipeline that builds and tests both halves of the Transactions Dispute Portal on every push and pull request targeting `main`. The workflow runs the backend suite (`dotnet test`, including Testcontainers integration tests against PostgreSQL) and the frontend suite (`npm run test`) as separate jobs, with dependency caching for speed and a lint/build sanity check. This turns the TDP-TEST-01 and TDP-TEST-02 suites into an automated quality gate, giving a green-checkmark signal on the public submission repo. Marked optional in SPEC §4.4 ("CI, if time permits").

## 1. Context & Motivation

- **Background:** The backend (TDP-TEST-01) and frontend (TDP-TEST-02) test suites exist and pass locally. §4.4 proposes, as an optional stretch, a "GitHub Actions workflow: `dotnet test` + `npm run test` on push to `main`". The repo is public on GitHub (§4.2), so Actions is available at no cost.
- **Business Impact:** A visible passing CI badge signals engineering maturity to the reviewer and guarantees the submitted `main` actually builds and tests green on a clean machine — closing the gap between "works on my laptop" and the §4.4 QA promise. It also prevents a last-minute regression from slipping into the graded commit.
- **User Story:** As the developer, I want CI to build and test both projects automatically on every push so that I get immediate, machine-independent confirmation that `main` is healthy.
- **Dependencies:** TDP-TEST-01 and TDP-TEST-02 (the suites CI executes). Milestone: **Day 7 (22 Jul)** — optional/nice-to-have per §4.3 risk ("build ops dashboard last… CI if time permits").

## 2. Detailed Description

### 2.1 Workflow file

Create `.github/workflows/ci.yml`. Two parallel jobs — `backend` and `frontend` — triggered on push and PR to `main`. Concurrency cancels superseded runs on the same ref.

```yaml
name: CI

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

concurrency:
  group: ci-${{ github.ref }}
  cancel-in-progress: true

jobs:
  backend:
    name: Backend (.NET 8)
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET 8
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Cache NuGet packages
        uses: actions/cache@v4
        with:
          path: ~/.nuget/packages
          key: nuget-${{ runner.os }}-${{ hashFiles('**/*.csproj', '**/packages.lock.json') }}
          restore-keys: nuget-${{ runner.os }}-

      - name: Restore
        run: dotnet restore DisputePortal.sln

      - name: Build
        run: dotnet build DisputePortal.sln --configuration Release --no-restore

      - name: Test (unit + integration via Testcontainers)
        # ubuntu-latest ships with a running Docker daemon, so Testcontainers
        # can start the PostgreSQL 16 container used by the integration tests.
        run: dotnet test DisputePortal.sln --configuration Release --no-build
             --logger "trx;LogFileName=test-results.trx"
             --results-directory ./TestResults
        env:
          # No real secrets needed: the integration factory injects a test key
          # and fakes Kafka/Anthropic (see TDP-TEST-01 §2.6).
          Anthropic__ApiKey: test-key
          Jwt__Secret: ci-only-secret-value-at-least-32-characters-long

      - name: Upload backend test results
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: backend-test-results
          path: ./TestResults/*.trx

  frontend:
    name: Frontend (Vitest)
    runs-on: ubuntu-latest
    defaults:
      run:
        working-directory: src/dispute-portal-ui
    strategy:
      fail-fast: false
      matrix:
        node-version: ['20', '22']
    steps:
      - uses: actions/checkout@v4

      - name: Setup Node ${{ matrix.node-version }}
        uses: actions/setup-node@v4
        with:
          node-version: ${{ matrix.node-version }}
          cache: 'npm'
          cache-dependency-path: src/dispute-portal-ui/package-lock.json

      - name: Install
        run: npm ci

      - name: Lint
        run: npm run lint --if-present

      - name: Type-check & build
        run: npm run build

      - name: Test
        run: npm run test
```

### 2.2 Job design rationale

- **Two independent jobs** run in parallel; a backend failure does not mask a frontend failure and vice versa.
- **Matrix on the frontend** across Node 20 and 22 (the supported LTS + current) validates the SPA on both without duplicating YAML; `fail-fast: false` lets both legs report. The backend has a single .NET 8 target (§ tech stack), so no matrix there.
- **Caching:** `actions/cache` keyed on project files for NuGet; `setup-node`'s built-in `cache: 'npm'` keyed on `package-lock.json`. These cut minutes off repeat runs.
- **Testcontainers on CI:** GitHub's `ubuntu-latest` runner has Docker preinstalled and running, so the TDP-TEST-01 integration tests start their `postgres:16-alpine` container with no extra service configuration. (If a runner without Docker were ever used, the integration tests would need to be split behind a `--filter Category!=Integration` flag — noted for reference only.)
- **No real secrets:** the pipeline supplies throwaway `Anthropic__ApiKey`/`Jwt__Secret` env values purely to satisfy config binding; the tests fake Kafka and Anthropic. No repository secrets are required, keeping the public repo safe (§3.6).

### 2.3 Optional status badge

Add to the top of `README.md` (TDP-DOC-02):

```markdown
![CI](https://github.com/<owner>/dmc-fin-motion_topicproducer/actions/workflows/ci.yml/badge.svg?branch=main)
```

### 2.4 Optional branch protection

If time permits, enable a branch-protection rule on `main` requiring the `Backend (.NET 8)` and `Frontend (Vitest)` status checks to pass before merge. Documented as a manual repo setting, not part of the YAML.

## 3. Acceptance Criteria

- `.github/workflows/ci.yml` exists and triggers on push and pull_request to `main`.
- The workflow runs two jobs: a backend job executing `dotnet restore` → `dotnet build` → `dotnet test DisputePortal.sln`, and a frontend job executing `npm ci` → `npm run build` → `npm run test` in `src/dispute-portal-ui`.
- The frontend job uses a Node version matrix (20 and 22) with `fail-fast: false`.
- NuGet and npm dependencies are cached between runs.
- The backend integration tests (Testcontainers PostgreSQL, TDP-TEST-01) run and pass on the runner without additional service configuration.
- No GitHub repository secrets are required for the workflow to succeed; test-only env values are inlined.
- A push to `main` (or a PR into it) produces a green run when the local suites are green; a failing test fails the corresponding job and the overall check.
- Concurrency cancels in-progress runs superseded by a newer push on the same ref.

## 4. Technical Notes

- **Action versions:** pin to `@v4` for `checkout`, `setup-dotnet`, `setup-node`, `cache`, and `upload-artifact` (current majors as of mid-2026). Avoid floating `@main`.
- **`--no-build` on test** requires the preceding `dotnet build` to use the same `--configuration Release`; keep them consistent or drop `--no-build`.
- **`npm ci` needs a committed `package-lock.json`** in `src/dispute-portal-ui`; ensure it is checked in (also required by TDP-TEST-02 / TDP-DOC-02).
- **`--if-present` on lint** keeps the pipeline green if a `lint` script is not defined, so the workflow does not have to be edited depending on frontend tooling maturity.
- **This workflow does not build Docker images or deploy** — the submission runs locally via compose (§4.2 "no cloud hosting required"). CI scope is strictly build + test, matching §4.4.
- **Runner minutes:** parallel jobs + caching keep a run well within free-tier limits; the frontend matrix doubles frontend minutes but each leg is short.
- **Optional ticket:** if the Day-7 budget is exhausted by tests, README, and QA, this can be deferred without affecting the core deliverables — but it is low effort once TDP-TEST-01/02 exist.

## 5. Definition of Done

- [ ] `.github/workflows/ci.yml` committed with backend and frontend jobs as specified.
- [ ] A run has executed on `main` (or a PR) and appears in the repo's Actions tab, green.
- [ ] Backend job runs unit + Testcontainers integration tests successfully on the runner.
- [ ] Frontend job runs `npm run test` green across the Node matrix and builds the SPA.
- [ ] Dependency caching confirmed active (cache hit on a second run).
- [ ] No repository secrets required; workflow succeeds on a fresh clone/fork.
- [ ] (Optional) CI badge added to the README (TDP-DOC-02) and branch protection enabled on `main`.
- [ ] Reviewed and merged to `main`.
