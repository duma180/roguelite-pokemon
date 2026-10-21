# Phase 3.4 mechanics coverage

This table describes implemented support, not full HGSS coverage. “Source-grounded” means the behavior was compared with the pinned pokeheartgold source and a targeted automated test. It does not mean it has been observed on the original cartridge. All original-ROM conformance remains pending.

| Area | Phase 3.4 status | Evidence and limits |
|---|---|---|
| Single-battle action lifecycle | Implemented bounded pipeline | Selected action validation, switch/move ordering, priority, speed ties, Pursuit interception, action interruption after fainting/switching, events, residual pass and battle-end state are integrated. No full cartridge action queue, AI, multi-hit or complete cancellation matrix. |
| Voluntary switching | Partial | Living non-active party members are accepted; switch locks, binding, Ingrain and Arena Trap/Shadow Tag/Magnet Pull are checked. Switch cleanup clears supported volatile state while retaining major status, HP and PP. Full trapping exceptions and Substitute are absent. |
| Faint/replacement | Explicit manual replacement | `ReplaceFainted` accepts an explicit legal reserve and runs entry hazards and entry abilities. No automatic trainer/wild policy, AI choice, chained replacement orchestration or complete battle UI/controller. Unsupported policy is never replaced by a guessed choice. |
| Forced switching | Explicitly unsupported | Roar/Whirlwind-style effects report `replacement-selection-policy-not-supplied`; no reserve is selected. Suction Cups, binding, Ingrain and switch lock block the implemented path. |
| Binding | Partial, source-grounded | The generic binding handler stores attacker and target individual identities, move ID and 2–5 turn duration; applies 1/16 residual while active; blocks voluntary switching; clears when the source or target switches. Bind-family move data/effect details are not individually cataloged, Grip Claw is unsupported, and exact duration start/last-tick timing lacks runtime validation. |
| Rapid Spin | Partial, corrected to source priority | A successful damaging hit removes one applicable condition per use, in pinned source order: binding, Leech Seed, Spikes, Toxic Spikes, Stealth Rock. Test covers overlapping binding, seed and multiple hazards. Rapid Spin does not clear more than one category in one use. |
| Leech Seed | Partial | HGSS eighth-max-HP drain, source identity/side transfer, Magic Guard, Big Root, Liquid Ooze, Heal Block, target switching, source switching and Rapid Spin cleanup are represented. Substitute, Baton Pass, multi-battles, duplicate/type/accuracy matrix and every faint edge are not complete. |
| Ingrain / Aqua Ring | Partial | Sixteenth-max-HP recovery is in the residual pipeline; Heal Block suppresses healing, Big Root increases it, Magic Guard does not suppress positive recovery. Ingrain blocks voluntary switching. Move setup/reapplication and complete residual competition remain partial. |
| Heal Block | Partial | Timer and healing checks cover Leech Seed recipient recovery, Ingrain and Aqua Ring. Other healing effects are unsupported unless listed. |
| Entry hazards | Partial | Stealth Rock, Spikes and Toxic Spikes use selected type/grounding/ability checks; Poison entrants absorb Toxic Spikes; hazard fainting suppresses entry abilities. Full immunities, grounded-state effects and hazard-removal behavior are incomplete. |
| Entry abilities | Partial | On modeled switch/replacement: hazards first; a fainted entrant receives no abilities; Trace copies only registered target abilities; permanent HGSS weather setters alter field weather; Intimidate drops Attack unless selected blocking abilities apply. Suppression and re-entry reset are tested. Initial encounter send-out ability dispatch, Mist/Substitute/stat-drop immunity breadth and exact order between opposing simultaneous send-outs are not implemented or validated. |
| Major/volatile status | Partial | Previously accepted sleep, freeze, poison/bad poison, burn, paralysis, confusion, flinch, infatuation, Disable, Encore, Taunt and Imprison behavior is retained. Cure moves, Safeguard, Natural Cure, Synchronize, Uproar, Yawn, Nightmare, Curse and broad interaction matrices are incomplete. |
| Weather / screens / residuals | Partial | Existing weather, screen countdowns, selected residual abilities, poison, burn, Leech Seed, binding, Ingrain, Aqua Ring and Heal Block share the end-turn processing path. The complete HGSS field and individual residual script order, items, expiration edge cases and all same-turn faint/replacement cases are not implemented. |
| Content and profiles | Pilot allowlists only | Effect and ability registries remain versioned and reject unsupported handlers. HGSS remains default; the cross-generation profile is narrow, not a modern battle ruleset. Full Gen I–IX content and per-effect compatibility decisions remain future work. |
| Persistence/progression | Preserved, not modified | Phase 1/2 save, identity, catalog and ruleset contracts were not changed. No roguelite economy, Gym ceiling, zone scaling, Challenge Mode or battle settlement was implemented. |

## Exact automated acceptance

| Check | Result |
|---|---:|
| Phase 1 suite | 1,191 passed, 0 failed |
| Phase 2 suite | 1,866 passed, 0 failed |
| Phase 3 suite | 79 passed, 0 failed |
| Total | 3,136 passed, 0 failed |
| Build diagnostics | Release test builds completed without compiler warnings or errors |
| Unity integration / ROM runtime | Not performed; neither was in scope |

The suite includes targeted P3.4 overlap checks for Rapid Spin precedence, binding identity/switch cleanup, Ingrain/Aqua Ring/Big Root/Heal Block, entry hazard fainting and entry ability ordering, and Magic Guard suppression. Passing unit tests validate this bounded code; they are not proof of complete mechanics coverage.
