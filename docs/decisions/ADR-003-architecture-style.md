# ADR-003-architecture-style

## Context and Problem Statement

Architecture for the applcation

## Considered Options

* Monolith
* Modular Monolith
* Microservices

## Decision Outcome

Chosen option: "Modular Monolith" following Clean Architecture/Onion Architecture principles, because the goal
is to have a long term project, easy to test

Reference matrix:
FleetTrack.Api -> FleetTrack.Application
FleetTrack.Application -> FleetTrack.Domain
FleetTrack.Infrastructure -> FleetTrack.Domain + FleetTrack.Application
### Consequences

* Good, because will have a well structured solution, with easy changeble moving parts (e.g allows
* infrastructure concerns to be changed easily without other components being change), testable
* Bad, because of high number of projects that add complexity
