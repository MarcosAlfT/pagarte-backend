# ADR 0002: SQL Outbox for Post-Charge Delivery

## Status

Superseded by
[ADR 0003: Quote Execution Saga and Legacy Quote Removal](0003-quote-execution-saga.md).

## Historical Decision

The former flow charged the card, committed a `PaymentRequestMessage` to the
SQL outbox, and let Worker deliver the service payment to the company. This
prevented losing company-delivery intent between a database commit and a broker
publish.

## Superseding Design

Company payment now occurs before final card charge inside Processor's durable
payment saga. The `payment.request` contract, queue, publisher use case, and
Worker consumer are removed.

The general reliability rule remains: persist transition intent before relying
on an external call or delayed dispatch. Processor now persists saga status,
attempt counters, next-attempt time, stable idempotency keys, and row-version
concurrency on the payment itself. The SQL outbox remains available for refund
and notification messages.
