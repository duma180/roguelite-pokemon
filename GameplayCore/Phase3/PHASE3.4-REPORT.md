# Phase 3.4: HGSS battle-system completion report

## Result

Phase 3.4 adds a connected, project-owned battle slice for binding, residual healing and damage, selected entry abilities, and hazard/entry interactions. It preserves the accepted Phase 1/2 contracts and existing Phase 3 rules. The work is **source-grounded, partial, and not original-ROM conformant**. It is not a complete HGSS battle engine or Generation I–IX content implementation.

Two source-comparison defects found during acceptance review were corrected:

1. Rapid Spin now clears only one applicable effect per successful use, in HGSS order: binding, Leech Seed, Spikes, Toxic Spikes, Stealth Rock. The pinned command returns after each branch. Previously the project cleared binding, seed and a hazard category together.
2. Magic Guard no longer suppresses beneficial Ingrain/Aqua Ring healing. It continues to suppress applicable indirect damage.

The reported test-run race was resolved by running suites sequentially. Three concurrent `dotnet test` commands attempted to write the shared Core assembly; no source defect was implicated.

## Implemented behavior

- Binding effects track both source and target by stable individual ID, plus source side and move ID. The handler represents a 2–5 turn duration, 1/16 maximum-HP residual damage, voluntary-switch restriction, and cleanup when either relevant individual switches. The exact bind-family move definitions, Grip Claw extension, and duration edge timing are not complete.
- Leech Seed remains connected to source-side recovery and target damage. Supported interactions include Magic Guard, Big Root, Liquid Ooze, Heal Block, switching and Rapid Spin. Multi-battles, Substitute and some faint timing remain unsupported.
- Ingrain and Aqua Ring recovery use the shared residual path; Heal Block blocks supported healing, Big Root boosts it, and Magic Guard does not block it. Ingrain blocks voluntary switching.
- Entry hazards run before supported switch-entry abilities. An entrant fainted by hazards receives no entry-ability effect. The bounded ability hooks include Trace, Drizzle, Drought, Sand Stream, Snow Warning and Intimidate; suppression and selected stat-drop blockers are modeled.
- Supported effects feed the same event stream, battle snapshots and deterministic state hash as the earlier battle engine.
- Unsupported forced-switch selection remains explicit: the effect returns `replacement-selection-policy-not-supplied`. No trainer or wild reserve is guessed.

The detailed coverage status and remaining effects are in [MECHANICS-COVERAGE-3.4.md](MECHANICS-COVERAGE-3.4.md) and [PRIORITIZED-EFFECTS-3.4.md](PRIORITIZED-EFFECTS-3.4.md). Pinned source details and evidence boundaries are in [CONFORMANCE-SOURCES-3.4.md](CONFORMANCE-SOURCES-3.4.md).

## Verification

Release tests ran sequentially with locked dependency restore:

| Suite | Passed | Failed | Skipped |
|---|---:|---:|---:|
| Phase 1 Core | 1,191 | 0 | 0 |
| Phase 2 Domain/Persistence | 1,866 | 0 | 0 |
| Phase 3 Battle | 79 | 0 | 0 |
| **Total** | **3,136** | **0** | **0** |

The Phase 3.4 battle proof was built in Release with 0 warnings and 0 errors. Two independent executions produced identical complete JSON output (SHA-256 `e0a0c57e60d88389b2e9b9f5488cd1bd40240d2f920d0b718362e5c276ed51bb`). In the overlap demonstration, one Rapid Spin use cleared binding while Leech Seed, Spikes, Toxic Spikes and Stealth Rock remained. The targeted tests also verify subsequent one-category-per-use ordering.

Tests cover binding identity and switch cleanup; Rapid Spin overlap precedence; Ingrain/Aqua Ring, Big Root and Heal Block; Trace/weather/Intimidate; hazard-caused entry fainting; Magic Guard suppression of residual damage; and deterministic state hashing. Test success is evidence for these tested cases, not a substitute for broader mechanic coverage.

## Acceptance matrix

| Requirement | Result | Detail |
|---|---|---|
| Preserve Phase 1/2 behavior | Pass | 1,191 + 1,866 regression tests passed. No Phase 1/2 source was changed. |
| Preserve existing Phase 3 behavior | Pass within tested scope | 79 Phase 3 tests passed; targeted additions cover the Phase 3.4 interactions. |
| Shared battle execution, residual and event pipeline | Partial pass | Supported handlers run through the same resolution/events/hash model. Full cartridge queue and residual state machine remain incomplete. |
| Binding and seed identity/switch interactions | Partial pass | The tested source/target IDs and supported cleanup work. Exact runtime duration and all move variants remain open. |
| Entry hazards and abilities | Partial pass | Supported voluntary switch and explicit faint replacement paths tested; initial encounter send-out is not separately initialized. |
| Trainer/wild replacement policy | Not accepted / unsupported | Explicit reserve index only; forced-switch automatic selection remains rejected. |
| Cartridge conformance | Pending | No accepted, reproducible battle observation from the original ROM was available in this phase. |
| Gen I–IX readiness | Architecture preserved, content incomplete | Versioned allowlists reject unsupported effects. No broad catalog population or modern default substitution was made. |
| Unity integration / baseline promotion | Not performed | GameplayCore remains presentation-independent; no Unity files or settings were changed by this phase. |

## Protected state and Git

The work is confined to `GameplayCore/Phase3/` on `dev/gameplay-core`. `main` and the New Bark branch are not advanced. The Unity `ProjectSettings/ProjectSettings.asset` contains a pre-existing local Cloud metadata diff; it is excluded from the Phase 3.4 change set. No ROM, emulator save, original-game media, Unity scene, or Gameplay Core runtime data was added.

## Remaining P0 work and recommendation

Before claiming broad HGSS completion or populating a large move/ability catalog, the next milestone should resolve the initial encounter send-out transition, trainer/wild replacement controller and forced-switch policies, and the complete field/individual residual ordering. Exact bind timing and weather-final-turn behavior need a reproducible original-ROM trace. Until then, new effects should be limited to source-grounded, explicitly bounded handlers and labeled cartridge-conformance-pending.

The current slice is suitable for review and continued isolated engine development. It is not ready for Unity integration, a production baseline, or broad effect catalog expansion.
