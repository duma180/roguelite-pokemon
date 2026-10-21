using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Roguelite.Gameplay;

namespace Roguelite.Proof
{
    internal static class Program
    {
        private static Guid Id(int n) => Guid.Parse("00000000-0000-0000-0000-" + n.ToString("D12"));
        private static BattlePokemon Mon(int n, SpeciesDefinition species, int level, Ability ability, params MoveDefinition[] moves) => new BattlePokemon(new PokemonRecord(Id(n), species, level, SixStats.Uniform(15), SixStats.Uniform(0), Nature.Hardy, ability, moves));
        private static BattleState Initial(bool trainer, uint seed)
        {
            if (!trainer) return new BattleState(new[] { Mon(1, PilotCatalog.Bulbasaur, 12, Ability.Overgrow, PilotCatalog.RazorLeaf, PilotCatalog.Tackle, PilotCatalog.Growl) }, new[] { Mon(2, PilotCatalog.Rattata, 12, Ability.RunAway, PilotCatalog.QuickAttack, PilotCatalog.Tackle) }, seed, Id(100), EncounterKind.Wild);
            return new BattleState(new[] { Mon(11, PilotCatalog.Charmander, 30, Ability.Blaze, PilotCatalog.WillOWisp, PilotCatalog.Ember, PilotCatalog.Slash, PilotCatalog.Growl), Mon(12, PilotCatalog.Squirtle, 30, Ability.Torrent, PilotCatalog.WaterGun, PilotCatalog.Tackle) }, new[] { Mon(21, PilotCatalog.Magnemite, 30, Ability.Sturdy, PilotCatalog.ThunderWave, PilotCatalog.Tackle, PilotCatalog.Swift), Mon(22, PilotCatalog.Rattata, 30, Ability.RunAway, PilotCatalog.QuickAttack, PilotCatalog.Tackle) }, seed, Id(200));
        }
        private static List<StepResult> Run(bool trainer, uint seed, List<object>? transcript = null)
        {
            var s = Initial(trainer, seed); var results = new List<StepResult>();
            for (int turn = 0; turn < 30 && s.Outcome == BattleOutcome.Ongoing; turn++)
            {
                foreach (Side side in new[] { Side.Player, Side.Opponent })
                {
                    if (!s.NeedsReplacement(side)) continue;
                    int index = Enumerable.Range(0, s.Team(side).Count).First(i => s.Team(side)[i].Hp > 0);
                    var replacement = BattleEngine.ReplaceFainted(s, side, index); results.Add(replacement); transcript?.Add(new { kind = "replacement", side = side.ToString(), index, beforeHash = Canonical.Hash(Canonical.State(s)), afterHash = replacement.StateHash, events = replacement.Events }); s = replacement.State;
                }
                BattleAction Choose(Side side)
                {
                    var p = s.Active(side);
                    if (trainer && turn == 1 && side == Side.Player) return BattleAction.Switch(1);
                    int slot = 0;
                    if (trainer && side == Side.Player && p.Record.Species.Id == "charmander" && turn > 0) slot = 1;
                    if (trainer && side == Side.Opponent && p.Record.Species.Id == "magnemite") slot = turn == 0 || turn == 3 ? 0 : turn == 1 ? 1 : 2;
                    if (p.Pp[slot] == 0) slot = Enumerable.Range(0, p.Pp.Count).First(i => p.Pp[i] > 0);
                    return BattleAction.Move(slot);
                }
                var player = Choose(Side.Player); var opponent = Choose(Side.Opponent); var r = BattleEngine.ResolveTurn(s, player, opponent);
                transcript?.Add(new { kind = "turn", turn = r.State.Turn, player = new { kind = player.Kind.ToString(), index = player.Index }, opponent = new { kind = opponent.Kind.ToString(), index = opponent.Index }, beforeHash = Canonical.Hash(Canonical.State(s)), afterHash = r.StateHash, transitionHash = Canonical.TransitionHash(s, player, opponent, r), events = r.Events });
                results.Add(r); s = r.State;
            }
            if (s.Outcome == BattleOutcome.Ongoing) throw new InvalidOperationException("Bounded fixture did not finish within 30 turns."); return results;
        }
        private static object Scenario(bool trainer, uint seed)
        {
            var transcript = new List<object>(); var initial = Initial(trainer, seed); var results = Run(trainer, seed, transcript); var final = results.Last().State;
            return new { encounter = trainer ? "trainer" : "wild", seed, initialState = Canonical.State(initial), initialHash = Canonical.Hash(Canonical.State(initial)), finalState = Canonical.State(final), finalHash = Canonical.Hash(Canonical.State(final)), outcome = final.Outcome.ToString(), turns = final.Turn, randomDraws = final.RandomDraws, mechanics = results.SelectMany(r => r.Events).GroupBy(e => e.Kind.ToString()).ToDictionary(g => g.Key, g => g.Count()), transcript };
        }
        private static int Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--select-seed")
            {
                for (uint seed = 1; seed <= 512; seed++)
                {
                    var events = Run(true, seed).SelectMany(r => r.Events).ToArray();
                    if (events.Any(e => e.Kind == EventKind.Critical) && events.Any(e => e.Kind == EventKind.Missed) && events.Any(e => e.Kind == EventKind.StatusChanged && e.Detail == "Burn") && events.Any(e => e.Kind == EventKind.StatusChanged && e.Detail == "Paralysis")) { Console.WriteLine("Bounded illustrative trainer seed: " + seed); return 0; }
                }
                throw new Exception("No qualifying illustrative seed in 512 candidates.");
            }
            uint trainerSeed = 5; // First qualifying illustrative seed in the documented bounded authoring search (1..512).
            var options = new JsonSerializerOptions { WriteIndented = true }; options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
            var generated = PokemonFactory.Create(Id(300), PilotCatalog.Bulbasaur, 5, Ability.Overgrow, new[] { PilotCatalog.Tackle }, 1);
            var output = new { proofVersion = "1.0.0", ruleset = HgssRules.Version, randomAlgorithm = HgssRules.RandomVersion, notes = "Illustrative headless fixtures; scripted opponents, not AI; no learnset legality, rewards, XP or saves implemented. Seed selection is illustrative, not a probability study.", formulaEvidence = new { noStab = HgssRules.Damage(50, 40, 100, 100, false, false, 100, false, Element.Normal, Element.Normal), withStab = HgssRules.Damage(50, 40, 100, 100, false, false, 100, true, Element.Normal, Element.Normal), criticalWithStab = HgssRules.Damage(50, 40, 100, 100, false, true, 100, true, Element.Normal, Element.Normal), resistedWithStab = HgssRules.Damage(50, 40, 100, 100, false, false, 100, true, Element.Normal, Element.Rock) }, generatedCreature = new { algorithm = PokemonFactory.Version, seed = generated.SourceSeed, id = generated.Pokemon.IndividualId, ivs = generated.Pokemon.Ivs.ToArray(), nature = generated.Pokemon.Nature, finalRandomState = generated.FinalRandomState }, scenarios = new[] { Scenario(false, 2026), Scenario(true, trainerSeed) } };
            string json = JsonSerializer.Serialize(output, options); if (args.Length > 0) File.WriteAllText(args[0], json + "\n"); else Console.WriteLine(json); return 0;
        }
    }
}
