---
name: pagarte-requirement
description: Run Pagarte's requirement workflow from investigation and drafting through readiness review, development, implementation documentation, and completion verification. Use for starting or continuing a requirement, including short stage requests. Do not apply the full workflow to unrelated small fixes or general documentation edits.
---

# Pagarte Requirement

Follow [Requirement Development](../../../docs/standards/requirement-workflow.md).
It owns the six-stage procedure and readiness/completion criteria. Read AGENTS.md
and architecture first, then the target requirement/work record and only the
contexts, flows, decisions, standards, source, and tests relevant to that stage.

## Resolve the Request

Recognize ordinary language and these short actions:

| Action | Stage |
| --- | --- |
| `start`, `investigate`, analyze an inventory section | Investigate |
| `draft`, `save the requirement`, revise requirement scope | Draft |
| `review`, `ready for dev?` | Review readiness |
| `develop`, `implement` | Develop |
| `document`, update docs after development | Document implementation |
| `verify`, review requirement completion | Verify completion |

Examples: `$pagarte-requirement start Payment Execution` and
`$pagarte-requirement review docs/requirements/payment-history.md`.
Names are examples, not preselected scopes. Natural-language equivalents work.

Resolve the target from the current conversation or a unique named requirement,
work record, or transaction section. In a new chat, inspect matching files before
asking for missing context. If multiple targets remain plausible, ask one concise
clarification instead of choosing arbitrarily. A bare skill invocation without a
target requests that missing target, not a repository-wide feature programme.

`Continue` resumes an unfinished stage. After a completed stage, give the next
short action; do not infer development authorization from an ambiguous continue.
Honor explicit multi-stage authorization and existing decisions without repeated
permission requests. Otherwise complete the requested stage and hand off.

## Keep the Work Resumable

Use [the requirement template](../../../docs/templates/requirement.md) for
`docs/requirements/<name>.md` and [the work-record template](../../../docs/templates/requirement-work.md)
for `docs/work/<name>.md`. During investigation, create only the work record;
create the requirement during drafting. Reuse existing artifacts and transaction
IDs; adopt existing requirements without a wholesale rewrite. Follow explicit
read-only or no-file-change instructions instead when present.

Record source-backed findings, selected scope, real decisions, blockers, baseline,
and next action in the work record. Keep standards in their own documents. Use
pagarte-docs for documentation work and pagarte-use-case when implementing; their
repository paths are adjacent to this skill.

For a readiness request, inspect source and tests as well as prose, distinguish
blockers from optional improvements, and record the reviewed requirement digest
and code baseline. Do not treat your recommendations as product approval.
For development, check earlier-stage evidence and material baseline changes;
resolve actual blockers before dependent edits. Development hands off to Document
implementation, not directly to completion review. For documentation, use
[stage 5](../../../docs/standards/requirement-workflow.md#5-document-the-implementation)
and pagarte-docs to map delivered behavior to maintained contexts/flows and
applicable standards/decisions. Audit affected summaries, inventory, gaps, and
test guides against source. Record documentation coverage in the work record;
updating only the requirement is insufficient.

For completion, inspect the diff and map acceptance criteria to observed
verification. Check that documentation coverage matches the current implementation;
stale or missing references prevent Complete. Later code fixes reopen affected
documentation checks. A fresh chat or an existing test file is not evidence that
the behavior passed.

## Finish the Requested Stage

Update the work record and relevant index links. Report the stage outcome,
material decisions/blockers, artifact paths, and the exact next short request.
State verification limits. Follow the workflow's completion criteria; do not
claim the broader transaction or live provider is complete from a narrower slice.
The skill does not authorize commits, pushes, deployments, or external actions;
follow the user's existing authorization and AGENTS.md.
