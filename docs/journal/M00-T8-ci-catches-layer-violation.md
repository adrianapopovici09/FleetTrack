# M00-T8: CI catches a Domain → Infrastructure reference

* **Date:** 2026-10-15
* **Task:** M00-T8 (Break it), part 2

## What I did

I tried to add a reference in the Domain project to the Infrastructure project then I added entity framework dependency on Domain project.

## What I observed

The project graph already forbids adding the reference because Infrastructure uses Application that uses Domain. Adding such reference closes the cycle.

Building the Domain project with the entity framework dependency works just fine.
What failed: the architecture tests.

## What would happen in production

The compiler could spot some issues, but not all. 
The compiler can spot a dependency cycle, but a not allowed dependency could still pass by unnoticed without tests.
If these kind of dependencies are left unnoticed, with code growing more and more, more difficult it will be to change. Infrastructure should isolate implementation details from the domain. The domain should be testable, functional regardless of what is used in infrastructure as implementation.
## Why

The compiler enforces no cycles, architecture tests enforce direction and allowed dependencies

## The fix / the rule I'll follow

I can rely on the build alone for obvious mistakes to be caught like circular dependencies (the reverse if a referece that already exists)
Compiler doesn't help when allowed dependencies should be checked (eg nuget packages). These can be enforced with architecture tests
