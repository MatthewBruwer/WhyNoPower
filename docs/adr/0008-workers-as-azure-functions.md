# 0008 - Sync workers as Azure Functions (two single-runtime Function Apps)

**Status:** Accepted
**Date:** 2026-07-15

## Context

The platform's ingestion is scheduled, not request-driven: Growatt telemetry
every 15 minutes, Open-Meteo a few times daily, EskomSePush on a fixed quota
budget in Phase 2. The obvious alternative - hosted BackgroundServices inside
the API process - couples ingestion uptime to the API's lifecycle, which on
free-tier App Service includes idling and cold starts; a sleeping API means
missed syncs. There is also a language constraint: the Growatt worker must be
Python (the only practical client is the community growattServer library,
per ADR-005), while the weather sync is naturally C#. An Azure Function App
hosts a single runtime, so these cannot share one app.

## Decision

Run all scheduled ingestion as timer-triggered Azure Functions on the
consumption plan, in **two Function Apps**: one .NET (weather sync now, ESP
sync in Phase 2) and one Python (Growatt sync now, the ML forecast batch job
alongside it, sharing the ml/ package). Each worker owns its failure domain,
logging, and retry behaviour; all writes are idempotent upserts so overlapping
or re-run invocations are safe.

## Consequences

**Good:**
- Timers fire regardless of whether the API is warm - data freshness is
  decoupled from user traffic entirely.
- Per-worker logs and alerts (e.g. Growatt AuthFailed transitions) instead of
  one process's interleaved noise; failures isolate instead of cascading.
- Consumption-plan pricing rounds to zero at this scale.

**Bad / trade-offs accepted:**
- Two Function Apps means two deployment targets, two sets of app settings,
  and two CI/CD pipeline legs - real overhead for a solo developer.
- Local development of timer functions is clumsier than a BackgroundService
  (Functions Core Tools required, or invoking job code directly as scripts).
- Consumption-plan cold starts add seconds to each timer invocation -
  irrelevant for a 15-minute sync cadence, but worth knowing before reusing
  these workers for anything latency-sensitive.
