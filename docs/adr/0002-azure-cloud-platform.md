# 0002 - Cloud platform: Azure

**Status:** Accepted
**Date:** 2026-07-14

## Context

The project needs to demonstrate real cloud deployment and storage (app,
database, and the whole system) as a portfolio goal, on a student budget.
AWS, Azure, and GCP are all viable technically. Azure aligns directly with
existing coursework, and Azure for Students provides free credit alongside
free tiers for the specific services this project needs (App Service, Azure
SQL, Functions) - reducing both the learning curve and the risk of
unexpected cost on a project with no revenue.

## Decision

Deploy the whole system on Azure: App Service for the API and ML service,
Azure SQL for the database, and Azure Functions for background/sync jobs
where appropriate. Docker is used for reproducibility and local development
(docker-compose); Kubernetes is explicitly out of scope.

## Consequences

**Good:**
- Reuses coursework knowledge instead of learning a new cloud provider from
  zero.
- Free/student tiers cover Phase 1 comfortably, keeping cost near zero.
- Azure SQL gives a fully managed relational database with minimal ops
  overhead.

**Bad / trade-offs accepted:**
- Less transferable than AWS experience in some job markets, where AWS is
  the more commonly requested cloud skill.
- Free-tier limits (compute, storage, SQL DTUs) will eventually cap what
  the deployed demo can handle under real load - acceptable for a portfolio
  piece, would need revisiting for real users.
- Vendor lock-in to Azure-specific services (e.g. Azure SQL specifics) if
  the project ever needed to migrate.