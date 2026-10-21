-- M01-T3: is the pg_sleep query still running?
-- Interactive (then paste the query and type \watch 1 to rerun it every second; \q to quit):
--   docker compose -f deploy/compose.yaml exec postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB"'
-- One-off from PowerShell:
--   Get-Content labs/M01-T3-cancellation/check.sql | docker compose -f deploy/compose.yaml exec -T postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB"' Expect: state = active while the query runs; idle (query still shows pg_sleep) once it was cancelled.

SELECT pid,
       state,
       wait_event,
       now() - query_start AS running_for,
       query
FROM pg_stat_activity
WHERE query LIKE '%pg_sleep%'
  AND pid <> pg_backend_pid();
