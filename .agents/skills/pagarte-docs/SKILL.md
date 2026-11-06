---
name: pagarte-docs
description: Create, reorganize, or update Pagarte repository documentation while keeping standards, architecture, context, flow, and decision documents consistent with the source code.
---

# Pagarte Documentation

Read `AGENTS.md`, `docs/architecture.md`, and `docs/README.md`. Inspect relevant
source code and context documents before stating current ownership or behavior.

Use the documentation homes defined in the
[index](../../../docs/README.md#where-guidance-belongs):

- Requirements contain requested product behavior, scope, and acceptance criteria;
  after delivery they retain traceability and link to current reference documents.
- Standards contain maintained implementation and verification conventions.
- Contexts contain ownership, current implementation, and specialized design.
- Flows describe behavior crossing runtime boundaries.
- Decisions retain accepted choices, rationale, and consequences.
- Current Problems tracks observed unresolved conflicts.

Keep one authoritative home for detailed guidance and link from other documents.
For quote Composite and Strategy design, use the
[PayableServices context](../../../docs/contexts/payable-services.md#quote-design-composite-and-strategy).
Do not turn requirements, agent steering, or skills into duplicate coding manuals.

Keep implementation facts separate from proposals and acceptance gaps. Do not
resolve an open architectural question through wording alone. Link API/gRPC
schema sources rather than copying complete schemas into Markdown.

When behavior or implementation status changes, check affected root/index
summaries, context and flow diagrams, ADR status, and test guides for conflicting
claims. Preserve historical verification dates; source inspection is not a new
test run. Follow [Testing](../../../docs/standards/testing.md#evidence-and-limits)
for documentation checks, preserve CRLF, and maintain index links. Documentation-only
work does not change application code.

For post-development documentation, follow
[Document the Implementation](../../../docs/standards/requirement-workflow.md#5-document-the-implementation).
Trace each delivered behavior/design decision from the actual diff and source to
its maintained context, flow, standard, or ADR. A future maintainer should be able
to understand the implementation from those documents without reading the original
requirement. Keep requirement IDs and acceptance history; do not erase them or
copy the entire specification into multiple reference documents.

Record coverage and applicable summary/inventory/test-guide checks in the work
record. Search for old behavior and inspect contradictions rather than relying
only on link validation. Report code/requirement mismatches as gaps; documentation
must not silently approve them. Missing coverage or known stale affected docs
prevents documentation completion. Later implementation changes require rechecking
the affected references.
