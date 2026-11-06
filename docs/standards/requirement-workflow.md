# Requirement Development Workflow

Use the same six stages for every requirement: investigate, draft, review
readiness, develop, document the implementation, and verify completion. Keep the sequence stable and scale
the detail to the change. Existing requirements can enter at their current
stage after checking earlier evidence; they do not need to be rewritten.

## Short Requests

The repository [pagarte-requirement skill](../../.agents/skills/pagarte-requirement/SKILL.md)
handles the procedure. These are ordinary chat requests, not shell commands.

| Action | Example | Result |
| --- | --- | --- |
| Start | `Start a requirement for Payment Execution` | Investigate the inventory and repository; record gaps and propose a coherent scope. |
| Draft | `Draft the payment-history requirement` | Write or revise the requirement using verified findings and selected scope. |
| Review | `Review payment-history for development` | Check readiness against the requirement, source, tests, and decisions. |
| Develop | `Develop payment-history` | Implement the ready scope and run its verification. |
| Document | `Document payment-history` | Audit and update the maintained reference documentation against the implemented code. |
| Verify | `Verify payment-history completion` | Review the implementation and acceptance evidence; report remaining gaps. |

The name payment-history is illustrative, not a selected project requirement.
An explicit invocation such as `$pagarte-requirement start Payment Execution`
works as an alternative. In the same chat, the target can be omitted when it is
unambiguous. In a new chat, provide the name or requirement/work-record path.

`Continue <name>` resumes the unfinished current stage. When that stage is
finished, report the next action rather than interpreting an ambiguous continue
as permission to start implementation. An explicit request for several stages
can authorize them together; carry that authorization forward without asking
again. A readiness verdict by itself is not a request to develop.

## Durable Artifacts

Use two tracking files per selected requirement, alongside the maintained
reference documentation affected by its implementation:

- `docs/requirements/<name>.md`, based on the
  [requirement template](../templates/requirement.md), owns product behavior,
  scope, acceptance criteria, and links to accepted design. After delivery it
  remains the traceable statement of what was requested, with links to the
  maintained implementation reference; it is not the runtime handbook.
- `docs/work/<name>.md`, based on the
  [work-record template](../templates/requirement-work.md), owns investigation,
  stage status, readiness findings, implementation progress, and verification
  evidence. Create the directory when the first record is needed.

Investigation can begin with only a work record and a proposed scope. Mark the
requirement path as not created until drafting. If a broad investigation yields
several deliverables, ask the user to select a scope when it cannot be inferred;
do not silently draft or implement every missing feature. Link any resulting
records rather than duplicating the investigation. Register created requirement
and work-record links in the documentation index.

Keep code conventions in standards, domain design and implemented rules in
contexts, cross-service behavior in flows, and accepted architectural rationale
in decisions. Stage 5 ensures delivered knowledge is maintained in those homes
rather than left only in requirements or a work record. Preserve requirement
IDs and original acceptance criteria; link to current references rather than
copying the full requirement into several documents or deleting its history.
Templates describe what to fill in; they
are not requirements themselves. Do not create empty records for hypothetical
future features or copy chat transcripts into the repository.

## 1. Investigate

Read AGENTS.md and architecture, then the selected transaction rows, matching
contexts/flows, applicable requirements/decisions, and Current Problems. Inspect
the actual source and tests behind the claims; inventory status alone is not
proof of a missing or complete capability.

Record verified behavior with source links, missing behavior, dependencies,
assumptions, and product/architecture decisions needed. Classify each finding
as verified fact, existing accepted decision, proposal, or unresolved question.
Recommend a small coherent scope and explicit exclusions. Preserve transaction
IDs and identify existing requirements that should be extended instead of
creating overlapping ones. This stage changes documentation only.

## 2. Draft

Use the requirement template for the selected scope. State actors, ownership,
observable behavior, non-goals, and numbered acceptance scenarios. Include
applicable ownership/authentication failures, concurrency, idempotency, uncertain
outcomes, and recovery; use not applicable with a reason where appropriate.

Keep unresolved decisions explicit. AI recommendations do not become accepted
product policy without evidence of the user's or team's decision. Routine
implementation choices can follow accepted design without new approval.
Reference standards and contexts rather than embedding a coding manual.

## 3. Review Readiness

Challenge the requirement against current source, tests, and accepted decisions.
A fresh chat is useful but does not replace evidence. Separate blocking findings
from optional improvements; give each blocker an impact and a resolution.
Resolve substantive ambiguity rather than repeatedly polishing wording.

The selected scope is Ready when its outcome and exclusions are clear, owners
and integration boundaries are decided, acceptance criteria are observable,
relevant failure behavior is specified, and verification is feasible. A missing
external contract blocks only the scope depending on it. Record a recommendation
of Ready or Needs decisions/changes, not a vague approval.

Record the reviewed requirement's SHA-256, source commit, and relevant uncommitted
changes in the work record. Do not mutate the requirement after recording its
reviewed digest without revisiting affected findings. Documentation corrections
within a requested readiness pass are allowed; do not silently resolve product
choices or start coding. Preserve any explicitly read-only request.

## 4. Develop

An explicit development request authorizes code changes for its selected scope.
Check readiness and compare the requirement and relevant code with the reviewed
baseline. Reassess material changes; if an uncommitted baseline cannot be
reconstructed, recheck the affected area. Do not repeat unchanged review work.
Resolve actual blockers before dependent implementation. An unrelated open
question does not block a ready slice.

Follow AGENTS.md Git rules, [Application Design](application-design.md), the
owning context, and [Testing](testing.md). Record a short implementation plan in
the work record, with independently verifiable slices. Implement, test, and fix
within the authorized scope. Record observed results as work proceeds. Update
affected context/flow documentation and preserve acceptance criteria; do not
weaken them to fit the code. Material scope changes return to drafting/review.

Finishing implementation moves the record to Document implementation, not
completion review or Complete. Keep the context/flow updates required during
development; the next stage is a deliberate consistency audit, not permission
to leave those updates out of the development change. Commit, push, deployment,
and external actions follow the user's existing authorization and repository
rules; this workflow grants none by itself.

## 5. Document the Implementation

Read the selected requirement, accepted decisions, actual implementation diff,
relevant source/tests, and observed verification. Map every delivered behavior
and material design decision to a maintained documentation home. A requirement
status update or a work-log entry alone does not satisfy this stage.

| Maintained home | What to update or explicitly verify as unchanged |
| --- | --- |
| `docs/contexts/` | Ownership, use cases, domain rules, persistence, implementation design, supported scope, and limitations. |
| `docs/flows/` | End-to-end sequence, cross-service contracts, failure/recovery behavior, and rollout compatibility. |
| `docs/standards/` | Reusable implementation conventions when an accepted change affects them. Keep feature-specific rules in the owning context. |
| `docs/decisions/` | Accepted architectural rationale and consequences; correct implementation status without rewriting history or inventing decisions. |
| `docs/architecture.md` | System map, runtime boundaries, integrations, and shared ownership when affected. |
| `docs/transactions.md` and `docs/current-problems.md` | Evidence-backed delivery status and remaining gaps, including partial and live-acceptance scope. |
| Root `README.md`, `docs/README.md`, and affected test guides | Accurate summaries, navigation, configuration/verification instructions, and dated evidence. |

Update existing reference documents before creating new ones. Search for the
changed concepts, transaction IDs, type/endpoint names, and old behavior across
documentation to find stale prose, tables, diagrams, and proposed/pending claims.
Inspect matches in context; historical decisions and dated test results are not
current claims to overwrite. Verify implementation claims against source and
verification claims against actual test evidence.

Explain implemented business rules in contexts/flows so a future maintainer can
understand the delivered feature without reading its original requirement.
Preserve traceability through links back to requirements and ADRs. Link source
schemas rather than copying them. If the code differs from an accepted
requirement, record a gap and return it to development or an explicit scope
decision; do not document the mismatch as newly accepted behavior.

In the work record, complete a documentation coverage table: delivered behavior
or decision, maintained destination, implementation evidence, and review result.
Also account for the reference areas above as updated, reviewed unchanged, or
not applicable with a reason. This records the audit without duplicating the
feature specification. Add current implementation links to the requirement.

Run link/anchor, CRLF, UTF-8, and whitespace checks from [Testing](testing.md).
A passing link check alone does not establish semantic consistency. Mark this
stage Done only when each delivered behavior has a maintained home and no known
stale statement remains in affected documentation. Otherwise record the missing
coverage or contradiction. Documentation-only requests do not change code.

## 6. Verify Completion

Check that the documentation stage is Done for the current implementation.
Independently cross-check its coverage against the diff and the maintained
references; do not accept its checklist as proof. Missing or stale documentation
blocks Complete and returns to stage 5. Later code fixes invalidate affected
documentation checks and must pass through stage 5 again before completion.

Review the actual diff and behavior against every acceptance criterion. Trace
criteria to tests or other observed evidence, including relevant negative and
recovery cases. Use the testing standard to fill evidence gaps; do not rerun
unchanged passing checks without a reason. A verify request is review and
verification, not automatic authorization for broad implementation changes.

Report findings with concrete impact. Mark Complete only when the agreed scope
is implemented, applicable checks passed, required documentation is consistent,
and no acceptance blocker remains. Otherwise record Needs fixes or Verification
blocked with the exact unresolved condition. Return fixes to development and
document any resulting behavior changes and verify them again. Distinguish synthetic,
SQL/transport, Aspire, and live-provider
acceptance. A scoped Development completion does not establish production readiness.

Update transactions.md only to the level supported by evidence. A completed
slice does not make a broader transaction Complete. Commit/push status is
separate from acceptance status.

## Handoff Between Chats

At the end of a stage, update the work record with the target scope, current
stage/status, branch and baseline, relevant decisions, blockers, and evidence
links. End the response with the exact short request for the next stage.
Carry forward valid work; do not restart investigation because the chat is new.

A new chat reads the requirement and its work record, checks repository state,
and loads only relevant context. It must not assume an earlier chat's uncommitted
files exist in a different checkout. Report a missing artifact or revision
instead of reconstructing decisions from guesses. Preserve useful evidence and
resolve stale status when the repository has moved ahead.
