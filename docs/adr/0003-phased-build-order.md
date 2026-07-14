# 0003 - Phased build order: solar -> power -> water

**Status:** Accepted
**Date:** 2026-07-14

## Context

The project spans three data domains of very different difficulty: solar
(a well-documented free API, a tractable regression problem), power
(a rate-limited third-party API with no live event to demo against, since
load-shedding is currently suspended), and water (no official API at all -
requires scraping and NLP parsing of unstructured municipal notices, the
hardest and most novel domain). Attempting all three in parallel risks a
project that is broad but shallow, or one that stalls midway with nothing
finished. Portfolio projects are judged more favourably as one complete,
polished thing than as three incomplete ones.

## Decision

Build strictly in order - solar, then power, then water - where each phase
is a complete vertical slice (feature + auth + database + tests + CI/CD +
Azure deployment + documentation) before the next phase begins. Order is
chosen by ascending difficulty and ascending dependency on infrastructure
already built in the previous phase.

## Consequences

**Good:**
- If the project stops after any phase, what exists is still a finished,
  deployed, documented product - not an unfinished skeleton.
- Later phases reuse infrastructure (auth, sync-worker pattern, CI/CD
  pipeline) built in Phase 1, so they get progressively faster to build.
- Matches the difficulty curve: the hardest domain (water) is tackled last,
  once the architecture has already been proven twice.

**Bad / trade-offs accepted:**
- The most distinctive, differentiating feature (water-outage NLP parsing)
  is also the last thing built - if time runs out, the project's most
  novel piece may never ship.
- Cross-domain features (the "week ahead" combined view) can't be fully
  built or demoed until Phase 3 is at least partially done.