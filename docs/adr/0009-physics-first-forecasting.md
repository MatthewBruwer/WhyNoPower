# 0009 - Physics-first forecasting with learned residual correction

**Status:** Accepted
**Date:** 2026-07-15

## Context

The Phase 1 promise is a generation forecast corrected by the user's measured
reality. A pure-ML approach (predict generation directly from weather) wastes
what is already known deterministically - panel geometry, irradiance physics,
temperature derating, inverter clipping - and with only ~9 months of history
would have to relearn it from scratch. A pure-physics approach knows nothing
about this specific roof: its shading pattern by sun position, soiling, or the
inverter's real efficiency curve. There is also a data honesty problem unique
to the off-grid installation: once the battery is full and load is low, the
inverter curtails the panels, so measured generation is demand-limited, not
sun-limited. Training naively on that history teaches a model that sunshine
produces little power. (Full treatment: docs/analytics/analytics-and-ml.md.)

## Decision

Forecast in two layers. A physics baseline (per-group plane-of-array
irradiance, NOCT temperature correction, static losses, inverter clip) ships
first and is always computed. A regression - trained only on hours that pass a
curtailment filter, using only features available at forecast issue time -
corrects the baseline. Every forecast row stores both figures. A model is
promoted to serve users only if it beats the physics baseline by a skill score
of at least 0.15 on a held-out recent month under expanding-window time-series
cross-validation; if a serving model's rolling 7-day skill turns negative, the
system automatically falls back to physics (see ADR-0007).

## Consequences

**Good:**
- The product works on day one (physics-only) and improves measurably; the
  stored physics-vs-predicted pair makes the ML's lift permanently auditable.
- Curtailment filtering means the model learns the roof's potential rather
  than the household's demand habits - and the UI can badge battery-full days
  instead of reporting them as forecast misses.
- The ship bar (skill >= 0.15) makes "the ML is real" a falsifiable claim
  rather than a marketing line.

**Bad / trade-offs accepted:**
- The model may fail the bar: if 9 months of clean data cannot beat physics by
  15%, Phase 1 ships physics-only with the analysis documented. Accepted -
  an honest negative result with the receipts is still a portfolio outcome.
- Filtering curtailed hours shrinks an already small training set.
- Predicting potential rather than delivered energy means the rand figure
  needs a separate demand-envelope step in heavily-curtailed periods; whether
  Phase 1 needs it is decided by the backfill's measured curtailment share
  (open item in the analytics doc).
