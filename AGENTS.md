# Repository Instructions

- Use Windows line endings (`CRLF`) for edited and newly created files.
- Keep documentation changes in markdown files unless the user explicitly asks for code changes.
- Read `design.md` first, then the matching `design_<project>.md` file before editing a project.
- When a change touches more than one project boundary, update the relevant design docs in the same pass.

## Documentation Structure

- `design.md` is the system-level map.
- `design_<project>.md` is the detailed contract for one bounded context or project.
- Shared rules live only in `AGENTS.md` so we do not end up with several slightly different versions of the same policy.
- `design.md` should link to every project-specific design document in the repo.
- Each `design_<project>.md` should stay focused on that project and avoid re-explaining the whole repository.

## Design Doc Index

- [design.md](design.md) - system-level map, project relationships, runtime boundaries, and integration style.
- [design_client_identity_service.md](design_client_identity_service.md) - client-user identity bounded context.
- [design_clients_api.md](design_clients_api.md) - client profile API and its internal layering.
- [design_pagarte_api.md](design_pagarte_api.md) - public Pagarte HTTP boundary focused on credit cards.
- [design_pagarte_services.md](design_pagarte_services.md) - internal Pagarte payment core and gRPC surface.
- [design_pagarte_engine.md](design_pagarte_engine.md) - async delivery and refund worker.
- [design_payment_services.md](design_payment_services.md) - catalogue and quote bounded context.
- [design_external_connections.md](design_external_connections.md) - adapter layer for external payment and company integrations.

## Collaboration

- Prefer the repo's existing patterns over inventing new ones.
- Keep edits scoped to the bounded context or project implied by the request.
- Do not revert user changes unless explicitly asked.
- If a task is documentation-only, do not change code.

## Application Use Case Pattern

For backend/API projects, prefer the Application Use Case Pattern to organize business workflows. This is also known as the Interactor Pattern or Application Service Pattern in Clean Architecture.

## GitHub Workflow Instructions

- Check the current branch before committing.
- Do not commit directly to `main` unless explicitly requested.
- Use small, focused commits.
- Use clear commit messages.
- Do not commit secrets, tokens, passwords, connection strings, `.env` files, or local configuration files.
- Before pushing, verify that `origin` points to the expected GitHub repository.
- Before committing, verify the repository Git identity is configured with the expected user name and email.
- Push only the branch related to the current work.
