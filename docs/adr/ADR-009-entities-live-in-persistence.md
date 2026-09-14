# ADR-009 — Entity classes live in Persistence, not Domain

Date: 2026-09-15 · Status: accepted

| ID | Decision | Reason |
|---|---|---|
| ADR-009 | The EF Core entity classes (`Account`, `Instrument`, `Trade`, `Position`, `InstrumentPrice`) sit in `Persistence/Entities`. `Domain` holds only the enums they share and the position arithmetic. | Design section 9 says `Domain` is plain C# with no framework dependency. The entities carry persistence concerns: foreign key ids, navigation properties, a `rowversion` token. Putting them in `Domain` would either drag those concerns in or force a second set of classes and a mapping layer, which is the indirection ADR-003 rejected. `Features` may reference both `Domain` and `Persistence`, so nothing is out of reach. |

Consequences: `Trade` is `init`-only so the append-only rule (ADR-001) is
enforced by the compiler as well as by the absence of update endpoints.
`Position` has ordinary setters because the trade capture handler writes the
output of `PositionMath` to it; the rule that nothing else does is a code
review rule, not a type-system one. Timestamps are re-stamped as UTC on load
by a value converter registered in `ConfigureConventions`, because
`datetime2` stores no offset.
