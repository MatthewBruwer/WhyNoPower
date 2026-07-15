## 2026-07-14 — Visual direction for "week ahead" screen

**Prompt summary:** Provided B2B SaaS dark/green template as visual reference,
asked for the aesthetic to be adapted (not re-skinned) to dashboard/wizard/
form UI, starting with the "week ahead" overview screen.

**AI produced:** Translation analysis flagging 5 non-obvious adaptations
(dual-purpose green as brand+semantic colour; negative-deviation colour
choice; glass-vs-flat surfaces for charts vs cards; type scale must shrink
for dense data; 3D renders dropped from working screens), plus a proposed
screen structure (header status strip, hero rand stat, 7-day measured-vs-
forecast chart with weather row, secondary stat cards) and a design-tokens-
first process.

**Hand decisions:** Negative deviation = red (simpler, overriding AI's
amber/colour-blindness suggestion). Desktop-first layout. Rest of proposal
accepted as default.




## 2026-07-14 — First mockup: "week ahead" screen

**Prompt summary:** Requested a basic editable HTML mockup of the agreed
screen structure for iteration.

**AI produced:** Self-contained HTML/CSS mockup (no dependencies). Tokens
centralised in :root; CSS-only bar chart (measured vs dashed forecast
ghost, ±R deviation labels, red = under); weather row grid-aligned to
chart columns; glass cards for battery runtime, best load hours, rolling
accuracy; commented-out degraded "inverter offline" header state. Dummy
data modelled on the real system (4.8 kWp, R3.50/kWh, winter yields).

**Hand decisions/edits:** (log your edits here as you make them)




## 2026-07-14 — Power simulation + water feed mockups (v3)

**Prompt summary:** Extend mockup with Loadshedding Impact Calculator
(simulation view chosen over historical analytics) and Water Outage
Forecaster (feed/timeline chosen over charts).

**AI produced:** Simulation page — stage picker, 7×24h outage tracks
overlaid on solar window, per-block coverage verdicts (solar/battery/dark),
weekly coverage stats. Water page — timeline feed of parsed-notice cards
with JW-vs-model restoration predictions, provenance links, planned-outage
banner, model-accuracy cards.

**AI's own additions:** battery-depletion logic for 02:00 dark slots +
explainer; "worst week of 2023" replay option; actionable planned-outage
banner; model-vs-JW accuracy comparison.

**Hand decisions/edits:** (log yours here)




## 2026-07-14 — Suburb + load inputs (v4)

**Prompt summary:** Add suburb capture to power/water pages; add household
load input to the loadshedding page.

**AI produced:** "Your area & load" panel on power page — suburb pre-filled
from shared profile, load as preset pills (0.5/1.0/2.0/3.0 kW, framed as
outage essentials) + custom field, live battery-runtime calculation.
"Your suburbs" panel on water page with follow-another-suburb chip.
Domain-coloured focus states.

**AI's own additions:** presets instead of bare kW field (users don't know
their draw); cross-domain suburb pre-fill; multi-suburb follow concept;
live runtime feedback on preset click.

**Hand decisions/edits:** (log yours here)




## 2026-07-15 — Phase 1 API skeleton

**Prompt:** Build the Phase 1 ASP.NET Core API skeleton.
**AI produced:** 4-project solution; Identity+JWT auth w/ rotating refresh
tokens; Systems wizard CRUD; Dashboard week endpoint (rand/battery/best-
hours/deviation per analytics doc); manual entries w/ measured-wins rule
+ reward; public status stub; 8 unit tests. Unverified by compiler (no
NuGet access in sandbox) — flagged for local `dotnet build`.
**AI's own additions:** RefreshTokens table + reuse-detection chain
revocation (not in original schema); IsCurtailed column; 404-not-403 IDOR
choice (deviates from system-design doc, flagged).
**Hand decisions/edits:** (log yours after `dotnet build` + review)




