# Phase 3.2 architecture addendum

The battle core remains pure C#/.NET Standard 2.1. Existing persistent Phase 2 records and save contracts are not changed. Phase 3 battle state remains a clone/snapshot and does not settle HP/PP/status into persistent records.

## Ordering model

Each `ResolveTurn` captures starting actor identities, samples the speed-sorted residual order independently from move-action order, then resolves action class/priority/speed. Voluntary switches precede moves; Pursuit is a specific interception before the selected switch. After each action, the original selected actor identity is checked so an entrant cannot inherit its predecessor's action. A fainted actor or target cannot execute a queued action. The bounded model permits both active individuals to faint from a residual phase and reports a draw if both teams are exhausted.

This is still a two-actor single-battle scheduler, not the full HGSS command queue. Items, run actions, all before-turn actions, multi-battles, trainer AI, replacement prompts and many speed modifiers are not modeled.

## End-turn model

Supported side screens expire after actions. The precomputed residual speed order governs supported per-active conditions. The model processes Speed Boost/Shed Skin, the weather/ability branches represented by the model, major status damage, selected duration countdowns, then weather expiration. The runtime source's field-condition and per-mon state machines establish a richer order; final weather-tick ordering remains flagged pending a ROM execution trace. Leech Seed and binding are explicitly unsupported because their ownership/link state is absent.

The ability-hook registry is now `hgss-ability-hooks-1.2.0`. New handlers are limited to tested Speed Boost, Shed Skin, Quick Feet, Sand Veil, Snow Cloak, Rain Dish, Dry Skin, Ice Body, Hydration and Solar Power branches. Their registration does not imply full ability coverage.

## Compatibility, content and protection

`hgss-gen4-singles-1.0.0` remains the default. The explicit cross-generation catalog profile remains an opt-in prototype profile, not a default chosen by species generation. HGSS Protect remains unchanged in the default profile. The independent content-release rule in `PROGRESSIVE-CONTENT-RELEASES.md` remains authoritative: engine support and content availability are separate.

The six independent, uncapped +5% enhancements, price curve, Gym ceiling removal, exact difficulty formulas and BigInteger requirement remain documented in `NUMERIC-SAFETY-3.1.md`; no progression code was added. Standard and Challenge save isolation remains a future constraint in `FUTURE-MODE-ISOLATION.md`.
