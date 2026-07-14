# WhyNoPower — South African Utility Resilience Platform
## Project Handoff Brief

**Author:** Matthew (with Claude), July 2026
**Status:** Ideation complete — ready to begin Phase 1
**Purpose of this document:** Full context for starting the build. Drop into the build chat or a Claude Project's knowledge.

---

## 1. Vision

A household utility resilience platform for South Africa. It ingests power, water, and weather/solar data, stores and analyses it, and gives households **predictive, personalised, rand-denominated insight** — not just schedules. Trackers display; WhyNoPower predicts and advises.

**Context that shaped the idea (verified July 2026):**
- Load-shedding is suspended — 365+ consecutive days as of May 2026, none forecast for winter 2026 — but officially "paused with meaningful risk of recurrence" (analysts flag 2027–28 risk; Eskom's own high-risk scenario would resume stages 2–6).
- Unplanned outages are the live problem: ~92,000 grid outages nationally last year; Gauteng's average unplanned outage lasts ~14 hours.
- Johannesburg water insecurity is severe (multi-day/multi-week outages, 12-hour planned shutdowns across 20+ suburbs), and there is **no official water-outage API** — notices are unstructured text.
- Rooftop solar has boomed (5,800+ MW added since 2022); with tariffs up 400%+ since 2010 and ~18% more increases approved over two years, the solar question is now economic, not emergency.

Therefore: load-shedding is one module (dormant-but-ready, with historical analysis and a simulation mode), while solar economics, unplanned-outage awareness, and water-outage intelligence carry the live value.

## 2. Portfolio goals this project must demonstrate

1. System & software architecture (documented, deliberate)
2. AI-assisted frontend — entire UI/UX co-designed with AI, with the process itself documented
3. Well-designed, safe database
4. Security features (auth, OWASP, secrets, POPIA-aware design)
5. External API ingestion + genuinely valuable insight (the AI/ML layer)
6. Cloud deployment & storage (app, DB, whole system)
7. Documentation: C4 model, UML, ADRs
8. Full professional Git/GitHub usage (branching, PRs, Actions/CI-CD, issues)

Additions agreed during ideation: CI/CD (GitHub Actions), automated testing with coverage, Docker, own documented REST API (OpenAPI/Swagger), observability (structured logging, health checks), resilience around external APIs (retries, timeouts, caching), secrets/config management, POPIA-aware data design, dashboard visualisation, background jobs.

## 3. The three data domains

### Solar & weather (Phase 1)
- **Sources:** Open-Meteo (free, no key, includes satellite solar-radiation data) as workhorse; Solcast free "Home PV System" tier for purpose-built rooftop PV forecasts; AfriGIS/SAWS weather API as optional local alerts source (tight free tier: 50 credits/day pilot).
- **Features:** user describes their panel setup (capacity, orientation, area); app forecasts week-ahead generation (kWh) and translates it into rands saved at current tariffs.
- **ML component:** regression predicting the user's PV output from irradiance/weather forecast (train/validate against PVGIS or historical data).

### Power (Phase 2)
- **Source:** EskomSePush API. **Free tier = 50 calls/day** — this forces the correct architecture: a scheduled sync worker fetches a few times daily into our DB; users are always served from our own data. WhyNoPower is a data platform, not a proxy.
- **Features:** area schedules (dormant but ready), historical load-shedding analytics, grid-risk indicator (EAF / unplanned-outage figures), **simulation mode** that can replay e.g. a Stage 4 week — essential because there's no live load-shedding to demo, and a professional feature in its own right.
- **ML component:** anomaly/risk scoring on grid health indicators.

### Water (Phase 3 — hardest, highest distinctiveness)
- **Source:** none official. Scrape Johannesburg Water's daily notices (website; optionally social feeds) and parse unstructured announcement text into structured outage records (area, cause, planned/unplanned, expected restoration). Scope strictly to Johannesburg Water first; other municipalities = future work.
- **Features:** outage feed per suburb, outage history, restoration-time patterns, planned-maintenance warnings.
- **ML/AI component:** the NLP/LLM parsing pipeline itself, plus predicting restoration time from cause/area/history.

**Cross-domain insight layer:** "Your week ahead — expected solar generation and savings, water interruptions in your area, grid risk status — in rands and hours."

## 4. Stack & architecture (decided)

- **Frontend:** React + Tailwind (best fit for AI-assisted UI generation; adds a second ecosystem to the portfolio).
- **Core backend:** C# / ASP.NET Core Web API — auth, business logic, data access, sync workers. Builds on existing coursework (ASP.NET Core, C#).
- **ML service:** Python. Starts life as a Jupyter notebook (explore + train); promoted to a small FastAPI service (or scheduled batch job writing predictions to the shared DB) once the model works. Do NOT build both services from day one.
- **Database:** Azure SQL (relational, normalised, constraints/indexes; parameterised access; least-privilege accounts; encrypt sensitive columns).
- **Cloud:** Azure (aligns with coursework; Azure for Students credit + free tiers: App Service, SQL DB, Functions). Docker for reproducibility; docker-compose for local dev. Kubernetes explicitly out of scope.
- **Shape:** modular monolith core + separate ML service. C4 containers: React frontend · ASP.NET Core API · Python ML service · SQL database · sync worker(s) · external APIs.

**Rationale for polyglot:** mirrors industry practice (production ML is Python; ML.NET ecosystem is thin), enriches the C4/architecture story, and builds exactly the Python/sklearn/pandas base the UJ Honours in AI will assume.

## 5. Build order (decided): solar → power → water

Each phase is a **complete vertical slice**: feature + auth + DB + tests + CI/CD + Azure deploy + docs, finished before the next begins. If the project stalls after Phase 1, what exists is still a shipped, documented portfolio piece.

Suggested Phase 1 sequence: repo + hygiene setup → C4 L1/L2 + first ADRs → DB schema → ASP.NET Core API skeleton + auth → Open-Meteo ingestion → notebook ML model → React dashboard (AI-assisted, logged) → tests + Actions pipeline → Azure deploy → promote model to service.

## 6. Security & privacy principles

- Auth: ASP.NET Core Identity (or OAuth2/OIDC), role-based authorisation.
- OWASP Top 10 awareness; input validation; HTTPS everywhere.
- **Secrets:** never in Git. `.env` (gitignored) + committed `.env.example`; GitHub Actions secrets; Azure config/Key Vault. A leaked key = rotate immediately (Git history is forever).
- **POPIA by design:** store suburb/zone, never exact address; data minimisation; document consent/deletion thinking in the security write-up.
- Public "status page" view (area-level, logged-out) vs personalised insight (authenticated) — a deliberate anonymous/authenticated security boundary.

## 7. Documentation plan

- **C4 model:** L1 system context, L2 containers, L3 backend components (L4 optional).
- **UML:** class diagrams for core domain, sequence diagrams for key flows (e.g. sync-worker fetch, prediction request).
- **ADRs** in `docs/adr/`: short Context/Decision/Consequences files. First four are already decided: polyglot C#+Python split; Azure; phase order; EskomSePush caching strategy. Add one per significant future decision.
- **`docs/ai-workflow.md`:** running log of the AI-assisted UI process — prompts, iterations, what was changed by hand. A distinctive portfolio artifact in its own right.
- **OpenAPI/Swagger** for the app's own REST API.
- README with architecture summary, setup, and badges (build, coverage).

## 8. Git/GitHub practice

- **Public repo from day one** (portfolio visibility; enforces hygiene). Secrets hygiene from commit one.
- Branching: feature branches → PRs → merge to main (self-review PRs are fine solo; write real descriptions).
- Issues + milestones per phase; conventional commits; tags/releases at phase completions.
- GitHub Actions: build + test on PR; deploy to Azure on merge to main.

## 9. Risks & mitigations

| Risk | Mitigation |
|---|---|
| Scope sprawl / burnout | Vertical slices; Phase 1 alone must stand as a finished product |
| No live load-shedding to demo | Historical data + simulation mode |
| ESP 50 calls/day quota | Sync worker + own DB (architectural feature) |
| JW notice format changes / scraping fragility | Isolate parsing behind an interface; store raw notices; Phase 3 only |
| ML underdelivers | Solar regression is the one must-ship model; others are stretch |
| Secrets leak | .gitignore from first commit; rotate on any leak |
| Free-tier cloud limits | Azure for Students; keep services minimal; document costs |

## 10. Naming & identity

- **Name:** WhyNoPower — South African Utility Resilience Platform. Deliberately humorous/non-conventional in the EskomSePush tradition: the name earns the memorability, the subtitle carries the credibility.
- Known, accepted trade-off: the name is power-centric while the platform also covers water and solar; the subtitle states the full scope. Power is the emotional anchor of the SA utility experience, so the mismatch is acceptable.
- Check GitHub username/repo availability before first commit; repo slug suggestion: `whynopower`.

## 11. Stretch goals (explicitly parked)

- Notifications (email/push) for outage announcements — valuable, but requires messaging infrastructure; revisit after Phase 3.
- Municipalities beyond Johannesburg Water.
- Mobile app / PWA packaging.

---

**First actions in the build chat:** create the public repo with .gitignore + .env.example + README skeleton → write ADRs 001–004 → draw C4 L1/L2 → design the Phase 1 (solar) database schema.
