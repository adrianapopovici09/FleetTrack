# M00-T8: liveness probe checks the database

* **Date:** 2026-10-15
* **Task:** M00-T8 (Break it), part 1

## What I did

I changed health/live endpoint so it checks that postgres db is running

```app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = (check) => check.Tags.Contains(HealthCheckTags.Ready) });```

Then I stopped the container hosting the postgres db

## What I observed

Now the health/live endpoints shows that the service is not healthy and following response was provided:

HTTP/1.1 503 Service Unavailable

## What would happen in production

The orchestrator that checks for the service health will kill and restart all service instances.
This will not even fix the issue because postgres will still be unavailable.
When postgres comes back online all instances of the service will try to connect to the database all at once.

Requests that don't need the database at all will not be honoured since the services will be killed, same for the ones halfway through 

If health/ready was the only one failing, the instance is removed from the load balancer and put back once postgres is back online

## Why

Because the service returns 503 service unavailable, the orchestrator will kill and start it again because it should solve a process being stuck/in a deadlock
The issue is not solved because postgres lives in another process and restarting the service has no effect on the other process
This would mean that all instances of the service will be killed and restarted for nothing

## The fix / the rule I'll follow

Liveness should only detect problems that only a restart would fix (deadlock, process being stuck).
Readiness means that dependencies are also available and reachable
