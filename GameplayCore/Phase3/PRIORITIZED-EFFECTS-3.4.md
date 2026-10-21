# Phase 3.4 remaining-effects inventory

This inventory prevents the bounded Phase 3.4 work from being mistaken for a complete Generation IV battle engine. P0 denotes work blocking broad effect-family population or a reliable conformance claim.

| Priority | Remaining work | Why it remains open |
|---|---|---|
| P0 | Original-ROM conformance evidence | The deterministic melonDS harness does not yet provide accepted battle-runtime observations. Every new handler remains source-grounded with cartridge conformance pending. |
| P0 | Initial encounter send-out pipeline | Entry hazards/abilities are currently called on modeled switch and faint-replacement operations. Initial send-out entry hooks are not a distinct initialized battle transition. Add this with explicit ordering and tests before using initial ability/weather effects as representative combat evidence. |
| P0 | Trainer/wild replacement controller | Replacement is explicit-index only. Trainer AI selection, wild switch behavior, chains of entry hazards, replacement fainting and cancellation need a policy-bearing controller; do not guess. |
| P0 | Forced-switch mechanics | The effect intentionally fails with `replacement-selection-policy-not-supplied`; implement only after the encounter-kind selection rules are modeled. |
| P0 | Complete residual and weather ordering | The shared path omits held items/Leftovers and many state-machine branches; final-weather-turn timing, simultaneous residual KOs, replacement and event order need runtime evidence. |
| P1 | Bind family semantics | Handler is a generic pilot, not six validated move scripts. Exact duration counter start/end, Grip Claw, each move's hit/accuracy/effect chances, and switch/faint edge cases need per-move data and tests. |
| P1 | Ability entry completeness | Initial entry, full Intimidate blockers (including Mist and Substitute), Trace copy exclusions, suppress/re-entry rules and weather setter conflicts require source/test expansion. |
| P1 | Hazard interaction matrix | Grounding from Magnet Rise/Telekinesis, ability/item/type immunities, Poison absorption with existing major status, rapid repeated entry and hazard-caused reserve fainting remain partial. |
| P1 | Status interactions and move locks | Status cures, Safeguard, Natural Cure, Synchronize, Uproar, Yawn, Nightmare, Curse, Perish Song, multi-turn moves and complete PP restriction semantics are incomplete. |
| P1 | Rapid Spin interactions | First-category priority is tested; substitute/protection/failure and move-effect script timing need additional fixtures. Defog is a later-generation extension and must be separately registered. |
| P2 | Move/ability dispatch expansion | Implement effect handlers only after the shared timing contract is known; keep unsupported registrations rejected. No broad Gen I–IX catalog population is part of this milestone. |
| P2 | Damage and field interaction breadth | Multi-hit, contact abilities, recoil/drain, spread targeting, items, critical interactions, additional field effects and all ability suppression combinations remain outside this bounded slice. |
| P3 | Persistent settlement and roguelite | Battle-to-save settlement, PP/reward transactions, difficulty scaling, enhancement ceilings and Challenge Mode isolation are not implemented here. |

**Recommendation:** Continue source-grounded implementation for mechanics whose runtime structure and scripts are clear, while keeping cartridge conformance labels pending. Before broad Generation I–IX effect population, close the initial-entry/replacement architecture and add independently repeatable original-ROM traces for action timing, entry, and end-turn interactions.
