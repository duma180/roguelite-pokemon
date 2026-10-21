using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using Roguelite.Gameplay;
using Roguelite.Gameplay.Phase3;

static object Run(string ruleset, uint seed)
{
    var species = new SpeciesDefinition("proof-grass", "phase3-proof-1", new SixStats(95, 100, 90, 100, 90, 100), Element.Grass);
    var record = new PokemonRecord(Guid.Parse("30000000-0000-0000-0000-000000000001"), species, 38, SixStats.Uniform(31), SixStats.Uniform(0), Nature.Hardy, Ability.NoneFixture, new[] { PilotCatalog.Tackle });
    var blocker = new AdvancedPokemon(record, new[] { new BattleMove("protect", Element.Normal, Category.Status, 0, 0, 10, BattleMoveEffect.Protect, priority: 3) }, abilityId: "none", protectChain: 1);
    var attackerSpecies = new SpeciesDefinition("proof-normal", "phase3-proof-1", new SixStats(110, 95, 95, 95, 95, 90), Element.Normal);
    var attackerRecord = new PokemonRecord(Guid.Parse("30000000-0000-0000-0000-000000000002"), attackerSpecies, 38, SixStats.Uniform(31), SixStats.Uniform(0), Nature.Hardy, Ability.NoneFixture, new[] { PilotCatalog.Tackle });
    var attacker = new AdvancedPokemon(attackerRecord, new[] { new BattleMove("fixture-strike", Element.Normal, Category.Physical, 45, 100, 20) }, abilityId: "none");
    var state = new AdvancedBattleState(new[] { blocker }, new[] { attacker }, seed, Guid.Parse("30000000-0000-0000-0000-000000000099"), ruleset);
    var first = AdvancedBattleEngine.ResolveTurn(state, BattleAction3.Move(0), BattleAction3.Move(0));
    var second = AdvancedBattleEngine.ResolveTurn(first.State, BattleAction3.Move(0), BattleAction3.Move(0));
    return new
    {
        ruleset,
        seed,
        firstTurnHash = BattleCanonical.Hash(first.State, first.Events),
        firstTurnEvents = first.Events.Select(e => new { e.Turn, kind = e.Kind.ToString(), e.Actor, e.Target, e.Detail, e.Amount, e.Roll }),
        secondTurnHash = BattleCanonical.Hash(second.State, second.Events),
        secondTurnEvents = second.Events.Select(e => new { e.Turn, kind = e.Kind.ToString(), e.Actor, e.Target, e.Detail, e.Amount, e.Roll }),
        remainingProtectChain = second.State.Active(Side.Player).ProtectChain,
        targetHp = second.State.Active(Side.Opponent).Hp
    };
}

static object RunCompleteEncounter(uint seed)
{
    var species = new SpeciesDefinition("proof-normal-encounter", "phase3-1-proof-1", new SixStats(100, 100, 100, 100, 100, 100), Element.Normal);
    var move = new BattleMove("proof-impact", Element.Normal, Category.Physical, 35, 100, 20);
    AdvancedPokemon Mon(int n) {
        var record = new PokemonRecord(Guid.Parse("40000000-0000-0000-0000-" + n.ToString("D12")), species, 20, SixStats.Uniform(20), SixStats.Uniform(0), Nature.Hardy, Ability.NoneFixture, new[] { PilotCatalog.Tackle });
        return new AdvancedPokemon(record, new[] { move }, abilityId: "none", hp: 1);
    }
    var state = new AdvancedBattleState(new[] { Mon(1), Mon(2) }, new[] { Mon(3), Mon(4) }, seed, Guid.Parse("40000000-0000-0000-0000-000000000099"));
    var transcript = new List<object>();
    for (int guard = 0; state.Outcome == BattleOutcome.Ongoing && guard < 20; guard++)
    {
        foreach (Side side in new[] { Side.Player, Side.Opponent })
        {
            if (state.NeedsReplacement(side))
            {
                int next = Enumerable.Range(0, state.Team(side).Count).First(i => i != state.ActiveIndex(side) && state.Team(side)[i].Hp > 0);
                var replacement = AdvancedBattleEngine.ReplaceFainted(state, side, next);
                transcript.Add(new { kind = "replacement", side = side.ToString(), events = replacement.Events.Select(e => new { e.Kind, e.Actor, e.Target, e.Detail, e.Amount, e.Roll }), hash = replacement.StateHash });
                state = replacement.State;
            }
        }
        if (state.Outcome != BattleOutcome.Ongoing) break;
        var step = AdvancedBattleEngine.ResolveTurn(state, BattleAction3.Move(0), BattleAction3.Move(0));
        transcript.Add(new { kind = "turn", events = step.Events.Select(e => new { e.Turn, kind = e.Kind.ToString(), e.Actor, e.Target, e.Detail, e.Amount, e.Roll }), hash = step.StateHash });
        state = step.State;
    }
    if (state.Outcome == BattleOutcome.Ongoing) throw new InvalidOperationException("Proof encounter exceeded its bounded turn count.");
    return new { seed, ruleset = state.Ruleset, transcript, outcome = state.Outcome.ToString(), finalStateHash = BattleCanonical.Hash(state) };
}

static object RunResidualOrderProof(uint seed)
{
    var species = new SpeciesDefinition("proof-residual", "phase3-2-proof-1", new SixStats(100, 100, 100, 100, 100, 100), Element.Normal);
    var guard = new BattleMove("proof-guard", Element.Normal, Category.Status, 0, 0, 10, BattleMoveEffect.Protect, priority: 3);
    AdvancedPokemon Mon(int n, Condition status, string ability)
    {
        var record = new PokemonRecord(Guid.Parse("50000000-0000-0000-0000-" + n.ToString("D12")), species, 30, SixStats.Uniform(20), SixStats.Uniform(0), Nature.Hardy, Ability.NoneFixture, new[] { PilotCatalog.Tackle });
        return new AdvancedPokemon(record, new[] { guard }, abilityId: ability, condition: status);
    }
    var state = new AdvancedBattleState(new[] { Mon(1, Condition.Burn, "speed-boost") }, new[] { Mon(2, Condition.Poison, "none") }, seed, Guid.Parse("50000000-0000-0000-0000-000000000099"));
    var step = AdvancedBattleEngine.ResolveTurn(state, BattleAction3.Move(0), BattleAction3.Move(0));
    return new { seed, events = step.Events.Select(e => new { e.Turn, kind = e.Kind.ToString(), e.Actor, e.Target, e.Detail, e.Amount, e.Roll }), stateHash = step.StateHash, finalHash = BattleCanonical.Hash(step.State) };
}

static object RunRapidSpinPrecedenceProof(uint seed)
{
    var species = new SpeciesDefinition("proof-spin", "phase3-4-proof-1", new SixStats(100, 100, 100, 100, 100, 100), Element.Normal);
    var record = new PokemonRecord(Guid.Parse("60000000-0000-0000-0000-000000000001"), species, 30, SixStats.Uniform(20), SixStats.Uniform(0), Nature.Hardy, Ability.NoneFixture, new[] { PilotCatalog.Tackle });
    var spin = new BattleMove("proof-rapid-spin", Element.Normal, Category.Physical, 20, 100, 40, BattleMoveEffect.RapidSpin);
    var spinner = new AdvancedPokemon(record, new[] { spin }, abilityId: "none", bindingTurns: 3, bindingSourceSide: (int)Side.Opponent,
        bindingSourceIndividualId: Guid.Parse("60000000-0000-0000-0000-000000000002"), bindingMoveId: "proof-wrap", leechSeedSourceSide: (int)Side.Opponent);
    var opponentRecord = new PokemonRecord(Guid.Parse("60000000-0000-0000-0000-000000000002"), species, 30, SixStats.Uniform(20), SixStats.Uniform(0), Nature.Hardy, Ability.NoneFixture, new[] { PilotCatalog.Tackle });
    var opponent = new AdvancedPokemon(opponentRecord, new[] { new BattleMove("proof-counterhit", Element.Normal, Category.Physical, 1, 100, 10) }, abilityId: "none");
    var state = new AdvancedBattleState(new[] { spinner }, new[] { opponent }, seed, Guid.Parse("60000000-0000-0000-0000-000000000099"),
        playerConditions: new SideConditions(spikesLayers: 1, toxicSpikesLayers: 1, stealthRock: true));
    var step = AdvancedBattleEngine.ResolveTurn(state, BattleAction3.Move(0), BattleAction3.Move(0));
    return new { seed, cleared = step.Events.Where(e => e.Detail.StartsWith("rapid-spin-cleared-", StringComparison.Ordinal)).Select(e => e.Detail), remainingBinding = step.State.Active(Side.Player).BindingTurns,
        remainingSeed = step.State.Active(Side.Player).LeechSeedSourceSide, remainingHazards = step.State.Conditions(Side.Player), eventHash = step.StateHash };
}

var output = new
{
    proofVersion = "phase3.4-battle-proof-1.0.0",
    engineAssembly = typeof(AdvancedBattleEngine).Assembly.GetName().Name,
    effectRegistryVersion = EffectRegistry.Foundational.Version,
    effectRegistryHash = EffectRegistry.Foundational.ContentHash,
    abilityRegistryVersion = AbilityRegistry.Version,
    abilityRegistryHash = AbilityRegistry.ContentHash,
    demonstrations = new[] { Run(CompatibilityProfile.Hgss, 2026), Run(CompatibilityProfile.CrossGenerationCatalog, 2026) },
    completedHgssEncounter = RunCompleteEncounter(8128),
    residualOrderingExample = RunResidualOrderProof(2048),
    rapidSpinPrecedenceExample = RunRapidSpinPrecedenceProof(4096)
};
Console.WriteLine(JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true }));
