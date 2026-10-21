# HGSS completeness roadmap and Unity integration proposal

## Recommended next milestones

This is a proposed sequence, not authorization to begin implementation or a promise that the small prototype already covers HGSS. Each milestone should freeze its supported content, fixtures, ruleset version and explicit exclusions before expansion.

| Milestone | Scope | Acceptance gate |
|---|---|---|
| 2. Persistent domain and catalog boundary | Full individual DTO, form/species IDs, personality metadata, party/PC/box identity, immutable catalog registry, save format and migrations; remove synthetic ingress from production | Round-trip every field; preserve deep-copy independence; reject unsupported schema/catalog; atomic save failure/recovery tests; no world or presentation dependency |
| 3. Broader Gen IV singles state machine | PP/Struggle, major/volatile statuses and durations, stat/accuracy stages, Protect and other action constraints, trapping/switch mechanics, weather, screens, hazards and residual phase order | Original-game/reference vectors for rounding and timing edges; deterministic replay through all supported events; no silent effect fallback |
| 4. HGSS move/ability/item conformance | Versioned Gen IV move effect registry, secondary effects, multi-hit/recoil/drain/two-turn moves, Pursuit, Explosion's Gen IV defense handling, held items and ability interactions | Enumerate supported catalog entries; require behavior tests per effect plus interaction suites; fail closed for unsupported entries; measure semantic coverage separately from code coverage |
| 5. Progression and encounter lifecycle | Capture/balls, flee rules, experience/growth, EV awards, evolution/learnsets/learning, forms/friendship/gender/shiny rules, trainer victory/rewards/AI | Independent formula and probability-bound tests; catch-to-party/PC round trips; progression/save/replay consistency; lawful original-HGSS differential reference cases |
| 6. Additional HGSS battle formats | Doubles, multi/trainer combinations, spread targets, simultaneous faints and format-specific phase ordering, special encounters and relevant Frontier rules | Explicit format matrix and conformance cases; no inference from singles success |
| 7. Roguelite policy implementation | Reviewed enhancement/inheritance math, reset-to-5, protected boxes, repeatable reward receipts and zone scaling | Preserve immutable catalog and individual identity; transactional crash/retry tests; reviewed rounding examples, caps and source-to-zone mappings |
| 8. Modern content extension | Approved later-generation species/forms/moves/abilities and explicit rules for their interaction with Gen IV | Versioned opt-in catalog/ruleset; no silent modern type-chart, critical, status or item substitutions; save migration and regression suite |

Milestones 3–6 are major bodies of work, not an estimate that full HGSS fidelity follows after a few additional tests. A battle engine alone does not supply Johto/Kanto story scripts, encounters or Gym progression. Those remain separate world/progression systems and must not alter the approved structural IR.

## Review decisions before roguelite implementation

The interfaces intentionally leave ambiguous policy choices visible. Confirm +5% stacking and rounding, inheritance of percentages versus flat earned deltas/additional evolution bonuses, enhancement caps, protected-box reset semantics, the 1/10/30 encounter-class mapping and exact geographic zone tiers. Decide reset HP/status/PP/XP/held-item behavior and treatment of one-HP species. No current battle result applies these bonuses.

A repeated encounter must be rewardable again, while replaying or retrying delivery of the same defeat must not duplicate a payment. Use a new encounter identity and stable per-defeat receipt, not a global “this Pokémon has already rewarded points” flag. Commit rewards, roster updates and reset state in one persistence transaction.

## Proposed Unity integration after approval

Do not copy the prototype into the existing project during this review. The eventual integration should follow these boundaries:

1. Package the reviewed gameplay core as its own source assembly/UPM package with an asmdef that has no UnityEngine dependency. Keep pure C# tests runnable outside Unity. Move PilotCatalog behind a versioned registry provider and reserve synthetic fixtures for tests.
2. Add a thin Unity-facing application adapter that submits commands and observes ordered events. Rendering/animations, menus, audio, cameras, GameObjects and URP assets are consumers; none may determine damage, timing, RNG calls or persistent identity. Simulation advances at command boundaries, independent of frame rate.
3. Introduce explicit world ports for encounter definitions, trainer identities, progression flags and geographic zone IDs. Preserve the existing HGSS importer and New Bark source-linked identities. Use a separate reviewed mapping artifact; do not reinterpret or mutate structural IR fields for battle content.
4. Keep the save repository and catalog loader outside the battle kernel. Start with validated versioned data and atomic DTO persistence. No legacy SQLite binaries or BinaryFormatter path should be introduced merely to reuse the audit candidates.
5. Add an isolated gameplay demonstration scene only after approval, preserving SampleScene, New Bark and project/render settings. No original Pokémon assets are required: a diagnostic UI can present text/events from the headless fixtures.
6. Validate Unity 6000.3.26f1/C# 9 compilation, Editor execution and a Windows x64 IL2CPP player. Check stripping, SHA-256 API availability, serialization paths, supported data loading and replay agreement with the headless goldens. The current .NET 10 hosts are not shipped as Unity plugins.

Integration acceptance should require exact state/event hash agreement between headless and Unity for approved fixtures, no console warnings/errors, no project/source-data mutation, no duplicate identities, stable save round trips and an explicit license/provenance manifest. Cross-runtime canonical hashing should be tested before it becomes a saved compatibility promise.

## Definition of HGSS mechanics completeness

Maintain a supported-effects/abilities/items/formats matrix with explicit known exceptions, independent expected results and targeted interactions. Passing every unit test only means the assertions passed; it does not prove coverage of every HGSS move or combination. Complement formula vectors with lawful original-game comparisons and reviewed mechanics research. In particular, validation must include faint/replacement timing, self-KOs, capture/storage, multi-target damage, source-era data parameters and late-game special encounters.

No integration, broader mechanics implementation, additional world construction, artwork or baseline promotion is authorized by this document. Stop for review of Phase 1.
