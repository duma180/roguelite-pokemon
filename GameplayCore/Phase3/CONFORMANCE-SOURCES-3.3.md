# HGSS battle mechanics source ledger for resumed Phase 3.3 work

Pinned primary source: pokeheartgold commit 6988158691eb6178cff697cda0428045ddcba5fb. Source inspection confirms code structure only; all cartridge behavior remains pending unless explicitly labeled otherwise.

| Mechanic | Source | Source-confirmed facts implemented | Remaining runtime validation |
|---|---|---|---|
| Leech Seed residual order | src/battle/battle_controller_player.c, UpdateMonConditionState and UMC_STATE_LEECH_SEED | Seed processing is after Leftovers and before Poison; requires seed flag, living source battler, non-Magic-Guard target, and living target. | Full exact sequencing against other same-turn residuals and replacement timing. |
| Leech Seed damage and healing | files/battledata/script/subscript/subscript_0069_LeechSeed.s | Drains one eighth of the seeded target's max HP; transfers actual damage to source battler, subject to Liquid Ooze, source Magic Guard, and Heal Block. | Runtime confirmation of integer division/minimum damage, heal clipping, Liquid Ooze messaging/order. Heal Block and Big Root are not modeled. |
| Leech Seed application | files/battledata/script/effect_script/effect_script_0084.s; seed start script | Side effect targets defender and rejects Grass typing or an already-seeded target. | Accuracy and every substitute/type interaction from a captured battle. |
| Leech Seed switch cleanup | src/battle/overlay_12_0224E4FC.c, InitSwitchWork | Switch-out clears non-Baton-Pass volatile move-effect state. Seeded target's volatile ends on switch. | Baton Pass and multi-battler identity mapping; this prototype supports singles. |
| Rapid Spin removal | src/battle/battle_command.c, BtlCmd_RapidSpin | Clears the user's binding effect, Leech Seed, and entry hazards in the source branch order. | Runtime interaction matrix; current slice only clears Leech Seed on a successful damaging Rapid Spin. |

The implementation is source-derived, not cartridge-conformant. Unsupported Heal Block and Big Root interactions continue to require explicit future support.