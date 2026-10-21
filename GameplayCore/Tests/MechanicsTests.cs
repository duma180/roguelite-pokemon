using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Roguelite.Gameplay;
using Xunit;

namespace Roguelite.Tests
{
    public static class Fixture
    {
        public static readonly Guid Encounter = Guid.Parse("10000000-0000-0000-0000-000000000001");
        public static Guid Id(int x) => Guid.Parse("00000000-0000-0000-0000-" + x.ToString("D12"));
        public static SpeciesDefinition Species(Element type = Element.Normal, Element? second = null, int speed = 50) => new SpeciesDefinition("fixture-" + type.ToString().ToLowerInvariant() + (second?.ToString().ToLowerInvariant() ?? "") + "-" + speed, "fixture-1", new SixStats(100, 100, 100, 100, 100, speed), type, second);
        public static MoveDefinition Hit(int power = 40, int accuracy = 100, int priority = 0, int crit = 0, Element type = Element.Normal, Category category = Category.Physical) => new MoveDefinition("fixture-hit-" + power + "-" + accuracy + "-" + (priority + 7) + "-" + crit + "-" + type.ToString().ToLowerInvariant() + "-" + category.ToString().ToLowerInvariant(), type, category, power, accuracy, 64, priority, crit);
        public static PokemonRecord Record(int id = 1, SpeciesDefinition? species = null, MoveDefinition[]? moves = null, int level = 50, Nature nature = Nature.Hardy, Ability ability = Ability.NoneFixture) => new PokemonRecord(Id(id), species ?? Species(), level, SixStats.Uniform(31), SixStats.Uniform(0), nature, ability, moves ?? new[] { Hit() });
        public static BattlePokemon Mon(int id = 1, SpeciesDefinition? species = null, MoveDefinition[]? moves = null, int? hp = null, MajorStatus status = MajorStatus.Healthy, int[]? stages = null, int[]? pp = null, Ability ability = Ability.NoneFixture, int level = 50) => new BattlePokemon(Record(id, species, moves, level, ability: ability), hp, status, pp, stages);
        public static BattleState State(BattlePokemon? player = null, BattlePokemon? opponent = null, uint seed = 123, EncounterKind kind = EncounterKind.Trainer) => new BattleState(new[] { player ?? Mon() }, new[] { opponent ?? Mon(2) }, seed, Encounter, kind);
        public static StepResult Step(BattleState? s = null) => BattleEngine.ResolveTurn(s ?? State(), BattleAction.Move(0), BattleAction.Move(0));
        public static BattleState Seed(BattleState s, uint seed) => new BattleState(s.Team(Side.Player), s.Team(Side.Opponent), seed, s.EncounterId, s.Kind, s.ActiveIndex(Side.Player), s.ActiveIndex(Side.Opponent), s.Ruleset, s.Turn, s.RandomDraws);
        public static StepResult Find(BattleState s, Func<StepResult, bool> predicate)
        {
            for (uint seed = 1; seed <= 2000; seed++) { var result = Step(Seed(s, seed)); if (predicate(result)) return result; }
            throw new Exception("No qualifying deterministic fixture in bounded seed search.");
        }
    }

    public sealed class ReferenceTests
    {
        private static readonly JsonDocument Oracle = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "oracle.json")));
        public static IEnumerable<object[]> DamageVectors() => Oracle.RootElement.GetProperty("damageCases").EnumerateArray().Select((_, i) => new object[] { i });
        public static IEnumerable<object[]> StatVectors() => Oracle.RootElement.GetProperty("statCases").EnumerateArray().Select((_, i) => new object[] { i });
        public static IEnumerable<object[]> TypeVectors() { for (int a = 0; a < 17; a++) for (int d = 0; d < 17; d++) yield return new object[] { a, d }; }
        [Theory, MemberData(nameof(DamageVectors))]
        public void DamageMatchesIndependentFractionOracleForAllSixteenRolls(int index)
        {
            var row = Oracle.RootElement.GetProperty("damageCases")[index]; var x = row.GetProperty("input");
            for (int r = 85; r <= 100; r++) Assert.Equal(row.GetProperty("rolls")[r - 85].GetInt32(), HgssRules.Damage(x[0].GetInt32(), x[1].GetInt32(), x[2].GetInt32(), x[3].GetInt32(), x[4].GetBoolean(), x[5].GetBoolean(), r, x[6].GetBoolean(), (Element)x[7].GetInt32(), (Element)x[8].GetInt32(), (Element)x[9].GetInt32()));
        }
        [Theory, MemberData(nameof(StatVectors))]
        public void StatsMatchPrimaryNatureTableAndIndependentRationalFixture(int index)
        {
            var row = Oracle.RootElement.GetProperty("statCases")[index]; var e = row.GetProperty("evs").EnumerateArray().Select(v => v.GetInt32()).ToArray();
            var p = new PokemonRecord(Fixture.Id(1), PilotCatalog.Bulbasaur, row.GetProperty("level").GetInt32(), SixStats.Uniform(row.GetProperty("iv").GetInt32()), new SixStats(e[0], e[1], e[2], e[3], e[4], e[5]), (Nature)row.GetProperty("nature").GetInt32(), Ability.Overgrow, new[] { PilotCatalog.Tackle });
            Assert.Equal(row.GetProperty("expected").EnumerateArray().Select(v => v.GetInt32()), HgssRules.CalculateStats(p).ToArray());
        }
        [Theory, MemberData(nameof(TypeVectors))]
        public void AllGenFourTypeMatchupsMatchPinnedRuntimeFacts(int a, int d) => Assert.Equal(Oracle.RootElement.GetProperty("chart")[a][d].GetInt32(), TypeChart.HalfUnits((Element)a, (Element)d));
        [Fact] public void PublishedStaraptorRoseradeExampleMatches114To134()
        {
            Assert.Equal(114, HgssRules.Damage(47, 60, 140, 77, false, false, 85, true, Element.Flying, Element.Grass, Element.Poison));
            Assert.Equal(134, HgssRules.Damage(47, 60, 140, 77, false, false, 100, true, Element.Flying, Element.Grass, Element.Poison));
        }
        [Fact] public void SmallCatalogStatsMatchPinnedHgssPersonalFacts()
        {
            foreach (var s in new[] { PilotCatalog.Bulbasaur, PilotCatalog.Charmander, PilotCatalog.Squirtle, PilotCatalog.Rattata, PilotCatalog.Magnemite }) Assert.Equal(Oracle.RootElement.GetProperty("species").GetProperty(s.Id).EnumerateArray().Select(x => x.GetInt32()), s.BaseStats.ToArray());
        }
        [Fact] public void AllPilotMovePowerCategoryAccuracyPpPriorityAndBurnFactsMatchHgssRecords()
        {
            var codes = new[] { 0, 10, 11, 13, 12, 15, 1, 3, 4, 2, 14, 6, 5, 7, 16, 17, 8 };
            foreach (var m in new[] { PilotCatalog.Tackle, PilotCatalog.QuickAttack, PilotCatalog.Ember, PilotCatalog.WaterGun, PilotCatalog.RazorLeaf, PilotCatalog.Slash, PilotCatalog.Growl, PilotCatalog.ThunderWave, PilotCatalog.WillOWisp, PilotCatalog.Swift })
            {
                var r = Oracle.RootElement.GetProperty("moves").GetProperty(m.Id); Assert.Equal(r.GetProperty("power").GetInt32(), m.Power); Assert.Equal(r.GetProperty("accuracy").GetInt32(), m.Accuracy); Assert.Equal(r.GetProperty("pp").GetInt32(), m.MaxPp); Assert.Equal(r.GetProperty("priority").GetInt32(), m.Priority); Assert.Equal(r.GetProperty("category").GetInt32(), (int)m.Category); Assert.Equal(r.GetProperty("typeCode").GetInt32(), codes[(int)m.Type]);
                if (m.Id == "ember") Assert.Equal(r.GetProperty("secondaryChance").GetInt32(), m.BurnChance);
            }
        }
    }

    public sealed class FormulaEdgeTests
    {
        [Fact] public void QuantizedAccuracyUsesHgssPercentagesAndClampsCombinedStages()
        {
            int[] percent = { 33, 36, 43, 50, 60, 75, 100, 133, 166, 200, 233, 266, 300 };
            for (int a = -6; a <= 6; a++) for (int e = -6; e <= 6; e++) for (int accuracy = 1; accuracy <= 100; accuracy++) Assert.Equal(Math.Min(100, accuracy * percent[Math.Clamp(a - e, -6, 6) + 6] / 100), HgssRules.AccuracyThreshold(accuracy, a, e));
            Assert.Equal(99, HgssRules.AccuracyThreshold(75, 1, 0)); Assert.Equal(100, HgssRules.AccuracyThreshold(0, -6, 6));
        }
        [Theory, InlineData(-6, 25), InlineData(-5, 28), InlineData(-4, 33), InlineData(-3, 40), InlineData(-2, 50), InlineData(-1, 66), InlineData(0, 100), InlineData(1, 150), InlineData(2, 200), InlineData(3, 250), InlineData(4, 300), InlineData(5, 350), InlineData(6, 400)]
        public void StatStagesFloor(int stage, int expected) => Assert.Equal(expected, HgssRules.ApplyStage(100, stage));
        [Fact] public void GenFourCritRatesAreNotModernRates() => Assert.Equal(new[] { 16, 8, 4, 3, 2 }, Enumerable.Range(0, 5).Select(HgssRules.CriticalDenominator));
        [Fact] public void BurnHappensBeforePlusTwoAndCritStillRespectsBurn()
        {
            Assert.Equal(10, HgssRules.Damage(50, 40, 100, 100, true, false, 100, false, Element.Normal, Element.Normal));
            Assert.Equal(19, HgssRules.Damage(50, 40, 100, 100, false, false, 100, false, Element.Normal, Element.Normal));
            Assert.Equal(20, HgssRules.Damage(50, 40, 100, 100, true, true, 100, false, Element.Normal, Element.Normal));
        }
        [Fact] public void DualTypeOrderFollowsChartEntriesNotDefenderSlotOrder()
        {
            int a = HgssRules.Damage(1, 1, 1, 999, false, false, 100, false, Element.Fire, Element.Grass, Element.Fire);
            int b = HgssRules.Damage(1, 1, 1, 999, false, false, 100, false, Element.Fire, Element.Fire, Element.Grass);
            Assert.Equal(2, a); Assert.Equal(a, b);
        }
        [Fact] public void DamageHasMinimumOneButImmunityStaysZero()
        {
            Assert.Equal(1, HgssRules.Damage(1, 1, 1, 1000000, true, false, 85, false, Element.Normal, Element.Rock, Element.Steel));
            Assert.Equal(0, HgssRules.Damage(100, 250, 1000000, 1, false, true, 100, true, Element.Normal, Element.Ghost));
        }
        [Fact] public void CriticalIgnoresNegativeAttackAndPositiveDefenseOnly()
        {
            var a = Fixture.Mon(stages: new[] { -6, 0, 0, 0, 0, 0, 0 }); var d = Fixture.Mon(2, stages: new[] { 0, 6, 0, 0, 0, 0, 0 }); var m = Fixture.Hit();
            Assert.Equal(HgssRules.Damage(Fixture.Mon(), Fixture.Mon(2), m, true, 100), HgssRules.Damage(a, d, m, true, 100));
            var up = Fixture.Mon(stages: new[] { 6, 0, 0, 0, 0, 0, 0 }); var down = Fixture.Mon(2, stages: new[] { 0, -6, 0, 0, 0, 0, 0 });
            Assert.True(HgssRules.Damage(up, down, m, true, 100) > HgssRules.Damage(a, d, m, true, 100));
        }
        [Fact] public void PhysicalAndSpecialUseDifferentStatsAndStages()
        {
            var a = Fixture.Mon(stages: new[] { -6, 0, 6, 0, 0, 0, 0 }); var d = Fixture.Mon(2);
            Assert.True(HgssRules.Damage(a, d, Fixture.Hit(category: Category.Special), false, 100) > HgssRules.Damage(a, d, Fixture.Hit(), false, 100));
        }
        [Fact] public void ParalysisQuartersSpeedAndCanReachZero()
        {
            var p = Fixture.Mon(status: MajorStatus.Paralysis); Assert.Equal(p.Stats[Stat.Speed] / 4, HgssRules.EffectiveSpeed(p));
            var low = new SpeciesDefinition("low", "fixture-1", SixStats.Uniform(1), Element.Normal);
            var q = Fixture.Mon(species: low, level: 1, status: MajorStatus.Paralysis, stages: new[] { 0, 0, 0, 0, -6, 0, 0 }); Assert.Equal(0, HgssRules.EffectiveSpeed(q));
        }
        [Fact] public void ShedinjaOneHpIgnoresIvsEvsLevelAndNature()
        {
            var s = new SpeciesDefinition("shedinja", "fixture-1", new SixStats(1, 90, 45, 30, 30, 40), Element.Bug, Element.Ghost, true);
            var p = new PokemonRecord(Fixture.Id(1), s, 100, SixStats.Uniform(31), new SixStats(255, 255, 0, 0, 0, 0), Nature.Lonely, Ability.NoneFixture, new[] { Fixture.Hit() }); Assert.Equal(1, HgssRules.CalculateStats(p)[Stat.Hp]);
        }
        [Fact] public void EvRemaindersAndGenFour255CapArePreserved()
        {
            PokemonRecord P(int ev) => new PokemonRecord(Fixture.Id(1), PilotCatalog.Bulbasaur, 100, SixStats.Uniform(31), new SixStats(ev, 0, 0, 0, 0, 0), Nature.Hardy, Ability.Overgrow, new[] { PilotCatalog.Tackle });
            Assert.Equal(HgssRules.CalculateStats(P(252))[Stat.Hp], HgssRules.CalculateStats(P(255))[Stat.Hp]); Assert.Equal(255, P(255).Evs[Stat.Hp]);
        }
        [Theory, InlineData(Ability.Overgrow, Element.Grass), InlineData(Ability.Blaze, Element.Fire), InlineData(Ability.Torrent, Element.Water)]
        public void StarterAbilitiesBoostPowerAtOneThirdThresholdOnly(Ability ability, Element type)
        {
            var a = Fixture.Mon(species: Fixture.Species(type), ability: ability); var d = Fixture.Mon(2); var move = Fixture.Hit(type: type);
            var low = new BattlePokemon(a.Record, a.Stats[Stat.Hp] / 3); var above = new BattlePokemon(a.Record, a.Stats[Stat.Hp] / 3 + 1);
            Assert.True(HgssRules.Damage(low, d, move, false, 100) > HgssRules.Damage(above, d, move, false, 100));
            Assert.Equal(HgssRules.Damage(a, d, move, false, 100), HgssRules.Damage(above, d, move, false, 100));
        }
        [Fact] public void DamageOverflowThrowsRatherThanWrapping() => Assert.Throws<OverflowException>(() => HgssRules.Damage(100, 375, 1000000, 1, false, true, 100, true, Element.Fire, Element.Grass, Element.Bug));
        [Fact] public void FormulaRejectsInvalidInputs()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => HgssRules.Damage(0, 40, 100, 100, false, false, 100, false, Element.Normal, Element.Normal)); Assert.Throws<ArgumentOutOfRangeException>(() => HgssRules.Damage(50, 40, 100, 0, false, false, 100, false, Element.Normal, Element.Normal)); Assert.Throws<ArgumentOutOfRangeException>(() => HgssRules.Damage(50, 40, 100, 100, false, false, 84, false, Element.Normal, Element.Normal)); Assert.Throws<ArgumentOutOfRangeException>(() => HgssRules.ApplyStage(100, 7)); Assert.Throws<ArgumentOutOfRangeException>(() => TypeChart.HalfUnits((Element)17, Element.Normal));
        }
    }
}
