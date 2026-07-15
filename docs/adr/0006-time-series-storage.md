# 0006 - Time-series storage: raw samples + daily rollups, append-only forecasts

**Status:** Accepted
**Date:** 2026-07-15

## Context

Growatt telemetry arrives at ~5-minute grain (~100k rows/year/system) and is
the ML training data. Dashboards, however, mostly need daily totals, and
querying 100k rows to draw seven bars is wasteful. Separately, forecasts are
re-issued several times a day as weather updates: if a re-issue overwrites the
previous row, the question "what did we predict yesterday morning?" becomes
unanswerable - and that question is both the ML training set (forecast inputs
as they were issued) and the honest basis for the forecast-vs-actual accuracy
screen. Nothing about either need is served by a single mutable table.

## Decision

Store generation data at two grains: raw `GENERATION_SAMPLES` (never
aggregated in place, feeds ML) and `DAILY_GENERATION_ROLLUPS` computed from
them (feeds dashboards; keyed to the SAST calendar day; carries a
measured/manual source flag and an `IsCurtailed` badge). Store all forecasts -
weather, irradiance, and our own generation forecasts - **append-only**, keyed
by `issued_at` plus target time. A re-issue is a new row; nothing is ever
updated or deleted. The forecast graded against measured data is always the
day-ahead snapshot (latest row issued before the target day began).

## Consequences

**Good:**
- Dashboard queries are cheap and index-friendly regardless of sample volume.
- Training data provably matches what was known at issue time - no leakage
  from hindsight, and accuracy stats can be recomputed for any historical day.
- Model skill (predicted vs physics vs measured) is permanently auditable in
  SQL, since every forecast row stores both figures.

**Bad / trade-offs accepted:**
- Storage grows without bound; an archival/retention policy will eventually be
  needed (explicitly deferred - not a Phase 1 problem at one system's volume).
- Rollups can drift from samples if a recompute bug slips in; mitigated by
  making rollup computation idempotent and re-runnable from raw at any time.
- Two representations of "yesterday's generation" means every consumer must
  know which one to read; the convention (dashboards read rollups, ML reads
  samples) is documented in the schema design doc.
