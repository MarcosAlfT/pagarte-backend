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
- `docs/standards/` contains implementation and verification conventions.
- `docs/decisions/` records accepted architectural decisions, rationale, and
  consequences.
- `docs/requirements/` contains product behavior, scope, and acceptance criteria;
  use standards and context documents for implementation guidance.
- `docs/current-problems.md` tracks observed architectural problems that have
  not been resolved.

Keep current implementation facts separate from proposed architecture. Do not
describe a proposed boundary as implemented until the code follows it.

## Implementation Guidance

- Follow [Application Design](docs/standards/application-design.md) for backend
  structure and the matching context document for domain-specific design.
- For quote composition, pricing strategies, and bill-key strategies, follow
  [PayableServices: Quote Design](docs/contexts/payable-services.md#quote-design-composite-and-strategy).
- For monetary changes, follow [Money Handling](docs/standards/money-handling.md).
- Select checks using [Testing](docs/standards/testing.md), and report results
  and any verification limits.
- For identity or authorization changes, read [Authentication](docs/flows/authentication.md).
  For card lifecycle changes, read [Card Registration](docs/flows/card-registration.md)
  and its linked payment-method requirements.

## Quote and Payment Rules

- Before changing quote or payment lifecycle behavior, read
  [the Quotes requirement](docs/requirements/quotes.md) and
  [the accepted quote/payment decision](docs/decisions/0005-resumable-quotes-and-payment-revalidation.md),
  together with the relevant context and flow documents.
- Use those documents as the source of accepted business rules. Keep their
  implementation status explicit; do not duplicate the detailed lifecycle
  rules in agent instructions or skills.

## Collaboration

- Prefer the repository's existing patterns over inventing new ones.
- Keep edits scoped to the bounded context or project implied by the request.
- Do not revert user changes unless explicitly asked.
- If a task is documentation-only, do not change code.

## Application Use Case Pattern

Use the Application Use Case Pattern described in
[Application Design](docs/standards/application-design.md).
[ADR 0001](docs/decisions/0001-application-use-case-pattern.md) records why it was
selected. Keep detailed implementation conventions in the standard.

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
