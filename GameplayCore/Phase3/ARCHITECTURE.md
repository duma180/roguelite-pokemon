# Phase 3 architecture

## Boundary

`Battle/Phase3.Battle.csproj` is pure C# on .NET Standard 2.1 and has no production package references. It references the accepted `Core/GameplayCore.csproj` for Gen IV stats, damage primitives, type chart, and seeded RNG. Phase 1 battle classes, golden files, and proof code remain unchanged. Phase 3 owns `AdvancedBattleState`, `AdvancedPokemon`, turn events, and hashes because the frozen Phase 1 state deliberately represents a much smaller rules subset.

An `AdvancedPokemon` is a battle snapshot cloned from the Phase 1 `PokemonRecord`; it does not write PP, HP, conditions, status counters, or battle changes back to the Phase 2 persistent record. A settlement adapter is required before game saves can consume these results. Public arrays are exposed as read-only views, deep copies are independent, and every supported random draw is recorded with its purpose and seed state in the state hash.

## Ruleset profiles and content

`hgss-gen4-singles-1.0.0` uses HGSS arithmetic and rules. `hgss-combat-gen1-9-content-1.0.0` is a compatibility profile for selected newer content while keeping the same Gen IV foundation; it currently changes only repeated Protect odds to the modern one-third progression. It is not a full Scarlet/Violet battle ruleset. Compatibility is explicit per registered effect; a missing effect registration or ability hook throws `NotSupportedException`.

Each `BattleMove` carries an introduction generation, immutable mechanics fields, and one Phase 3 effect enum. `EffectRegistry` pins registration version and SHA-256 over canonical entries. `AbilityRegistry` pins its own version/hash and generation introductions. Neither registry contains the full species, move, or ability catalog. They are execution allowlists for the pilot slice. Gen VI Fairy type and its later type-chart changes are not representable through the accepted Gen IV `Element` enum; those require a versioned type catalog and matchup table before Fairy species or moves are admitted.

The project owns the implementation. Pinned source is used to verify behavior and data structure facts; no third-party engine code, game graphics, audio, or models are copied.

## Turn pipeline in this prototype

The supported flow validates both actions, clones the state, begins a turn, resolves switch/move priority and speed, checks action-blocking conditions, spends PP, applies accuracy/effect/damage, and then processes a bounded end-turn pass. Switching and fainted replacement are separate operations. Hazards run on entry; they are not synthesized for existing occupants.

The implementation does not claim the complete cartridge turn pipeline. Residual ordering, overlapping residual causes, ability activation timing, items, move-specific scripts, two-turn moves, multi-hit, protection-family interactions, and many switch effects still need direct runtime fixtures. Those omissions are listed in the acceptance matrix.

## Identity and numeric model

Battle snapshots retain `PokemonRecord.IndividualId`; teams reject duplicate identities. Volatile state is keyed by the active snapshot rather than slot-derived Pokémon identity. Switches clear stat stages and field volatiles on the outgoing creature while retaining major status and PP; toxic progression resets on switch-out.

The battle calculations use checked integer arithmetic and the Phase 1 validated Gen IV integer formulas. Persistent enhancements and unbounded zone scaling are separate domain values and are not implemented in this phase. They cannot safely be projected into the current bounded `int` battle-stat/damage API without a reviewed numeric bridge; see `NUMERIC-SAFETY.md`.
