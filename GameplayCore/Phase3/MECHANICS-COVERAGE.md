# Phase 3 mechanics coverage

| Area | Phase 3 status | Evidence and limits |
|---|---|---|
| Gen IV stats, stages, type chart, STAB, damage, burn penalty | Reused and regression-tested | Calls accepted Phase 1 `HgssRules`; unchanged Phase 1 damage/stat/type golden fixtures remain separate. |
| Turn choice, move priority, switch priority, speed ties | Partial | Single battles, one chosen move per side, start-turn speed and ties; no items, move lock, Quick Claw, Pursuit, Trick Room, or full HGSS action queue. |
| Sleep | Partial | 1–3 turn counter fixture, blocks action, deterministic wake timing; sleep moves/items/abilities and exact every-script timing remain incomplete. |
| Freeze | Partial | 20% thaw roll per action, Ice immunity, fire move thaw; no full ability/item/script interaction matrix. |
| Poison and badly poisoned | Partial | 1/8 regular residual; toxic counter 1/16 through 15/16, reset on switch; complete ordering with all residual effects is unresolved. |
| Burn and paralysis | Partial | Burn damage residual/physical damage penalty, Fire immunity; paralysis action roll and quarter-speed; full HGSS ability/status cures remain incomplete. |
| Confusion | Partial | 2–5 turn volatile, deterministic 50% action block, typeless self-hit formula and random range; exact cartridge timing needs a direct fixture. |
| Flinch, infatuation | Partial | Flinch stops later actor, gender/genderless checks and 50% block; no complete move/ability/item triggers. |
| Trapping | Partial | Mean Look style switch lock plus Arena Trap, Shadow Tag, Magnet Pull hooks. Bind/Wrap/Fire Spin/Clamp/Whirlpool partial-trap residuals and duration are unsupported. |
| PP and Struggle | Partial | Per-slot PP consumption, bounded restoration helper, automatic Struggle at zero PP, typeless damage and quarter-max-HP recoil. Disable/Taunt/Encore/Imprison/Pressure and PP Up aren't implemented. |
| Protect | Implemented for the basic action | First use guaranteed. HGSS success thresholds are 1, 1/2, 1/4, then 1/8 capped; modern-content profile is 1, 1/3, 1/9 … capped at 1/729. Protect family and Feint interactions are not implemented. |
| Weather | Partial | Rain, sun, sand, hail; five-turn move duration; basic damage/residual, Sand Rock special-defense modifier, Air Lock/Cloud Nine and selected speed abilities. Weather rocks, abilities, accuracy/solar-beam effects, and exact rounding order remain open. |
| Entry hazards | Partial | Spikes layers, Toxic Spikes layers/absorption, Stealth Rock type multiplier, Flying/Levitate grounding, Magic Guard. No Rapid Spin/Defog/removal, Boots, Magnet Rise, full immunities or source-script comparison for every layer. |
| Screens | Partial | Reflect/Light Screen, five turn base, physical/special single-battle reduction and critical bypass. Safeguard, Mist, Brick Break, screens extension items and exact rounding need tests. |
| Ability hooks | Narrow validated set | Overgrow/Blaze/Torrent, Levitate, Magic Guard, Air Lock/Cloud Nine, status cures/immunities, switch traps, Swift Swim/Chlorophyll/Sand Rush. Unknown hooks fail. Full Gen I–IX abilities are not present. |
| Move content | Pilot definitions only | New move schema carries generation and mechanics fields; no full HGSS, Gen I–IX, form, or learnset catalog. Fairy type and added type-chart changes are unsupported. |
| Modern compatibility | Narrow | Protect repeat odds are profile-specific; other registered base effects are explicitly ported with unchanged behavior. No global Gen IX rule substitutions are made. Individual post-Gen IV move/ability effects are mostly unsupported. |
| Persistent battle settlement | Not implemented | No commit back to persistent HP/PP/status, no XP/rewards/capture/evolution/reset progression. |

The Phase 3 unit suite has 41 cases. That count is code validation, not a claim of complete HGSS move or ability coverage.
