# Requirement: {{Title}}

Template: copy into `docs/requirements/<name>.md`. Replace placeholders, preserve
existing transaction IDs, and remove this instruction paragraph. Sections may
say not applicable with a reason; implementation conventions belong in standards
and contexts. Follow the [workflow](../standards/requirement-workflow.md).

## Scope

| Field | Value |
| --- | --- |
| Product decision status | Draft; mark accepted only with decision evidence |
| Transactions | Existing IDs, or explicitly proposed new IDs |
| Primary owner | Context owning the behavior/state |
| Integration owners | Affected adapters/services |
| Work record | Relative link to the companion record when created |
| Acceptance environment | Development, synthetic/offline, or live scope explicitly selected |

## Problem and Outcome

Describe the user's problem and the observable outcome this requirement delivers.
Explain why the existing behavior does not satisfy it. Link the work record's
verified baseline rather than duplicating the source inventory.

## In Scope and Non-Goals

Name the included transactions and exclusions. Identify any ready slice within
a larger capability; do not imply that delivering it closes all remaining work.

## Actors and Ownership

Identify authorized actors, ownership checks, public/internal entry points, and
which context owns state. Mark undecided boundaries explicitly. Link existing
contracts rather than copying complete API schemas.

## Required Behavior

Assign stable requirement IDs within this document, such as R-001. Describe
inputs, observable outcomes, and rules, without prescribing classes or methods.
Retain existing IDs when extending a requirement. Link accepted architecture.

## Failure and Recovery Behavior

Describe relevant validation, missing/foreign resources, replay/conflict,
concurrent requests, dependency failure, uncertain outcomes, restart recovery,
and compensation behavior. For irrelevant cases, explain why they do not apply.
Distinguish a known failure from an unknown external result.

## Acceptance Criteria

| ID | Given / When | Observable expected result | Related rule |
| --- | --- | --- | --- |
| AC-001 | Concrete starting state and action | Verifiable outcome | R-001 |

Cover the successful path and applicable failure/boundary scenarios. Keep test
execution results in the work record. A statement that code or a test exists is
not a passing acceptance result.

## Dependencies and Open Decisions

List external contracts, capabilities, or decisions needed for this scope.
Distinguish accepted decisions (with source), assumptions, and unresolved choices.
State which criterion or slice each unresolved item blocks.

## Compatibility and Delivery Constraints

Describe behavior that existing users/data must retain and relevant rollout or
migration constraints. Link the context/flow for technical deployment details.
Keep the implementation sequence in the work record.

## Design and Verification References

Link relevant contexts, flows, ADRs, [application design](../standards/application-design.md),
[money handling](../standards/money-handling.md) when relevant, and
[testing](../standards/testing.md). Add the applicable suite guide. These documents
own implementation guidance; this requirement owns expected product behavior.

## Delivered Implementation References

After development, link the maintained context and flow sections describing the
implemented behavior and applicable standards/decisions. Identify any undelivered
scope separately. The documentation stage populates these links and verifies
their content against code. Retain the requirement and acceptance IDs for
traceability; keep the current implementation handbook outside this folder.
