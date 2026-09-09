---
name: pagarte-docs
description: Create, reorganize, or update Pagarte repository documentation while keeping architecture, context, flow, decision, and current-problem documents consistent with the source code.
---

# Pagarte Documentation

Read `AGENTS.md`, `docs/README.md`, and `docs/architecture.md`. Inspect the
relevant source code and context documents before stating current ownership or
behavior.

Use the existing structure:

- `docs/architecture.md` for the current system map.
- `docs/contexts/` for one bounded context or adapter area.
- `docs/flows/` for behavior crossing runtime boundaries.
- `docs/decisions/` for accepted decisions and their consequences.
- `docs/current-problems.md` for observed but unresolved conflicts.

Keep current implementation facts separate from proposals. Do not resolve an
open architectural question through wording alone. Avoid copying complete
OpenAPI, gRPC, configuration, or code schemas into Markdown; link to their
source and document ownership and behavior.

Maintain links in `docs/README.md`, use the standard context headings already
present in `docs/contexts/`, and preserve CRLF line endings. When the request
is documentation-only, do not edit application code.
