# WhyNoPower — System Design

**Status:** Draft for review · July 2026
**Suggested repo path:** `docs/architecture/system-design.md`
**Companion documents:** `docs/database/schema-design.md` (ERD), `docs/adr/` (decisions 0001–0005)

This document describes the system top-down: C4 Context → Containers → Components → Deployment, then inward with UML (class, sequence, state) for the Phase 1 domain, and finally the designed-but-not-yet-built shape of Phases 2–3. Every diagram is Mermaid, so GitHub renders this file as living documentation — no exported images to drift out of date.

---

## 0. Diagram inventory

| # | Diagram | Method | Section | Answers the question |
|---|---|---|---|---|
| 1 | System Context | C4 L1 | §2 | Who uses it, what does it talk to? |
| 2 | Containers | C4 L2 | §3 | What are the deployable pieces? |
| 3 | API Components | C4 L3 | §4.1 | How is the ASP.NET Core API structured inside? |
| 4 | ML Service Components | C4 L3 | §4.2 | How is the Python side structured inside? |
| 5 | Deployment | C4 Deployment | §5 | What runs where on Azure? |
| 6 | Core Domain | UML Class | §6 | What are the domain objects and seams? |
| 7 | Growatt sync | UML Sequence | §7.1 | How do measured samples get in? (ADR-005) |
| 8 | Forecast pipeline | UML Sequence | §7.2 | How does a forecast come to exist? |
| 9 | Dashboard read | UML Sequence | §7.3 | Why is the user path fast and resilient? |
| 10 | Manual entry + reward | UML Sequence | §7.4 | How does the fallback/reward mechanic work? |
| 11 | Inverter connection | UML State | §8 | What drives the sync-status chip? |
| 12 | Water NLP pipeline | Flowchart | §9.2 | How will Phase 3 turn text into rows? |

---

## 1. Architecture overview & principles

WhyNoPower is a **modular monolith core with satellite workers and one ML sidecar**, not a microservices system. Five principles shape every diagram below:

1. **Data platform, not proxy** (ADR-004 generalised). External sources are ingested on *our* schedule into *our* database; users are always served from our data. This applies to EskomSePush (quota), Growatt (unofficial API), Open-Meteo (courtesy), and — the new decision in this document — to our own ML service.
2. **ML off the hot path.** No user request ever blocks on the Python service. Forecasts are precomputed in batch and read from Azure SQL like any other data. The ML service's synchronous API exists for operations (retrain, recompute, health), not for serving users. *(ADR-007 candidate.)*
3. **Raw-first ingestion** (ADR-005 generalised). Every external payload is stored as received, then parsed. Parsed rows reference their raw source. Re-parseable, auditable, format-change-tolerant.
4. **Captured truth vs derived data.** The schema separates what was measured/entered from what was computed; the architecture keeps the writers separate too (workers write truth, ML writes derived, API mostly reads).
5. **Vertical slices** (ADR-003). Phase 2/3 elements appear in these diagrams greyed/annotated — designed so Phase 1 doesn't block them, built only when their phase begins.

---

## 2. C4 Level 1 — System Context

```mermaid
C4Context
    title System Context — WhyNoPower (Phase 1 scope solid, later phases noted)

    Person(user, "Household user", "SA homeowner with rooftop PV. Wants generation, savings, and outage resilience in rands and hours.")
    Person(visitor, "Anonymous visitor", "Sees area-level public status only; no personal data.")

    System(wnp, "WhyNoPower", "Utility resilience platform: solar forecasting & savings (P1), loadshedding simulation (P2), water-outage intelligence (P3).")

    System_Ext(openmeteo, "Open-Meteo", "Free weather + tilted-plane solar irradiance forecasts. No API key.")
    System_Ext(growatt, "Growatt server API", "Unofficial ShinePhone endpoint. Measured inverter/battery telemetry. (ADR-005)")
    System_Ext(esp, "EskomSePush API", "Loadshedding schedules & stage. 50 calls/day. (Phase 2, ADR-004)")
    System_Ext(jw, "Johannesburg Water", "Unstructured outage notices on the public website. No API. (Phase 3)")

    Rel(user, wnp, "Configures system, views forecasts & savings", "HTTPS")
    Rel(visitor, wnp, "Views public area status", "HTTPS")
    Rel(wnp, openmeteo, "Pulls weather + per-orientation irradiance, few times daily", "HTTPS/JSON")
    Rel(wnp, growatt, "Pulls measured generation every 15 min", "HTTPS/JSON (unofficial)")
    Rel(wnp, esp, "Pulls schedules & stage on a fixed budget", "HTTPS/JSON")
    Rel(wnp, jw, "Scrapes & parses notices", "HTTPS/HTML")
```

**Reading notes.** Two actor types encode the anonymous/authenticated security boundary from the brief: the public "status page" view exists precisely so there is a deliberate, designed logged-out surface rather than an accidental one. All four external systems are *pull-only* — WhyNoPower initiates every exchange, holds no inbound webhooks, and can therefore survive any of them being down (it just serves its latest data). The arrows to ESP and JW exist at context level now, even though their phases aren't built, because the system's identity — the reason it's called a *resilience platform* — is the combination.

---

## 3. C4 Level 2 — Containers

```mermaid
C4Container
    title Containers — WhyNoPower

    Person(user, "Household user")

    System_Boundary(wnp, "WhyNoPower") {
        Container(spa, "Web App", "React + Tailwind, Vite", "Onboarding wizard, week-ahead dashboard, simulation & water views. AI-assisted build, logged in docs/ai-workflow.md.")
        Container(api, "Core API", "ASP.NET Core 8 Web API", "Auth (Identity + JWT), system profiles, dashboard/forecast queries, savings & battery maths, manual entries, OpenAPI.")
        Container(mlsvc, "ML Service", "Python, FastAPI", "Trains solar regression; batch-writes forecasts (physics + corrected). Ops endpoints only — not on the user path.")
        Container(wsync, "Weather Sync Worker", "Azure Function, C# timer", "Fetches Open-Meteo weather (per suburb) + tilted irradiance (per panel group); stores raw + parsed.")
        Container(gsync, "Growatt Sync Worker", "Azure Function, Python timer", "Pulls inverter telemetry via community growattServer lib behind ActualsProvider seam (ADR-005); writes raw + samples + rollups.")
        ContainerDb(db, "Database", "Azure SQL", "Single shared relational store. Captured-truth and derived tables; least-privilege logins per container.")
    }

    System_Ext(openmeteo, "Open-Meteo")
    System_Ext(growatt, "Growatt server API")

    Rel(user, spa, "Uses", "HTTPS")
    Rel(spa, api, "JSON/HTTPS", "JWT bearer")
    Rel(api, db, "Reads/writes app data", "EF Core, parameterised")
    Rel(api, mlsvc, "Ops calls only: retrain / recompute / health", "HTTP, internal")
    Rel(mlsvc, db, "Reads samples & irradiance; writes forecasts + model registry", "SQLAlchemy, least-priv login")
    Rel(wsync, openmeteo, "Few fetches daily", "HTTPS")
    Rel(wsync, db, "Writes raw + weather + irradiance", "")
    Rel(gsync, growatt, "Every 15 min", "HTTPS (unofficial)")
    Rel(gsync, db, "Writes raw + samples + rollups + connection status", "")
```

### 3.1 Container responsibilities & boundaries

| Container | Owns | Explicitly does *not* |
|---|---|---|
| **Web App (React)** | All UI states incl. degraded "forecast-only" mode; client-side validation as UX nicety | Business rules (server re-validates everything); direct external API calls |
| **Core API (C#)** | AuthN/AuthZ, profile CRUD, all read models for dashboards, rand/battery/best-hours calculations, manual entries + reward stamping | Talking to Growatt/Open-Meteo (workers do), running ML (Python does), serving forecasts it computed itself |
| **ML Service (Python)** | Feature building, physics baseline, regression train/predict, batch forecast writes, model registry rows | Auth, user data mutation, anything a user request waits on |
| **Weather Sync (C# Fn)** | Open-Meteo fetch orchestration per suburb & per panel-group orientation; raw+parsed writes | Interpreting forecasts (ML's job) |
| **Growatt Sync (Py Fn)** | ShinePhone login, telemetry pull, raw+samples+rollup writes, `INVERTER_CONNECTIONS.status` transitions | Being on any user-facing path |
| **Azure SQL** | Single source of truth, integrity constraints, the contract between languages | — |

**Why the boundaries sit here.** The C#/Python split follows ADR-001 (each language where it's strongest). The *workers-as-Functions* choice keeps scheduled concerns out of the API's process (App Service free-tier instances sleep; Functions timers don't care), and gives each ingestion its own failure domain and logs. The database is deliberately the integration point between C# and Python — a shared relational contract with per-login least privilege — rather than an internal HTTP mesh, which would add failure modes without adding portfolio value. The one HTTP link (API→ML) is operational only.

**Polyglot consequence worth noting:** an Azure Function App is single-runtime, so the C# weather worker and Python Growatt worker are **two Function Apps**, not two functions in one app. Cheap on free tier, but it must be reflected in CI/CD (§5).

---

## 4. C4 Level 3 — Components

### 4.1 Inside the Core API (ASP.NET Core)

```mermaid
C4Component
    title Components — Core API (ASP.NET Core)

    Container_Boundary(api, "Core API") {
        Component(authc, "Auth endpoints", "Identity + JWT", "Register/login/refresh; role policy: User, Admin.")
        Component(sysc, "Systems & Profile endpoints", "Controller", "Wizard CRUD: system, panel groups, tariff history, suburbs followed.")
        Component(dashc, "Dashboard endpoints", "Controller", "Week/day read models for the SPA; public area-status endpoint (anonymous).")
        Component(manc, "Manual entry endpoints", "Controller", "Fallback daily readings; triggers reward.")
        Component(simc, "Simulation endpoints (P2)", "Controller", "Stage/week replay requests.")

        Component(profsvc, "SystemProfileService", "Application service", "Validation + persistence rules for wizard data; kWp derivation.")
        Component(fcqsvc, "ForecastQueryService", "Application service", "Joins rollups (past) + forecasts (future) + weather into the week/day view.")
        Component(randsvc, "SavingsCalculator", "Domain service", "Wh × tariff-in-effect → cents. Tariff resolved by date from history.")
        Component(battsvc, "BatteryRuntimeCalculator", "Domain service", "usable Wh ÷ essential load W → hours.")
        Component(bestsvc, "BestHoursAdvisor", "Domain service", "Peak window extraction from hourly forecast curve.")
        Component(rewardsvc, "RewardService", "Application service", "Stamps ad_free_until on qualifying manual entry.")
        Component(simeng, "SimulationEngine (P2)", "Domain service", "Schedule slots × stage × solar window × battery model → coverage verdicts.")

        Component(dbctx, "AppDbContext", "EF Core", "Mappings incl. PvSystem→SYSTEMS; migrations; parameterised only.")
        Component(mlops, "MlOpsClient", "Typed HttpClient + Polly", "retrain/recompute/health calls to ML service.")
        Component(health, "Health & telemetry", "Middleware", "/healthz per dependency; structured logs; correlation ids.")
    }

    Rel(authc, dbctx, "Identity stores")
    Rel(sysc, profsvc, "")
    Rel(dashc, fcqsvc, "")
    Rel(fcqsvc, randsvc, "")
    Rel(fcqsvc, battsvc, "")
    Rel(fcqsvc, bestsvc, "")
    Rel(manc, rewardsvc, "")
    Rel(profsvc, dbctx, "")
    Rel(fcqsvc, dbctx, "")
    Rel(rewardsvc, dbctx, "")
    Rel(simc, simeng, "")
    Rel(simeng, dbctx, "Reads schedule slots (P2)")
```

**Design notes.**
- Layering is *pragmatic clean-ish*: Controllers → application services → domain services → EF Core. No repository layer over EF (EF **is** the repository); the tests mock at the service seams instead. This is a deliberate anti-ceremony choice worth an interview sentence.
- `SavingsCalculator` takes a **date** and resolves the tariff from `TARIFFS` history — the schema decision (§schema-design) surfaces here as an API-level guarantee that historical rand figures never drift.
- The **public status endpoint** lives in Dashboard endpoints with `[AllowAnonymous]` and serves *suburb-level aggregates only* — the designed anonymous surface from §2.
- Where's the ADR-005 `IActualsProvider` seam? **Not here.** The API never talks to Growatt; measured data reaches it as rows. The isolation seam lives in the Python worker (§4.2), where Growatt is actually touched. Putting an unused interface in C# would be architecture theatre.

### 4.2 Inside the ML Service + Growatt worker (Python)

The `ml/` folder is one Python package with two entrypoints (FastAPI app; Growatt timer Function) sharing modules:

```mermaid
C4Component
    title Components — ML Service & Growatt Worker (shared ml/ package)

    Container_Boundary(ml, "ml/ package") {
        Component(fapi, "FastAPI app", "fastapi", "/healthz, /ops/retrain, /ops/recompute-forecasts. Ops-only surface.")
        Component(batch, "Forecast batch job", "timer entry", "Nightly + post-weather-sync: writes GENERATION_FORECASTS (physics + predicted).")
        Component(feat, "FeatureBuilder", "pandas", "Joins IRRADIANCE_FORECASTS + GENERATION_SAMPLES into training/inference frames.")
        Component(phys, "PhysicsBaseline", "pure fn", "Σ groups: kWp × (POA/1000) × PR(temp) → clip at inverter AC W.")
        Component(reg, "RegressionModel", "scikit-learn", "Corrects physics using measured history; persisted per MODEL_VERSIONS.")
        Component(registry, "ModelRegistry", "module", "Reads/writes MODEL_VERSIONS + metrics.")
        Component(actprov, "ActualsProvider (seam)", "ABC / Protocol", "ADR-005 isolation: interface the pipeline depends on.")
        Component(gadapter, "GrowattAdapter", "growattServer lib", "The only module that knows Growatt exists. Login, fetch, raw payload capture.")
        Component(gjob, "Growatt sync job", "timer entry", "15-min cadence: adapter → raw → samples → rollup → status transition (§8).")
        Component(pydb, "DB access", "SQLAlchemy", "Least-priv login: read samples/irradiance, write forecasts/models/raw/samples.")
    }

    Rel(gjob, actprov, "depends on")
    Rel(actprov, gadapter, "implemented by")
    Rel(gjob, pydb, "")
    Rel(batch, feat, "")
    Rel(batch, phys, "")
    Rel(batch, reg, "")
    Rel(batch, registry, "")
    Rel(batch, pydb, "")
    Rel(fapi, batch, "triggers on demand")
    Rel(reg, registry, "versioned by")
```

**Design notes.** `PhysicsBaseline` is a pure function — trivially unit-testable, and its output is stored per forecast row so the model's *lift over physics* is always measurable. A future non-Growatt inverter (or an official API) is a new `ActualsProvider` implementation; nothing upstream changes — that's ADR-005's promise made concrete. Notebook→service promotion path: exploration happens in `ml/notebooks/`, and code graduates into these modules; the notebook never becomes load-bearing.

---

## 5. C4 Deployment — Azure

```mermaid
C4Deployment
    title Deployment — Azure (Phase 1)

    Deployment_Node(gh, "GitHub", "Source + CI/CD") {
        Container(actions, "GitHub Actions", "CI/CD", "Build+test on PR; deploy on merge to main. One workflow per deployable.")
    }

    Deployment_Node(az, "Azure subscription", "Azure for Students") {
        Deployment_Node(swa, "Static Web Apps", "Free tier") {
            Container(spa_d, "Web App", "React build artefacts")
        }
        Deployment_Node(asp1, "App Service (Linux)", "Free/B1") {
            Container(api_d, "Core API", "ASP.NET Core 8")
        }
        Deployment_Node(asp2, "App Service (Linux)", "Free/B1") {
            Container(ml_d, "ML Service", "FastAPI + gunicorn")
        }
        Deployment_Node(fn1, "Function App — dotnet", "Consumption") {
            Container(wsync_d, "Weather Sync", "C# timer")
        }
        Deployment_Node(fn2, "Function App — python", "Consumption") {
            Container(gsync_d, "Growatt Sync + forecast batch", "Python timers")
        }
        Deployment_Node(sqln, "Azure SQL", "Serverless / free offer") {
            ContainerDb(db_d, "whynopower-db", "3 least-priv logins: api, ml, workers")
        }
        Deployment_Node(kv, "Key Vault", "") {
            Container(secrets, "Secrets", "JWT signing key, Growatt creds key, conn strings")
        }
        Deployment_Node(ai, "Application Insights", "") {
            Container(tel, "Telemetry", "Structured logs, traces, alerts")
        }
    }

    Rel(actions, swa, "deploy")
    Rel(actions, asp1, "deploy")
    Rel(actions, asp2, "deploy")
    Rel(actions, fn1, "deploy")
    Rel(actions, fn2, "deploy")
    Rel(api_d, db_d, "TLS")
    Rel(ml_d, db_d, "TLS")
    Rel(wsync_d, db_d, "TLS")
    Rel(gsync_d, db_d, "TLS")
    Rel(api_d, secrets, "managed identity")
    Rel(gsync_d, secrets, "managed identity")
```

**Deployment notes.**
- **Local dev mirrors this** with docker-compose: `api`, `ml`, `frontend`, `sql` (Azure SQL Edge image), plus the two workers runnable as plain processes. Kubernetes remains explicitly out of scope (ADR-002).
- **Secrets flow:** nothing in Git ever; local `.env` (gitignored) → GitHub Actions secrets → App/Function settings referencing Key Vault via **managed identity** (no connection secrets in app settings at all). Growatt credentials are additionally encrypted at the column level; Key Vault holds the wrapping key.
- **CI/CD shape (monorepo):** path-filtered workflows — `api/**` builds/tests/deploys the API; `ml/**` the ML service and Python Function App; `frontend/**` the SWA; `docs/**` runs link/lint only. PRs must be green to merge (branch protection already live).
- Free-tier honesty: App Service free instances cold-start; acceptable for a portfolio demo and irrelevant to workers (Functions) and to data freshness (batch model).

---

## 6. UML Class Diagram — Phase 1 core domain

```mermaid
classDiagram
    direction LR

    class UserProfile {
      +long Id
      +string AspNetUserId
      +string DisplayName
      +DateTime? AdFreeUntilUtc
      +GrantAdFreeDay(now) void
    }

    class Suburb {
      +long Id
      +string Name
      +string Municipality
      +decimal Lat
      +decimal Lng
    }

    class PvSystem {
      <<named to avoid .NET System namespace clash>>
      +long Id
      +string Name
      +int InverterMaxW
      +InverterType Type
      +int? BatteryCapacityWh
      +int BatteryUsablePct
      +int EssentialLoadW
      +TotalKwp() decimal
      +UsableBatteryWh() int
    }

    class PanelGroup {
      +long Id
      +int PanelCount
      +int PanelWatt
      +int? AzimuthDeg
      +int? TiltDeg
      +GroupKwp() decimal
      +EffectiveAzimuth() int
      +EffectiveTilt(latitude) int
    }

    class Tariff {
      +long Id
      +int RateCentsPerKwh
      +DateOnly EffectiveFrom
    }

    class InverterConnection {
      +long Id
      +string Provider
      +byte[] CredentialsEncrypted
      +string DataloggerSn
      +ConnectionStatus Status
      +DateTime? LastSyncedAtUtc
    }

    class GenerationSample {
      +long Id
      +DateTime SampledAtUtc
      +int AcPowerW
      +int EnergyTodayWh
      +int? BatterySocPct
    }

    class DailyGenerationRollup {
      +long Id
      +DateOnly DateLocal
      +int TotalWh
      +int PeakW
      +RollupSource Source
    }

    class GenerationForecast {
      +long Id
      +DateTime IssuedAtUtc
      +DateOnly TargetDateLocal
      +DateTime? TargetHourUtc
      +int PhysicsWh
      +int PredictedWh
      +DeviationWh(actual) int
    }

    class ModelVersion {
      +long Id
      +string Domain
      +string Version
      +DateTime TrainedAtUtc
      +string MetricsJson
    }

    class SavingsCalculator {
      <<domain service>>
      +ToCents(wh, onDate, tariffHistory) long
    }
    class BatteryRuntimeCalculator {
      <<domain service>>
      +Hours(system) decimal
    }
    class BestHoursAdvisor {
      <<domain service>>
      +PeakWindow(hourlyForecast) TimeRange
    }

    UserProfile "1" --> "0..*" PvSystem : owns
    UserProfile "1" --> "1..*" Suburb : follows
    PvSystem "1" --> "1..*" PanelGroup
    PvSystem "1" --> "1..*" Tariff : history
    PvSystem "1" --> "0..1" InverterConnection
    PvSystem "1" --> "0..*" GenerationSample
    PvSystem "1" --> "0..*" DailyGenerationRollup
    PvSystem "1" --> "0..*" GenerationForecast
    PvSystem --> Suburb : located in
    GenerationForecast --> ModelVersion : produced by
    SavingsCalculator ..> Tariff : resolves by date
    BatteryRuntimeCalculator ..> PvSystem
    BestHoursAdvisor ..> GenerationForecast
```

**Reading notes.** Nullable `TiltDeg/AzimuthDeg` pair with `EffectiveTilt/EffectiveAzimuth()` — the *entity remembers what the user actually said*; defaults are applied by behaviour, never written back. `GenerationForecast.DeviationWh(actual)` is where "−R12" is born (via `SavingsCalculator`). Domain services are stateless and pure where possible; they are the primary unit-test surface. The Python side deliberately has no mirrored class model — it works in DataFrames against the same tables; the **database is the shared contract**, and duplicating an ORM domain in two languages would double maintenance for zero benefit.

---

## 7. UML Sequence Diagrams — key flows

### 7.1 Growatt sync (measured truth enters the system)

```mermaid
sequenceDiagram
    autonumber
    participant T as Timer (15 min)
    participant G as Growatt sync job (Py Fn)
    participant KV as Key Vault
    participant GA as GrowattAdapter
    participant GS as Growatt server
    participant DB as Azure SQL

    T->>G: fire
    G->>KV: get credential-wrapping key (managed identity)
    G->>DB: load InverterConnection (creds, datalogger, status)
    G->>GA: fetch_latest(creds)
    GA->>GS: login + telemetry request
    alt success
        GS-->>GA: JSON telemetry
        GA-->>G: payload
        G->>DB: INSERT RawIngestPayloads (as received, hash)
        G->>DB: UPSERT GenerationSamples (system, sampled_at) — idempotent
        G->>DB: UPSERT today's DailyGenerationRollup (source=measured)
        G->>DB: InverterConnection: status→Connected, last_synced=now
    else auth failure (401)
        G->>DB: status→AuthFailed (user action required)
        G->>G: log warning + telemetry event
    else timeout / 5xx
        G->>G: retry w/ backoff (bounded)
        G->>DB: after N consecutive failures: status→Disconnected
    end
```

Idempotent upserts make the job safe to re-run (missed timer, overlapping run, backfill). The **9-month history backfill** is this same path fed by a one-off range fetch/CSV import — it writes the identical tables, so ML training data and live data are indistinguishable by construction.

### 7.2 Forecast pipeline (a prediction comes to exist)

```mermaid
sequenceDiagram
    autonumber
    participant WT as Timer (3×/day)
    participant W as Weather sync (C# Fn)
    participant OM as Open-Meteo
    participant DB as Azure SQL
    participant BT as Timer (nightly + post-sync)
    participant B as Forecast batch (Py)

    WT->>W: fire
    W->>OM: per suburb: weather · per panel group: tilted irradiance
    OM-->>W: JSON
    W->>DB: RawIngestPayloads + WeatherForecasts + IrradianceForecasts (append-only, issued_at)
    BT->>B: fire
    B->>DB: read latest irradiance per group + active ModelVersion
    B->>B: PhysicsBaseline per hour → clip at inverter AC
    B->>B: RegressionModel correction (if a model exists)
    B->>DB: INSERT GenerationForecasts (physics_wh + predicted_wh, model_version)
```

Append-only + `issued_at` on both weather and generation forecasts means the system can always answer *"what did we believe on Tuesday morning?"* — which is precisely the training set, and also the honest basis for the accuracy screen.

### 7.3 Dashboard read (why the user path is fast and unbreakable)

```mermaid
sequenceDiagram
    autonumber
    participant U as Browser (SPA)
    participant A as Core API
    participant DB as Azure SQL

    U->>A: GET /api/dashboard/week (JWT)
    A->>A: authenticate + authorize (owner of system)
    A->>DB: rollups (past days) + latest forecasts (future) + weather + tariff history + connection status
    DB-->>A: rows
    A->>A: SavingsCalculator (cents, tariff-by-date) · deviations · battery hours · best window
    A-->>U: one JSON view model (incl. sync-status chip state)
    Note over U,DB: Zero external calls. Growatt down? ML down?<br/>Page still renders; chip says "last synced 6h ago".
```

This is principle 2 made visible: the entire user experience degrades to *staleness*, never to *failure*.

### 7.4 Manual entry + reward (fallback path & placeholder mechanic)

```mermaid
sequenceDiagram
    autonumber
    participant U as Browser (SPA)
    participant A as Core API
    participant DB as Azure SQL

    U->>A: POST /api/manual-entries {date, total_wh}
    A->>A: validate: not future · plausible bounds vs system kWp
    A->>DB: measured rollup exists for date?
    alt measured exists
        A-->>U: 409 — measured data wins, entry rejected
    else no measured data
        A->>DB: INSERT ManualGenerationEntries
        A->>DB: UPSERT DailyGenerationRollup (source=manual)
        A->>DB: UserProfile.ad_free_until = now + 24h
        A-->>U: 201 + new rollup + reward status
    end
```

"Measured wins" is a data-integrity rule: hand-entered numbers can never overwrite inverter telemetry. The reward stamp is the *entire* ad mechanic — the ad slot itself is a feature-flagged placeholder in the SPA, per the settled Phase 1 scope.

---

## 8. UML State Diagram — InverterConnection

The status chip in the UI ("synced 2h ago" / "offline — forecast only") renders whatever this machine last wrote:

```mermaid
stateDiagram-v2
    [*] --> Unverified : user saves credentials
    Unverified --> Connected : first successful sync
    Unverified --> AuthFailed : 401 on first sync
    Connected --> Connected : sync ok (refresh last_synced)
    Connected --> Disconnected : N consecutive timeouts/5xx
    Disconnected --> Connected : sync ok
    Connected --> AuthFailed : 401 (password changed)
    Disconnected --> AuthFailed : 401
    AuthFailed --> Unverified : user re-enters credentials
    note right of Disconnected : Transient — job keeps retrying.\nUI: "offline — showing forecast only".
    note right of AuthFailed : Terminal until user acts.\nUI prompts re-auth; no retries (avoid lockout).
```

`Disconnected` (keep trying) vs `AuthFailed` (stop and ask the human) is the important distinction — retrying a bad password against an unofficial endpoint risks account lockout on a service with no support channel.

---

## 9. Phase 2–3 design outlook (designed now, built later)

### 9.1 Loadshedding simulation (Phase 2)
`SimulationEngine` (§4.1) is a pure domain service: inputs = `AREA_SCHEDULE_SLOTS` (for suburb's ESP area, chosen stage) + hourly generation curve (forecast or historical) + battery model (`UsableBatteryWh`, `EssentialLoadW`); output = per-block verdicts (solar/battery/dark) and totals — exactly the mockup's shapes. It runs in-process in C# (fast, no ML), and the ESP sync worker is a third timer in the existing C# Function App, budgeted at ~2 syncs/day within the 50-call quota (ADR-004).

### 9.2 Water-notice NLP pipeline (Phase 3)

```mermaid
flowchart LR
    S[Scraper<br/>timer, Py] --> H{content_hash<br/>seen?}
    H -- yes --> X[skip]
    H -- no --> R[(WATER_NOTICES<br/>raw text)]
    R --> P[NLP parse<br/>LLM w/ structured output]
    P --> V{valid vs<br/>schema+rules?}
    V -- no --> Q[quarantine + log<br/>for manual review]
    V -- yes --> O[(WATER_OUTAGES upsert<br/>by notice lineage)]
    O --> M[suburb matcher<br/>vs SUBURBS table]
    M --> J[(WATER_OUTAGE_SUBURBS)]
    O --> RP[restoration model] --> PR[(RESTORATION_PREDICTIONS)]
```

Same raw-first spine as Growatt: hash-dedupe, raw retained forever, parse re-runnable, model output separate from JW's stated estimate. The quarantine branch is the honest answer to "what happens when the notice format changes" — the risk the brief flagged.

---

## 10. Cross-cutting concerns

**AuthN/Z.** ASP.NET Core Identity as the user store; short-lived JWT bearer for the SPA with refresh rotation; role policies (`User`, `Admin`); ownership checks on every system-scoped endpoint (`403` on other users' ids — IDOR is the OWASP item this design most actively defends). Anonymous surface is the single public status endpoint.

**Resilience.** All outbound HTTP via typed clients wrapped in Polly: timeout (10 s) → retry ×3 with jittered backoff → circuit breaker. Workers additionally rely on idempotent upserts so retries can't double-write. ESP budget enforcement (P2) is a hard counter in the worker, not a hope.

**Observability.** Structured logging (Serilog / structlog) with correlation ids across API→ML ops calls; Application Insights sinks; `/healthz` on every container (API's version reports DB reachability + last successful sync timestamps — which doubles as the data-freshness monitor); alert on `AuthFailed` transitions and on batch-job silence > 24 h.

**Testing seams.** Domain services (pure) → unit tests; `ActualsProvider` → fake for pipeline tests without Growatt; API endpoints → integration tests on SQL testcontainer; `PhysicsBaseline` → property-based sanity (never exceeds inverter cap; zero at zero irradiance). CI runs all on PR.

**Config & secrets.** Options-pattern config in C#; pydantic-settings in Python; secrets only via Key Vault-backed settings (§5); `.env.example` documents every variable.

---

## 11. Maintenance & ADR candidates surfaced by this document

Diagrams live next to code and change via the same PRs (a PR touching container boundaries must touch this file — a stated convention, reviewable in PR descriptions). Decisions this document makes that deserve ADRs now:

- **ADR-006 — Time-series storage:** raw 5-min samples + daily rollups; append-only forecast snapshots keyed by `issued_at`. (Flagged since the handoff; now fully specified here + in schema doc.)
- **ADR-007 — ML off the hot path:** batch-precomputed forecasts; FastAPI surface is ops-only; user requests never block on Python.
- **ADR-008 — Workers as Azure Functions:** two single-runtime Function Apps (C#, Python); scheduled concerns out of the API process.
