# CLAUDE.md — Distributed Logistics & Dispatch Platform

Read this file fully before every task. Follow every rule. If a request conflicts with a rule, state the conflict and ask before proceeding.

---

## 1. Project Definition

- System: Distributed Logistics & Dispatch Platform.
- Backends: ASP.NET (system of record) and Go (hot path).
- Frontend: Angular.
- Messaging: Kafka (already installed on the server; do NOT add it to this project's Compose file).
- Data: PostgreSQL + PostGIS (via PgBouncer), Redis.
- Edge: Nginx.
- Deployment: single server, Docker already installed.
- Load target: 10,000 concurrent users. Stretch: 10,000 requests/second.
- Terminology: always use **courier**, never "driver", in code, contracts, tables, topics, and docs.

---

## 2. Feature Scope (ONLY these features)

1. Order creation
2. Courier registration
3. Courier online/offline
4. Real-time location
5. Automatic courier assignment
6. Order state machine
7. Cancellation
8. Retry
9. Idempotency
10. Rate limiting
11. WebSockets
12. Kafka events
13. Redis geo queries
14. PostgreSQL transactions

### Out of scope (do NOT build unless explicitly requested)
Billing, invoices, payments, reports, pricing rules, ETA/route service, proof of delivery, shifts, zones, geofences, notification worker, multi-stop routing.

Rule: do not add features, tables, topics, or services outside this list without approval.

---

## 3. Ownership Boundaries (NEVER VIOLATE)

1. ASP.NET owns: identity, auth, customers, couriers (registration, profile, status), vehicles, orders, order status history, assignments (read model), idempotency keys.
2. Go owns: location ingest, dispatch engine, realtime gateway.
3. Each service owns its own schema: `core` (ASP.NET), `dispatch` (Go dispatch), `tracking` (Go ingest/location consumers).
4. A service MUST NOT read or write another service's schema.
5. No foreign keys across schemas. Reference by ID only.
6. Services communicate through Kafka events only. Direct service-to-service HTTP calls are forbidden unless approved.
7. Angular talks only to Nginx. Never to a service port.
8. One database user per service, with rights only on its own schema.

---

## 4. Services and Containers

| Service | Stack | Responsibility |
|---|---|---|
| `dotnet-api` | ASP.NET | REST API, auth, orders, couriers, cancellation, outbox writes |
| `dotnet-worker` | ASP.NET | Outbox publisher, consumes `assignment.events`, idempotent state updates |
| `go-ingest` | Go | Courier location ingest, publishes to Kafka |
| `go-dispatch` | Go | Assignment engine, offers, dispatch state machine, retries |
| `go-gateway` | Go | WebSocket server, Redis Pub/Sub fan-out |

Supporting: Nginx, Redis, PostgreSQL + PostGIS, PgBouncer, Prometheus, Grafana, Loki. Kafka is external (already running).

---

## 5. Repository Structure

- `/services/dotnet-api`
- `/services/dotnet-worker`
- `/services/go-ingest`
- `/services/go-dispatch`
- `/services/go-gateway`
- `/web/angular`
- `/contracts` — event schemas, OpenAPI spec, WebSocket message definitions
- `/infra` — Compose file, Nginx config, Prometheus, Grafana, Loki
- `/loadtest` — k6 scripts, courier simulator, results

Rules:
- Do NOT create files outside this structure without approval.
- Do NOT share code between .NET and Go. Share contracts only.

---

## 6. Docker and Infrastructure Rules

1. Kafka and Docker already exist on the server. Do NOT define Kafka in this project's Compose file.
2. Compose defines only: .NET containers, Go containers, Nginx, Redis, Postgres, PgBouncer, and monitoring.
3. Attach the app containers to the existing Kafka Docker network (declared as an external network).
4. Verify Kafka advertised listeners are reachable from inside app containers. Never assume `localhost` works.
5. **One Dockerfile per language, one container per service.**
   - .NET: one Dockerfile and one image, run twice with different commands (API and worker).
   - Go: one multi-stage Dockerfile that builds a small image per service (or one image holding all Go binaries, each container selecting its binary).
6. NEVER combine .NET and Go in the same image.
7. Every container has a health check, a restart policy, and explicit CPU/memory limits.
8. Use `depends_on` with health conditions.
9. Images: multi-stage, minimal runtime, non-root user, pinned versions. Never use the `latest` tag.
10. Configuration through environment variables. Secrets through Docker secrets. Never commit secrets.
11. Only Nginx exposes ports to the host. Everything else stays on internal networks.
12. Use named volumes for Postgres and Redis data.
13. Nginx: TLS termination, per-IP rate limits, request size limits, keep-alive upstreams, WebSocket upgrade support, `X-Correlation-Id` pass-through.
14. Nginx routes: `/api/*` → dotnet-api, `/ingest/*` → go-ingest, `/ws/*` → go-gateway, everything else → Angular static files.

---

## 7. Contracts Rules

1. `/contracts` is the single source of truth.
2. Update the contract FIRST, then the code.
3. Every event uses this envelope: `eventId`, `eventType`, `version`, `occurredAt`, `correlationId`, `payload`.
4. Schema changes MUST bump the version. Breaking changes within a version are forbidden.
5. Producers and consumers MUST validate against the schema.
6. Never invent a field, topic, or endpoint that is not in the contracts.

---

## 8. Kafka Rules

### Topics
| Topic | Key | Producer | Consumers |
|---|---|---|---|
| `order.events` | orderId | dotnet (outbox) | go-dispatch |
| `courier.events` | courierId | dotnet (outbox) | go-dispatch, location/status updater |
| `courier.location` | courierId | go-ingest | Redis updater, history writer, gateway feeder |
| `assignment.events` | orderId | go-dispatch (outbox) | dotnet-worker, go-gateway |
| `*.dlq` | n/a | failed consumers | replay tooling |

### Event types
`order.created`, `order.cancelled`, `order.status.changed`, `courier.registered`, `courier.status.changed`, `assignment.offered`, `assignment.confirmed`, `assignment.failed`.

### Rules
1. Partitions: 12 for `courier.location`, 6 for all others.
2. Replication factor is 1 (single server). Document it as a known risk.
3. `courier.location` retention: 1 hour. Order and assignment events: long retention.
4. Every consumer MUST be idempotent (deduplicate by `eventId`).
5. Disable auto-commit. Commit offsets only after successful processing.
6. Failed messages go to the DLQ after the retry limit. Never drop a message silently.
7. Each service uses its own consumer group name. Never share a group across services.
8. Propagate `correlationId` in Kafka headers.
9. Topics are created by an init step only. Never create topics on the fly.

---

## 9. Data Rules

### Conventions
- IDs: UUID (time-ordered preferred), generated by the service.
- Time: `timestamptz`, always UTC.
- Money: not used in this scope.
- Locations: PostGIS `geography(Point, 4326)` with a GiST index.
- Status fields: constrained text or enum matching the contract state names.
- Mutable aggregates carry a `version` column (optimistic concurrency).
- Every table has `created_at` and `updated_at`.
- Business entities use soft delete.

### Schema `core` (ASP.NET)
`users`, `roles`, `user_roles`, `refresh_tokens`, `customers`, `couriers`, `vehicles`, `orders`, `order_stops`, `order_status_history`, `assignments` (read model), `idempotency_keys`, `outbox_messages`, `processed_events`.

### Schema `dispatch` (go-dispatch)
`dispatch_orders` (local snapshot), `offers`, `dispatch_transitions`, `courier_profiles` (local read model), `dispatch_config`, `outbox_messages`, `processed_events`.

### Schema `tracking` (go-ingest and location consumers)
`courier_location_history` (daily partitions), `courier_last_location`, `processed_events`.

### PostgreSQL rules
1. Services connect only through PgBouncer (transaction pooling).
2. Every schema change goes through a migration. Never edit schemas by hand.
3. Migrations are forward-only and backward-compatible with the previous app version.
4. Location history is inserted in batches only, never row by row.
5. Create daily partitions ahead of time. Drop old partitions for retention. Never bulk-delete rows.
6. Every hot-path query MUST have a supporting index. Verify with the query plan.
7. Enforce with unique partial indexes: one Pending offer per courier, one Pending offer per order.
8. Tune autovacuum aggressively for `outbox_messages`, `offers`, and `dispatch_orders`.
9. Keep transactions short. NEVER call Kafka, Redis, or HTTP inside a transaction.
10. Default isolation: read committed, plus explicit version checks and state checks.
11. Use row-level locking with skip-locked where several workers could claim the same row (offer-timeout sweeper, outbox publisher).

### Outbox rules
1. Every state change that emits an event writes an outbox row in the SAME transaction.
2. A background publisher sends outbox rows to Kafka and marks them published.
3. Publishing to Kafka directly from a request handler is forbidden.
4. Purge published outbox rows after a retention window.

### Redis rules
| Key pattern | Type | Purpose |
|---|---|---|
| `couriers:geo` | GEO set | Positions of available couriers |
| `courier:{id}:state` | Hash with TTL | Status, current order, last-seen (heartbeat) |
| `lock:courier:{id}` | String with TTL | One offer per courier at a time |
| `lock:order:{id}` | String with TTL | Prevents double assignment |
| `order:{id}:live` | Hash | Latest tracking snapshot |
| `ratelimit:*` | Counters with TTL | Application rate limiting |
| Pub/Sub channels per order, courier, and dispatcher | Pub/Sub | Gateway fan-out |

1. Every lock and cache key MUST have a TTL.
2. Pub/Sub is for realtime fan-out only, never durable storage.
3. GEO sets have no per-member TTL. Remove stale couriers through heartbeat-expiry logic plus a periodic cleanup.

---

## 10. Feature Rules

### 10.1 Order creation
1. Require an `Idempotency-Key` header.
2. In ONE transaction: write the order, stops, status history, and outbox row.
3. Return `201` with the order ID and status `Created`.
4. The API never waits for dispatch. Assignment is asynchronous.

### 10.2 Courier registration
1. Create the user, assign the Courier role, create the courier profile and vehicle.
2. Initial status: `PendingApproval`. After admin approval: `Offline`.
3. Emit `courier.registered` on approval.

### 10.3 Courier online/offline
1. Status changes go through the API, which emits `courier.status.changed`.
2. Going offline removes the courier from the Redis GEO set.
3. Heartbeat expiry: if location pings stop for N seconds, treat the courier as offline automatically.
4. A courier with an active order cannot go offline.

### 10.4 Real-time location
1. Couriers send pings every 3–5 seconds to go-ingest.
2. Ingest validates auth, coordinate ranges, timestamp freshness, and rate limit, then publishes to `courier.location` keyed by `courierId`.
3. Ingest MUST NOT write to Postgres inline.
4. Consumers: Redis updater, batch history writer, gateway feeder (separate consumer groups).
5. Ignore out-of-order pings (timestamp older than the last stored one).

### 10.5 Automatic courier assignment
1. Default style: **sequential offers** (one courier at a time). Broadcast-to-many is not allowed without approval.
2. Flow: consume `order.created` → query Redis GEO → filter (online, available, matching vehicle) → score → lock courier → create Pending offer with TTL → push via gateway.
3. Accept: confirm assignment. Reject or timeout: release locks and offer to the next candidate.
4. No candidates: expand radius and retry. After the maximum attempts, mark the order `Unassigned` and emit an event.
5. Search has a maximum radius and a maximum attempt count (both configurable).
6. Scoring order: distance first, then rating and load.
7. Offer TTL default: 15 seconds.

### 10.6 Order state machine
| Phase | States | Owner |
|---|---|---|
| Intake | `Created` | dotnet |
| Assignment | `Searching → Offered → Assigned` | go-dispatch |
| Fulfilment | `PickedUp → Delivered` | dotnet (courier app calls) |
| Terminal | `Cancelled`, `Failed`, `Unassigned` | by cause |

1. Only one service writes any given transition.
2. Validate every transition against an allowed-transition table. Reject and log illegal transitions.
3. Every transition writes a history row and an outbox event in the same transaction.
4. Dispatch persists state after every transition and MUST recover correctly after a crash mid-offer.

### 10.7 Cancellation
1. dotnet-api checks cancellability (not after `PickedUp` unless an admin overrides), sets `Cancelled`, emits `order.cancelled`.
2. go-dispatch consumes it, expires pending offers, releases courier locks, stops the search.
3. Notify the courier through the gateway.
4. Accept-versus-cancel race: the first committed transaction wins, enforced by the version column and state checks. The loser gets a clear rejection.

### 10.8 Retry
| Level | Rule |
|---|---|
| Assignment | Next candidate, wider radius, maximum attempts |
| Consumer | Limited attempts, exponential backoff with jitter, then DLQ |
| Outbox | Publisher retries unpublished rows |
| Client | Safe through idempotency keys |

1. Every retry has a limit and a backoff. Never retry forever.
2. Never retry non-idempotent operations without an idempotency key.

### 10.9 Idempotency
1. HTTP: store the key, request hash, and saved response with an expiry. Same key and same body returns the original response. Same key and different body returns an error.
2. Kafka: insert into `processed_events` in the SAME transaction as the state change.
3. Dispatch: offers and transitions are idempotent by order, courier, and attempt.

### 10.10 Rate limiting
1. Nginx: per-IP limits, connection limits, request size limits.
2. Application: per-user and per-courier limits through Redis counters (location pings per courier, order creations per customer).
3. Gateway: cap WebSocket connections per user and messages per second.
4. Return `429` with a `Retry-After` header. Count rejected requests in metrics.

### 10.11 WebSockets
1. Only go-gateway serves WebSockets. Authenticate with a short-lived ticket issued by the API.
2. Channels: customer → own order, dispatcher → all active orders, courier → offers and cancellations.
3. Authorize every subscription. A customer can only subscribe to their own order.
4. Heartbeats, dead-connection cleanup, and backpressure (drop or coalesce stale location updates for slow clients). Never block on a slow client.
5. Receive events through Redis Pub/Sub so multiple gateway instances can run.

---

## 11. ASP.NET Rules

1. Async all the way. No `.Result` or `.Wait()`.
2. Layers: API → Application → Domain → Infrastructure. Keep controllers thin.
3. Validate every request. Return consistent errors with a `correlationId`.
4. Paginate every list endpoint. No unbounded queries.
5. Use `AsNoTracking` for read-only queries.
6. Never expose entities. Use DTOs.
7. JWT with an asymmetric key, JWKS endpoint, roles: `Admin`, `Dispatcher`, `Courier`, `Customer`. Enforce authorization on every endpoint.
8. Structured JSON logging. Never log secrets, tokens, or personal data.
9. Expose health, readiness, and Prometheus metrics.
10. Graceful shutdown: finish in-flight requests, stop consumers cleanly.

---

## 12. Go Rules

1. Pass `context.Context` as the first parameter of every function that does I/O.
2. Handle every error. Wrap errors with context. Never ignore a returned error.
3. Every goroutine has an owner, a stop condition, and a cancellation path.
4. Bounded worker pools and bounded channels only.
5. Timeouts on every outbound call (Redis, Postgres, Kafka, HTTP).
6. Validate JWTs locally through the JWKS. Never call dotnet-api for auth.
7. Protect against double assignment with a Redis lock PLUS a database state check.
8. Run tests with the race detector.
9. Graceful shutdown: stop accepting work, drain consumers and sockets, then exit.
10. Expose health, readiness, and Prometheus metrics.

---

## 13. Angular Rules

1. Latest stable Angular, standalone components, strict TypeScript.
2. Generate typed API clients from the OpenAPI spec. Never hand-write API types.
3. All HTTP behavior (auth header, errors, correlation ID) lives in interceptors.
4. One WebSocket service with auto-reconnect, exponential backoff, and resubscription.
5. `OnPush` change detection. Unsubscribe from every stream.
6. Throttle map updates. Never re-render on every location message.
7. Lazy-loaded routes per area: customer, dispatcher, admin.
8. Production build is served as static files by Nginx.
9. Courier behavior is simulated by `/loadtest` first. Build a courier UI only if requested.

---

## 14. Security Rules

1. Validate input at every boundary.
2. Enforce authorization inside each service, not only at Nginx.
3. TLS terminates at Nginx.
4. Never log tokens, passwords, or full location trails.
5. Rate limit login, ingest, and order creation.
6. WebSocket authentication uses a short-lived ticket.
7. Run dependency and image vulnerability scans in CI.

---

## 15. Observability Rules

1. Every service exposes Prometheus metrics, health, and readiness.
2. Logs are structured JSON with `correlationId`, service name, and level.
3. Propagate `correlationId` through HTTP headers and Kafka headers.
4. Mandatory metrics: request rate, error rate, latency percentiles, Kafka consumer lag, DB pool usage, Redis latency, WebSocket connection count, goroutine count, .NET thread-pool and GC stats.
5. Required alerts: consumer lag, error rate, p95 latency, disk space, container restarts.

---

## 16. Testing Rules

1. No feature is done without tests.
2. Required: unit, integration (real Postgres, Redis, Kafka), contract, and end-to-end tests of the main flow.
3. Idempotency: replay the same event twice and assert a single effect.
4. Crash recovery: restart consumers mid-processing and assert no loss and no duplication.
5. Outbox: stop Kafka during a write and assert the event is eventually published.
6. Concurrency: simultaneous orders and couriers must never produce a double assignment.
7. Race tests: accept versus cancel, accept versus timeout.
8. Tests MUST be deterministic. No sleeps as synchronization.

---

## 17. Load Testing Rules

1. Tool: k6. Scripts and the courier simulator live in `/loadtest`.
2. Run the load generator on a separate machine when possible. Otherwise cap it and mark results as pessimistic.
3. Scenario order: smoke (50), baseline (1,000), ramp (to 10,000), soak (30–60 min), spike (1k → 10k in 30s), failure injection.
4. Workload mix: 60% customers tracking orders, 25% dispatchers, 10% order creators, 5% simulated couriers.
5. SLOs:
   - REST p95 < 300 ms, p99 < 800 ms.
   - Order created to offer delivered < 2 s p95.
   - Location ping to customer screen < 1 s p95.
   - Error rate < 1%.
   - Kafka lag returns near zero after bursts.
6. Fix one bottleneck at a time, then re-run the same scenario. Keep the results of every run.
7. Never change the test and the system in the same run.
8. Seed realistic data first (for example 100k orders, 2,000 couriers). Never test on empty tables.
9. OS tuning before testing: file descriptor limit, `somaxconn`, ephemeral port range, Nginx worker connections.

---

## 18. Build Order

1. Contracts frozen at v1.
2. Infra: Compose (without Kafka), Nginx, Postgres, PgBouncer, Redis, monitoring, topic init.
3. Auth, courier registration, order creation with outbox and idempotency keys.
4. Courier online/offline and real-time location into Redis GEO.
5. Dispatch engine: assignment, state machine, cancellation.
6. WebSocket gateway.
7. Angular: customer tracking and dispatcher console.
8. Retry, DLQ, and rate limiting hardening.
9. Load test, fix, re-test.

Rules:
- Do not start a phase until the previous phase gate passes.
- Keep the whole stack runnable at all times. Add each service to Compose the day it is created.
- Work in vertical slices.

### Phase gates
| Phase | Gate |
|---|---|
| Infra | All containers healthy, Grafana shows metrics |
| Orders | Order created through Nginx appears once in Kafka |
| Location | 500 simulated couriers keep Redis GEO current, lag near zero |
| Dispatch | 100 simultaneous orders and 200 couriers produce zero double assignments and zero stuck orders |
| Gateway | 5,000 idle sockets with stable memory, updates delivered in under 1 s |
| Hardening | Killing any single service never loses an order or leaves a courier locked |
| Load test | All SLOs met at 10k and held through the soak |

---

## 19. Workflow Rules

1. One logical change per commit. Conventional commit messages.
2. Every pull request states: what changed, which contracts changed, how it was tested, and the impact on load.
3. CI runs lint, format, tests, and image builds before merge.
4. Add a CI check that fails if a producer changes a schema without a version bump.

---

## 20. How Claude Must Behave

1. Read the contracts and relevant existing code before writing anything.
2. State assumptions. If a requirement is ambiguous, ask one focused question before building.
3. Make the smallest change that satisfies the task. Do not refactor unrelated code.
4. Follow existing patterns. Do not introduce new libraries or frameworks without approval.
5. Explain trade-offs briefly when making a design choice.
6. Mention the effect on performance, reliability, and security when relevant.
7. Never claim something works without verifying it. Report what was tested and what was not.
8. When a task touches contracts, ownership boundaries, or the outbox, list the affected services first.

---

## 21. Forbidden Actions

- Adding Kafka to this project's Compose file.
- Combining .NET and Go in one Docker image.
- Using the word "driver" instead of "courier".
- Building out-of-scope features.
- Writing to another service's schema, or adding cross-schema foreign keys.
- Direct service-to-service HTTP calls without approval.
- Publishing to Kafka from a request handler (bypassing the outbox).
- Calling Kafka, Redis, or HTTP inside a database transaction.
- Auto-committing Kafka offsets.
- Unbounded queries, channels, queues, or retries.
- Row-by-row location inserts.
- Locks or cache keys without TTLs.
- Hard-coded secrets, ports, hostnames, or credentials.
- Blocking calls on async paths in .NET.
- Ignoring errors in Go.
- Using the `latest` Docker tag.
- Skipping a phase gate.
- Changing a frozen contract without a version bump.

---

## 22. Definition of Done

A task is done only when ALL are true:

1. Contracts updated first, if affected.
2. Code follows every applicable rule above.
3. Tests written and passing (unit, integration, idempotency, and race tests where relevant).
4. Health, metrics, and logs in place.
5. Runs correctly inside Docker Compose.
6. No new lint, race, or security warnings.
7. Impact on the load-test SLOs considered and noted.
8. Documentation or runbook updated if behavior changed.

---

## 23. Open Decisions (confirm before building the affected part)

| Decision | Current default |
|---|---|
| Assignment style | Sequential offers, one courier at a time |
| Courier app | Simulated by script; no courier UI until requested |