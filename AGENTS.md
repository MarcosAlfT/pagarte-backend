# Repository Instructions

- Use Windows line endings (`CRLF`) for edited and newly created files.
- Keep documentation changes in Markdown files unless the user explicitly asks
  for code changes.
- Read `docs/architecture.md` first, then the matching file under
  `docs/contexts/` before editing a project.
- For workflows that cross project boundaries, also read the matching document
  under `docs/flows/`.
- When a change touches more than one project boundary, update all affected
  context and flow documents in the same pass.

## Documentation

- `docs/README.md` is the documentation index.
- `docs/architecture.md` describes the current system-level map, runtime
  boundaries, and integration style.
- `docs/contexts/` contains one focused contract for each bounded context or
  adapter area.
- `docs/flows/` describes workflows that cross runtime boundaries.
- `docs/decisions/` records accepted architectural decisions and their
  consequences.
- `docs/current-problems.md` tracks observed architectural problems that have
  not been resolved.

Keep current implementation facts separate from proposed architecture. Do not
describe a proposed boundary as implemented until the code follows it.

## Collaboration

- Prefer the repository's existing patterns over inventing new ones.
- Keep edits scoped to the bounded context or project implied by the request.
- Do not revert user changes unless explicitly asked.
- If a task is documentation-only, do not change code.

## Application Use Case Pattern

For backend and API projects, prefer the Application Use Case Pattern, also
known as the Interactor or Application Service Pattern in Clean Architecture.
Keep controllers, gRPC services, consumers, and hosted-service loops as thin
transport or scheduling adapters.

## GitHub Workflow

- Before starting coding work, fetch `origin` and verify the latest
  `origin/main` state.
- Check the current branch before committing.
- Do not commit directly to `main` unless explicitly requested.
- Use small, focused commits with clear commit messages.
- Do not commit secrets, tokens, passwords, connection strings, `.env` files,
  or local configuration files.
- Before pushing, verify that `origin` points to the expected GitHub
  repository.
- Before committing, verify the repository Git identity has the expected user
  name and email.
- Push only the branch related to the current work.
