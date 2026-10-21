using System;
using System.Linq;
using Roguelite.Gameplay;
using Roguelite.Gameplay.Phase3;
using Xunit;

namespace Roguelite.Phase3.Tests
{
    public sealed class Phase34InteractionsTests
    {
        private static BattleMove Wrap() => new BattleMove("wrap", Element.Normal, Category.Physical, 15, 100, 20, BattleMoveEffect.Bind, effectChance: 100, generationIntroduced: 1);
        private static BattleMove Spin() => new BattleMove("rapid-spin", Element.Normal, Category.Physical, 20, 100, 40, BattleMoveEffect.RapidSpin);
        private static AdvancedPokemon WithState(AdvancedPokemon p, int? hp = null, bool ingrained = false, bool aquaRing = false, int healBlock = 0,
            int bindingTurns = 0, int bindingSide = -1, Guid bindingSource = default, string? bindingMove = null, string? item = null, bool abilitySuppressed = false, int seedSide = -1)
            => new AdvancedPokemon(p.Record, p.Moves, p.IsMale, p.IsGenderless, p.AbilityId, hp ?? p.Hp, p.Condition, p.ConditionCounter, p.ToxicCounter,
                pp: p.Pp, stages: p.Stages, ingrained: ingrained, aquaRing: aquaRing, healBlockTurns: healBlock, leechSeedSourceSide: seedSide, bindingTurns: bindingTurns,
                bindingSourceSide: bindingSide, bindingSourceIndividualId: bindingSource, bindingMoveId: bindingMove, heldItemId: item, abilitySuppressed: abilitySuppressed);

        [Fact]
        public void BindingTracksBothIndividualsBlocksSwitchingAndClearsWhenSourceLeaves()
        {
            var user = F.Mon(1, moves: new[] { Wrap() }); var target = F.Mon(2);
            var s = new AdvancedBattleState(new[] { user }, new[] { target, F.Mon(4) }, 91, F.Id(901));
            var applied = AdvancedBattleEngine.ResolveTurn(s, BattleAction3.Move(0), BattleAction3.Move(0));
            Assert.Contains(applied.Events, e => e.Detail == "binding:wrap" && e.Actor == F.Id(1).ToString("N") && e.Target == F.Id(2).ToString("N"));
            var bound = applied.State.Active(Side.Opponent);
            Assert.Equal(F.Id(1), bound.BindingSourceIndividualId);
            Assert.Equal((int)Side.Player, bound.BindingSourceSide);
            Assert.Equal("wrap", bound.BindingMoveId);
            Assert.Throws<InvalidOperationException>(() => AdvancedBattleEngine.ResolveTurn(applied.State, BattleAction3.Move(0), BattleAction3.Switch(1)));

            var sourceSwitch = new AdvancedBattleState(new[] { applied.State.Team(Side.Player)[0], F.Mon(3) }, applied.State.Team(Side.Opponent), 92, F.Id(902), turn: applied.State.Turn);
            var left = AdvancedBattleEngine.ResolveTurn(sourceSwitch, BattleAction3.Switch(1), BattleAction3.Move(0));
            Assert.Equal(0, left.State.Active(Side.Opponent).BindingTurns);
            Assert.Equal(Guid.Empty, left.State.Active(Side.Opponent).BindingSourceIndividualId);
        }

        [Fact]
        public void RapidSpinClearsBindingAndIngrainPreventsVoluntarySwitch()
        {
            var spinnerBase = F.Mon(1, moves: new[] { Spin() });
            var spinner = WithState(spinnerBase, bindingTurns: 3, bindingSide: (int)Side.Opponent, bindingSource: F.Id(2), bindingMove: "wrap");
            var result = AdvancedBattleEngine.ResolveTurn(F.State(spinner, F.Mon(2), 10), BattleAction3.Move(0), BattleAction3.Move(0));
            Assert.Equal(0, result.State.Active(Side.Player).BindingTurns);
            Assert.Contains(result.Events, e => e.Detail == "rapid-spin-cleared-binding");

            var rooted = WithState(F.Mon(3), ingrained: true);
            var rootedState = new AdvancedBattleState(new[] { rooted, F.Mon(4) }, new[] { F.Mon(5) }, 11, F.Id(903));
            Assert.Throws<InvalidOperationException>(() => AdvancedBattleEngine.ResolveTurn(rootedState, BattleAction3.Switch(1), BattleAction3.Move(0)));
        }

        [Fact]
        public void RapidSpinClearsOnlyTheFirstApplicableHgssConditionPerUse()
        {
            var spinner = WithState(F.Mon(1, moves: new[] { Spin() }), bindingTurns: 3,
                bindingSide: (int)Side.Opponent, bindingSource: F.Id(2), bindingMove: "wrap", seedSide: (int)Side.Opponent);
            var state = new AdvancedBattleState(new[] { spinner }, new[] { F.Mon(2) }, 101, F.Id(909),
                playerConditions: new SideConditions(spikesLayers: 1, toxicSpikesLayers: 1, stealthRock: true));

            var binding = AdvancedBattleEngine.ResolveTurn(state, BattleAction3.Move(0), BattleAction3.Move(0));
            Assert.Contains(binding.Events, e => e.Detail == "rapid-spin-cleared-binding");
            Assert.DoesNotContain(binding.Events, e => e.Detail == "rapid-spin-cleared-leech-seed");
            Assert.Equal((int)Side.Opponent, binding.State.Active(Side.Player).LeechSeedSourceSide);
            Assert.Equal(1, binding.State.Conditions(Side.Player).SpikesLayers);
            Assert.Equal(1, binding.State.Conditions(Side.Player).ToxicSpikesLayers);
            Assert.True(binding.State.Conditions(Side.Player).StealthRock);

            var seed = AdvancedBattleEngine.ResolveTurn(binding.State, BattleAction3.Move(0), BattleAction3.Move(0));
            Assert.Contains(seed.Events, e => e.Detail == "rapid-spin-cleared-leech-seed");
            Assert.Equal(1, seed.State.Conditions(Side.Player).SpikesLayers);

            var spikes = AdvancedBattleEngine.ResolveTurn(seed.State, BattleAction3.Move(0), BattleAction3.Move(0));
            Assert.Contains(spikes.Events, e => e.Detail == "rapid-spin-cleared-spikes");
            Assert.Equal(0, spikes.State.Conditions(Side.Player).SpikesLayers);
            Assert.Equal(1, spikes.State.Conditions(Side.Player).ToxicSpikesLayers);
            Assert.True(spikes.State.Conditions(Side.Player).StealthRock);

            var toxicSpikes = AdvancedBattleEngine.ResolveTurn(spikes.State, BattleAction3.Move(0), BattleAction3.Move(0));
            Assert.Contains(toxicSpikes.Events, e => e.Detail == "rapid-spin-cleared-toxic-spikes");
            Assert.Equal(0, toxicSpikes.State.Conditions(Side.Player).ToxicSpikesLayers);
            Assert.True(toxicSpikes.State.Conditions(Side.Player).StealthRock);

            var stealthRock = AdvancedBattleEngine.ResolveTurn(toxicSpikes.State, BattleAction3.Move(0), BattleAction3.Move(0));
            Assert.Contains(stealthRock.Events, e => e.Detail == "rapid-spin-cleared-stealth-rock");
            Assert.False(stealthRock.State.Conditions(Side.Player).StealthRock);
        }

        [Fact]
        public void IngrainAndAquaRingHealForOneSixteenthBigRootBoostsAndHealBlockPreventsThem()
        {
            int hp = F.Mon(1).Hp; int max = HgssRules.CalculateStats(F.Mon(1).Record)[Stat.Hp];
            var root = WithState(F.Mon(1), hp: hp - 20, ingrained: true, item: "big-root");
            var ring = WithState(F.Mon(2), hp: hp - 20, aquaRing: true);
            var r = AdvancedBattleEngine.ResolveTurn(F.State(root, ring, 12), BattleAction3.Move(0), BattleAction3.Move(0));
            int expected = (Math.Max(1, max / 16) * 130) / 100;
            Assert.Contains(r.Events, e => e.Detail == "ingrain-heal" && e.Amount == expected);
            Assert.Contains(r.Events, e => e.Detail == "aqua-ring-heal" && e.Amount == Math.Max(1, max / 16));

            var blocked = WithState(F.Mon(3), hp: hp - 20, ingrained: true, healBlock: 3);
            var b = AdvancedBattleEngine.ResolveTurn(F.State(blocked, F.Mon(4), 13), BattleAction3.Move(0), BattleAction3.Move(0));
            Assert.DoesNotContain(b.Events, e => e.Detail == "ingrain-heal");
        }

        [Fact]
        public void EntryAbilitiesRespectHazardFaintingAndImplementTraceWeatherAndIntimidate()
        {
            var trace = F.Mon(3, ability: "trace");
            var traceState = new AdvancedBattleState(new[] { F.Mon(1), trace }, new[] { F.Mon(2, ability: "swift-swim") }, 14, F.Id(904));
            var traced = AdvancedBattleEngine.ResolveTurn(traceState, BattleAction3.Switch(1), BattleAction3.Move(0));
            Assert.Equal("swift-swim", traced.State.Active(Side.Player).AbilityId);
            Assert.Contains(traced.Events, e => e.Detail == "trace-copied:swift-swim");

            var drizzle = F.Mon(5, ability: "drizzle");
            var rainState = new AdvancedBattleState(new[] { F.Mon(4), drizzle }, new[] { F.Mon(6) }, 15, F.Id(905));
            var rain = AdvancedBattleEngine.ResolveTurn(rainState, BattleAction3.Switch(1), BattleAction3.Move(0));
            Assert.Equal(Weather.Rain, rain.State.Weather);
            Assert.Equal(0, rain.State.WeatherTurns); // HGSS weather setters use permanent field-condition bits.

            var intimidator = F.Mon(7, ability: "intimidate");
            var intimidateState = new AdvancedBattleState(new[] { F.Mon(8), intimidator }, new[] { F.Mon(9) }, 16, F.Id(906));
            var lowered = AdvancedBattleEngine.ResolveTurn(intimidateState, BattleAction3.Switch(1), BattleAction3.Move(0));
            Assert.Equal(-1, lowered.State.Active(Side.Opponent).Stages[(int)Stage.Attack]);

            var faintingEntry = WithState(F.Mon(10, ability: "intimidate"), hp: 1);
            var hazardState = new AdvancedBattleState(new[] { F.Mon(11), faintingEntry }, new[] { F.Mon(12) }, 17, F.Id(907), playerConditions: new SideConditions(stealthRock: true));
            var fainted = AdvancedBattleEngine.ResolveTurn(hazardState, BattleAction3.Switch(1), BattleAction3.Move(0));
            Assert.Equal(0, fainted.State.Active(Side.Player).Hp);
            Assert.DoesNotContain(fainted.Events, e => e.Detail == "intimidate-attack-drop");
        }

        [Fact]
        public void AbilitySuppressionDisablesMagicGuardForResidualDamage()
        {
            var suppressed = WithState(F.Mon(1, ability: "magic-guard"), abilitySuppressed: true);
            var state = new AdvancedBattleState(new[] { suppressed }, new[] { F.Mon(3) }, 18, F.Id(908), weather: Weather.Sandstorm, weatherTurns: 2);
            var result = AdvancedBattleEngine.ResolveTurn(state, BattleAction3.Move(0), BattleAction3.Move(0));
            Assert.Contains(result.Events, e => e.Target == F.Id(1).ToString("N") && e.Detail == "sandstorm-residual");
            Assert.Equal(BattleCanonical.Hash(result.State), BattleCanonical.Hash(result.State.DeepCopy()));
        }

        [Fact]
        public void MagicGuardDoesNotSuppressIngrainOrAquaRingHealing()
        {
            int maxHp = HgssRules.CalculateStats(F.Mon(1).Record)[Stat.Hp];
            var protectedHealer = WithState(F.Mon(1, ability: "magic-guard"), hp: maxHp - 20, ingrained: true, aquaRing: true);
            var result = AdvancedBattleEngine.ResolveTurn(F.State(protectedHealer, F.Mon(2), 19), BattleAction3.Move(0), BattleAction3.Move(0));
            Assert.Contains(result.Events, e => e.Detail == "ingrain-heal");
            Assert.Contains(result.Events, e => e.Detail == "aqua-ring-heal");
        }
    }
}
