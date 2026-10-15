# ADR-003: Layered monolith with Clean Architecture boundaries

* **Status:** Accepted for stage 1 (layered). Revisit in M05 (modular monolith) and M09 (service extraction).
* **Date:** 2026-10-03

## Context and Problem Statement

Architecture style for a learning project that should follow modern guideliness, testability for only one person. The current plan grows the application into models and then services

## Considered Options

* Layered Monolith
* Modular Monolith
* Microservices

## Decision Outcome

Chosen option: start as a layered monolith with Clean Architecture / Onion project boundaries, because the goal
is to have a long term project, easy to test. The plan evolves it into a modular monolith in M05.

The budged for this project is $0, managing microservices will add cost.
Module boundaries follow business areas that cannot be drawn well before understanding the domain.

Reference matrix:
FleetTrack.Api -> FleetTrack.Application
FleetTrack.Api -> FleetTrack.Infrastructure, but only for the exposed extension method used for registration in the composition root
FleetTrack.Application -> FleetTrack.Domain
FleetTrack.Infrastructure -> FleetTrack.Domain + FleetTrack.Application

### Consequences

* Good, because will have a well structured solution, with easy changeable moving parts (e.g. allows
  infrastructure concerns to be changed easily without other components being changed), testable
* Bad, because of high number of projects that add complexity
* Bad, because of features coupling to each other inside a layer

## Revisit trigger

* Features start coupling inside a layer: a change in one business area (e.g. vehicle tracking) forces changes in unrelated code (e.g. fleet management).
* A second business area with its own data and rules appears, so it should own its own schema.
* Planned: M05 (modular monolith), then M09 (extracting Tracking as a service).
