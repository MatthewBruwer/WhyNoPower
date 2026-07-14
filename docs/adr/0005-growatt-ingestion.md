# 0005 - Growatt ShinePhone ingestion: unofficial API, isolated

**Status:** Accepted
**Date:** 2026-07-14

## Context

Actual (measured) solar generation data is needed to train and validate
the Phase 1 ML regression, and to show real forecast-vs-actual accuracy to
users - without it, "today's generation" would just be the model checking
its own estimate. The installed inverter (Growatt SPF 5000 ES) is monitored
through the ShinePhone app, which is a client for Growatt's server API.
There is no official public API or documented contract for this endpoint;
a community-maintained Python library exists that reverse-engineers it.
This mirrors the water-notice scraping problem in Phase 3: a valuable data
source with no stable, supported interface.

## Decision

Ingest Growatt data through the unofficial server API via a scheduled
Python sync worker, isolated behind an internal interface (e.g. an
`ActualsProvider` abstraction) so the rest of the system depends only on
that interface, not on Growatt specifics. Raw API responses are stored
as received, in addition to any parsed/normalised data. ShinePhone
credentials are treated as secrets: never committed to Git, stored via
`.env`/Azure Key Vault, and encrypted at rest if persisted per-user.

## Decision (scope)

For Phase 1, this integration is built and used for a single real system
(the author's own installation). Support for other inverter brands/users
is out of scope for Phase 1; manual entry remains the fallback path for
users on unsupported hardware.

## Consequences

**Good:**
- Unlocks real training/validation data (Growatt's history) instead of
  relying only on generic sources like PVGIS.
- The interface abstraction means a future break in the unofficial API, or
  a switch to an official API/other inverter brands, only requires a new
  implementation behind the same interface - not a system-wide rewrite.
- Storing raw responses preserves data even if the parsing logic downstream
  has a bug, and gives an audit trail if the upstream format changes.

**Bad / trade-offs accepted:**
- Unofficial and unsupported: Growatt could change or break this API at
  any time with no notice, unlike a documented public API.
- Introduces a second set of user credentials to manage securely (in
  addition to the app's own auth), with its own compromise risk if leaked.
- Legally/ethically greyer than a documented public API - acceptable for a
  personal system the author owns and controls, but would need reassessing
  before offering this integration to other users at scale.