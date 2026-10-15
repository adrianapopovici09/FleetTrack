# M00-T8: CI catches a Domain → Infrastructure reference

* **Date:** 2026-10-15
* **Task:** M00-T8 (Break it), part 2

## What I did

I tried to add a reference in the Domain project to the Infrastructure project then I added entity framework dependency on Domain project.

## What I observed

The project graph already forbids adding the reference because Infrastructure uses Application that uses Domain. Adding such reference closes the cycle.

Building the Domain project with the entity framework dependency works just fine.

## What would happen in production

{Scale it up: several instances, a load balancer, an orchestrator. Walk through it as a timeline: what fails first, what the platform does, what state things are in when the cause goes away.}

## Why

{The underlying rule or concept that explains it, in your own words.}

## The fix / the rule I'll follow

{One or two sentences you could say in an interview.}
