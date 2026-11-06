# Requirement Work: {{Title}}

Template: copy into `docs/work/<name>.md`, replace placeholders, and remove this
instruction paragraph. Maintain concise current state and material evidence,
not a conversation log. Follow the [workflow](../standards/requirement-workflow.md).

## Current State

| Field | Value |
| --- | --- |
| Requirement | Not created during investigation; add its relative link when drafted |
| Selected scope | Transactions/outcomes included; proposed until selected |
| Current stage | Investigate / Draft / Review readiness / Develop / Document implementation / Verify completion |
| Stage status | In progress / Done / Needs decisions or changes / Needs fixes / Verification blocked / Complete |
| Repository branch and baseline commit | Observed values, including relevant dirty-worktree changes |
| Next action | Exact short request, or the blocking decision/evidence needed |

## Investigation

| Finding | Classification | Source evidence | Impact on scope |
| --- | --- | --- | --- |
| Describe current or missing behavior | Verified fact / accepted decision / proposal / unresolved | Source/test/doc link; distinguish inspection from execution | Dependency or candidate slice |

Record the selected scope and exclusions. Keep unselected follow-ups separate.

## Decisions and Blockers

| Item | Decision or question | Source/authority | Affected scope | Status |
| --- | --- | --- | --- | --- |

Record material user/team decisions and links to ADRs. Do not treat an AI
recommendation as approval. Remove obsolete blockers or explain their resolution.

## Readiness Review

- Verdict: Not reviewed / Ready / Needs decisions or changes.
- Reviewed requirement SHA-256: record after reviewing the final draft.
- Reviewed code baseline: commit and relevant uncommitted changes; recheck an
  unreconstructable dirty baseline before implementation.
- Findings: separate blockers from optional improvements and record resolutions.
- Verification feasibility: necessary suites, fixtures, and external evidence.

## Implementation Slices

| Slice | Criteria covered | Dependencies | Status | Change/evidence links |
| --- | --- | --- | --- | --- |

Keep this an execution plan, not a second statement of product requirements.

## Verification Evidence

| Acceptance ID | Test/check or other evidence | Observed result and date | Code revision/environment | Limits |
| --- | --- | --- | --- | --- |

Record actual commands and outcomes, including failures or unavailable checks.
Distinguish inspected tests from tests run. Link detailed existing suite evidence
rather than duplicating it. Label results stale when affected code changes.

## Documentation Review

- Stage status: Not started / In progress / Needs changes / Done.
- Reviewed implementation revision/base and relevant uncommitted changes:

| Delivered behavior or decision | Maintained documentation destination | Implementation evidence | Review result |
| --- | --- | --- | --- |

Cover every delivered behavior; link source and destination sections. Requirements
and this record are traceability artifacts, not substitutes for maintained
contexts, flows, standards, and decisions.

| Reference area | Updated / reviewed unchanged / not applicable | Evidence or reason |
| --- | --- | --- |
| Contexts | | |
| Flows | | |
| Standards | | |
| Decisions | | |
| Architecture | | |
| Transactions and current problems | | |
| Root README, documentation index, and test guides | | |

Record stale-behavior searches, contradictions resolved or remaining, requirement
links to current references, and link/encoding/whitespace results. Do not mark
Done while coverage is missing or known affected documentation is stale. Later
behavior changes reopen the relevant checks.

## Completion Review

- Reviewed diff/base and implementation revision:
- Unresolved findings and affected criteria:
- Documentation stage status and independent consistency check against current code:
- Verdict: Not reviewed / Needs fixes / Verification blocked / Complete.
- Remaining work outside this scope and live-acceptance limits:
- Commit/push/merge status, only when observed:

Complete means this requirement's selected acceptance scope is satisfied and its
maintained reference documentation is current. Done refers only to an individual
stage; it is not overall completion. Update
the transaction inventory only to the extent supported by the evidence.
