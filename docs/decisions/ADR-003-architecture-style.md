# ADR-003-architecture-style

* **Status:** Accepted for stage 1 (layered). Revisit in M05 (modular monolith) and M09 (service extraction).
* **Date:** 2026-10-03

## Context and Problem Statement

Architecture for the application

## Considered Options

* Monolith
* Modular Monolith
* Microservices

## Decision Outcome

Chosen option: start as a layered monolith with Clean Architecture / Onion project boundaries, because the goal
is to have a long term project, easy to test. The plan evolves it into a modular monolith in M05.

Reference matrix:
FleetTrack.Api -> FleetTrack.Application
FleetTrack.Api -> FleetTrack.Infrastructure, but only for the exposed extension method used for registration in the composition root
FleetTrack.Application -> FleetTrack.Domain
FleetTrack.Infrastructure -> FleetTrack.Domain + FleetTrack.Application

### Consequences

* Good, because will have a well structured solution, with easy changeable moving parts (e.g. allows
  infrastructure concerns to be changed easily without other components being changed), testable
* Bad, because of high number of projects that add complexity

## Revisit trigger

<!-- TODO (M00): when should this decision be reopened? -->
