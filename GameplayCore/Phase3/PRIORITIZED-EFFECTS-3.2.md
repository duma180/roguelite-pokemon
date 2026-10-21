# Phase 3.2 prioritized remaining work

| Priority | Area | Required next evidence / work |
|---|---|---|
| P0 | Original ROM action-order fixtures | Capture controlled HGSS cases for move priority, switch action classes, speed ties, Quick Feet, Quick Claw/Custap, Trick Room, Pursuit, Protect and interrupt/faint behavior. Pin capture state, seed, selected actions, event timeline, HP/PP and outcome. |
| P0 | Weather expiration/residual ordering | Resolve field-condition weather countdown versus the final residual tick using original ROM execution. Current code retains damage-before-expiration and flags this as unresolved. |
| P0 | Residual faint and replacement | Verify simultaneous toxic/weather/ability KOs, outcome draw, faint event ordering, trainer replacement selection, wild forced-switch selection and hazard-caused entry faint. |
| P0 | Entry ability activation | Implement and validate a minimal Gen IV entry set with exact ordering (weather setters, Intimidate, Trace or others), hazard interaction, switching and re-entry behavior. |
| P1 | Complete condition state transitions | Add major-status cure moves/abilities, Safeguard, Natural Cure, Rest, Synchronize, Uproar, Yawn, infatuation and confusion lifecycle with source and runtime fixtures. |
| P1 | Leech Seed and binding | Add attacker identity/link, healing transfer, duration and switch cleanup. Validate faint/magic-guard/substitute/Rapid Spin precedence and secondary damage. |
| P1 | Residual field phases | Add supported Ingrain, Aqua Ring, Leftovers, weather form/ability, held-item and volatile duration phases in the source state order. Keep unsupported handlers rejected. |
| P1 | Replacement and switching policy | Add explicit encounter type/controller policies; confirm wild/trainer replacement choices, forced-switch failure and legal reserves, trapping exceptions, entry order and action cancellation. |
| P1 | Move restrictions | Complete Encore effect-equivalence blacklist, Disable/Encore timer edge cases, Taunt/Imprison, Torment/Gravity/Heal Block, Struggle and PP interactions. |
| P2 | Field condition breadth | Add other Gen IV field conditions (Mist, Safeguard, Tailwind, Wish, Lucky Chant, delayed attacks and Trick Room) as separately tested mechanics. |
| P2 | Full damage/action interaction ordering | Multi-hit, contact abilities, recoil/drain, secondary effects, status cures/thaw, flinch and Protect combinations with direct cartridge expected values. |
| P3 | Per-content compatibility framework | Replace current enum-effect pilot with versioned handler registrations per move/ability and explicit Gen IV-vs-later profiles. Keep HGSS rules as default for all content. |
| P3 | Numeric integration bridge | Preserve existing unbounded progression contract; implement BigInteger stats with zero-modifier parity against frozen Phase 1 fixtures before any battle use. |
| P3 | Mode save isolation | When mode work is authorized, implement Challenge save ownership and prove permadeath cannot mutate any Standard save payload or identity. |
