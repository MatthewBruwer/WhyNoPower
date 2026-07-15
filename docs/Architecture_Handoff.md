# WhyNoPower — Architecture Handoff
**Purpose of this document:** Everything decided in the build chat so far, written for a fresh chat (UI design) that won't have that conversation's history. Treat this, alongside `whynopower-project-brief.md`, as authoritative context — don't re-litigate settled decisions unless a genuine problem is spotted.

**Status as of this handoff:** Repo is live, public, bootstrapped, branch-protected. ADRs 001–005 merged. C4 diagrams and DB schema not yet built. UI design is the next piece of work, happening in a separate chat.

---

## 1. Project identity

**WhyNoPower — South African Utility Resilience Platform.** A household utility resilience tool covering solar, power outages, and water outages in South Africa, built as Matthew's CS portfolio project (feeding into an Honours in AI). Name is deliberately humorous/non-conventional in the EskomSePush tradition — subtitle carries the credibility, name carries the memorability. Known accepted trade-off: name is power-centric, platform covers all three domains.

Load-shedding has been suspended for 365+ days as of mid-2026, so the platform's live value proposition is **unplanned outages, water insecurity, and solar economics** — load-shedding is a dormant historical/simulation module, not the headline feature.

## 2. Portfolio goals this must demonstrate

System/software architecture · AI-assisted frontend (process documented) · secure, well-designed database · security features (auth, OWASP, POPIA) · external API ingestion + genuine ML-derived insight · cloud deployment · documentation (C4, UML, ADRs) · full professional Git/GitHub usage.

## 3. Decided stack (see ADRs 001–002)

- **Frontend:** React + Tailwind
- **Core backend:** ASP.NET Core (C#) — auth, business logic, data access, sync workers
- **ML service:** Python — starts as a notebook, promoted to FastAPI once a model works
- **Database:** Azure SQL
- **Cloud:** Azure (App Service, Azure SQL, Functions; Azure for Students credit)
- **Repo shape:** polyglot monorepo, one top-level folder per C4 container: `api/`, `ml/`, `frontend/`, `docs/`

A Blazor scaffold was accidentally created first (Visual Studio default) and was deliberately removed in favour of React, to preserve the AI-assisted-frontend and two-ecosystem polyglot story. This is settled — do not reintroduce Blazor without a new ADR.

## 4. Build order (ADR-003): solar → power → water

Each phase is a **complete vertical slice** — feature + auth + DB + tests + CI/CD + Azure deploy + docs — finished before the next begins. **We are in Phase 1 (Solar).** The rest of this document is Phase 1 scope only.

## 5. Repo state

- Public repo: `github.com/MatthewBruwer/WhyNoPower`
- Bootstrap commit done: `.gitignore` (combined .NET/Node/Python/IDE/secrets), `.env.example`, `LICENSE` (MIT), `README.md` skeleton
- Branch protection on `main`: PRs required, 0 required approvals (solo dev — GitHub blocks self-approval), admins included, force-push/deletion blocked
- Workflow: feature branches (`feat/…`, `docs/…`, `chore/…`) → PR → merge. Conventional commits (`type(scope): summary`)
- `docs/adr/0001`–`0005` merged (see below)
- `docs/ai-workflow.md` exists, empty — **this is where the UI chat must log every AI prompt/iteration/hand-edit as it happens.** This is a required portfolio artifact, not optional documentation.
- `api/`, `frontend/`, `ml/` folders not yet scaffolded (intentionally — created by their own tooling when that work starts, not pre-created as empty placeholders)

## 6. ADR summary (full text in `docs/adr/`)

| # | Decision |
|---|---|
| 0001 | Polyglot backend: C# (ASP.NET Core) + Python, not a single-language stack |
| 0002 | Azure as the cloud platform |
| 0003 | Phased build order: solar → power → water, each a complete vertical slice |
| 0004 | EskomSePush: scheduled sync worker + own DB, never a live proxy (50 calls/day quota) |
| 0005 | Growatt ShinePhone ingestion: unofficial API, isolated behind an interface, raw responses stored, credentials treated as secrets |

## 7. Phase 1 (Solar) domain — settled feature set

### 7.1 The real system this is modelled on
- Installer: Growatt SPF 5000 ES inverter (off-grid — **no grid export, ever**; Export to Grid reads permanently 0.00W). This simplifies the economics model: **savings = self-consumed solar × tariff**, no feed-in/net-metering logic needed.
- Panel array, **two groups with different orientation** (this matters — schema must support per-group tilt/azimuth, not a single system-wide value):
  - Group A: 5 × 590W panels, North-East facing, 30° tilt
  - Group B: 3 × 625W panels, South-West facing, 80° tilt (steep — favours low winter sun / late-afternoon generation, deliberately widening the generation curve for an off-grid system)
  - Total: 4,825W ≈ 4.8 kWp
- Battery: 5 kWh Li-ion, assume 80% usable DoD (4 kWh usable) unless told otherwise
- Inverter cap: 5 kW AC output
- Tariff: R3.50/kWh (single flat user-entered rand rate — no tariff-table complexity in Phase 1)
- Monitoring: Growatt ShinePhone app / server.growatt.com. Installed 2025-10-15, so ~9 months of real generation history exists. Ingestion via the community `growattServer` Python library (unofficial API — see ADR-005). Datalogger connectivity should be confirmed as online before relying on live sync.

### 7.2 Core features (settled)
1. **Weather display** — hourly forecast (temp, conditions), sourced from the same Open-Meteo call feeding the generation model. Show weather and its generation consequence side by side — that juxtaposition is the product's core thesis.
2. **System profile wizard (onboarding)** — captures: panel groups (count × wattage each, tilt, azimuth), battery capacity + usable DoD, inverter max AC kW, tariff R/kWh. Scoped to *"help me find my kWp, tilt and orientation"* with sensible defaults (e.g. north-facing, 26° ≈ Joburg latitude) — explicitly **not** an electrical string-design tool (series/parallel wiring calculations were considered and cut as scope sprawl).
3. **Generation forecast** — irradiance forecast × per-group kWp/tilt/azimuth, summed across groups, capped at inverter AC max (clipping). Open-Meteo handles tilted-plane irradiance directly — **we consume tilted irradiance, we don't compute solar geometry ourselves.**
4. **Rand translation** — forecast kWh × tariff, shown everywhere generation numbers appear (e.g. "this week's sun is worth ~R340").
5. **Battery backup-runtime estimate** — battery kWh (usable) ÷ typical household load → "ride out an X-hour outage." First bridge between the solar and power domains.
6. **Best-hours-for-loads recommendation** — from the generation curve, suggest when to run geyser/pool pump/dishwasher. Turns a chart into actionable advice.
7. **Analytics board — measured vs forecast, not forecast alone:**
   - Past days: **measured** generation (from Growatt, once ingestion is live) — the source of ML training/validation data
   - Future days: **forecast** (physics baseline + regression correction)
   - **Forecast-vs-actual deviation view** — shown per day and as a rolling accuracy stat, in both kWh and rands (e.g. "R12 less than expected"). This is the visible proof the ML layer is real, not decorative — a priority screen for the portfolio story.
   - Manual daily-entry stays as a fallback input for users without a supported inverter (i.e., not needed for Matthew's own system once Growatt sync is live, but a real feature for the multi-user product).
8. **Ad/reward mechanic (deferred, but design it now)** — do **not** integrate a real ad network (traffic too low to matter, adds POPIA/tracking complexity, looks bad to reviewers). Instead: build a feature-flagged placeholder ad slot with fully implemented reward logic — contribute a manual daily reading → ad-free for 24h. Demonstrates monetisation *design* without shipping a tracker. Real ad network is a stretch goal, not Phase 1 scope.

### 7.3 ML component
Two layers, both must be visible in the product, not just the backend:
1. **Physics baseline** — panels × irradiance × losses (a formula, not ML)
2. **Regression** — trained on the user's actual Growatt readings against forecast inputs, corrects the naive calculation for real-world effects (shading, dust, temperature derating, inverter losses). This is what the forecast-vs-actual view is proving.

## 8. Design decisions carried into schema (not yet built — next task after this handoff)

- **Multi-user from day one** — `Users` → `Systems` → `PanelGroups`, even though Matthew is user #1. Retrofitting multi-user later is the mistake to avoid.
- **Panel groups are per-system, plural, with their own tilt/azimuth/wattage/count** — not a single flat system-level orientation field.
- **Time-series data: store raw + rollup** — Growatt logs ~5-minute samples (~100k rows/year/user). Store raw samples (feeds ML) *and* a daily rollup table (feeds the dashboard cheaply). This is itself an ADR candidate, not yet written.
- **Units:** all timestamps UTC (SAST display-only), energy stored as Wh integers (not kWh floats) to avoid rounding debt.
- **POPIA-aware:** suburb/zone only, never exact address.

## 9. Open items / things the UI chat may need to ask about or flag back

- Growatt ShinePhone account access was not yet available at last check — bulk CSV backfill of the 9-month history and confirming the exact inverter/battery model via the device list are still pending.
- Confirm whether `server.growatt.com`'s web portal offers a CSV export for bulk backfill (mentioned as a strong option, not yet verified).
- Datalogger showed **Disconnected** in the last screenshot check — worth confirming it's online before designing any "live now" UI states.

## 10. What the UI chat should produce

Per the brief and this project's working style: **discuss approach before generating large artifacts**, explain reasoning for significant choices (practical, not lecture-length), and **log every AI-assisted UI prompt/iteration/hand-edit into `docs/ai-workflow.md` as it happens** — this log is itself a graded portfolio deliverable, not a nice-to-have.

Suggested starting point: one screen answering *"what does my week look like?"* — combining forecast, rand savings, weather, and (once available) measured-vs-forecast — since that's the platform's thesis in a single view.
