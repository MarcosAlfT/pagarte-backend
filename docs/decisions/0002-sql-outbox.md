# ADR 0002: SQL Outbox for Post-Charge Delivery

## Status

Accepted.

## Context

After a card charge succeeds, the system must durably continue company delivery.
Publishing directly to RabbitMQ after committing a payment can lose the message
if the process stops between the database commit and broker publication.

## Decision

Processor stores the post-charge `PaymentRequestMessage` in a SQL outbox in
`PaymentDb`.

- The successfully charged payment state and outbox record are saved together.
- A hosted polling service invokes
  `PublishPendingOutboxMessagesUseCase`.
- The publisher retries failed broker publications and records attempt state.
- Worker begins company delivery only after consuming the published message.

## Consequences

- A successful card charge has durable delivery intent.
- Publishing is at least once, so consumers and downstream status changes must
  tolerate duplicate delivery.
- Outbox monitoring, retention, and retry behavior become operational
  responsibilities.
