# 0001 - Polyglot backend: C# + Python

**Status:** Accepted
**Date:** 2026-07-14

## Context

The project must ingest external data, apply an ML model to it, and serve
predictions through a documented REST API - while also demonstrating
production-realistic engineering practice for a CS/AI Honours portfolio.
ASP.NET Core (C#) is the strongest existing skill and the natural home for
auth, business logic, and data access. Machine learning, however, is
overwhelmingly a Python ecosystem - the mature libraries (scikit-learn,
pandas), the eventual Honours in AI coursework, and prevailing industry
practice (production ML is written in Python; the ML.NET ecosystem is thin
by comparison) all point the same way. Building the ML component in C# would
mean fighting the tools; building the entire system in Python would abandon
existing ASP.NET Core strength and the auth/data-access maturity it offers.

## Decision

Split the backend into two containers: an ASP.NET Core (C#) Web API that
owns auth, business logic, and data access; and a separate Python service
for the ML component (starting as a notebook, promoted to a small FastAPI
service once a model works). The two communicate over HTTP within the same
monorepo.

## Consequences

**Good:**
- Each language is used where it's strongest, rather than forcing one
  language to do a job it's weak at.
- Mirrors real-world architecture (a polyglot system, not a toy), which
  strengthens the architecture/documentation portfolio goal.
- Builds the Python/pandas/scikit-learn foundation the AI Honours will
  assume.

**Bad / trade-offs accepted:**
- Two runtimes to build, test, and deploy instead of one - more CI/CD and
  Docker complexity than a single-language app.
- A network boundary between API and ML service adds latency and a new
  failure mode (the ML service being down or slow) that a single-process
  app wouldn't have.
- Solo development means context-switching between two languages and
  ecosystems throughout the project.