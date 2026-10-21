using System;
using System.Globalization;
using System.Linq;
using Roguelite.Gameplay;
using Xunit;

namespace Roguelite.Tests
{
    public sealed class BattleTests
    {
        private static string Id(int x) => Fixture.Id(x).ToString("N");
        private static BattlePokemon StatusMon(int id, MoveDefinition move, Element type = Element.Normal, int speed = 120) => Fixture.Mon(id, Fixture.Species(type, speed: speed), new[] { move });
        [Fact] public void SameSeedAndCommandsProduceSameStateAndOrderedEvents()
        {
            var initial = Fixture.State(); var a = Fixture.Step(initial); var b = Fixture.Step(initial);
            Assert.Equal(a.StateHash, b.StateHash); Assert.Equal(Canonical.Events(a.Events), Canonical.Events(b.Events)); Assert.Equal(a.State.RandomDraws, b.State.RandomDraws);
        }
        [Fact] public void ManySeedsAreDeterministicAcrossSeveralTurns()
        {
            for (uint seed = 1; seed <= 100; seed++)
            {
                var a = Fixture.State(seed: seed); var b = a.DeepCopy();
                for (int turn = 0; turn < 10 && a.Outcome == BattleOutcome.Ongoing; turn++) { var x = Fixture.Step(a); var y = Fixture.Step(b); Assert.Equal(Canonical.Events(x.Events), Canonical.Events(y.Events)); Assert.Equal(x.StateHash, y.StateHash); a = x.State; b = y.State; }
            }
        }
        [Fact] public void SpeedDeterminesSamePriorityOrder() => Assert.Equal(Id(1), Fixture.Step(Fixture.State(Fixture.Mon(species: Fixture.Species(speed: 120)), Fixture.Mon(2))).Events.Single(e => e.Kind == EventKind.Order).Actor);
        [Fact] public void PriorityOverridesSpeed() => Assert.Equal(Id(2), Fixture.Step(Fixture.State(Fixture.Mon(species: Fixture.Species(speed: 120)), Fixture.Mon(2, moves: new[] { PilotCatalog.QuickAttack }))).Events.Single(e => e.Kind == EventKind.Order).Actor);
        [Fact] public void EqualSpeedTieUsesRandomChoiceAndBothSidesAreReachable()
        {
            var actors = Enumerable.Range(1, 100).Select(i => Fixture.Step(Fixture.State(seed: (uint)i)).Events.Single(e => e.Kind == EventKind.Order).Actor).Distinct().ToArray(); Assert.Equal(2, actors.Length);
            Assert.Contains(Fixture.Step().Events, e => e.Kind == EventKind.RandomRoll && e.Detail == "order-speed-tie" && e.Amount == 2);
        }
        [Fact] public void AccuracyMissConsumesPpButNoDamageOrCritDrawForThatActor()
        {
            var s = Fixture.State(Fixture.Mon(moves: new[] { Fixture.Hit(accuracy: 1) })); var r = Fixture.Find(s, x => x.Events.Any(e => e.Kind == EventKind.Missed && e.Actor == Id(1)));
            Assert.Equal(63, r.State.Active(Side.Player).Pp[0]); Assert.DoesNotContain(r.Events, e => e.Kind == EventKind.Damage && e.Actor == Id(1)); Assert.DoesNotContain(r.Events, e => e.Detail == "critical" && e.Actor == Id(1));
        }
        [Fact] public void AccuracyZeroBypassesEvenWorstStages()
        {
            var r = Fixture.Step(Fixture.State(Fixture.Mon(moves: new[] { PilotCatalog.Swift }, stages: new[] { 0, 0, 0, 0, 0, -6, 0 }), Fixture.Mon(2, stages: new[] { 0, 0, 0, 0, 0, 0, 6 })));
            Assert.DoesNotContain(r.Events, e => e.Detail == "accuracy" && e.Actor == Id(1)); Assert.Contains(r.Events, e => e.Kind == EventKind.Damage && e.Actor == Id(1));
        }
        [Fact] public void CriticalAndRandomDamageAreExplicitInEvents()
        {
            var r = Fixture.Find(Fixture.State(), x => x.Events.Any(e => e.Kind == EventKind.Critical)); Assert.All(r.Events.Where(e => e.Kind == EventKind.Damage), e => Assert.InRange(e.Roll, 85, 100)); Assert.All(r.Events.Where(e => e.Kind == EventKind.Critical), e => Assert.Equal(2, e.Amount));
        }
        [Fact] public void DamageEventMatchesFormulaAndClampsToRemainingHp()
        {
            var s = Fixture.State(Fixture.Mon(moves: new[] { Fixture.Hit(power: 250, priority: 1) }), Fixture.Mon(2, hp: 1)); var r = Fixture.Step(s);
            Assert.Single(r.Events, e => e.Kind == EventKind.Fainted); Assert.Equal(1, r.Events.Single(e => e.Kind == EventKind.Damage).Amount); Assert.Equal(0, r.State.Active(Side.Opponent).Hp); Assert.Equal(BattleOutcome.PlayerWin, r.State.Outcome);
        }
        [Fact] public void FaintedSecondActorDoesNotSpendPpOrUseMove()
        {
            var s = Fixture.State(Fixture.Mon(moves: new[] { Fixture.Hit(power: 250, priority: 1) }), Fixture.Mon(2, hp: 1)); var r = Fixture.Step(s);
            Assert.DoesNotContain(r.Events, e => e.Kind == EventKind.MoveUsed && e.Actor == Id(2)); Assert.Equal(64, r.State.Active(Side.Opponent).Pp[0]);
        }
        [Fact] public void TypeImmunityProducesNoDamageOrCriticalDraw()
        {
            var r = Fixture.Step(Fixture.State(opponent: Fixture.Mon(2, species: Fixture.Species(Element.Ghost)))); Assert.Contains(r.Events, e => e.Kind == EventKind.Immune && e.Actor == Id(1)); Assert.DoesNotContain(r.Events, e => e.Kind == EventKind.Damage && e.Actor == Id(1));
        }
        [Fact] public void GrowlWorksAgainstGhostAndAttackStageCannotGoBelowMinusSix()
        {
            var s = Fixture.State(StatusMon(1, PilotCatalog.Growl), Fixture.Mon(2, Fixture.Species(Element.Ghost), stages: new[] { -6, 0, 0, 0, 0, 0, 0 })); var r = Fixture.Step(s);
            Assert.Contains(r.Events, e => e.Kind == EventKind.StageChanged && e.Amount == 0); Assert.Equal(-6, r.State.Active(Side.Opponent).StageValue(Stage.Attack)); Assert.DoesNotContain(r.Events, e => e.Kind == EventKind.Immune && e.Actor == Id(1));
        }
        [Fact] public void GrowlLowersAttackOneStage() => Assert.Equal(-1, Fixture.Step(Fixture.State(StatusMon(1, PilotCatalog.Growl))).State.Active(Side.Opponent).StageValue(Stage.Attack));
        [Fact] public void ThunderWaveInflictsParalysisIncludingElectricTypesInGenFour()
        {
            var r = Fixture.Step(Fixture.State(StatusMon(1, PilotCatalog.ThunderWave), Fixture.Mon(2, Fixture.Species(Element.Electric)))); Assert.Equal(MajorStatus.Paralysis, r.State.Active(Side.Opponent).Status); Assert.Contains(r.Events, e => e.Kind == EventKind.StatusChanged && e.Detail == "Paralysis");
        }
        [Fact] public void ThunderWaveDoesNotAffectGround() => Assert.Contains(Fixture.Step(Fixture.State(StatusMon(1, PilotCatalog.ThunderWave), Fixture.Mon(2, Fixture.Species(Element.Ground)))).Events, e => e.Kind == EventKind.Immune);
        [Fact] public void FullParalysisStopsMoveWithoutSpendingPp()
        {
            var r = Fixture.Find(Fixture.State(Fixture.Mon(status: MajorStatus.Paralysis)), x => x.Events.Any(e => e.Kind == EventKind.UnableToAct && e.Actor == Id(1) && e.Detail == "full-paralysis")); Assert.Equal(64, r.State.Active(Side.Player).Pp[0]);
        }
        [Fact] public void NewParalysisDoesNotReorderAlreadyChosenTurn()
        {
            var r = Fixture.Step(Fixture.State(StatusMon(1, PilotCatalog.ThunderWave), Fixture.Mon(2, species: Fixture.Species(speed: 80)))); Assert.Equal(Id(1), r.Events.Single(e => e.Kind == EventKind.Order).Actor); Assert.Equal(MajorStatus.Paralysis, r.State.Active(Side.Opponent).Status);
        }
        [Fact] public void WillOWispInflictsBurnAndDealsOneEighthResidual()
        {
            var r = Fixture.Find(Fixture.State(StatusMon(1, PilotCatalog.WillOWisp)), x => x.Events.Any(e => e.Kind == EventKind.StatusChanged)); Assert.Equal(MajorStatus.Burn, r.State.Active(Side.Opponent).Status); Assert.Equal(r.State.Active(Side.Opponent).Stats[Stat.Hp] / 8, r.Events.Single(e => e.Detail == "burn-residual").Amount);
        }
        [Fact] public void FireTypesCannotBeBurned()
        {
            var r = Fixture.Find(Fixture.State(StatusMon(1, PilotCatalog.WillOWisp), Fixture.Mon(2, Fixture.Species(Element.Fire))), x => x.Events.Any(e => e.Kind == EventKind.StatusBlocked)); Assert.Equal(MajorStatus.Healthy, r.State.Active(Side.Opponent).Status);
        }
        [Fact] public void ExistingStatusCannotBeOverwritten()
        {
            var r = Fixture.Find(Fixture.State(StatusMon(1, PilotCatalog.WillOWisp), Fixture.Mon(2, status: MajorStatus.Paralysis)), x => x.Events.Any(e => e.Kind == EventKind.StatusBlocked)); Assert.Equal(MajorStatus.Paralysis, r.State.Active(Side.Opponent).Status);
        }
        [Fact] public void EmberSecondaryCanBurnAndOtherSeedsDoNotBurn()
        {
            var s = Fixture.State(Fixture.Mon(moves: new[] { PilotCatalog.Ember }));
            Assert.Contains(Fixture.Find(s, x => x.Events.Any(e => e.Kind == EventKind.StatusChanged)).Events, e => e.Detail == "secondary-burn");
            Assert.DoesNotContain(Fixture.Find(s, x => x.Events.Any(e => e.Detail == "secondary-burn" && e.Roll >= 10)).Events, e => e.Kind == EventKind.StatusChanged);
        }
        [Fact] public void NoSecondaryStatusOnKnockedOutTarget()
        {
            var r = Fixture.Step(Fixture.State(Fixture.Mon(moves: new[] { PilotCatalog.Ember }, species: Fixture.Species(speed: 120)), Fixture.Mon(2, hp: 1))); Assert.DoesNotContain(r.Events, e => e.Detail == "secondary-burn");
        }
        [Fact] public void ResidualBurnFaintingProducesOutcome()
        {
            var s = Fixture.State(StatusMon(1, PilotCatalog.Growl), Fixture.Mon(2, moves: new[] { PilotCatalog.Growl }, hp: 1, status: MajorStatus.Burn)); var r = Fixture.Step(s); Assert.Equal(BattleOutcome.PlayerWin, r.State.Outcome); Assert.Contains(r.Events, e => e.Kind == EventKind.Fainted && e.Detail == "burn-residual");
        }
        [Fact] public void TerminalKnockoutEndsBeforeResidualDamage()
        {
            var s = Fixture.State(Fixture.Mon(status: MajorStatus.Burn, moves: new[] { Fixture.Hit(priority: 1) }), Fixture.Mon(2, hp: 1)); var r = Fixture.Step(s); Assert.DoesNotContain(r.Events, e => e.Detail == "burn-residual"); Assert.Contains(r.Events, e => e.Kind == EventKind.BattleEnded);
        }
        [Fact] public void SwitchHappensBeforeMovesAndTargetsIncomingPokemon()
        {
            var s = new BattleState(new[] { Fixture.Mon(), Fixture.Mon(3) }, new[] { Fixture.Mon(2, moves: new[] { Fixture.Hit(priority: 5) }) }, 42, Fixture.Encounter);
            var r = BattleEngine.ResolveTurn(s, BattleAction.Switch(1), BattleAction.Move(0)); Assert.Equal(1, r.State.ActiveIndex(Side.Player)); Assert.Equal(s.Team(Side.Player)[0].Hp, r.State.Team(Side.Player)[0].Hp); Assert.Contains(r.Events, e => e.Kind == EventKind.Damage && e.Target == Id(3));
        }
        [Fact] public void SwitchClearsStagesButPreservesHpStatusAndPp()
        {
            var s = new BattleState(new[] { Fixture.Mon(hp: 80, status: MajorStatus.Paralysis, pp: new[] { 10 }, stages: new[] { -3, 0, 0, 0, 0, 0, 0 }), Fixture.Mon(3) }, new[] { Fixture.Mon(2, moves: new[] { PilotCatalog.Growl }) }, 42, Fixture.Encounter);
            var r = BattleEngine.ResolveTurn(s, BattleAction.Switch(1), BattleAction.Move(0)); var p = r.State.Team(Side.Player)[0]; Assert.Equal(80, p.Hp); Assert.Equal(MajorStatus.Paralysis, p.Status); Assert.Equal(10, p.Pp[0]); Assert.All(p.Stages, v => Assert.Equal(0, v));
        }
        [Fact] public void BenchedBurnDoesNotTick()
        {
            var s = new BattleState(new[] { Fixture.Mon(hp: 80, status: MajorStatus.Burn), Fixture.Mon(3) }, new[] { Fixture.Mon(2) }, 42, Fixture.Encounter); var r = BattleEngine.ResolveTurn(s, BattleAction.Switch(1), BattleAction.Move(0)); Assert.Equal(80, r.State.Team(Side.Player)[0].Hp);
        }
        [Fact] public void FaintWithReserveRequiresExplicitReplacementWithoutExtraTurnOrRng()
        {
            var s = new BattleState(new[] { Fixture.Mon(moves: new[] { Fixture.Hit(priority: 1) }) }, new[] { Fixture.Mon(2, hp: 1), Fixture.Mon(3) }, 42, Fixture.Encounter); var r = Fixture.Step(s); Assert.Equal(BattleOutcome.Ongoing, r.State.Outcome); Assert.True(r.State.NeedsReplacement(Side.Opponent));
            Assert.Throws<InvalidOperationException>(() => Fixture.Step(r.State)); var next = BattleEngine.ReplaceFainted(r.State, Side.Opponent, 1); Assert.Equal(r.State.Turn, next.State.Turn); Assert.Equal(r.State.RandomState, next.State.RandomState); Assert.Equal(r.State.RandomDraws, next.State.RandomDraws); Assert.Equal(1, next.State.ActiveIndex(Side.Opponent));
        }
        [Fact] public void InvalidCommandsLeaveInputAndRngUnchanged()
        {
            var s = Fixture.State(); string before = Canonical.State(s); Assert.Throws<ArgumentOutOfRangeException>(() => BattleEngine.ResolveTurn(s, BattleAction.Move(0), BattleAction.Move(4))); Assert.Equal(before, Canonical.State(s));
        }
        [Fact] public void NoPpRejectsInsteadOfInventingFallback()
        {
            var s = Fixture.State(Fixture.Mon(pp: new[] { 0 })); Assert.Throws<InvalidOperationException>(() => Fixture.Step(s));
        }
        [Fact] public void CannotSwitchToActiveFaintedOrNonexistentSlot()
        {
            var s = new BattleState(new[] { Fixture.Mon(), Fixture.Mon(3, hp: 0) }, new[] { Fixture.Mon(2) }, 42, Fixture.Encounter);
            Assert.Throws<InvalidOperationException>(() => BattleEngine.ResolveTurn(s, BattleAction.Switch(0), BattleAction.Move(0))); Assert.Throws<InvalidOperationException>(() => BattleEngine.ResolveTurn(s, BattleAction.Switch(1), BattleAction.Move(0))); Assert.Throws<ArgumentOutOfRangeException>(() => BattleEngine.ResolveTurn(s, BattleAction.Switch(2), BattleAction.Move(0)));
        }
        [Fact] public void CannotReplaceLivingActiveOrActAfterBattleEnd()
        {
            Assert.Throws<InvalidOperationException>(() => BattleEngine.ReplaceFainted(Fixture.State(), Side.Player, 0)); Assert.Throws<InvalidOperationException>(() => Fixture.Step(Fixture.State(opponent: Fixture.Mon(2, hp: 0))));
        }
        [Fact] public void BothEncounterKindsRunToCompletion()
        {
            foreach (var kind in new[] { EncounterKind.Wild, EncounterKind.Trainer }) { var s = Fixture.State(kind: kind); for (int i = 0; i < 30 && s.Outcome == BattleOutcome.Ongoing; i++) s = Fixture.Step(s).State; Assert.NotEqual(BattleOutcome.Ongoing, s.Outcome); }
        }
        [Fact] public void BranchingAndDeepCopyDoNotMutateOriginalArraysOrRecords()
        {
            var s = Fixture.State(); string before = Canonical.State(s); var branch = Fixture.Step(s.DeepCopy()); Assert.Equal(before, Canonical.State(s)); Assert.NotEqual(before, Canonical.State(branch.State));
            var a = s.Active(Side.Player).Record.Ivs.ToArray(); a[0] = 0; Assert.Equal(31, s.Active(Side.Player).Record.Ivs[Stat.Hp]); Assert.NotSame(s.Active(Side.Player), s.DeepCopy().Active(Side.Player));
        }
        [Fact] public void SnapshotConstructorDefensivelyCopiesCallerArrays()
        {
            int[] pp = { 40 }, stages = new int[7]; var p = Fixture.Mon(pp: pp, stages: stages); pp[0] = 0; stages[0] = -6; Assert.Equal(40, p.Pp[0]); Assert.Equal(0, p.StageValue(Stage.Attack));
        }
        [Fact] public void CheckpointRestoreReplaysExactly()
        {
            var s = Fixture.Step().State; var restored = new BattleState(s.Team(Side.Player), s.Team(Side.Opponent), s.RandomState, s.EncounterId, s.Kind, s.ActiveIndex(Side.Player), s.ActiveIndex(Side.Opponent), s.Ruleset, s.Turn, s.RandomDraws); var a = Fixture.Step(s); var b = Fixture.Step(restored); Assert.Equal(a.StateHash, b.StateHash); Assert.Equal(Canonical.Events(a.Events), Canonical.Events(b.Events));
        }
        [Fact] public void TransitionDigestIncludesCommandsAsWellAsResult()
        {
            var s = Fixture.State(); var r = Fixture.Step(s); string digest = Canonical.TransitionHash(s, BattleAction.Move(0), BattleAction.Move(0), r);
            Assert.Equal(digest, Canonical.TransitionHash(s.DeepCopy(), BattleAction.Move(0), BattleAction.Move(0), Fixture.Step(s)));
            Assert.NotEqual(digest, Canonical.TransitionHash(s, BattleAction.Switch(0), BattleAction.Move(0), r)); Assert.Equal(64, digest.Length);
        }
        [Fact] public void SimultaneousVoluntarySwitchesAreDeterministicAndDoNotDamage()
        {
            var s = new BattleState(new[] { Fixture.Mon(), Fixture.Mon(3) }, new[] { Fixture.Mon(2), Fixture.Mon(4) }, 42, Fixture.Encounter);
            var r = BattleEngine.ResolveTurn(s, BattleAction.Switch(1), BattleAction.Switch(1)); Assert.Equal(2, r.Events.Count(e => e.Kind == EventKind.Switched)); Assert.DoesNotContain(r.Events, e => e.Kind == EventKind.Damage); Assert.Equal(1, r.State.ActiveIndex(Side.Player)); Assert.Equal(1, r.State.ActiveIndex(Side.Opponent));
        }
        [Fact] public void BothPendingReplacementsMustBeResolvedBeforeNextTurn()
        {
            var s = new BattleState(new[] { Fixture.Mon(hp: 0), Fixture.Mon(3) }, new[] { Fixture.Mon(2, hp: 0), Fixture.Mon(4) }, 42, Fixture.Encounter);
            var first = BattleEngine.ReplaceFainted(s, Side.Player, 1); Assert.Throws<InvalidOperationException>(() => Fixture.Step(first.State)); var second = BattleEngine.ReplaceFainted(first.State, Side.Opponent, 1); Assert.Equal(1, Fixture.Step(second.State).State.Turn);
        }
        [Fact] public void BurnResidualOrderingCanDetermineWinnerBeforeOtherResidual()
        {
            var s = Fixture.State(Fixture.Mon(species: Fixture.Species(speed: 120), moves: new[] { PilotCatalog.Growl }, hp: 1, status: MajorStatus.Burn), Fixture.Mon(2, moves: new[] { PilotCatalog.Growl }, hp: 1, status: MajorStatus.Burn));
            var r = Fixture.Step(s); Assert.Equal(BattleOutcome.OpponentWin, r.State.Outcome); Assert.Equal(Id(1), r.Events.Single(e => e.Kind == EventKind.Fainted).Actor); Assert.Equal(1, r.State.Active(Side.Opponent).Hp);
        }
        [Fact] public void HashIsCultureIndependentAndIncludesDefinitionAndRngChanges()
        {
            var s = Fixture.State(); string hash = Canonical.Hash(Canonical.State(s)); var old = CultureInfo.CurrentCulture; try { CultureInfo.CurrentCulture = new CultureInfo("fr-FR"); Assert.Equal(hash, Canonical.Hash(Canonical.State(s))); } finally { CultureInfo.CurrentCulture = old; }
            Assert.NotEqual(hash, Canonical.Hash(Canonical.State(Fixture.Seed(s, 124)))); Assert.NotEqual(hash, Canonical.Hash(Canonical.State(Fixture.State(Fixture.Mon(moves: new[] { Fixture.Hit(power: 41) })))));
        }
    }

    public sealed class ContractAndRandomTests
    {
        [Fact] public void XorshiftHasPublishedAlgorithmGoldenVectorAndRestorableState()
        {
            var r = new BattleRandom(1); Assert.Equal(new uint[] { 270369, 67634689, 2647435461, 307599695, 2398689233 }, Enumerable.Range(0, 5).Select(_ => r.NextUInt())); var copy = new BattleRandom(r.State, r.Draws); Assert.Equal(r.Next(100), copy.Next(100)); Assert.Equal(r.Draws, copy.Draws);
        }
        [Fact] public void RandomRangesAreBoundedIncludingOneAndLargeBound()
        {
            var r = new BattleRandom(42); foreach (int bound in new[] { 1, 2, 3, 4, 16, 100, int.MaxValue }) for (int i = 0; i < 1000; i++) Assert.InRange(r.Next(bound), 0, bound - 1);
        }
        [Fact] public void RejectionSamplingDiscardsIncompleteBucket()
        {
            // Inverse xorshift gives a state whose next output is uint.MaxValue.
            static uint UndoLeft(uint x, int s) { uint v = x; for (int i = 0; i < 32; i++) v = x ^ (v << s); return v; }
            static uint UndoRight(uint x, int s) { uint v = x; for (int i = 0; i < 32; i++) v = x ^ (v >> s); return v; }
            uint seed = UndoLeft(UndoRight(UndoLeft(uint.MaxValue, 5), 17), 13); var r = new BattleRandom(seed); Assert.InRange(r.Next(16), 0, 15); Assert.Equal(2, r.Draws);
        }
        [Fact] public void InvalidRandomArgumentsReject()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BattleRandom(0)); Assert.Throws<ArgumentOutOfRangeException>(() => new BattleRandom(1, -1)); Assert.Throws<ArgumentOutOfRangeException>(() => new BattleRandom(1).Next(0)); Assert.Throws<OverflowException>(() => new BattleRandom(1, long.MaxValue).NextUInt());
        }
        [Fact] public void UnknownRulesetAndModernTypeAreRejected()
        {
            Assert.Throws<NotSupportedException>(() => new BattleState(new[] { Fixture.Mon() }, new[] { Fixture.Mon(2) }, 1, Fixture.Encounter, ruleset: "gen9")); Assert.Throws<ArgumentOutOfRangeException>(() => Fixture.Species((Element)17));
        }
        [Fact] public void DuplicateIndividualAndConflictingDefinitionsReject()
        {
            Assert.Throws<ArgumentException>(() => new BattleState(new[] { Fixture.Mon() }, new[] { Fixture.Mon() }, 1, Fixture.Encounter));
            var species = new SpeciesDefinition(Fixture.Species().Id, "fixture-1", SixStats.Uniform(99), Element.Normal); Assert.Throws<ArgumentException>(() => Fixture.State(opponent: Fixture.Mon(2, species)));
            var bad = new MoveDefinition(Fixture.Hit().Id, Element.Normal, Category.Physical, 41, 100, 64); Assert.Throws<ArgumentException>(() => Fixture.State(opponent: Fixture.Mon(2, moves: new[] { bad })));
        }
        [Fact] public void WildPartySizeAndEmptyPartyReject()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BattleState(new[] { Fixture.Mon() }, new[] { Fixture.Mon(2), Fixture.Mon(3) }, 1, Fixture.Encounter, EncounterKind.Wild)); Assert.Throws<ArgumentOutOfRangeException>(() => new BattleState(Array.Empty<BattlePokemon>(), new[] { Fixture.Mon(2) }, 1, Fixture.Encounter));
        }
        [Fact] public void IndividualIdentitySurvivesSpeciesAndLevelChange()
        {
            var p = Fixture.Record(species: PilotCatalog.Bulbasaur, ability: Ability.Overgrow); var ivysaur = new SpeciesDefinition("ivysaur", "fixture-1", new SixStats(60, 62, 63, 80, 80, 60), Element.Grass, Element.Poison); var q = p.WithSpeciesAndLevel(ivysaur, 5); Assert.Equal(p.IndividualId, q.IndividualId); Assert.Equal(p.Nature, q.Nature); Assert.Equal(p.Ability, q.Ability); Assert.Equal(p.Moves, q.Moves); Assert.Equal(p.Ivs.ToArray(), q.Ivs.ToArray()); Assert.Equal(50, p.Level); Assert.Equal(5, q.Level);
        }
        [Fact] public void InvalidRecordsAndUnsupportedMoveShapesReject()
        {
            Assert.Throws<ArgumentException>(() => new PokemonRecord(Guid.Empty, PilotCatalog.Rattata, 5, SixStats.Uniform(0), SixStats.Uniform(0), Nature.Hardy, Ability.RunAway, new[] { PilotCatalog.Tackle }));
            Assert.Throws<ArgumentOutOfRangeException>(() => Fixture.Record(level: 101)); Assert.Throws<ArgumentOutOfRangeException>(() => Fixture.Record(nature: (Nature)25)); Assert.Throws<ArgumentOutOfRangeException>(() => Fixture.Record(ability: (Ability)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PokemonRecord(Fixture.Id(1), PilotCatalog.Rattata, 5, SixStats.Uniform(32), SixStats.Uniform(0), Nature.Hardy, Ability.RunAway, new[] { PilotCatalog.Tackle }));
            Assert.Throws<ArgumentException>(() => new PokemonRecord(Fixture.Id(1), PilotCatalog.Rattata, 5, SixStats.Uniform(0), SixStats.Uniform(100), Nature.Hardy, Ability.RunAway, new[] { PilotCatalog.Tackle }));
            Assert.Throws<ArgumentException>(() => Fixture.Record(moves: Array.Empty<MoveDefinition>())); Assert.Throws<ArgumentException>(() => Fixture.Record(moves: new[] { PilotCatalog.Tackle, PilotCatalog.Tackle }));
            Assert.Throws<ArgumentException>(() => new MoveDefinition("bad", Element.Normal, Category.Status, 0, 100, 10)); Assert.Throws<ArgumentException>(() => new MoveDefinition("bad", Element.Normal, Category.Physical, 0, 100, 10)); Assert.Throws<ArgumentOutOfRangeException>(() => new MoveDefinition("bad", Element.Normal, Category.Physical, 40, 101, 10));
        }
        [Fact] public void InvalidSnapshotsReject()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Fixture.Mon(hp: -1)); Assert.Throws<ArgumentException>(() => Fixture.Mon(stages: new[] { 0 })); Assert.Throws<ArgumentOutOfRangeException>(() => Fixture.Mon(pp: new[] { 65 })); Assert.Throws<ArgumentException>(() => Fixture.Mon(species: Fixture.Species(Element.Fire), status: MajorStatus.Burn)); Assert.Throws<ArgumentOutOfRangeException>(() => Fixture.Mon(status: (MajorStatus)99));
        }
        [Fact] public void SeededCreatureGenerationIsRepeatableAndCallerOwnsIdentity()
        {
            var a = PokemonFactory.Create(Fixture.Id(1), PilotCatalog.Bulbasaur, 5, Ability.Overgrow, new[] { PilotCatalog.Tackle }, 1);
            var b = PokemonFactory.Create(Fixture.Id(1), PilotCatalog.Bulbasaur, 5, Ability.Overgrow, new[] { PilotCatalog.Tackle }, 1);
            var c = PokemonFactory.Create(Fixture.Id(2), PilotCatalog.Bulbasaur, 5, Ability.Overgrow, new[] { PilotCatalog.Tackle }, 1);
            Assert.Equal(a.Pokemon.Ivs.ToArray(), b.Pokemon.Ivs.ToArray()); Assert.Equal(a.Pokemon.Nature, b.Pokemon.Nature); Assert.Equal(a.FinalRandomState, b.FinalRandomState); Assert.Equal(7, a.RandomDraws); Assert.Equal(a.Pokemon.Ivs.ToArray(), c.Pokemon.Ivs.ToArray()); Assert.NotEqual(a.Pokemon.IndividualId, c.Pokemon.IndividualId);
            Assert.All(a.Pokemon.Ivs.ToArray(), x => Assert.InRange(x, 0, 31)); Assert.All(a.Pokemon.Evs.ToArray(), x => Assert.Equal(0, x)); Assert.Equal(Fixture.Id(1), a.Pokemon.IndividualId);
            Assert.Equal(new[] { 0, 0, 4, 14, 16, 15 }, a.Pokemon.Ivs.ToArray()); Assert.Equal(Nature.Docile, a.Pokemon.Nature); Assert.Equal(1u, a.SourceSeed); Assert.Equal(632435482u, a.FinalRandomState);
        }
        [Fact] public void DifferentGenerationSeedsYieldDifferentFixturesWithoutMutatingSpecies()
        {
            string before = Canonical.Species(PilotCatalog.Bulbasaur);
            var values = Enumerable.Range(1, 100).Select(seed => PokemonFactory.Create(Fixture.Id(seed), PilotCatalog.Bulbasaur, 5, Ability.Overgrow, new[] { PilotCatalog.Tackle }, (uint)seed)).ToArray();
            Assert.True(values.Select(v => string.Join(",", v.Pokemon.Ivs.ToArray())).Distinct().Count() > 90); Assert.True(values.Select(v => v.Pokemon.Nature).Distinct().Count() > 15); Assert.Equal(before, Canonical.Species(PilotCatalog.Bulbasaur));
        }
        [Fact] public void FactoryRejectsZeroSeedAndEmptyIdentity()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => PokemonFactory.Create(Fixture.Id(1), PilotCatalog.Bulbasaur, 5, Ability.Overgrow, new[] { PilotCatalog.Tackle }, 0)); Assert.Throws<ArgumentException>(() => PokemonFactory.Create(Guid.Empty, PilotCatalog.Bulbasaur, 5, Ability.Overgrow, new[] { PilotCatalog.Tackle }, 1));
        }
        [Fact] public void TerminalSnapshotDrawIsExplicitAndCannotAdvance()
        {
            var s = Fixture.State(Fixture.Mon(hp: 0), Fixture.Mon(2, hp: 0)); Assert.Equal(BattleOutcome.Draw, s.Outcome); Assert.Throws<InvalidOperationException>(() => Fixture.Step(s));
        }
        [Fact] public void InvalidIdsTypesAndEncounterIdentityReject()
        {
            Assert.Throws<ArgumentException>(() => new SpeciesDefinition("Bad ID", "fixture-1", SixStats.Uniform(50), Element.Normal)); Assert.Throws<ArgumentException>(() => new SpeciesDefinition("duplicate", "fixture-1", SixStats.Uniform(50), Element.Fire, Element.Fire)); Assert.Throws<ArgumentOutOfRangeException>(() => new SpeciesDefinition("zero-stat", "fixture-1", SixStats.Uniform(0), Element.Normal));
            Assert.Throws<ArgumentException>(() => new BattleState(new[] { Fixture.Mon() }, new[] { Fixture.Mon(2) }, 1, Guid.Empty));
        }
        [Fact] public void UnknownRealMoveSemanticsNeverFallBackToGenericDamage()
        {
            var explosion = new MoveDefinition("explosion", Element.Normal, Category.Physical, 250, 100, 5); Assert.Throws<NotSupportedException>(() => Fixture.State(Fixture.Mon(moves: new[] { explosion })));
        }
        [Fact] public void UnknownSpeciesRequiresNewCatalogVersionRatherThanSilentAcceptance()
        {
            var missing = new SpeciesDefinition("missing-species", "fixture-1", SixStats.Uniform(50), Element.Normal); Assert.Throws<NotSupportedException>(() => Fixture.State(Fixture.Mon(species: missing)));
        }
        [Fact] public void ExistingSpeciesAndMoveFactsCannotBeOverriddenEvenWithoutIdConflict()
        {
            var changed = new SpeciesDefinition("bulbasaur", PilotCatalog.DataVersion, SixStats.Uniform(200), Element.Grass); Assert.Throws<ArgumentException>(() => Fixture.State(Fixture.Mon(species: changed)));
            var modernTackle = new MoveDefinition("tackle", Element.Normal, Category.Physical, 40, 100, 35); Assert.Throws<ArgumentException>(() => Fixture.State(Fixture.Mon(moves: new[] { modernTackle })));
        }
    }
}
