using System;
using System.Linq;
using Roguelite.Gameplay;
using Roguelite.Gameplay.Phase3;
using Xunit;

namespace Roguelite.Phase3.Tests
{
    internal static class F
    {
        public static Guid Id(int n) => Guid.Parse("20000000-0000-0000-0000-" + n.ToString("D12"));
        public static readonly SpeciesDefinition Normal = new SpeciesDefinition("p3-normal", "fixture-1", new SixStats(120, 100, 100, 100, 100, 100), Element.Normal);
        public static readonly SpeciesDefinition Poison = new SpeciesDefinition("p3-poison", "fixture-1", new SixStats(120, 100, 100, 100, 100, 100), Element.Poison);
        public static readonly SpeciesDefinition Ice = new SpeciesDefinition("p3-ice", "fixture-1", new SixStats(120, 100, 100, 100, 100, 100), Element.Ice);
        public static readonly SpeciesDefinition Steel = new SpeciesDefinition("p3-steel", "fixture-1", new SixStats(120, 100, 100, 100, 100, 100), Element.Steel);
        public static readonly SpeciesDefinition Flying = new SpeciesDefinition("p3-flying", "fixture-1", new SixStats(120, 100, 100, 100, 100, 100), Element.Flying);
        public static readonly SpeciesDefinition Ground = new SpeciesDefinition("p3-ground", "fixture-1", new SixStats(120, 100, 100, 100, 100, 100), Element.Ground);
        public static readonly SpeciesDefinition Water = new SpeciesDefinition("p3-water", "fixture-1", new SixStats(120, 100, 100, 100, 100, 100), Element.Water);
        public static readonly SpeciesDefinition Fire = new SpeciesDefinition("p3-fire", "fixture-1", new SixStats(120, 100, 100, 100, 100, 100), Element.Fire);
        public static readonly SpeciesDefinition Grass = new SpeciesDefinition("p3-grass", "fixture-1", new SixStats(120, 100, 100, 100, 100, 100), Element.Grass);
        public static BattleMove Hit(int power = 35, int priority = 0, Element type = Element.Normal, int accuracy = 100, int criticalStage = 0, int effectChance = 0, BattleMoveEffect effect = BattleMoveEffect.Damage) => new BattleMove("hit-" + power + "-" + priority + "-" + type.ToString().ToLowerInvariant() + "-" + effect.ToString().ToLowerInvariant(), type, Category.Physical, power, accuracy, 20, effect, priority, effectChance, criticalStage);
        public static BattleMove Status(string id, BattleMoveEffect effect, int priority = 0, int accuracy = 100) => new BattleMove(id, Element.Normal, Category.Status, 0, accuracy, 10, effect, priority);
        public static AdvancedPokemon Mon(int n, SpeciesDefinition? species = null, BattleMove[]? moves = null, int? hp = null, Condition status = Condition.Healthy, int counter = 0, int toxic = 0, int[]? pp = null, int confusion = 0, int flinch = 0, bool switchLocked = false, int protect = 0, string? ability = null, bool male = true, bool genderless = false, int[]? stages = null, string? lastMoveId = null, string? disabledMoveId = null, int disableTurns = 0, string? encoreMoveId = null, int encoreTurns = 0, int tauntTurns = 0, bool imprisoning = false)
        {
            var record = new PokemonRecord(Id(n), species ?? Normal, 50, SixStats.Uniform(31), SixStats.Uniform(0), Nature.Hardy, Ability.NoneFixture, new[] { PilotCatalog.Tackle });
            return new AdvancedPokemon(record, moves ?? new[] { Hit() }, male, genderless, ability, hp, status, counter, toxic, confusion, flinch, -1, switchLocked, protect, pp, stages, lastMoveId, disabledMoveId, disableTurns, encoreMoveId, encoreTurns, tauntTurns, imprisoning);
        }
        public static AdvancedBattleState State(AdvancedPokemon? p = null, AdvancedPokemon? o = null, uint seed = 42, SideConditions? ps = null, SideConditions? os = null) => new AdvancedBattleState(new[] { p ?? Mon(1) }, new[] { o ?? Mon(2) }, seed, Id(99), playerConditions: ps, opponentConditions: os);
        public static BattleStep3 Step(AdvancedBattleState s, BattleAction3? p = null, BattleAction3? o = null) => AdvancedBattleEngine.ResolveTurn(s, p ?? BattleAction3.Move(0), o ?? BattleAction3.Move(0));
        public static BattleStep3 Find(uint from, Func<BattleStep3, bool> f)
        {
            for (uint seed = from; seed < from + 10000; seed++) { var r = Step(State(seed: seed)); if (f(r)) return r; }
            throw new Exception("No deterministic seed in fixture range.");
        }
    }

    public sealed class StatusAndVolatileTests
    {
        [Fact] public void SleepBlocksSelectedActionsAndWakesAfterRecordedDuration()
        {
            var s = F.State(F.Mon(1, status: Condition.Sleep, counter: 1)); var r = F.Step(s);
            Assert.Contains(r.Events, e => e.Detail == "sleep"); Assert.Equal(Condition.Healthy, r.State.Active(Side.Player).Condition); Assert.Equal(20, r.State.Active(Side.Player).Pp[0]); Assert.Equal(20, s.Active(Side.Player).Pp[0]);
        }
        [Fact] public void FreezeCanThawDeterministicallyOrPreventAction()
        {
            var frozen = F.State(F.Mon(1, status: Condition.Freeze)); BattleStep3? thaw = null;
            for (uint seed = 1; seed < 1000 && thaw == null; seed++) { var candidate = AdvancedBattleEngine.ResolveTurn(F.State(F.Mon(1, status: Condition.Freeze), seed: seed), BattleAction3.Move(0), BattleAction3.Move(0)); if (candidate.Events.Any(e => e.Detail == "thawed")) thaw = candidate; }
            Assert.NotNull(thaw); Assert.Equal(Condition.Healthy, thaw!.State.Active(Side.Player).Condition); Assert.Contains(F.Step(frozen).Events, e => e.Detail == "freeze-thaw");
        }
        [Fact] public void PoisonAndBadPoisonApplyDistinctResidualDamageAndToxicEscalates()
        {
            var p = F.Mon(1, status: Condition.Poison); int normalHp = p.Hp;
            var regular = F.Step(F.State(p)); Assert.Contains(regular.Events, e => e.Detail == "poison-residual" && e.Amount == Math.Max(1, HgssRules.CalculateStats(p.Record)[Stat.Hp] / 8)); Assert.True(regular.State.Active(Side.Player).Hp < normalHp);
            var toxic = F.Step(F.State(F.Mon(3, status: Condition.BadPoison, toxic: 1))); Assert.Equal(2, toxic.State.Active(Side.Player).ToxicCounter); Assert.Contains(toxic.Events, e => e.Detail == "badpoison-residual");
        }
        [Theory]
        [InlineData("freeze-immune", Condition.Healthy, 0, 1)]
        public void ExistingStateCanBeConstructedForImmunityFixtures(string _, Condition status, int counter, int seed)
        { Assert.Equal(status, F.Mon(seed, F.Normal, status: status, counter: counter).Condition); }
        [Fact] public void IceCannotReceiveFreezeAndPoisonOrSteelCannotReceivePoison()
        {
            Assert.Throws<ArgumentException>(() => F.Mon(1, F.Ice, status: Condition.Freeze));
            Assert.Throws<ArgumentException>(() => F.Mon(1, F.Poison, status: Condition.Poison));
            Assert.Throws<ArgumentException>(() => F.Mon(1, F.Steel, status: Condition.Poison));
        }
        [Fact] public void ConfusionSelfHitIsSeededAndVolatileStateLivesInSnapshot()
        {
            var baseState = F.State(F.Mon(1, confusion: 2)); BattleStep3? hit = null;
            for (uint seed = 1; seed < 1000 && hit == null; seed++) { var candidate = F.Step(F.State(F.Mon(1, confusion: 2), seed: seed)); if (candidate.Events.Any(e => e.Detail == "confusion-self-hit")) hit = candidate; }
            Assert.NotNull(hit);
            Assert.Equal(BattleCanonical.Hash(hit!.State), BattleCanonical.Hash(hit.State.DeepCopy())); Assert.True(hit.State.Active(Side.Player).Hp < baseState.Active(Side.Player).Hp);
        }
        [Fact] public void FasterFlinchStopsSecondMoveAndConsumesNoPp()
        {
            var flincher = F.Mon(1, moves: new[] { F.Status("fake-out", BattleMoveEffect.Flinch, 1) }); var slow = F.Mon(2);
            var r = F.Step(F.State(flincher, slow)); Assert.Contains(r.Events, e => e.Detail == "flinched"); Assert.Equal(20, r.State.Active(Side.Opponent).Pp[0]); Assert.DoesNotContain(r.Events, e => e.Actor == F.Id(2).ToString("N") && e.Kind == BattleEventKind.MoveUsed);
        }
        [Fact] public void TrappingPreventsVoluntarySwitchAndFaintReplacementIsExplicit()
        {
            var trapped = new AdvancedBattleState(new[] { F.Mon(1, switchLocked: true), F.Mon(3) }, new[] { F.Mon(2) }, 42, F.Id(108)); Assert.Throws<InvalidOperationException>(() => F.Step(trapped, BattleAction3.Switch(1)));
            var doomed = new AdvancedBattleState(new[] { F.Mon(1, hp: 1), F.Mon(3) }, new[] { F.Mon(2, moves: new[] { F.Hit(250, 1) }) }, 9, F.Id(102));
            var r = F.Step(doomed); Assert.True(r.State.NeedsReplacement(Side.Player));
        }
        [Fact] public void InfatuationAndConfusionUseIndependentVolatileCounters()
        {
            var a = F.Mon(1, moves: new[] { F.Status("attract", BattleMoveEffect.Infatuate, 1) }); var b = F.Mon(2);
            var r = F.Step(F.State(a, b)); Assert.Contains(r.Events, e => e.Detail == "infatuation-incompatible");
        }
        [Fact] public void OppositeGenderInfatuationIsAppliedAndGenderlessCannotReceiveIt()
        {
            var move = F.Status("attract", BattleMoveEffect.Infatuate, 1); var male = F.Mon(1, moves: new[] { move }); var female = F.Mon(2, male: false);
            var applied = F.Step(F.State(male, female)); Assert.Contains(applied.Events, e => e.Detail == "infatuation"); Assert.True(applied.State.Active(Side.Opponent).InfatuatedBy >= 0);
            var genderless = F.Mon(4, genderless: true); Assert.Contains(F.Step(F.State(male, genderless)).Events, e => e.Detail == "infatuation-incompatible");
        }
        [Fact] public void TrapMovePreventsTheTargetFromSwitchingUntilItsVolatileExpires()
        {
            var trap = F.Status("mean-look", BattleMoveEffect.TrapSwitch, 1); var state = new AdvancedBattleState(new[] { F.Mon(1, moves: new[] { trap }) }, new[] { F.Mon(2), F.Mon(3) }, 3, F.Id(121));
            var applied = AdvancedBattleEngine.ResolveTurn(state, BattleAction3.Move(0), BattleAction3.Move(0)); Assert.True(applied.State.Active(Side.Opponent).SwitchLocked);
            Assert.Throws<InvalidOperationException>(() => F.Step(applied.State, BattleAction3.Move(0), BattleAction3.Switch(1)));
        }
        [Fact] public void SleepMoveRecordsDurationAndAbilityImmunityIsExplicit()
        {
            var sleep = F.Status("sleep-powder", BattleMoveEffect.InflictSleep, 1); var r = F.Step(F.State(F.Mon(1, moves: new[] { sleep }), F.Mon(2)));
            Assert.Contains(r.Events, e => e.Kind == BattleEventKind.Status && e.Detail == Condition.Sleep.ToString() && e.Amount >= 1 && e.Amount <= 3); Assert.DoesNotContain(r.Events, e => e.Kind == BattleEventKind.MoveUsed && e.Actor == F.Id(2).ToString("N"));
            var immune = F.Step(F.State(F.Mon(1, moves: new[] { sleep }), F.Mon(2, ability: "insomnia")));
            Assert.Contains(immune.Events, e => e.Detail == "ability-immunity"); Assert.Equal(Condition.Healthy, immune.State.Active(Side.Opponent).Condition);
        }
        [Fact] public void FireMoveThawsFrozenTargetAfterDamage()
        {
            var fire = F.Hit(60, type: Element.Fire); var r = F.Step(F.State(F.Mon(1, moves: new[] { fire }), F.Mon(2, status: Condition.Freeze)));
            Assert.Contains(r.Events, e => e.Detail == "thawed-by-fire-move"); Assert.Equal(Condition.Healthy, r.State.Active(Side.Opponent).Condition);
        }
        [Fact] public void FireCannotBeBurnedAndSteelCannotBePoisonedByMove()
        {
            var burn = F.Status("will-o-wisp", BattleMoveEffect.InflictBurn, 1); var fireImmune = F.Step(F.State(F.Mon(1, moves: new[] { burn }), F.Mon(2, F.Fire)));
            Assert.Contains(fireImmune.Events, e => e.Detail == "type-immunity");
            var poison = F.Status("toxic", BattleMoveEffect.InflictPoison, 1); var steelImmune = F.Step(F.State(F.Mon(1, moves: new[] { poison }), F.Mon(2, F.Steel)));
            Assert.Contains(steelImmune.Events, e => e.Detail == "type-immunity");
        }
        [Fact] public void IdenticalSeedAndCommandsReplayExactStateAndOrderedEvents()
        {
            for (uint seed = 1; seed < 100; seed++) { var s = F.State(F.Mon(1, confusion: 3), F.Mon(2), seed); var x = F.Step(s); var y = F.Step(s.DeepCopy()); Assert.Equal(BattleCanonical.Hash(x.State), BattleCanonical.Hash(y.State)); Assert.Equal(BattleCanonical.Hash(x.State, x.Events), BattleCanonical.Hash(y.State, y.Events)); }
        }
    }

    public sealed class ProtectPpAndEnvironmentTests
    {
        [Fact] public void ProtectBlocksIncomingDamageAndNextUseChanceIsOneHalf()
        {
            var p = F.Mon(1, moves: new[] { F.Status("protect", BattleMoveEffect.Protect, 3) }); var o = F.Mon(2);
            BattleStep3? r = null; for (uint seed = 1; seed < 1000 && r == null; seed++) { var candidate = F.Step(F.State(p, o, seed)); if (candidate.Events.Any(e => e.Kind == BattleEventKind.Protected && e.Actor == F.Id(1).ToString("N") && e.Detail == "protect")) r = candidate; }
            Assert.NotNull(r); Assert.Contains(r!.Events, e => e.Kind == BattleEventKind.Protected && e.Target == F.Id(1).ToString("N")); Assert.Equal(p.Hp, r.State.Active(Side.Player).Hp);
            var firstUse = F.Step(F.State(p, o, seed: 17)); Assert.Contains(firstUse.Events, e => e.Kind == BattleEventKind.Protected && e.Detail == "protect"); Assert.DoesNotContain(firstUse.Events, e => e.Detail == "protect-success-chain");
            var chainUser = F.Mon(1, moves: new[] { F.Status("protect", BattleMoveEffect.Protect, 3) }, protect: 1);
            var hgss = AdvancedBattleEngine.ResolveTurn(new AdvancedBattleState(new[] { chainUser }, new[] { o }, 17, F.Id(106)), BattleAction3.Move(0), BattleAction3.Move(0));
            var modern = AdvancedBattleEngine.ResolveTurn(new AdvancedBattleState(new[] { chainUser }, new[] { o }, 17, F.Id(107), ruleset: CompatibilityProfile.CrossGenerationCatalog), BattleAction3.Move(0), BattleAction3.Move(0));
            Assert.Contains(hgss.Events, e => e.Detail == "protect-success-chain" && e.Amount == 2); Assert.Contains(modern.Events, e => e.Detail == "protect-success-chain" && e.Amount == 3);
        }
        [Fact] public void EmptyPpAutoSelectsStruggleAndAppliesQuarterHpRecoil()
        {
            var p = F.Mon(1, pp: new[] { 0 }); var s = F.State(p, F.Mon(2)); var r = F.Step(s);
            Assert.Contains(r.Events, e => e.Detail == "struggle"); Assert.Contains(r.Events, e => e.Detail == "struggle-recoil"); Assert.Equal(0, r.State.Active(Side.Player).Pp[0]);
        }
        [Fact] public void PpRestorationSaturatesAtCatalogMaximum()
        {
            var p = F.Mon(1, pp: new[] { 1 }); Assert.Equal(20, AdvancedPokemon.RestorePp(p.Pp[0], 20, int.MaxValue)); Assert.Throws<ArgumentOutOfRangeException>(() => AdvancedPokemon.RestorePp(-1, 20, 1));
        }
        [Fact] public void EntryHazardsUseGenFourLayerFractionsAndPoisonTypeAbsorbsToxicSpikes()
        {
            var condition = new SideConditions(spikesLayers: 3, toxicSpikesLayers: 2);
            var s = new AdvancedBattleState(new[] { F.Mon(1), F.Mon(3, F.Poison) }, new[] { F.Mon(2) }, 50, F.Id(100), playerConditions: condition);
            var sw = AdvancedBattleEngine.ResolveTurn(s, BattleAction3.Switch(1), BattleAction3.Move(0));
            Assert.Contains(sw.Events, e => e.Detail == "spikes"); Assert.Contains(sw.Events, e => e.Detail == "toxic-spikes-absorbed"); Assert.Equal(Condition.Healthy, sw.State.Active(Side.Player).Condition);
        }
        [Fact] public void StealthRockUsesTypeEffectivenessAndHazardRemainsSideScoped()
        {
            var side = new SideConditions(stealthRock: true); var ps = new AdvancedBattleState(new[] { F.Mon(1), F.Mon(3) }, new[] { F.Mon(2) }, 42, F.Id(105), playerConditions: side); var r = F.Step(ps, BattleAction3.Switch(1), BattleAction3.Move(0));
            Assert.Contains(r.Events, e => e.Detail == "stealth-rock"); Assert.True(r.State.Active(Side.Player).Hp < F.Mon(1).Hp);
        }
        [Fact] public void ScreensExpireAndReduceMatchingDamage()
        {
            var c = new SideConditions(reflectTurns: 2); var p = F.Mon(1, moves: new[] { F.Hit() }); var o = F.Mon(2, moves: new[] { F.Hit() });
            var screened = F.Step(F.State(p, o, ps: c)); var plain = F.Step(F.State(F.Mon(1, moves: new[] { F.Hit() }), F.Mon(2, moves: new[] { F.Hit() }), ps: new SideConditions()));
            var screenDamage = screened.Events.Where(e => e.Actor == F.Id(2).ToString("N") && e.Kind == BattleEventKind.Damage).Sum(e => e.Amount);
            var plainDamage = plain.Events.Where(e => e.Actor == F.Id(2).ToString("N") && e.Kind == BattleEventKind.Damage).Sum(e => e.Amount);
            Assert.True(screenDamage <= plainDamage); Assert.Equal(1, screened.State.Conditions(Side.Player).ReflectTurns);
        }
        [Fact] public void LightScreenReducesSpecialDamageAndSetterDoesNotMutateInput()
        {
            var screen = F.Status("light-screen", BattleMoveEffect.SetLightScreen, 1); var setup = F.Step(F.State(F.Mon(1, moves: new[] { screen }), F.Mon(2)));
            Assert.Equal(4, setup.State.Conditions(Side.Player).LightScreenTurns); Assert.Equal(0, F.State().Conditions(Side.Player).LightScreenTurns);
        }
        [Fact] public void RainMoveChangesWeatherAndWeatherPowerModifierIsDeterministic()
        {
            var rain = F.Status("rain-dance", BattleMoveEffect.SetRain, 1); var setup = F.Step(F.State(F.Mon(1, moves: new[] { rain }), F.Mon(2)));
            Assert.Equal(Weather.Rain, setup.State.Weather); Assert.Equal(4, setup.State.WeatherTurns); Assert.Contains(setup.Events, e => e.Kind == BattleEventKind.Weather && e.Detail == "Rain");
        }
        [Fact] public void WeatherDurationAndSandResidualAreSnapshotDeterministic()
        {
            var p = F.Mon(1); var o = F.Mon(2); var state = new AdvancedBattleState(new[] { p }, new[] { o }, 987, F.Id(101), weather: Weather.Sandstorm, weatherTurns: 2);
            var r = F.Step(state); Assert.Contains(r.Events, e => e.Detail == "sandstorm-residual"); Assert.Equal(1, r.State.WeatherTurns); Assert.Equal(Weather.Sandstorm, r.State.Weather);
        }
        [Fact] public void HailHurtsNonIceAndDoesNotDamageIceTypes()
        {
            var ordinary = new AdvancedBattleState(new[] { F.Mon(1) }, new[] { F.Mon(2) }, 987, F.Id(122), weather: Weather.Hail, weatherTurns: 2);
            var ice = new AdvancedBattleState(new[] { F.Mon(1, F.Ice) }, new[] { F.Mon(2, F.Ice) }, 987, F.Id(123), weather: Weather.Hail, weatherTurns: 2);
            Assert.Contains(F.Step(ordinary).Events, e => e.Detail == "hail-residual"); Assert.DoesNotContain(F.Step(ice).Events, e => e.Detail == "hail-residual");
        }
        [Fact] public void UnsupportedLaterGenerationEffectFailsExplicitly()
        {
            Assert.Throws<NotSupportedException>(() => EffectRegistry.Foundational.Require("gen9-signature-effect", CompatibilityProfile.CrossGenerationCatalog));
            Assert.Equal(CompatibilityDisposition.ExplicitlyPorted, EffectRegistry.Foundational.Require("protect", CompatibilityProfile.CrossGenerationCatalog).Disposition);
        }
        [Fact] public void ModernProfileUsesItsRegisteredProtectRuleAndUnknownEffectsRemainRejected()
        {
            Assert.Equal(CompatibilityDisposition.Native, EffectRegistry.Foundational.Require("protect", CompatibilityProfile.Hgss).Disposition);
            Assert.Equal(CompatibilityDisposition.ExplicitlyPorted, EffectRegistry.Foundational.Require("protect", CompatibilityProfile.CrossGenerationCatalog).Disposition);
            Assert.NotEqual(EffectRegistry.Foundational.ContentHash, new EffectRegistry("different", Array.Empty<EffectRegistration>()).ContentHash);
        }
        [Fact] public void ProtectChainCapsAtGenFourOneEighthAndModernOneOver729()
        {
            var protect = F.Status("protect", BattleMoveEffect.Protect, 3);
            var hgssUser = F.Mon(1, moves: new[] { protect }, protect: 3); var modernUser = F.Mon(1, moves: new[] { protect }, protect: 6);
            BattleStep3? hgss = null; BattleStep3? modern = null;
            for (uint seed = 1; seed < 100000 && (hgss == null || modern == null); seed++)
            {
                if (hgss == null) { var x = AdvancedBattleEngine.ResolveTurn(new AdvancedBattleState(new[] { hgssUser }, new[] { F.Mon(2) }, seed, F.Id(130)), BattleAction3.Move(0), BattleAction3.Move(0)); if (x.Events.Count(e => e.Detail == "protect-success-chain") == 3 && x.Events.Any(e => e.Kind == BattleEventKind.Protected && e.Actor == F.Id(1).ToString("N") && e.Detail == "protect")) hgss = x; }
                if (modern == null) { var x = AdvancedBattleEngine.ResolveTurn(new AdvancedBattleState(new[] { modernUser }, new[] { F.Mon(2) }, seed, F.Id(131), ruleset: CompatibilityProfile.CrossGenerationCatalog), BattleAction3.Move(0), BattleAction3.Move(0)); if (x.Events.Count(e => e.Detail == "protect-success-chain") == 6 && x.Events.Any(e => e.Kind == BattleEventKind.Protected && e.Actor == F.Id(1).ToString("N") && e.Detail == "protect")) modern = x; }
            }
            Assert.NotNull(hgss); Assert.NotNull(modern); Assert.Equal(3, hgss!.State.Active(Side.Player).ProtectChain); Assert.Equal(6, modern!.State.Active(Side.Player).ProtectChain);
        }
        [Fact] public void SwitchingToFaintedOrActiveSlotsIsRejectedAndSourceStateIsUnchanged()
        {
            var state = new AdvancedBattleState(new[] { F.Mon(1), F.Mon(3, hp: 0) }, new[] { F.Mon(2) }, 70, F.Id(120)); string before = BattleCanonical.Hash(state);
            Assert.Throws<InvalidOperationException>(() => F.Step(state, BattleAction3.Switch(0)));
            Assert.Throws<InvalidOperationException>(() => F.Step(state, BattleAction3.Switch(1)));
            Assert.Equal(before, BattleCanonical.Hash(state));
        }
    }

    public sealed class AbilityAndInteractionTests
    {
        [Fact] public void UnknownAbilityCannotBeSilentlyIgnored()
        {
            Assert.Throws<NotSupportedException>(() => F.Mon(1, ability: "gen9-unknown-ability"));
            Assert.Throws<NotSupportedException>(() => F.Mon(1, ability: "sturdy"));
        }
        [Fact] public void LevitateBlocksGroundMovesAndSpikesDamage()
        {
            var attacker = F.Mon(1, moves: new[] { F.Hit(80, type: Element.Ground) }); var target = F.Mon(2, ability: "levitate");
            var hit = F.Step(F.State(attacker, target)); Assert.Contains(hit.Events, e => e.Detail == "levitate-immunity");
            var side = new SideConditions(spikesLayers: 2); var state = new AdvancedBattleState(new[] { F.Mon(1), F.Mon(3, ability: "levitate") }, new[] { F.Mon(2) }, 50, F.Id(110), playerConditions: side);
            var entered = AdvancedBattleEngine.ResolveTurn(state, BattleAction3.Switch(1), BattleAction3.Move(0)); Assert.DoesNotContain(entered.Events, e => e.Detail == "spikes");
        }
        [Fact] public void MagicGuardSuppressesIndirectResidualAndEntryHazardDamage()
        {
            var side = new SideConditions(stealthRock: true, spikesLayers: 3); var magic = F.Mon(3, ability: "magic-guard", status: Condition.Poison);
            var state = new AdvancedBattleState(new[] { F.Mon(1), magic }, new[] { F.Mon(2) }, 50, F.Id(111), playerConditions: side);
            var r = AdvancedBattleEngine.ResolveTurn(state, BattleAction3.Switch(1), BattleAction3.Move(0));
            Assert.DoesNotContain(r.Events, e => e.Detail == "stealth-rock" || e.Detail == "spikes" || e.Detail == "poison-residual");
        }
        [Fact] public void AirLockSuppressesWeatherDamageAndWeatherSpeedAbilities()
        {
            var weather = new AdvancedBattleState(new[] { F.Mon(1, ability: "swift-swim") }, new[] { F.Mon(2, ability: "air-lock") }, 42, F.Id(112), weather: Weather.Sandstorm, weatherTurns: 4);
            var r = F.Step(weather); Assert.DoesNotContain(r.Events, e => e.Detail == "sandstorm-residual"); Assert.Equal(Weather.Sandstorm, r.State.Weather);
        }
        [Fact] public void ArenaTrapAndMagnetPullOnlyBlockTheirMatchingGroundedTargets()
        {
            var trapped = new AdvancedBattleState(new[] { F.Mon(1), F.Mon(3) }, new[] { F.Mon(2, ability: "arena-trap") }, 7, F.Id(113));
            Assert.Throws<InvalidOperationException>(() => F.Step(trapped, BattleAction3.Switch(1)));
            var flying = new AdvancedBattleState(new[] { F.Mon(1, F.Flying), F.Mon(3) }, new[] { F.Mon(2, ability: "arena-trap") }, 7, F.Id(114));
            Assert.NotNull(F.Step(flying, BattleAction3.Switch(1)));
            var steel = new AdvancedBattleState(new[] { F.Mon(1, F.Steel), F.Mon(3) }, new[] { F.Mon(2, ability: "magnet-pull") }, 7, F.Id(115));
            Assert.Throws<InvalidOperationException>(() => F.Step(steel, BattleAction3.Switch(1)));
        }
        [Fact] public void ShadowTagMirrorDoesNotCreateAFalseSwitchLock()
        {
            var mirror = new AdvancedBattleState(new[] { F.Mon(1, ability: "shadow-tag"), F.Mon(3) }, new[] { F.Mon(2, ability: "shadow-tag") }, 77, F.Id(124));
            Assert.NotNull(F.Step(mirror, BattleAction3.Switch(1)));
        }
        [Fact] public void OvergrowUsesOneThirdThresholdAndGenFourCriticalStage()
        {
            var grassHit = F.Hit(70, type: Element.Grass); var boosted = F.Mon(1, F.Grass, new[] { grassHit }, hp: 50, ability: "overgrow"); var plain = F.Mon(1, F.Grass, new[] { grassHit }, hp: 50);
            var a = F.Step(F.State(boosted, F.Mon(2), 10)); var b = F.Step(F.State(plain, F.Mon(2), 10));
            var da = a.Events.Single(e => e.Actor == F.Id(1).ToString("N") && e.Kind == BattleEventKind.Damage).Amount; var db = b.Events.Single(e => e.Actor == F.Id(1).ToString("N") && e.Kind == BattleEventKind.Damage).Amount; Assert.True(da >= db);
            var highCrit = F.Mon(3, moves: new[] { F.Hit(criticalStage: 4) }); BattleStep3? result = null;
            for (uint seed = 1; seed < 100 && result == null; seed++) { var candidate = F.Step(F.State(highCrit, F.Mon(4), seed)); if (candidate.Events.Any(e => e.Detail == "critical-hit")) result = candidate; }
            Assert.NotNull(result); Assert.Contains(result!.Events, e => e.Detail == "critical-hit" && e.Amount == 2);
        }
        [Fact] public void VolatileSwitchCleanupResetsStagesAndToxicRampButKeepsMajorCondition()
        {
            var p = F.Mon(1, status: Condition.BadPoison, toxic: 6, confusion: 2, stages: new[] { 2, 0, 0, 0, 0, 0, 0 });
            var state = new AdvancedBattleState(new[] { p, F.Mon(3) }, new[] { F.Mon(2) }, 25, F.Id(116)); var r = F.Step(state, BattleAction3.Switch(1), BattleAction3.Move(0));
            var outgoing = r.State.Team(Side.Player)[0]; Assert.Equal(Condition.BadPoison, outgoing.Condition); Assert.Equal(0, outgoing.ToxicCounter); Assert.Equal(0, outgoing.ConfusionTurns); Assert.False(outgoing.SwitchLocked); Assert.Equal(0, outgoing.Stages[(int)Stage.Attack]);
        }
        [Fact] public void GenFourDamageAgainstGhostNormalIsImmuneAndDoesNotConsumeCritOrDamageRolls()
        {
            var ghost = new SpeciesDefinition("p3-ghost", "fixture-1", new SixStats(120, 100, 100, 100, 100, 100), Element.Ghost);
            var s = F.State(F.Mon(1), F.Mon(2, ghost)); var r = F.Step(s);
            string attacker = F.Id(1).ToString("N"); Assert.Contains(r.Events, e => e.Detail == "type-immunity" && e.Actor == attacker); Assert.DoesNotContain(r.Events, e => (e.Detail == "critical" || e.Detail == "critical-hit" || e.Detail == "damage-range") && e.Actor == attacker);
        }
        [Fact] public void StruggleIgnoresGhostImmunityAndHasNoStab()
        {
            var ghost = new SpeciesDefinition("p3-ghost2", "fixture-1", new SixStats(120, 100, 100, 100, 100, 100), Element.Ghost);
            var s = F.State(F.Mon(1, pp: new[] { 0 }), F.Mon(2, ghost)); var r = F.Step(s);
            Assert.Contains(r.Events, e => e.Detail == "struggle"); Assert.Contains(r.Events, e => e.Detail == "struggle-recoil"); Assert.DoesNotContain(r.Events, e => e.Detail == "type-immunity");
        }
        [Fact] public void MoveSelectionRejectsOneEmptySlotWhenAnotherHasPpWithoutChangingInput()
        {
            var a = F.Mon(1, moves: new[] { F.Hit(35), F.Hit(55) }, pp: new[] { 0, 3 }); var state = F.State(a); string before = BattleCanonical.Hash(state);
            Assert.Throws<InvalidOperationException>(() => F.Step(state, BattleAction3.Move(0), BattleAction3.Move(0))); Assert.Equal(before, BattleCanonical.Hash(state));
            Assert.Equal(0, state.Active(Side.Player).Pp[0]); Assert.Equal(3, state.Active(Side.Player).Pp[1]);
        }
        [Fact] public void SnapshotsDoNotExposeMutablePpOrStageBuffers()
        {
            var p = F.Mon(1); var state = F.State(p); Assert.IsNotType<int[]>(state.Active(Side.Player).Pp); Assert.IsNotType<int[]>(state.Active(Side.Player).Stages);
            Assert.Equal(BattleCanonical.Hash(state), BattleCanonical.Hash(state.DeepCopy()));
        }
    }
}
