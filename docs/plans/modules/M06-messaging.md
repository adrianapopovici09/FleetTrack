# M06 · Messaging & reliability

**Time:** ~16 h · **Prereq:** M05 · **Outcome:** reliable asynchronous integration between modules: no lost messages, no double effects, observable failures. You build it by hand first, then with Wolverine.

**Why it matters:** "how do you make sure an event is published when the DB commit succeeds?" and "what happens if a message is delivered twice?" are among the most asked senior questions, and they're behind many production incidents.

Each task: 📖 **Learn** → 🔨 **Build** → ✅ **Done when**.

---

### T1 · RabbitMQ by hand
- 📖 **Learn (60 min):** exchanges (direct/topic/fanout), queues, bindings, acks, prefetch; commands vs events. [RabbitMQ tutorials 1–5 (.NET)](https://www.rabbitmq.com/tutorials) · [AMQP 0-9-1 model explained](https://www.rabbitmq.com/tutorials/amqp-concepts) · [Consumer acknowledgements](https://www.rabbitmq.com/docs/confirms)
- 🔨 **Build:** add `rabbitmq:management` to compose (with a healthcheck). With the raw `RabbitMQ.Client`, publish `ShipmentBooked` to a topic exchange and consume it in Dispatch with manual acks.
- ✅ **Done when:** the message flows end to end, and killing the consumer before it acks causes a redelivery you can see in the management UI.

### T2 · Hand-rolled transactional outbox
- 📖 **Learn (45 min):** the dual-write problem and the outbox. [Transactional outbox pattern](https://microservices.io/patterns/data/transactional-outbox.html) · [Publisher confirms](https://www.rabbitmq.com/docs/confirms#publisher-confirms) · [SELECT … FOR UPDATE SKIP LOCKED](https://www.postgresql.org/docs/current/sql-select.html#SQL-FOR-UPDATE-SHARE)
- 🔨 **Build:** an `outbox_messages` table in the Shipments schema; the booking handler inserts the event **in the same transaction** as the shipment; a `BackgroundService` relay polls with `FOR UPDATE SKIP LOCKED`, publishes with confirms, and marks rows sent.
- ✅ **Done when:** with RabbitMQ down during the commit, the message is published once it's back; a rollback never produces a message.

### T3 · Idempotent consumer + inbox
- 📖 **Learn (30 min):** at-least-once delivery + idempotent processing = effectively-once effects. [Idempotent consumer pattern](https://microservices.io/patterns/communication-style/idempotent-consumer.html) · [RabbitMQ reliability guide](https://www.rabbitmq.com/docs/reliability)
- 🔨 **Build:** Dispatch records `(message_id, consumer)` in an `inbox` table in the same transaction as its effect.
- ✅ **Done when:** delivering the same message 5 times produces exactly one planning entry.

### T4 · Poison messages & dead-lettering
- 📖 **Learn (30 min):** [Dead letter exchanges](https://www.rabbitmq.com/docs/dlx) · [Quorum queues: poison message handling](https://www.rabbitmq.com/docs/quorum-queues#poison-message-handling)
- 🔨 **Build:** bounded retries with backoff → a DLQ; a small admin endpoint/CLI to list and replay DLQ messages.
- ✅ **Done when:** an always-failing message lands in the DLQ after N attempts, and after the fix, replay succeeds.

### T5 · Now with Wolverine
- 📖 **Learn (60 min):** [Wolverine: getting started](https://wolverinefx.net/tutorials/getting-started.html) · [RabbitMQ transport](https://wolverinefx.net/guide/messaging/transports/rabbitmq/) · [Durable messaging, outbox & inbox](https://wolverinefx.net/guide/durability/) · [EF Core integration](https://wolverinefx.net/guide/durability/efcore.html) · [Error handling](https://wolverinefx.net/guide/handlers/error-handling.html)
- 🔨 **Build:** replace T1–T4 with Wolverine (RabbitMQ transport, Postgres persistence, EF Core transactional middleware, error policies). Delete the hand-rolled code but keep it on a branch/tag.
- ✅ **Done when:** the T2–T4 tests pass on Wolverine, and your journal has a comparison table (lines of code, features, failure behaviour, what's now hidden from you).

### T6 · Message contract evolution
- 📖 **Learn (30 min):** [Tolerant Reader (Fowler)](https://martinfowler.com/bliki/TolerantReader.html) · [Versioning in event-sourced systems (Young), ch. 1–3](https://leanpub.com/esversioning/read)
- 🔨 **Build:** put messages in `*.Contracts`; add a field to `ShipmentBooked` and simulate an old consumer; write the rules (no renames, no removals without a new version, ignore unknown fields).
- ✅ **Done when:** a test proves an old consumer handles a v2 message, and the rules are written down (for ADR-009).

### T7 · Break it
- 🔨 **Build:** (1) publish *before* committing, with no outbox, and make the commit fail. (2) Remove the inbox and redeliver. (3) Set prefetch to 1000 with a slow consumer, then kill it.
- ✅ **Done when:** you've recorded each failure (ghost event, duplicate, redelivery storm) in your journal.

### T8 · Decide: ADR-009
- 📖 **Learn (20 min):** [Choosing between Azure messaging services](https://learn.microsoft.com/azure/service-bus-messaging/compare-messaging-services) (good comparison framework even if you stay on RabbitMQ) · [MassTransit licensing announcement](https://masstransit.io/introduction/v9-announcement)
- 🔨 **Build:** **ADR-009**: broker (RabbitMQ vs Service Bus vs Kafka) and framework (Wolverine vs MassTransit v8/v9 vs hand-rolled), with licence, outbox/inbox and contract rules.
- ✅ **Done when:** the ADR has ≥2 options, negative consequences and a revisit trigger.

---

## Quiz → [answers](../answers/M06.md)
1. Explain the dual-write problem and how the outbox solves it. What does the outbox *not* solve?
2. Why is end-to-end exactly-once delivery a myth, and what do you build instead?
3. Commands vs events: how do naming, ownership and the number of handlers differ?
4. Three ways to make a consumer idempotent, from most to least preferable?
5. Why does the outbox relay use `FOR UPDATE SKIP LOCKED`?
6. *Code reading:* `await db.SaveChangesAsync(); await bus.PublishAsync(new ShipmentBooked(id));` What can go wrong in each failure order?
7. RabbitMQ: when is a message removed from a queue, and what happens if the consumer crashes before acking?
8. You need events for one shipment processed in order, but have 4 consumer instances. Options?
9. When would you pick Kafka over RabbitMQ for FleetTrack, and when not?
10. *Design:* a webhook to a customer must be delivered reliably, even when their endpoint is down for hours. Design it.

## Design drill (20 min)
"Design order processing for an e-commerce platform where payment, inventory and shipping are separate modules. Ensure no order is charged without stock and nothing is lost if a component crashes."

## Review
M05 Q4 · M05 Q8 · M02 Q4

## Exit check
Outbox, inbox and DLQ behaviour proven by tests (on Wolverine), the hand-rolled version preserved on a branch, and ADR-009.
