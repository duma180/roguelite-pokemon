# Phase 3.1 prioritized remaining-effects inventory

This inventory distinguishes unfinished mechanics from content population. The ranking is for the next mechanics milestone, not approval to begin it.

| Priority | Work | Reason / evidence needed |
|---|---|---|
| P0 | Cartridge-grounded action queue and interruption fixtures | Current single-turn ordering snapshots both actions. Verify priority ties, switches, Pursuit, Protect, fainting, replacement, forced-switch failure, and queued-action cancellation against pinned runtime traces. |
| P0 | Residual-phase ordering and timing matrix | Status, weather, abilities, screens, hazards, and fainting can interact. Add per-event-order fixtures from the original HGSS runtime/emulator before claiming complete residual behavior. |
| P0 | Switch and replacement conformance | Confirm random reserve selection, trainer/wild differences, entry hazards, ability entry timing, switch cleanup, trapping exceptions, and Pursuit edge cases with reproducible source evidence. |
| P1 | Complete volatile-condition state machines | Binding, Leech Seed, Uproar, Yawn, Perish Song, Nightmare, embargo-like restrictions, multi-turn moves, and exact duration/decrement order are absent or partial. |
| P1 | Restriction move equivalence and timer details | Encore currently blocks selected exact IDs; the HGSS source uses effect-equivalence exclusions. Add effect mapping, all excluded effects, and exact Disable/Encore/Taunt decrement fixtures. |
| P1 | PP and move-failure interactions | Struggle, Grudge, Spite, Torment, Gravity, Heal Block, PP-Up capacity, multi-target Pressure, and all zero-PP/failed-use pathways need implementation and conformance cases. |
| P1 | Full status removal and immunity matrix | Add cure moves/items, secondary-effect immunity sources, Rest, Safeguard, Synchronize, Natural Cure, Shed Skin, thaw and paralysis exceptions with source tests. |
| P1 | Entry hazard and grounding matrix | Validate all hazard layers, airborne/grounded cases, type/ability/item interactions, switching, fainting on entry, and exact rounding. Defog is a later-generation extension and must be explicit. |
| P2 | Weather, screens, and duration interaction completion | Validate setter turn, extension items, weather abilities, critical and move-specific screen bypass, duration decrement, sand special defense, and residual ordering. |
| P2 | Representative complete move/ability script families | The registry is an explicit supported pilot allowlist, not full dispatch coverage. Implement families only with per-effect Gen IV fixtures and explicit later-generation profiles. |
| P2 | Broader critical, damage, multi-hit and item ordering | Add cartridge-backed modifier order, damage rounding, multi-hit, recoil/drain, contact abilities, held-item timing, and priority-item behavior. |
| P3 | Gen I–IX compatibility handler graph and type-system expansion | Add versioned per-content behavior policies, later-generation type charts (including Fairy), and explicit deviations. Current compatibility profile does not imply Scarlet/Violet completeness. |
| P3 | Battle settlement into persistent records | Define a reviewed settlement adapter for HP, PP, status, experience, capture, rewards, and identity-safe persistence. |

## Current source-grounded slice

Pinned pokeheartgold code corroborates the presence and selected details of Pursuit interception, Disable/Encore duration draws, Pressure PP decrement in Pursuit, Imprison shared-move checks, Rapid Spin effect precedence, forced-switch effect registration, and turn-start flinch clearing. The present implementation follows only the bounded behaviors listed in `MECHANICS-COVERAGE-3.1.md`; table presence or source code inspection alone does not prove all cartridge edge cases.
