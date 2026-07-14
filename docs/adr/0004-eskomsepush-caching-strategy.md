# 0004 - EskomSePush caching strategy: sync worker, not a live proxy

**Status:** Accepted
**Date:** 2026-07-14

## Context

EskomSePush is the only practical source for South African load-shedding
and grid-status data, but its free tier is capped at 50 calls/day. A
typical web app pattern - calling the external API directly on each user
request - would exhaust that quota almost immediately with more than a
handful of users, making the app unusable in practice.

## Decision

Never call EskomSePush directly from a user-facing request. Instead, a
scheduled sync worker calls the API a small, fixed number of times per day
and writes the results into our own database; all user requests are served
from that database, regardless of how many users are active.

## Consequences

**Good:**
- User-facing performance is decoupled from a third party's rate limit and
  uptime - the app stays fast and available even if EskomSePush is slow or
  down between syncs.
- Scales to any number of users without scaling API usage, since usage is
  fixed by the sync schedule, not by traffic.
- Produces our own historical dataset over time, useful for analytics and
  the grid-risk ML component - a genuine architectural feature, not just a
  workaround.

**Bad / trade-offs accepted:**
- Data is only as fresh as the last sync - not real-time. A schedule change
  seconds after a sync won't be reflected until the next one.
- Adds a background-job component (with its own failure modes: missed
  syncs, partial writes) that a direct-proxy design wouldn't need.