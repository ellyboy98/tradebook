# TradeBook — document set

Everything here is specification and design. No application code has been
written yet.

| File | What it is | Who reads it |
|---|---|---|
| `CLAUDE.md` | Working rules for Claude Code: boundaries, conventions, traps, build order | Claude Code, every session |
| `docs/design.md` | Technical design: scope, data model, position arithmetic, concurrency, API and hub contracts, folder layout, testing, deployment, ADRs | Claude Code, you |
| `docs/ui-design.md` | UI and UX specification: principles, tokens, layout, components, states, accessibility, responsive | Claude Code, you |
| `prototypes/blotter.html` | Working prototype with mock data and a simulated price feed. Becomes `wwwroot/index.html`. | You, then Claude Code |
| `TradeBook-FSD-v0.1.docx` | Functional Specifications Document, JurisTech template, business register | Portfolio, business reviewers |
| `TradeBook-FSD-v0.1.pdf` | Same, rendered | Sharing |
| `diagrams/functional-flow-business.*` | Process flow in business language (embedded in the FSD) | FSD readers |
| `diagrams/functional-flow.*` | Process flow with technical detail (API paths, concurrency, SignalR) | Claude Code, you |
| `diagrams/erd.*` | Entity relationship diagram, PascalCase | Claude Code, you |

Diagram formats: `.png` to view, `.svg` to scale, `.dot` to edit and re-render
with Graphviz (`dot -Tpng erd.dot -o erd.png`).

## Repository layout to create

    tradebook/
      CLAUDE.md
      README.md
      docs/design.md
      docs/ui-design.md
      docs/adr/
      diagrams/
      prototypes/blotter.html
      src/TradeBook.Api/
      tests/TradeBook.Tests/

## Not yet written

- The Interface Agreement for the Keycloak boundary. It needs the realm name
  and client identifiers before it can be finalised.
