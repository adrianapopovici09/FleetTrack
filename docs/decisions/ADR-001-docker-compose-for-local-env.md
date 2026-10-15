#  ADR-001: Docker Compose vs Native install vs Aspire

* **Status:** Accepted
* **Date:** 2026-10-15

## Context and Problem Statement

The project needs a local Postgres today, plus more services later. It's a learning project with a $0 budget on Windows, and the goal is to understand the plumbing.

## Considered Options

* Native installs
* Docker compose
* .NET Aspire

## Decision Outcome

Chosen option: "Docker compose", because I want to learn what is about and do the wiring myself.

### Consequences

* Good, because I will understand better what happens in the background
* Good, because native install has the risk of the application working only for the pc it was developed on
* Bad, because it would be faster to do it with another tool that already sets up everything
* Bad, because connection string both .env and user secrets and keeping them in sync is manual

## Revisit trigger

* The stack reaches 5 services (Postgres, RabbitMQ, Valkey, Keycloak, LGTM by M12) and keeping the config in step between compose, `.env` and user secrets causes a bug.
* M13: compare the compose stack against an Aspire AppHost (planned in M13-T6) and record the verdict in ADR-016.