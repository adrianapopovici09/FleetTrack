# M00-T4 · Docker and docker compose

Task: [M00-T4](../modules/M00-foundations.md)

This file is the complete lesson for the task; you don't need to read anything else. Original sources, if you ever want to go deeper: [Docker overview](https://docs.docker.com/get-started/docker-overview/) · [Volumes](https://docs.docker.com/engine/storage/volumes/) · [Compose file reference: services](https://docs.docker.com/reference/compose-file/services/) · [Startup order](https://docs.docker.com/compose/how-tos/startup-order/) · [postgres on Docker Hub](https://hub.docker.com/_/postgres)

**Reading time:** about 45 minutes.

## Contents

1. [Images, containers and registries](#1-images-containers-and-registries)
2. [Storage: volumes, bind mounts and tmpfs](#2-storage-volumes-bind-mounts-and-tmpfs)
3. [Networking and ports](#3-networking-and-ports)
4. [docker compose: describing a stack in one file](#4-docker-compose-describing-a-stack-in-one-file)
5. [Health checks and startup order](#5-health-checks-and-startup-order)
6. [The official Postgres image](#6-the-official-postgres-image)
7. [FleetTrack's compose file, line by line](#7-fleettracks-compose-file-line-by-line)
8. [Everyday commands](#8-everyday-commands)
9. [Licensing and the $0 budget](#9-licensing-and-the-0-budget)
10. [Glossary](#10-glossary)
11. [Summary, check yourself, interview questions, for FleetTrack](#11-summary)

---

## 1. Images, containers and registries

**An image** is a read-only package containing everything needed to run a program: a minimal Linux file system, libraries, the program itself, and default settings (which command to run, which port it listens on). An image is built in **layers**, and each instruction in a `Dockerfile` adds one. Layers are **cached and shared**: if ten images are all based on the same Debian layer, it's downloaded and stored only once. That's why the second `docker pull` of a related image is fast.

**A container** is a **running instance** of an image. Docker adds a thin **writable layer** on top of the read-only image layers, and anything the program writes goes there. You can start many containers from one image, just as you can start many processes from one `.exe`.

**A registry** stores and distributes images. Docker Hub is the public default. Companies also run private registries, for example GitHub Container Registry or Azure Container Registry (you'll use one in M13). An image name like `postgres:17` means "the `postgres` repository on Docker Hub, tag `17`".

**Containers are disposable.** When a container is removed, its writable layer is deleted, along with everything written inside it. That's a feature: you can always throw a container away and start a fresh one from the image. But it means anything that must **survive** (database files!) has to live *outside* the container, in a volume (section 2).

**Containers are not virtual machines.** A virtual machine (VM) emulates a whole computer, with its own operating system kernel, and takes tens of seconds to boot. A container is just an **isolated process** on the host's kernel: Linux features hide the rest of the machine from it (namespaces) and limit its CPU and memory (cgroups). That's why a container starts in about a second. On Windows and macOS, Linux containers need a Linux kernel, so Docker Desktop runs **one small Linux VM** (on Windows, inside WSL 2), and all your containers run inside it. That detail matters for storage performance (section 2).

**Tags and pinning.** A tag is just a label, and labels move. `postgres:latest` today might be a new major version next month. Pin at least the **major** version (`postgres:17`), so you get patch fixes but never a surprise major upgrade that changes the data format. For the strictest reproducibility, pin a full version (`postgres:17.6`) or even a digest (`postgres@sha256:…`), and let Dependabot propose the bumps.

---

## 2. Storage: volumes, bind mounts and tmpfs

There are three ways to give a container storage beyond its disposable writable layer:

| Type | What it is | Who manages it | Typical use |
| --- | --- | --- | --- |
| **Named volume** | Storage Docker creates and manages, referred to by name (`postgres_data`) | **Docker** | Database data: the recommended default |
| **Bind mount** | A folder from your machine mapped into the container (`./data:/var/lib/postgresql/data`) | **You** (it's a normal host folder) | Source code or config files you want to edit on the host |
| **tmpfs** | Storage in memory only, gone when the container stops | — | Temporary or sensitive data that should never touch disk |

**Both named volumes and bind mounts persist** after the container is removed, so persistence is *not* the difference. **The difference is who manages the storage** (quiz Q9, a recurring weak spot):

- A **named volume** lives in Docker's own storage area. Docker creates it, sets the right permissions, can list it (`docker volume ls`) and remove it (`docker compose down -v`), and you don't care where it sits on disk.
- A **bind mount** is a folder you chose. You manage its location, its permissions and its lifecycle, and Docker just maps it in.

**Why named volumes are better for Postgres, especially on Windows:**
- **Performance.** On Windows, your `C:\` or `D:\` folders are on the Windows file system (NTFS), but the containers run inside the Linux VM. Every read and write through a bind mount crosses that boundary through a translation layer, which is much slower. A database does a huge number of small writes, so it suffers badly. A named volume lives *inside* the Linux VM's own file system, with no translation.
- **Permissions.** Postgres runs as a specific Linux user inside the container and refuses to start if its data folder has the wrong owner or permissions. Docker sets those up correctly for a named volume. A bind mount from Windows often can't satisfy them.
- **Durability.** Databases rely on `fsync` (forcing data to disk) behaving exactly as Linux promises, and a translated file system may not guarantee it fully.

Use **bind mounts** when *you* need to see or edit the files on the host: source code for hot reload, or a config file or init script you want to tweak.

---

## 3. Networking and ports

**Each container has its own network stack.** Inside a container, `localhost` means **that container itself**, not your machine and not another container. This is the classic beginner mistake: an API *container* trying to reach Postgres at `localhost:5432` fails, because there's no Postgres inside the API container.

**Compose creates a shared network** for all the services in one file, with built-in DNS. Each service is reachable from the others **by its service name**: an API container connects to `Host=postgres;Port=5432`. From your machine (outside Docker), you use `localhost` plus the **published** port.

**Port publishing** makes a container's port reachable from the host:

```yaml
ports:
  - "127.0.0.1:5433:5432"
  #  │          │    └── TARGET port: where the program listens INSIDE the container
  #  │          └─────── PUBLISHED port: what you connect to ON THE HOST
  #  └────────────────── host interface to bind (only this machine)
```

- **Published vs target port.** Postgres always listens on 5432 *inside* its container. You can publish it on a different host port, for example 5433 because a native Postgres already uses 5432. Your app on the host then connects to `localhost:5433`. **Other containers still use `postgres:5432`**: they talk on the Compose network directly and never go through the published port.
- **The bind address matters.** `"5432:5432"` with no IP publishes on **all** network interfaces, so anyone on your Wi-Fi or office network can try to log in to your database. On Linux, Docker's published ports even **bypass** common host firewalls (like `ufw`), because Docker writes its own firewall rules. Binding to `127.0.0.1` makes it reachable from this machine only.
- **Publish only what you need.** A service that only other containers use needs no `ports:` at all. That's a free security win.

---

## 4. docker compose: describing a stack in one file

**Docker Compose** describes a set of containers (services), their networks and volumes in one YAML file, and manages them together:

```bash
docker compose -f deploy/compose.yaml up -d     # create and start everything, detached
```

### 4.1 The main keys of a service

| Key | Meaning |
| --- | --- |
| `image` | Which image to run; pin the version (§1) |
| `environment` | Environment variables passed **into** the container |
| `env_file` | A file of variables passed **into** the container |
| `ports` | Published ports (§3) |
| `volumes` | Mounts: named volumes or bind mounts (§2) |
| `healthcheck` | A command Docker runs regularly to test the service actually works (§5) |
| `depends_on` | Startup order between services (§5) |
| `restart` | What to do when the container exits (`no`, `on-failure`, `unless-stopped`, `always`) |

Top-level keys: `name:` (the **project name**, used as a prefix for the containers, network and volumes) and `volumes:` (declares the named volumes).

### 4.2 Variable substitution and the two kinds of "env" file

These two are easy to confuse:

- **A `.env` file next to the compose file** is read by **Compose itself**, to **substitute** `${VARIABLE}` placeholders in the YAML *before* anything starts. The container never sees the file.
- **`env_file:`** passes a file's variables **into the container** as environment variables.

FleetTrack uses the first kind: `.env` provides values, and the YAML maps them explicitly into `environment:`.

The substitution syntax has useful variants:

| Syntax | Meaning |
| --- | --- |
| `${VAR}` | The value, or an empty string if it isn't set (with a warning) |
| `${VAR:-default}` | The value, or `default` if it's unset **or empty** |
| `${VAR:?message}` | The value, or **fail immediately** with `message` if it's unset or empty |
| `$$` | A literal `$` that Compose must *not* substitute. It's passed through for the **container's** shell to expand |

`${VAR:?…}` is a **fail-fast** guardrail: instead of starting Postgres with an empty password, Compose refuses to start and tells you which variable is missing. That's the same idea as `ValidateOnStart()` in T5.

**Secrets:** keep `.env` out of git (it holds passwords), and commit a `.env.example` listing the variables with placeholder values, so a newcomer knows exactly what to fill in.

### 4.3 The project name

Compose names everything after the **project**, by default the folder name. If the compose file lives in `deploy/`, the default project is `deploy`, and the volume becomes `deploy_postgres_data`. Another repository with its own `deploy/` folder would then **share** that name and could clash. Setting `name: fleettrack` at the top makes the names explicit and unique: `fleettrack-postgres-1`, `fleettrack_postgres_data`.

---

## 5. Health checks and startup order

### 5.1 `healthcheck`: "is it actually working?"

"The container is running" only means the **process exists**. A database can be running but still starting up, recovering, or refusing connections. A **healthcheck** is a command Docker runs inside the container at intervals. If it exits with 0, the container is `healthy`; if it fails, the status becomes `unhealthy`.

```yaml
healthcheck:
  test: ["CMD-SHELL", "pg_isready -h 127.0.0.1"]
  interval: 10s       # run every 10 s
  timeout: 3s         # a single check taking longer than this counts as a failure
  retries: 5          # this many failures in a row → unhealthy
  start_period: 5s    # grace period at startup: failures here don't count
```

`docker compose ps` shows the status: `starting`, then `healthy` or `unhealthy`.

### 5.2 `depends_on`: startup order is not readiness

By default, `depends_on: [postgres]` only waits until the Postgres **container has started**, not until Postgres **accepts connections**, which can take several seconds. So an API that starts right away can still crash with "connection refused" (quiz Q7). The conditions:

| Condition | Waits until… |
| --- | --- |
| `service_started` *(default)* | the dependency's container has started |
| `service_healthy` | the dependency's healthcheck passes |
| `service_completed_successfully` | a one-off job (such as database migrations) has finished with exit code 0 |

```yaml
api:
  depends_on:
    postgres:
      condition: service_healthy
```

### 5.3 The senior point: compose ordering is not a reliability strategy

Even with `service_healthy`, **the application must handle the database being unavailable**: retry the first connection with backoff, and report "not ready" through its own readiness endpoint (T5) until it can connect. The reasons:
- In production (Kubernetes, Azure Container Apps) there is **no `depends_on`**. Services start in any order.
- Databases restart, fail over and drop connections at any time, not just at startup.

So `depends_on` is a **local-development convenience**. Resilience belongs **in the app**.

**Vocabulary trap:** *liveness* and *readiness* probes are **orchestrator** concepts (Kubernetes, Container Apps), and they call HTTP endpoints *your app* exposes (T5). A Compose `healthcheck` is a separate, simpler mechanism: one command, one status, used for `depends_on` and for `docker compose ps`. Compose doesn't have "liveness" or "readiness", and it doesn't restart a container just because it's unhealthy.

---

## 6. The official Postgres image

**Environment variables:**
- `POSTGRES_PASSWORD`: **required** (unless you deliberately configure trust authentication, which you shouldn't).
- `POSTGRES_USER`: the superuser to create. Defaults to `postgres`.
- `POSTGRES_DB`: the database to create. Defaults to the value of `POSTGRES_USER`.
- `PGDATA`: where the data files live inside the container. The volume must be mounted there (`/var/lib/postgresql/data` for Postgres 17).

**These variables only apply on first initialisation.** When the container starts, its entrypoint script checks whether the data folder is empty:
- **Empty:** it runs `initdb`, creates the user and the database from the `POSTGRES_*` variables, and runs the init scripts.
- **Not empty:** it skips all of that and starts the server with the existing data.

The consequences catch almost everyone once:
- **Changing `POSTGRES_PASSWORD` in `.env` later does nothing.** The user already exists in the volume with the old password. To change it, either connect and run `ALTER USER fleettrack WITH PASSWORD 'new-password';`, or wipe the data with `docker compose down -v` (only for throwaway local data).
- **Init scripts** (`.sql`, `.sql.gz` and `.sh` files mounted into `/docker-entrypoint-initdb.d/`) run **only on first initialisation**, in alphabetical order. Edit a script and restart, and nothing happens. Delete the volume to re-run them.

**`pg_isready`** is a small client tool in the image that asks the server "do you accept connections?". It exits with 0 (yes), 1 (rejecting, e.g. still starting), 2 (no response) or 3 (bad parameters), which makes it perfect for a healthcheck.

**Why `-h 127.0.0.1` in the healthcheck?** During first initialisation, the entrypoint starts a **temporary** Postgres server that listens only on a local Unix socket (not on TCP), creates the user and database, runs the init scripts, and stops it again before starting the real server. A plain `pg_isready` connects over the Unix socket by default, so it can report "ready" while the *temporary* server is running, and Compose would mark the container healthy **before** initialisation has finished. Forcing TCP with `-h 127.0.0.1` checks the server that's actually reachable by clients.

**Major-version upgrades are deliberate.** Postgres 18's data files can't be read by Postgres 17 (or the other way round). Upgrading the image's major version against an existing volume makes the server refuse to start; you need `pg_upgrade` or a dump and restore. That's why FleetTrack's Dependabot ignores Postgres major bumps (M00-T7). The image layout also changed in the Postgres 18 image (a different default data path), which is another reason to treat it as a planned migration.

---

## 7. FleetTrack's compose file, line by line

```yaml
name: fleettrack                               # project name → fleettrack-postgres-1, fleettrack_postgres_data (§4.3)
services:
    postgres:                                  # service name = DNS name on the Compose network (§3)
        image: postgres:17                     # major version pinned; patches still arrive (§1)
        environment:                           # variables passed INTO the container
            POSTGRES_USER: ${POSTGRES_USER:?POSTGRES_USER is not set}            # from .env; fail fast if missing (§4.2)
            POSTGRES_PASSWORD: ${POSTGRES_PASSWORD:?POSTGRES_PASSWORD is not set}
            POSTGRES_DB: ${POSTGRES_DB:?POSTGRES_DB is not set}                  # used ONLY on first init (§6)
        ports:
            - "127.0.0.1:${POSTGRES_PORT:-5432}:5432"   # this machine only; host port from .env, default 5432; target 5432 (§3)
        volumes:
            - postgres_data:/var/lib/postgresql/data    # named volume mounted at PGDATA (§2, §6)
        healthcheck:
            test: ["CMD-SHELL", "pg_isready -U $${POSTGRES_USER} -d $${POSTGRES_DB} -h 127.0.0.1"]
            #      │                         └ $$ = literal $, so the CONTAINER's shell expands it (§4.2)
            #      └ CMD-SHELL runs the string through /bin/sh, which is what makes $VAR expansion work
            #                                                -h 127.0.0.1 → TCP, so the temporary init server can't fool it (§6)
            interval: 10s
            timeout: 3s
            retries: 5
            start_period: 5s
volumes:
    postgres_data:                             # declares the named volume; Docker manages it (§2)
```

The ✅ check for this task was that data **survives** `down` + `up` (the volume isn't touched by a plain `down`) and is **gone** after `down -v` (`-v` removes the named volumes declared in the file).

---

## 8. Everyday commands

```bash
docker compose -f deploy/compose.yaml up -d          # start (detached)
docker compose -f deploy/compose.yaml ps             # status, including (healthy)
docker compose -f deploy/compose.yaml logs -f postgres   # follow a service's logs
docker compose -f deploy/compose.yaml exec postgres psql -U <user> -d <db>   # psql INSIDE the container
docker compose -f deploy/compose.yaml config         # print the file AFTER variable substitution: great for debugging .env problems
docker compose -f deploy/compose.yaml down           # stop and remove containers + network; volumes kept
docker compose -f deploy/compose.yaml down -v        # …and delete the named volumes (data gone)
docker volume ls                                      # list volumes
```

From the **host**, connect with `psql -h 127.0.0.1 -p <POSTGRES_PORT> -U <user> -d <db>`, or with any GUI client, on the **published** port.

---

## 9. Licensing and the $0 budget

**Docker Desktop** (the Windows/macOS app) is free for personal use, education, open-source projects and small companies (fewer than 250 employees *and* less than $10 million in annual revenue). Larger companies need a paid subscription. Free alternatives:
- **Podman Desktop** and **Rancher Desktop**: free, open-source desktop apps that run the same images.
- **Docker Engine on Linux** (including inside WSL 2 on Windows): free and open source. Only the *Desktop* app has the licence terms.

This comes up in real companies: "can we use Docker Desktop?" is a **licensing** decision as well as a technical one, and it's the kind of thing an architect is expected to flag.

---

## 10. Glossary

| Term | Meaning |
| --- | --- |
| **Image** | A read-only, layered package of a program and everything it needs |
| **Container** | A running instance of an image, with a disposable writable layer on top |
| **Registry** | A server that stores images (Docker Hub, GitHub Container Registry, Azure Container Registry) |
| **Tag / digest** | A movable label on an image (`17`) / an immutable content hash (`sha256:…`) |
| **Named volume** | Docker-managed storage, referred to by name; survives container removal |
| **Bind mount** | A host folder mapped into a container; you manage it |
| **Published / target port** | The port on the host / the port the program listens on inside the container |
| **Compose project** | The group of services, networks and volumes from one compose file, named by `name:` |
| **Variable substitution** | Compose replacing `${VAR}` in the YAML with values from the shell or `.env` |
| **Healthcheck** | A command Docker runs periodically to mark a container `healthy` or `unhealthy` |
| **Probe (liveness/readiness)** | An **orchestrator** check that calls an app endpoint; not a Compose concept |
| **`PGDATA`** | The folder inside the Postgres container where its data files live |
| **Init scripts** | Files in `/docker-entrypoint-initdb.d/` that run only when the data folder is empty |

---

## 11. Summary

1. An image is a read-only template, and a container is a disposable running instance. Pin image versions.
2. Named volumes and bind mounts both persist. The difference is who manages the storage. Use named volumes for databases, especially on Windows (performance, permissions, durability).
3. Inside a container, `localhost` is the container itself. Containers reach each other by service name on the target port, and the host reaches them on the published port. Bind published ports to `127.0.0.1`.
4. `.env` feeds Compose's variable substitution, and `${VAR:?msg}` fails fast when a variable is missing.
5. `depends_on` waits for *started* by default, and for *healthy* only with `condition: service_healthy`. Real resilience (retries, readiness) belongs in the app.
6. `POSTGRES_*` variables and init scripts apply only on first initialisation of an empty volume. Change passwords with `ALTER USER`.

### Check yourself

1. You remove the Postgres container (`docker compose down`) and start it again. Is your data still there? What about after `down -v`? (§2, §7)
2. Explain to a colleague the real difference between a named volume and a bind mount, and why one is a bad idea for Postgres data on Windows. (§2)
3. An API **container** uses `Host=localhost;Port=5433`. Why does it fail, and what should it use? What would the API running on your **host** use? (§3)
4. You change `POSTGRES_PASSWORD` in `.env` and restart. Your app can't log in with the new password. Why, and what are your two options? (§6)
5. `depends_on: [postgres]` is set, but the API sometimes crashes with "connection refused" on startup. Why, and what are two fixes, one in Compose and one in the app? (§5)
6. What does `$$` do in the healthcheck line, and why is it needed there? (§4.2, §7)

### Interview questions this prepares you for

- "Why does my API container fail to connect to the DB on `localhost`?"
- "Image vs container? Volume vs bind mount?"
- "`depends_on` is set, but the app still fails on startup sometimes. Why, and how do you fix it properly?"
- "I changed my init SQL script (or the password) and nothing happened. Why?"
- "Is a Compose healthcheck the same as a Kubernetes readiness probe?"
- "Our company has 400 employees. Can every developer just install Docker Desktop?"

### For FleetTrack

- [deploy/compose.yaml](../../../deploy/compose.yaml) runs Postgres 17 with a named volume, credentials from a git-ignored `deploy/.env` with fail-fast substitution, a port bound to `127.0.0.1`, and a TCP `pg_isready` healthcheck. ADR-001 records why it's Compose and not native installs or Aspire (Aspire comes in M13).
- The API runs on the **host** for now, so its connection string uses `127.0.0.1` and the published port. If the API later runs as a container on the same Compose network, it will use `Host=postgres;Port=5432` instead.
- Compose will grow module by module: RabbitMQ (M06), Valkey (M10), Keycloak (M11), the observability stack (M12).
