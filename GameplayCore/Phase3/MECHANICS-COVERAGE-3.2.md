# Phase 3.2 mechanics coverage

| System | Phase 3.2 result | Test / source | Remaining limit |
|---|---|---|---|
| Action class ordering | **Source-grounded, partial** | `VoluntarySwitchPrecedesMovePriorityEvenWhenSlower`; `CalcExecutionOrder` | Switch/fight class handling and numeric move priority tested. No full Quick Claw/Custap, Trick Room, Stall, item, or ability modifier matrix. |
| Effective Speed | **Source-grounded, partial** | `QuickFeetUsesGenFourStatusSpeedModifierInsteadOfParalysisPenalty`; `CheckSortSpeed` | Stages, paralysis, Quick Feet, Swift Swim/Chlorophyll, and profile-gated Sand Rush supported. Other modifiers remain unsupported. |
| Separate action and residual order | **Implemented, structurally grounded** | `ResidualSpeedTieIsResolvedOnceBeforeResidualEffectsAndReplayIsStable`; `SortMonsBySpeed` | RNG equivalence to cartridge sort and battle-state speed snapshot nuances remain unvalidated. |
| Switch / Pursuit / actor identity | **Phase 3.1 retained, partial** | Pursuit suite; action cancellation test | Damage, priority, trainers, wild selection, trapped exceptions and entry sequence need cartridge fixtures. |
| Faint / replacement / outcome | **Partial** | fainted queued actor and simultaneous residual draw tests | `ReplaceFainted` is explicit legal-index selection. No trainer/wild mode policy; no complete replacement UI/AI pipeline. |
| Protect | **HGSS default retained; partial** | Existing protect chain suite | Interaction matrix is incomplete; cross-generation profile remains explicit and not the default. |
| Burn / poison / toxic | **Partial, source-grounded formulas** | retained status/toxic tests; `UpdateMonCondition` | Cure/immunity/ability/item effects and residual script/event sequencing remain incomplete. |
| Speed Boost / Shed Skin | **Source-grounded pilot** | ability-order tests; `ov12_02253068` | Only these representative UMC abilities; source RNG stream mapping and edge interactions are not emulator-verified. |
| Sandstorm / hail | **Source-grounded pilot** | weather branch tests; `BtlCmd_EndOfTurnWeatherEffect` | Weather final-tick and field timer interaction unresolved. |
| Rain/sun abilities | **Source-grounded pilot** | Rain Dish, Hydration, Dry Skin tests | Full weather-effect precedence, Heal Block and all ability/item interactions omitted. |
| Screens and volatile durations | **Partial** | screen/restriction/flinch tests | Only Reflect/Light Screen and selected restriction counters. Other field and volatile effects absent. |
| Leech Seed / binding residuals | **Unsupported explicitly** | No effects registered | Requires state representation, transfer/damage/cleanup scripts, switching semantics and independent fixtures. No approximate behavior is enabled. |
| Entry hazards / entry abilities | **Partial** | Phase 3.1 hazard tests | Hazards supported for selected records. Entry ability handlers and hazard/ability/faint ordering are not implemented. |
| Gen I–IX content and mechanics | **Extensible allowlist only** | Effect/ability registries | Not full catalogs or dispatch. Later rule changes remain per explicit ruleset; presence of a modern mon does not select modern mechanics. |
| Roguelite progression | **Not implemented; numeric contract preserved** | `NUMERIC-SAFETY-3.1.md` | No enhancement, ceiling, zone scaling, PP economy, or Gym progression code. |
| Challenge Mode isolation | **Documented only** | `FUTURE-MODE-ISOLATION.md` | No Challenge Mode saves, Nuzlocke behavior, timed seasons, or rewards implemented. |

## Conformance terminology

No full battle mechanic is claimed as end-to-end cartridge-conformed in this milestone. “Source-grounded” means implementation choices match a named pinned runtime branch and a targeted test asserts the supported case. The source checkout does not provide independent execution traces for all timings. Exact remaining gaps are detailed in `ACTION-ORDER-MATRIX-3.2.md`, `RESIDUAL-MATRIX-3.2.md`, and `PRIORITIZED-EFFECTS-3.2.md`.
