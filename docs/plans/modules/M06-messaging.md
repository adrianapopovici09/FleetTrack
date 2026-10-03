# M06 · Messaging & reliability

**Time:** ~16 h · **Prereq:** M05 · **Outcome:** reliable asynchronous integration between modules: no lost messages, no double effects, observable failures. You build it by hand first, then with Wolverine.

## Why it matters
"How do you make sure an event is published when the DB commit succeeds?" and "what happens if a message is delivered twice?" are among the most asked senior questions. They're also behind many real production incidents.

## Concepts
- **Commands vs events:** a command is addressed to one handler and can be refused; an event is a fact that already happened, broadcast to anyone.
- **The dual-write problem:** you can't atomically commit to Postgres *and* publish to RabbitMQ. The **transactional outbox** writes the message in the same DB transaction, and a relay publishes it.
- **Delivery semantics:** at-most-once / at-least-once / "exactly-once" (a myth end to end). At-least-once + **idempotent consumers** gives effectively-once effects.
- **Inbox / deduplication:** store processed message ids; natural idempotency (upserts, conditional updates).
- **RabbitMQ:** exchanges (direct/topic/fanout), queues, bindings, acks, prefetch, dead-letter exchanges, quorum queues.
- **Failures:** retries with backoff, poison messages, DLQ + replay, ordering guarantees (and when you can't have them).
- **Contract evolution:** versioned message types, additive changes, tolerant readers.

Read: [Transactional outbox](https://microservices.io/patterns/data/transactional-outbox.html) · [RabbitMQ tutorials](https://www.rabbitmq.com/tutorials) · [RabbitMQ reliability guide](https://www.rabbitmq.com/docs/reliability) · [Wolverine docs](https://wolverinefx.net/) · [Idempotent consumer](https://microservices.io/patterns/communication-style/idempotent-consumer.html)

## Labs

- [ ] **L1 · RabbitMQ by hand.** Add `rabbitmq:management` to compose (healthcheck). With the raw `RabbitMQ.Client`, publish `ShipmentBooked` to a topic exchange and consume it in Dispatch with manual acks. Look at the exchange, queue and messages in the management UI.
  ✅ A message flows end to end; killing the consumer mid-processing (before ack) causes redelivery, which you can see in the UI.
- [ ] **L2 · Hand-rolled outbox.** An `outbox_messages` table in the Shipments schema. The booking handler inserts the event **in the same transaction** as the shipment. A `BackgroundService` relay polls (`FOR UPDATE SKIP LOCKED`), publishes with publisher confirms, and marks rows sent.
  ✅ A test: the DB commit succeeds while RabbitMQ is down → the message is published after RabbitMQ comes back. A rollback → no message ever.
- [ ] **L3 · Idempotent consumer + inbox.** Dispatch records `(message_id, consumer)` in an `inbox` table in the same transaction as its effect.
  ✅ A test delivers the same message 5 times → exactly one planning entry.
- [ ] **L4 · Poison messages & DLQ.** Bounded retries with backoff, then a dead-letter queue; a small admin endpoint/CLI to list and replay DLQ messages.
  ✅ A message that always throws ends up in the DLQ after N attempts; after fixing the bug, replaying it succeeds.
- [ ] **L5 · Now with Wolverine.** Replace L1–L4 with Wolverine: `UseRabbitMq`, Postgres message persistence, EF Core transactional middleware (durable outbox/inbox), error policies. Delete your hand-rolled code, but keep a branch/tag with it.
  ✅ The same L2–L4 tests pass on Wolverine. You write a comparison table (lines of code, features, failure behaviour, what's now hidden from you) in your journal.
- [ ] **L6 · Contract evolution.** Put messages in `*.Contracts`. Add a field to `ShipmentBooked` (additive), then simulate an old consumer. Write the rules: no renames, no removals without a new version, consumers ignore unknown fields.
  ✅ A test proves an old consumer handles a v2 message; the rules are written in ADR-009.

## Break it
1. Publish *before* committing (no outbox) and make the commit fail: the consumer acts on a shipment that doesn't exist.
2. Remove the inbox and redeliver: you get a duplicate planning entry.
3. Set prefetch to 1000 with a slow consumer, kill it, and watch all those messages get redelivered.

## Decide
**ADR-009** Messaging: RabbitMQ (vs Service Bus vs Kafka), Wolverine (vs MassTransit v8/v9 vs hand-rolled), with licence, outbox/inbox and contract rules.

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
