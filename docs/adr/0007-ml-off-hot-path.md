# 0007 - ML off the hot path: batch-precomputed forecasts

**Status:** Accepted
**Date:** 2026-07-15

## Context

ADR-001 split the backend into a C# API and a Python ML service, accepting a
network boundary and a new failure mode (the ML service being down or slow) as
a trade-off. On free-tier hosting that failure mode is not hypothetical: App
Service instances cold-start, and a synchronous API-to-ML call on every
dashboard request would make the platform's core screen exactly as reliable as
its least reliable container. ADR-004 already established the house pattern
for third-party dependencies - serve users from our own database, never from a
live upstream call. Our own ML service deserves no more trust on the request
path than EskomSePush gets.

## Decision

No user-facing request ever calls the ML service. Forecasts are precomputed by
a scheduled batch job (nightly plus after each weather sync) that writes
physics-baseline and ML-corrected figures to `GENERATION_FORECASTS`; the API
serves dashboards purely from Azure SQL. The FastAPI surface exists for
operations only: retrain, recompute, health. If the model's rolling 7-day
skill vs the physics baseline goes negative, the batch job serves physics
figures (predicted = physics, null model version) until a retrained model
clears the promotion bar again.

## Consequences

**Good:**
- The dashboard's failure mode is staleness, never an error page: Growatt,
  Open-Meteo, and the ML service can all be down and the screen still renders.
- Free-tier cold starts and slow training jobs cannot affect user latency.
- The physics fallback means a bad model degrades forecast quality, not
  availability - and the degradation is visible and alertable, not silent.

**Bad / trade-offs accepted:**
- Forecasts are only as fresh as the last batch run; a mid-morning weather
  shift is not reflected until the next scheduled recompute.
- No on-demand "recalculate my forecast now" for users without additional
  plumbing (an ops endpoint exists, but exposing it per-user is future work).
- Two code paths produce forecast rows (scheduled batch and ops-triggered
  recompute), which must stay behaviourally identical - enforced by sharing
  the same module rather than duplicating logic.
