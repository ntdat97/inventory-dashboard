# Intelligent Inventory Dashboard

A capital-at-risk decision-support tool for a dealership inventory manager: aging spectrum,
carrying-cost model, AI-assisted action recommendations, and an action lifecycle with history.

### 🌐 Live demo available at: [**LIVE DEMO**](https://p01--inventory-dashboard-client--xwbq9zg4s42w.code.run/)

> Architecture and rationale live in [SYSTEM-DESIGN.md](./SYSTEM-DESIGN.md); deployment in [DEPLOY.md](./DEPLOY.md).

---

## Run the app

### Quick Start (Docker Compose) (Recommended)
The fastest way to run the entire stack (PostgreSQL + API + React Client):

```bash
docker compose up --build
```
* **React App**: <http://localhost:5173>
* **Sign In**: Click **"Continue as guest (demo)"** (global admin) or **"Continue as scoped manager"** (scoped to a single dealership).

---

### Local Dev

#### Prerequisites
* **.NET SDK 8+**
* **Node 20+** (npm)
* **Docker** (to host PostgreSQL)

#### Setup Steps (Run from the repo root in separate terminals)

**1 — Start PostgreSQL**
```bash
docker run -d --name inv-pg \
  -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=inventory \
  -p 5432:5432 postgres:15
```

**2 — Start API**
```bash
cd server/Inventory.Api
dotnet run
```
*(API runs at `http://localhost:5002`)*

**3 — Start Client**
```bash
cd client
npm install
npm run dev
```
*(SPA runs at `http://localhost:5173`)*

---

### Service Endpoints
* **React Client (SPA)**: <http://localhost:5173>
* **API Swagger UI**: <http://localhost:5002/swagger> (Local dev only)
* **API Swagger UI (Docker Compose)**: <http://localhost:8080/swagger>
* **API Health Check**: `http://localhost:5002/health` (returns `Healthy` when DB is up)

### Stop & Clean Up
* **Local Dev**: Stop the API and client processes (Ctrl+C), then run: `docker rm -f inv-pg`
* **Docker Compose**: Run: `docker compose down -v`

---

## Test

Run the test suite from the repository root:

```bash
dotnet test
```

---

## Project layout

```
server/
  Inventory.Api/            ASP.NET Core Web API (the fully-built, graded layer)
    Domain/                 entities, enums, pure services (Aging/CarryingCost/ActionWorkflow), config
    Application/            services, DTOs, validation, auth, recommendation subsystem
    Infrastructure/         EF Core DbContext, migrations, seeding, observability
    Controllers/            thin HTTP boundary
  Inventory.Tests/          xUnit + FluentAssertions (unit + WebApplicationFactory integration)
client/                     React 18 + Vite + TS SPA (real consumer of the live API)
SYSTEM-DESIGN.md            architecture, data flow, tech choices, observability, assumptions
DEPLOY.md                   deployment notes
```

---

## AI Collaboration Narrative

This project was built with GenAI as an essential collaborator across the full lifecycle. My strategy
had a clear division of labour: **I owned the direction, scope, and every architectural decision; the AI
accelerated exploration and execution, and I verified its output at every stage.** The process ran in
distinct stages rather than as ad-hoc prompting.

**1 — Scenario selection.** I first had the AI analyze all four scenarios against my previous
experience, my personal tech stack, and my domain knowledge, and separately characterize each scenario
— which leaned backend-heavy, which leaned more on AI, and what each one really emphasized. Combining
that analysis with my own preference, I chose **Scenario B**.

**2 — Direction & framing.** I then discussed ideas with the AI to establish the overall development
direction, using it to surface the real dealership economics — floorplan financing, aging stock,
carrying cost — that reframe "a filterable car list" into a **capital-at-risk decision-support tool**.
That framing, not the AI, drove the feature set. The output of this stage was a basic feature list and
a candidate tech stack.

**3 — Phased plan.** I asked the AI to turn that direction into an implementation plan split into
phases, each with explicit, testable acceptance criteria. After refining the plan together, the output
was a verified **System Design document** and a phased **implementation plan** to build against.

**4 — Isolated, phase-by-phase implementation.** I opened a **completely fresh, isolated conversation**
for each phase, strictly scoped to that phase's requirements, to prevent context drift, token bloat,
and compiler issues. Each session focused on implementing exactly one phase.

**5 — Dual-layer verification loop.** After the build bot completed a phase, I ran an **independent
validator bot in a separate context** as an adversarial auditor — it verified the work against the
phase's acceptance criteria by running builds, executing tests, and exercising the APIs. I combined this
with my own manual code and feature review to confirm design alignment. If gaps were found, I directed
the build bot to fix them before advancing. I repeated this loop across all four phases, ending with a
functionally complete application and the full test suite green.

**6 — UI makeover & polish.** With the app functionally complete, the default UI was functional but
generic and lacked any personal style. I used a specialized design bot (`claude-design`) to generate 3
distinct visual mockup styles, selected the one I preferred, and used it to reskin the frontend into a
cohesive, custom decision dashboard.

**7 — Final review, refinement & documentation.** This was a substantial pass in its own right. I did a
thorough personal review of the whole codebase and drove several rounds of AI-assisted refinement on the
parts I was not satisfied with, re-verifying after each change. I then had the AI rewrite a polished
**System Design document** — one of the deliverables alongside this README and the demo script — and
carefully reconciled all three against the as-built code so the docs describe what actually shipped.
A final end-to-end verification pass, then submission.

**How I verified and refined the output.**
- **Tests as the contract.** The pure domain logic (aging tiers, carrying-cost accrual, the lifecycle
  state machine) is fully unit-tested with an injected clock, so AI-written logic had to satisfy
  deterministic boundary cases (30/60/90 days, exactly-at-threshold, invalid transitions) rather than
  "look right." Integration tests boot the real app with faked clock and AI client.
- **I owned the deviations.** Where the shipped code departs from the initial plan, it was a
  deliberate call I can defend — e.g. using plain `JwtBearer` against the Entra authority instead of
  `Microsoft.Identity.Web` (equivalent validation, avoids a moderate CVE), and cutting OpenTelemetry
  tracing to protect the one-week window. These deltas are documented in `SYSTEM-DESIGN.md` rather than
  hidden, and this README, the design doc, and the code were reconciled against each other so the docs
  describe **what actually shipped**.

**AI as a product feature — verified, not trusted.** The recommendation feature embeds an LLM
responsibly: a deterministic **baseline-first** recommender always produces a result (the LLM only
enriches phrasing); output is **validated** against a typed schema (bad action / empty rationale /
insane price cut → rejected); the call is **resilience-wrapped** (Polly v8 timeout/retry/breaker) and
**degrades to baseline** on any failure so the endpoint never 5xxs; and it is **cost-guarded** with
per-vehicle caching and rate limiting for the open demo. The result is AI that is grounded, owned, and
provably fails safe.

