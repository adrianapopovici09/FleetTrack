# M00-T4 reading notes: Docker and docker compose

Task: [M00-T4](../modules/M00-foundations.md) · Sources: [Docker overview](https://docs.docker.com/get-started/docker-overview/) · [Volumes](https://docs.docker.com/engine/storage/volumes/) · [Compose file reference: services](https://docs.docker.com/reference/compose-file/services/) · [Startup order](https://docs.docker.com/compose/how-tos/startup-order/) · [postgres on Docker Hub](https://hub.docker.com/_/postgres)

---

## 1. Images and containers

- **An image** is a read-only package containing everything needed to run a program: a minimal operating system, libraries, the program and its default settings. It's built in **layers**, and each instruction in a `Dockerfile` adds one. Layers are cached and shared between images, which is why the second build or pull is fast.
- **A container** is a running instance of an image. Docker adds a thin writable layer on top of the image. You can start many containers from the same image, just as you can start many processes from the same `.exe`.
- **A registry** stores images; Docker Hub is the public default.

**Key point: containers are disposable.** When a container is removed, everything written inside it is gone. Anything that must survive (database files!) belongs in a volume.

**Containers are not virtual machines.** They share the host's operating system kernel and are just isolated processes, which is why they start in about a second. On Windows and macOS, Docker Desktop actually runs a small Linux virtual machine, and your Linux containers run inside it.

---

## 2. Volumes: keeping data

There are three ways to give a container storage that outlives it:

| Type | What it is | Typical use |
| --- | --- | --- |
| **Named volume** | Storage managed by Docker, referred to by name (`pgdata`) | Database data; this is the recommended default |
| **Bind mount** | A folder from your machine mapped into the container | Mounting source code or config files during development |
| **tmpfs** | Storage in memory only | Temporary or sensitive data that should never touch disk |

Named volumes are usually faster than bind mounts on Windows and macOS (no translation between file systems) and avoid file-permission issues.

---

## 3. Networks and ports

- **Each container has its own network address.** Inside a container, `localhost` means *that container itself*, not your machine. This is a classic beginner mistake: an app container trying to reach a database at `localhost:5432` fails.
- **Compose creates a shared network** for all services in a file, and each service can reach the others **by service name**. An API container connects to the database at `postgres:5432`, not `localhost:5432`.
- **Port mapping** (`"5432:5432"`, meaning `host:container`) makes a container port reachable from your machine. Without it, only other containers on the same network can reach the service. That's good for security: only publish the ports you really need.

---

## 4. docker compose

**Docker Compose** describes a set of containers in one YAML file and starts them together with `docker compose up -d`. The main parts of a service:

- `image`: which image to run. **Pin the version** (`postgres:17`, or even `postgres:17.2`) instead of `latest`, otherwise a new major version can arrive one day and break your data format.
- `environment` / `env_file`: environment variables passed into the container.
- `volumes`, `ports`, `networks`: as described above.
- `healthcheck`: a command Docker runs regularly to check the service actually *works*, not just that the process exists. It takes `test`, `interval`, `timeout`, `retries` and `start_period` (a grace period while the service starts).
- `depends_on`: startup order between services.

### Two kinds of "env" file that are easy to confuse
- **`.env` next to the compose file** is used by Compose itself for **variable substitution** in the YAML (`${POSTGRES_PASSWORD}`).
- **`env_file:`** passes variables **into the container**.

Keep `.env` out of git (it contains passwords) and commit a `.env.example` showing which variables are needed.

### Startup order is not readiness
By default `depends_on` only waits until the other container has **started**, not until it's **ready** (a database takes a few seconds to accept connections). The options:
- `condition: service_started`: the default, which only waits for the start.
- `condition: service_healthy`: waits until the healthcheck passes.
- `condition: service_completed_successfully`: waits for a one-off job (such as migrations) to finish.

**Senior point:** even with `service_healthy`, your application should still **retry** connections. In production (Kubernetes, the cloud) there's no `depends_on`, and databases restart or fail over at any time. Startup ordering is a convenience for local development, not a reliability strategy.

---

## 5. The official Postgres image

- `POSTGRES_PASSWORD` is **required**. `POSTGRES_USER` and `POSTGRES_DB` are optional and default to `postgres`.
- `PGDATA` is where the data files go, inside the container. Your volume must be mounted there.
- **Init scripts:** `.sql` and `.sh` files placed in `/docker-entrypoint-initdb.d/` run **only when the data folder is empty**, meaning on first creation. If you change an init script and restart, nothing happens, because the volume already has data. You must delete the volume (`docker compose down -v`) to re-run them. This catches almost everyone once.
- Likewise, changing `POSTGRES_PASSWORD` after the volume exists does **not** change the password. It's only used on first creation.
- `pg_isready` is a small tool in the image that checks whether the server accepts connections, which makes it ideal for the healthcheck.

---

## 6. Licensing and the $0 budget

Docker Desktop is free for personal use, education and small companies (fewer than 250 employees *and* less than $10 million in revenue). Larger companies need a paid subscription. Free alternatives are **Podman Desktop** and **Rancher Desktop**, and on Linux the Docker Engine itself is free and open source. This is a real question in companies: "can we use Docker Desktop?" is a licensing decision, not just a technical one.

---

## Interview questions this prepares you for

- "Why does my API container fail to connect to the DB on `localhost`?"
- "What's the difference between an image and a container? A volume and a bind mount?"
- "`depends_on` is set, but the app still fails on startup sometimes. Why, and how do you fix it properly?"
- "I changed my init SQL script and nothing happened. Why?"

## For FleetTrack

[deploy/compose.yaml](../../../deploy/compose.yaml) runs Postgres 17 with a named volume, credentials from a git-ignored `.env`, and a `pg_isready` healthcheck.
