using System;
using System.Linq;
using Roguelite.Gameplay;
using Roguelite.Gameplay.Phase3;
using Xunit;

namespace Roguelite.Phase3.Tests
{
    public sealed class LeechSeedConformanceTests
    {
        private static BattleMove SeedMove() => new BattleMove("leech-seed", Element.Grass, Category.Status, 0, 90, 10, BattleMoveEffect.LeechSeed, generationIntroduced: 1);

        private static BattleStep3 SeedSuccessfully(AdvancedBattleState state, BattleAction3 sourceAction)
        {
            for (uint seed = 1; seed < 10000; seed++)
            {
                var candidate = new AdvancedBattleState(state.Team(Side.Player), state.Team(Side.Opponent), seed, state.EncounterId,
                    state.Ruleset, state.ActiveIndex(Side.Player), state.ActiveIndex(Side.Opponent), state.Turn,
                    state.Weather, state.WeatherTurns, state.Conditions(Side.Player), state.Conditions(Side.Opponent));
                var result = AdvancedBattleEngine.ResolveTurn(candidate, sourceAction, BattleAction3.Move(0));
                if (result.Events.Any(e => e.Detail == "leech-seed")) return result;
            }
            throw new Exception("Could not find deterministic successful Leech Seed accuracy fixture.");
        }

        [Fact]
        public void LeechSeedDrainsOneEighthAndHealsTheSeedSourceInTheSameResidualPass()
        {
            var source = F.Mon(1, moves: new[] { SeedMove() });
            var sourceDamaged = new AdvancedPokemon(source.Record, source.Moves, abilityId: "none", hp: source.Hp - 10);
            var target = F.Mon(2, moves: new[] { F.Status("reflect", BattleMoveEffect.SetReflect) });
            var result = SeedSuccessfully(F.State(sourceDamaged, target, seed: 1), BattleAction3.Move(0));
            int maxTargetHp = HgssRules.CalculateStats(target.Record)[Stat.Hp];
            var drain = Assert.Single(result.Events, e => e.Detail == "leech-seed-drain");
            Assert.Equal(Math.Max(1, maxTargetHp / 8), drain.Amount);
            Assert.Contains(result.Events, e => e.Detail == "leech-seed-heal" && e.Actor == F.Id(1).ToString("N") && e.Amount == 10);
            Assert.Equal((int)Side.Player, result.State.Active(Side.Opponent).LeechSeedSourceSide);
            Assert.Equal(-1, F.State(sourceDamaged, target).Active(Side.Opponent).LeechSeedSourceSide);
            Assert.Equal(BattleCanonical.Hash(result.State), BattleCanonical.Hash(result.State.DeepCopy()));
        }

        [Fact]
        public void GrassTypesAreImmuneAndMagicGuardPreventsLeechSeedDamage()
        {
            var grassMove = SeedMove();
            var grass = F.Mon(2, F.Grass, moves: new[] { F.Status("reflect", BattleMoveEffect.SetReflect) });
            var immune = AdvancedBattleEngine.ResolveTurn(F.State(F.Mon(1, moves: new[] { grassMove }), grass, 13), BattleAction3.Move(0), BattleAction3.Move(0));
            Assert.Contains(immune.Events, e => e.Detail == "type-immunity");
            Assert.Equal(-1, immune.State.Active(Side.Opponent).LeechSeedSourceSide);

            var guarded = F.Mon(3, ability: "magic-guard", moves: new[] { F.Status("reflect", BattleMoveEffect.SetReflect) });
            var seeded = SeedSuccessfully(F.State(F.Mon(4, moves: new[] { grassMove }), guarded, 1), BattleAction3.Move(0));
            Assert.Equal((int)Side.Player, seeded.State.Active(Side.Opponent).LeechSeedSourceSide);
            Assert.DoesNotContain(seeded.Events, e => e.Detail == "leech-seed-drain");
        }

        [Fact]
        public void LiquidOozeReversesSeedHealingAndMagicGuardProtectsTheSeedSource()
        {
            var source = F.Mon(1, moves: new[] { SeedMove() });
            var damagedSource = new AdvancedPokemon(source.Record, source.Moves, abilityId: "none", hp: source.Hp - 30);
            var ooze = F.Mon(2, ability: "liquid-ooze", moves: new[] { F.Status("reflect", BattleMoveEffect.SetReflect) });
            var result = SeedSuccessfully(F.State(damagedSource, ooze, 2), BattleAction3.Move(0));
            Assert.Contains(result.Events, e => e.Detail == "liquid-ooze" && e.Target == F.Id(1).ToString("N"));
            Assert.DoesNotContain(result.Events, e => e.Detail == "leech-seed-heal");

            var guardedSource = F.Mon(3, ability: "magic-guard", moves: new[] { SeedMove() });
            var guardedDamaged = new AdvancedPokemon(guardedSource.Record, guardedSource.Moves, abilityId: "magic-guard", hp: guardedSource.Hp - 30);
            var guardedResult = SeedSuccessfully(F.State(guardedDamaged, ooze, 3), BattleAction3.Move(0));
            Assert.DoesNotContain(guardedResult.Events, e => e.Detail == "liquid-ooze");
            Assert.DoesNotContain(guardedResult.Events, e => e.Detail == "leech-seed-heal");
        }

        [Fact]
        public void SeedSourceTracksBattleSideAndSeedEndsWhenTheTargetSwitchesOrUsesRapidSpin()
        {
            var source = F.Mon(1, moves: new[] { SeedMove() });
            var target = F.Mon(2, moves: new[] { F.Status("reflect", BattleMoveEffect.SetReflect) });
            var seeded = SeedSuccessfully(F.State(source, target, 4), BattleAction3.Move(0));
            var switched = AdvancedBattleEngine.ResolveTurn(
                new AdvancedBattleState(seeded.State.Team(Side.Player), new[] { seeded.State.Team(Side.Opponent)[0], F.Mon(3) }, 44, F.Id(699),
                    playerActive: 0, opponentActive: 0, turn: seeded.State.Turn),
                BattleAction3.Move(0), BattleAction3.Switch(1));
            Assert.Equal(-1, switched.State.Team(Side.Opponent)[0].LeechSeedSourceSide);

            var rapidSpin = new BattleMove("rapid-spin", Element.Normal, Category.Physical, 20, 100, 40, BattleMoveEffect.RapidSpin);
            var seededUser = new AdvancedPokemon(source.Record, new[] { rapidSpin }, abilityId: "none", leechSeedSourceSide: (int)Side.Opponent);
            var spun = AdvancedBattleEngine.ResolveTurn(new AdvancedBattleState(new[] { seededUser }, new[] { target }, 45, F.Id(700)), BattleAction3.Move(0), BattleAction3.Move(0));
            Assert.Equal(-1, spun.State.Active(Side.Player).LeechSeedSourceSide);
            Assert.Contains(spun.Events, e => e.Detail == "rapid-spin-cleared-leech-seed");
        }

        [Fact]
        public void SeededTargetHealsCurrentActiveOnTheSourceSideAfterTheOriginalSourceSwitchesOut()
        {
            var source = F.Mon(1, moves: new[] { SeedMove() });
            var target = F.Mon(2, moves: new[] { F.Status("reflect", BattleMoveEffect.SetReflect) });
            var seeded = SeedSuccessfully(F.State(source, target, 6), BattleAction3.Move(0));
            var reserve = F.Mon(3);
            var damagedReserve = new AdvancedPokemon(reserve.Record, reserve.Moves, abilityId: "none", hp: reserve.Hp - 20);
            var state = new AdvancedBattleState(new[] { seeded.State.Team(Side.Player)[0], damagedReserve },
                new[] { seeded.State.Team(Side.Opponent)[0] }, 77, F.Id(701), playerActive: 0, turn: seeded.State.Turn);

            var switched = AdvancedBattleEngine.ResolveTurn(state, BattleAction3.Switch(1), BattleAction3.Move(0));
            Assert.Contains(switched.Events, e => e.Detail == "leech-seed-heal" && e.Actor == F.Id(3).ToString("N"));
            Assert.Equal((int)Side.Player, switched.State.Active(Side.Opponent).LeechSeedSourceSide);
            Assert.Equal(-1, switched.State.Team(Side.Player)[0].LeechSeedSourceSide);
        }
    }

    public sealed class HgssActionAndMoveRestrictionTests
    {
        [Fact]
        public void PursuitHitsTheSelectedSwitchingIndividualBeforeTheReplacementEnters()
        {
            var pursuit = new BattleMove("pursuit", Element.Dark, Category.Physical, 40, 100, 20, BattleMoveEffect.Pursuit);
            var switcher = F.Mon(1, moves: new[] { F.Hit() });
            var pursuer = F.Mon(2, moves: new[] { pursuit });
            var state = new AdvancedBattleState(new[] { switcher, F.Mon(3) }, new[] { pursuer }, 22, F.Id(501));
            var result = AdvancedBattleEngine.ResolveTurn(state, BattleAction3.Switch(1), BattleAction3.Move(0));
            int pursuitEvent = result.Events.ToList().FindIndex(e => e.Kind == BattleEventKind.MoveUsed && e.Detail == "pursuit");
            int switchEvent = result.Events.ToList().FindIndex(e => e.Kind == BattleEventKind.Switched && e.Detail == "voluntary");
            Assert.True(pursuitEvent >= 0 && switchEvent > pursuitEvent);
            Assert.Equal(F.Id(3), result.State.Active(Side.Player).Record.IndividualId);
            Assert.Contains(result.Events, e => e.Kind == BattleEventKind.Damage && e.Target == F.Id(1).ToString("N") && e.Detail == "pursuit");
        }

        [Fact]
        public void PursuitKnockoutCancelsVoluntarySwitchAndLeavesReplacementPending()
        {
            var pursuit = new BattleMove("pursuit", Element.Dark, Category.Physical, 120, 100, 20, BattleMoveEffect.Pursuit);
            var state = new AdvancedBattleState(new[] { F.Mon(1, hp: 1), F.Mon(3) }, new[] { F.Mon(2, moves: new[] { pursuit }) }, 25, F.Id(502));
            var result = AdvancedBattleEngine.ResolveTurn(state, BattleAction3.Switch(1), BattleAction3.Move(0));
            Assert.True(result.State.NeedsReplacement(Side.Player));
            Assert.DoesNotContain(result.Events, e => e.Kind == BattleEventKind.Switched && e.Detail == "voluntary");
            Assert.Contains(result.Events, e => e.Kind == BattleEventKind.Fainted && e.Actor == F.Id(1).ToString("N"));
        }

        [Fact]
        public void PursuitInterceptionUsesDoubledPowerComparedWithOrdinaryUse()
        {
            var pursuit = new BattleMove("pursuit", Element.Dark, Category.Physical, 40, 100, 20, BattleMoveEffect.Pursuit);
            var target = F.Mon(1);
            var pursuer = F.Mon(2, moves: new[] { pursuit });
            var intercepted = AdvancedBattleEngine.ResolveTurn(new AdvancedBattleState(new[] { target, F.Mon(3) }, new[] { pursuer }, 220, F.Id(506)), BattleAction3.Switch(1), BattleAction3.Move(0));
            var ordinary = AdvancedBattleEngine.ResolveTurn(new AdvancedBattleState(new[] { target }, new[] { pursuer }, 220, F.Id(507)), BattleAction3.Move(0), BattleAction3.Move(0));
            int boostedDamage = Assert.Single(intercepted.Events, e => e.Actor == F.Id(2).ToString("N") && e.Kind == BattleEventKind.Damage).Amount;
            int normalDamage = Assert.Single(ordinary.Events, e => e.Actor == F.Id(2).ToString("N") && e.Kind == BattleEventKind.Damage).Amount;
            Assert.True(boostedDamage > normalDamage);
        }

        [Fact]
        public void PressureConsumesAnAdditionalPpAndRestrictionsArePreservedInSnapshots()
        {
            var hitter = F.Mon(1);
            var pressure = F.Mon(2, ability: "pressure");
            var result = F.Step(F.State(hitter, pressure));
            Assert.Equal(18, result.State.Active(Side.Player).Pp[0]);
            Assert.Equal(2, Assert.Single(result.Events, e => e.Kind == BattleEventKind.MoveUsed && e.Actor == F.Id(1).ToString("N")).Amount);

            var disabled = new AdvancedPokemon(hitter.Record, hitter.Moves, abilityId: "none", disabledMoveId: hitter.Moves[0].Id, disableTurns: 2);
            Assert.Equal(BattleCanonical.Hash(F.State(disabled)), BattleCanonical.Hash(F.State(disabled.DeepCopy())));
            var blocked = F.Step(F.State(disabled), BattleAction3.Move(0), BattleAction3.Move(0));
            Assert.Contains(blocked.Events, e => e.Detail.StartsWith("restriction:Disabled", StringComparison.Ordinal));
        }

        [Fact]
        public void DisableAppliedBeforeTheTargetActsInterruptsItsAlreadyQueuedMove()
        {
            var disable = F.Status("disable", BattleMoveEffect.Disable, 1);
            var targetMoves = new[] { F.Hit(40), F.Hit(60) };
            var disabler = F.Mon(1, moves: new[] { disable }, stages: new[] { 0, 0, 0, 0, 0, 2, 0 });
            var target = F.Mon(2, moves: targetMoves, lastMoveId: targetMoves[0].Id);
            var result = F.Step(F.State(disabler, target, 72));
            Assert.Contains(result.Events, e => e.Detail.StartsWith("disable:", StringComparison.Ordinal));
            Assert.Contains(result.Events, e => e.Detail.StartsWith("restriction:Disabled", StringComparison.Ordinal));
            Assert.DoesNotContain(result.Events, e => e.Kind == BattleEventKind.MoveUsed && e.Actor == F.Id(2).ToString("N"));
        }

        [Fact]
        public void EncoreTauntAndImprisonRejectIncompatibleSelections()
        {
            var moves = new[] { F.Hit(40), F.Status("calm-mind", BattleMoveEffect.SetLightScreen) };
            var encored = F.Mon(1, moves: moves, encoreMoveId: moves[0].Id, encoreTurns: 2);
            var encoreResult = F.Step(F.State(encored), BattleAction3.Move(1), BattleAction3.Move(0));
            Assert.Contains(encoreResult.Events, e => e.Detail.StartsWith("restriction:Encore", StringComparison.Ordinal));
            var taunted = F.Mon(5, moves: moves, tauntTurns: 2);
            var tauntResult = F.Step(F.State(taunted), BattleAction3.Move(1), BattleAction3.Move(0));
            Assert.Contains(tauntResult.Events, e => e.Detail.StartsWith("restriction:Taunt", StringComparison.Ordinal));
            var player = F.Mon(3, moves: moves);
            var imprisoner = F.Mon(4, moves: moves, imprisoning: true);
            var imprisoned = F.Step(F.State(player, imprisoner), BattleAction3.Move(0), BattleAction3.Move(0));
            Assert.Contains(imprisoned.Events, e => e.Actor == F.Id(3).ToString("N") && e.Detail.StartsWith("restriction:Imprison", StringComparison.Ordinal));
        }

        [Fact]
        public void SwitchingClearsDisableEncoreTauntAndImprisonButRetainsMajorStatusAndPp()
        {
            var hit = F.Hit();
            var outgoing = F.Mon(1, moves: new[] { hit }, status: Condition.Burn, pp: new[] { 7 },
                disabledMoveId: hit.Id, disableTurns: 3, encoreMoveId: hit.Id, encoreTurns: 4, tauntTurns: 2, imprisoning: true);
            var state = new AdvancedBattleState(new[] { outgoing, F.Mon(6) }, new[] { F.Mon(2) }, 63, F.Id(505));
            var result = F.Step(state, BattleAction3.Switch(1), BattleAction3.Move(0));
            var returned = result.State.Team(Side.Player)[0];
            Assert.Equal(Condition.Burn, returned.Condition);
            Assert.Equal(7, returned.Pp[0]);
            Assert.Empty(returned.DisabledMoveId);
            Assert.Empty(returned.EncoreMoveId);
            Assert.Equal(0, returned.TauntTurns);
            Assert.False(returned.Imprisoning);
        }

        [Fact]
        public void RapidSpinRemovesOnlyItsUsersHazardsAfterAConnectedHit()
        {
            var spin = new BattleMove("rapid-spin", Element.Normal, Category.Physical, 20, 100, 40, BattleMoveEffect.RapidSpin);
            var conditions = new SideConditions(2, 1, true);
            var result = F.Step(F.State(F.Mon(1, moves: new[] { spin }), F.Mon(2), ps: conditions));
            var own = result.State.Conditions(Side.Player);
            var enemy = result.State.Conditions(Side.Opponent);
            Assert.Equal(0, own.SpikesLayers);
            Assert.Equal(1, own.ToxicSpikesLayers);
            Assert.True(own.StealthRock);
            Assert.Equal(0, enemy.SpikesLayers);
            Assert.Contains(result.Events, e => e.Detail == "rapid-spin-cleared-spikes");
        }

        [Fact]
        public void RoarRequiresAnExplicitReplacementPolicyAndDoesNotInventRandomSelection()
        {
            var roar = F.Status("roar", BattleMoveEffect.ForceSwitch, -6);
            var forced = F.Mon(1, moves: new[] { roar });
            var outgoing = F.Mon(2, moves: new[] { F.Hit() });
            var incoming = F.Mon(3, moves: new[] { F.Hit() });
            var state = new AdvancedBattleState(new[] { forced }, new[] { outgoing, incoming }, 44, F.Id(503));
            var result = AdvancedBattleEngine.ResolveTurn(state, BattleAction3.Move(0), BattleAction3.Move(0));
            Assert.Equal(F.Id(2), result.State.Active(Side.Opponent).Record.IndividualId);
            Assert.False(result.State.NeedsReplacement(Side.Opponent));
            Assert.Contains(result.Events, e => e.Detail == "replacement-selection-policy-not-supplied");
            Assert.DoesNotContain(result.Events, e => e.Kind == BattleEventKind.MoveUsed && e.Actor == F.Id(3).ToString("N"));
        }

        [Fact]
        public void SuctionCupsBlocksForcedSwitchAndDeterministicReplayMatches()
        {
            var roar = F.Status("roar", BattleMoveEffect.ForceSwitch, -6);
            var state = new AdvancedBattleState(new[] { F.Mon(1, moves: new[] { roar }) }, new[] { F.Mon(2, ability: "suction-cups"), F.Mon(3) }, 45, F.Id(504));
            var one = F.Step(state, BattleAction3.Move(0), BattleAction3.Move(0));
            var two = F.Step(state.DeepCopy(), BattleAction3.Move(0), BattleAction3.Move(0));
            Assert.Equal(F.Id(2), one.State.Active(Side.Opponent).Record.IndividualId);
            Assert.Contains(one.Events, e => e.Detail == "suction-cups-blocked");
            Assert.Equal(BattleCanonical.Hash(one.State, one.Events), BattleCanonical.Hash(two.State, two.Events));
        }

        [Fact]
        public void LateFlinchExpiresAtTurnEndInsteadOfBlockingTheNextTurn()
        {
            var flinchMove = F.Hit(25, effectChance: 100, effect: BattleMoveEffect.Flinch);
            var faster = F.Mon(1, moves: new[] { F.Hit() }, stages: new[] { 0, 0, 0, 0, 3, 0, 0 });
            var slower = F.Mon(2, moves: new[] { flinchMove });
            var first = F.Step(F.State(faster, slower, 101), BattleAction3.Move(0), BattleAction3.Move(0));
            Assert.Contains(first.Events, e => e.Detail == "flinch");
            Assert.Equal(1, first.State.Active(Side.Player).FlinchTurns);
            var next = F.Step(first.State, BattleAction3.Move(0), BattleAction3.Move(0));
            Assert.DoesNotContain(next.Events, e => e.Kind == BattleEventKind.UnableToAct && e.Detail == "flinched");
            Assert.Contains(next.Events, e => e.Kind == BattleEventKind.MoveUsed && e.Actor == F.Id(1).ToString("N"));
        }
    }

    public sealed class HgssCriticalConformanceTests
    {
        [Fact]
        public void CriticalHitIgnoresNegativeAttackAndPositiveDefenseStagesAndBypassesReflect()
        {
            var critical = F.Hit(50, criticalStage: 4);
            AdvancedBattleState Create(uint seed, bool reflect, int attackStage, int defenseStage)
            {
                var a = F.Mon(1, moves: new[] { critical }, stages: new[] { attackStage, 0, 0, 0, 0, 0, 0 });
                var d = F.Mon(2, stages: new[] { 0, defenseStage, 0, 0, 0, 0, 0 });
                return new AdvancedBattleState(new[] { a }, new[] { d }, seed, F.Id(reflect ? 509 : 508), opponentConditions: reflect ? new SideConditions(reflectTurns: 4) : null);
            }
            BattleStep3? withCritical = null;
            uint fixtureSeed = 1;
            for (; fixtureSeed < 1000 && withCritical == null; fixtureSeed++)
            {
                var candidate = AdvancedBattleEngine.ResolveTurn(Create(fixtureSeed, true, -2, 2), BattleAction3.Move(0), BattleAction3.Move(0));
                if (candidate.Events.Any(e => e.Actor == F.Id(1).ToString("N") && e.Detail == "critical-hit")) withCritical = candidate;
            }
            Assert.NotNull(withCritical);
            fixtureSeed--;
            var noScreen = AdvancedBattleEngine.ResolveTurn(Create(fixtureSeed, false, 0, 0), BattleAction3.Move(0), BattleAction3.Move(0));
            int screenedCriticalDamage = Assert.Single(withCritical!.Events, e => e.Actor == F.Id(1).ToString("N") && e.Kind == BattleEventKind.Damage).Amount;
            int neutralCriticalDamage = Assert.Single(noScreen.Events, e => e.Actor == F.Id(1).ToString("N") && e.Kind == BattleEventKind.Damage).Amount;
            Assert.Equal(neutralCriticalDamage, screenedCriticalDamage);
        }
    }

    public sealed class HgssActionQueueConformanceTests
    {
        [Fact]
        public void MovePriorityPrecedesSpeedAndRecordedOrderIsExecuted()
        {
            var quick = F.Mon(1, moves: new[] { F.Hit(25, priority: 1) }, stages: new[] { 0, 0, 0, 0, -6, 0, 0 });
            var fast = F.Mon(2, moves: new[] { F.Hit(25) }, stages: new[] { 0, 0, 0, 0, 6, 0, 0 });
            var result = F.Step(F.State(quick, fast));
            Assert.Equal(F.Id(1).ToString("N"), Assert.Single(result.Events, e => e.Kind == BattleEventKind.Order).Actor);
            Assert.Equal(new[] { F.Id(1).ToString("N"), F.Id(2).ToString("N") }, result.Events.Where(e => e.Kind == BattleEventKind.MoveUsed).Select(e => e.Actor));
        }

        [Fact]
        public void VoluntarySwitchPrecedesMovePriorityEvenWhenSlower()
        {
            var switching = F.Mon(1, stages: new[] { 0, 0, 0, 0, -6, 0, 0 });
            var fastPriority = F.Mon(2, moves: new[] { F.Hit(25, priority: 5) }, stages: new[] { 0, 0, 0, 0, 6, 0, 0 });
            var state = new AdvancedBattleState(new[] { switching, F.Mon(3) }, new[] { fastPriority }, 10, F.Id(601));
            var result = AdvancedBattleEngine.ResolveTurn(state, BattleAction3.Switch(1), BattleAction3.Move(0));
            Assert.True(result.Events.ToList().FindIndex(e => e.Kind == BattleEventKind.Switched && e.Detail == "voluntary")
                < result.Events.ToList().FindIndex(e => e.Kind == BattleEventKind.MoveUsed));
        }

        [Fact]
        public void FaintedStartingActorCannotExecuteItsQueuedMoveWhenReserveKeepsBattleAlive()
        {
            var fast = F.Mon(1, moves: new[] { F.Hit(250) }, stages: new[] { 0, 0, 0, 0, 6, 0, 0 });
            var slow = F.Mon(2, moves: new[] { F.Hit() }, hp: 1);
            var state = new AdvancedBattleState(new[] { fast }, new[] { slow, F.Mon(3) }, 41, F.Id(602));
            var result = AdvancedBattleEngine.ResolveTurn(state, BattleAction3.Move(0), BattleAction3.Move(0));
            Assert.Contains(result.Events, e => e.Kind == BattleEventKind.Fainted && e.Actor == F.Id(2).ToString("N"));
            Assert.DoesNotContain(result.Events, e => e.Kind == BattleEventKind.MoveUsed && e.Actor == F.Id(2).ToString("N"));
            Assert.True(result.State.NeedsReplacement(Side.Opponent));
        }

        [Fact]
        public void BothActivePokemonCanFaintInOneResidualPhaseAndProduceDraw()
        {
            var burn = new AdvancedBattleState(new[] { F.Mon(1, hp: 1, status: Condition.Burn) }, new[] { F.Mon(2, hp: 1, status: Condition.Burn) }, 902, F.Id(603));
            var reflect = F.Status("reflect", BattleMoveEffect.SetReflect);
            var a = new AdvancedPokemon(burn.Active(Side.Player).Record, new[] { reflect }, hp: 1, condition: Condition.Burn);
            var b = new AdvancedPokemon(burn.Active(Side.Opponent).Record, new[] { reflect }, hp: 1, condition: Condition.Burn);
            var state = new AdvancedBattleState(new[] { a }, new[] { b }, 902, F.Id(604));
            var result = F.Step(state);
            Assert.Contains(result.Events, e => e.Detail == "burn-residual" && e.Target == F.Id(1).ToString("N"));
            Assert.Contains(result.Events, e => e.Detail == "burn-residual" && e.Target == F.Id(2).ToString("N"));
            Assert.Equal(BattleOutcome.Draw, result.State.Outcome);
        }

        [Fact]
        public void ResidualSpeedTieIsResolvedOnceBeforeResidualEffectsAndReplayIsStable()
        {
            var state = F.State(F.Mon(1, status: Condition.Poison), F.Mon(2, status: Condition.Burn), seed: 987);
            var one = F.Step(state); var two = F.Step(state.DeepCopy());
            Assert.Contains(one.Events, e => e.Detail == "residual-speed-order-tie");
            Assert.Equal(BattleCanonical.Hash(one.State, one.Events), BattleCanonical.Hash(two.State, two.Events));
        }
    }

    public sealed class HgssResidualOrderConformanceTests
    {
        [Fact]
        public void ResidualAbilityPhasePrecedesMajorStatusDamage()
        {
            var speedBoost = F.Mon(1, status: Condition.Burn, ability: "speed-boost");
            var result = F.Step(F.State(speedBoost, F.Mon(2)));
            int boost = result.Events.ToList().FindIndex(e => e.Detail == "speed-boost");
            int burn = result.Events.ToList().FindIndex(e => e.Detail == "burn-residual");
            Assert.True(boost >= 0 && burn > boost);
            Assert.Equal(1, result.State.Active(Side.Player).Stages[(int)Stage.Speed]);

            var capped = F.Mon(3, status: Condition.Burn, ability: "speed-boost", stages: new[] { 0, 0, 0, 0, 6, 0, 0 });
            var capResult = F.Step(F.State(capped, F.Mon(4)));
            Assert.DoesNotContain(capResult.Events, e => e.Detail == "speed-boost");
            Assert.Equal(6, capResult.State.Active(Side.Player).Stages[(int)Stage.Speed]);
        }

        [Fact]
        public void ShedSkinUsesThirtyPercentCheckAndCanPreventStatusResidual()
        {
            BattleStep3? cured = null;
            for (uint seed = 1; seed < 1000 && cured == null; seed++)
            {
                var mon = F.Mon(1, status: Condition.Poison, ability: "shed-skin");
                var result = F.Step(F.State(mon, F.Mon(2), seed));
                if (result.Events.Any(e => e.Detail == "shed-skin-cured")) cured = result;
            }
            Assert.NotNull(cured);
            Assert.DoesNotContain(cured!.Events, e => e.Detail == "poison-residual" && e.Target == F.Id(1).ToString("N"));
            Assert.Equal(Condition.Healthy, cured.State.Active(Side.Player).Condition);
            Assert.Contains(cured.Events, e => e.Detail == "shed-skin" && e.Amount == 10 && e.Roll < 3);
        }

        [Fact]
        public void ShedSkinFailedRollPreservesStatusAndAllowsItsResidual()
        {
            BattleStep3? notCured = null;
            for (uint seed = 1; seed < 1000 && notCured == null; seed++)
            {
                var mon = F.Mon(5, status: Condition.Poison, ability: "shed-skin");
                var result = F.Step(F.State(mon, F.Mon(6), seed));
                if (result.Events.Any(e => e.Detail == "shed-skin" && e.Roll >= 3)) notCured = result;
            }
            Assert.NotNull(notCured);
            Assert.DoesNotContain(notCured!.Events, e => e.Detail == "shed-skin-cured");
            Assert.Contains(notCured.Events, e => e.Detail == "poison-residual" && e.Target == F.Id(5).ToString("N"));
            Assert.Equal(Condition.Poison, notCured.State.Active(Side.Player).Condition);
        }

        [Fact]
        public void RepresentedWeatherDamagePrecedesMajorStatusForOnePokemon()
        {
            var state = new AdvancedBattleState(new[] { F.Mon(7, status: Condition.Poison) }, new[] { F.Mon(8) }, 819, F.Id(615), weather: Weather.Sandstorm, weatherTurns: 2);
            var result = F.Step(state);
            int sand = result.Events.ToList().FindIndex(e => e.Detail == "sandstorm-residual" && e.Target == F.Id(7).ToString("N"));
            int poison = result.Events.ToList().FindIndex(e => e.Detail == "poison-residual" && e.Target == F.Id(7).ToString("N"));
            Assert.True(sand >= 0 && poison > sand);
        }

        [Fact]
        public void RainDishAndHydrationResolveInTheWeatherAbilityPhase()
        {
            var rainDish = F.Mon(1, ability: "rain-dish", status: Condition.Burn);
            var hurtRainDish = new AdvancedPokemon(rainDish.Record, rainDish.Moves, abilityId: "rain-dish", hp: rainDish.Hp - 20, condition: Condition.Burn);
            var state = new AdvancedBattleState(new[] { hurtRainDish }, new[] { F.Mon(2) }, 80, F.Id(605), weather: Weather.Rain, weatherTurns: 2);
            var result = F.Step(state);
            Assert.Contains(result.Events, e => e.Detail == "rain-dish-weather-heal");

            var hydration = F.Mon(3, ability: "hydration", status: Condition.Poison);
            var hydrationState = new AdvancedBattleState(new[] { hydration }, new[] { F.Mon(4) }, 81, F.Id(606), weather: Weather.Rain, weatherTurns: 2);
            var hydrationResult = F.Step(hydrationState);
            Assert.Contains(hydrationResult.Events, e => e.Detail == "hydration-cured");
            Assert.Equal(Condition.Healthy, hydrationResult.State.Active(Side.Player).Condition);
        }

        [Fact]
        public void WeatherImmunityAbilityAndIceBodyUseSourceGroundedResidualBranches()
        {
            var immune = F.State(F.Mon(1, ability: "sand-veil"), F.Mon(2), seed: 707);
            var sand = new AdvancedBattleState(immune.Team(Side.Player), immune.Team(Side.Opponent), 707, F.Id(607), weather: Weather.Sandstorm, weatherTurns: 2);
            Assert.DoesNotContain(F.Step(sand).Events, e => e.Detail == "sandstorm-residual" && e.Target == F.Id(1).ToString("N"));

            var iceMon = F.Mon(3, F.Ice, ability: "ice-body");
            var damaged = new AdvancedPokemon(iceMon.Record, iceMon.Moves, abilityId: "ice-body", hp: iceMon.Hp - 20);
            var hail = new AdvancedBattleState(new[] { damaged }, new[] { F.Mon(4) }, 708, F.Id(608), weather: Weather.Hail, weatherTurns: 2);
            var healed = F.Step(hail);
            Assert.Contains(healed.Events, e => e.Detail == "ice-body-weather-heal");
            Assert.DoesNotContain(healed.Events, e => e.Detail == "hail-residual" && e.Target == F.Id(3).ToString("N"));
        }

        [Fact]
        public void DrySkinHasDistinctRainAndSunBranchesAndWeatherExpiresAfterItsLastResidual()
        {
            var dry = F.Mon(5, ability: "dry-skin");
            var damagedDry = new AdvancedPokemon(dry.Record, dry.Moves, abilityId: "dry-skin", hp: dry.Hp - 20);
            var rain = new AdvancedBattleState(new[] { damagedDry }, new[] { F.Mon(6) }, 709, F.Id(611), weather: Weather.Rain, weatherTurns: 1);
            var rainResult = F.Step(rain);
            Assert.Contains(rainResult.Events, e => e.Detail == "dry-skin-weather-heal");
            Assert.Equal(Weather.Clear, rainResult.State.Weather);
            Assert.Contains(rainResult.Events, e => e.Kind == BattleEventKind.Weather && e.Detail == "cleared");

            var sun = new AdvancedBattleState(new[] { dry }, new[] { F.Mon(7) }, 710, F.Id(612), weather: Weather.Sun, weatherTurns: 2);
            Assert.Contains(F.Step(sun).Events, e => e.Detail == "dry-skin-sun-damage");

            var solarPower = F.Mon(8, ability: "solar-power");
            var solar = new AdvancedBattleState(new[] { solarPower }, new[] { F.Mon(9) }, 711, F.Id(613), weather: Weather.Sun, weatherTurns: 2);
            Assert.Contains(F.Step(solar).Events, e => e.Detail == "solar-power-sun-damage");

            var snowCloak = F.Mon(10, ability: "snow-cloak");
            var cloakState = new AdvancedBattleState(new[] { snowCloak }, new[] { F.Mon(11) }, 712, F.Id(614), weather: Weather.Hail, weatherTurns: 2);
            Assert.DoesNotContain(F.Step(cloakState).Events, e => e.Detail == "hail-residual" && e.Target == F.Id(10).ToString("N"));
        }

        [Fact]
        public void SandRushSpeedBoostIsNotSilentlyEnabledByTheHGSSRuleset()
        {
            var sandRush = F.Mon(1, ability: "sand-rush", moves: new[] { F.Hit() });
            var ordinary = F.Mon(2, moves: new[] { F.Hit() });
            uint seed = 1; string hgssOrder = string.Empty;
            for (; seed < 1000; seed++)
            {
                var candidate = new AdvancedBattleState(new[] { sandRush }, new[] { ordinary }, seed, F.Id(609), weather: Weather.Sandstorm, weatherTurns: 3);
                hgssOrder = F.Step(candidate).Events.Single(e => e.Kind == BattleEventKind.Order).Actor;
                if (hgssOrder == F.Id(2).ToString("N")) break;
            }
            Assert.True(seed < 1000, "A seeded equal-speed HGSS tie fixture should be discoverable.");
            var crossGeneration = new AdvancedBattleState(new[] { sandRush }, new[] { ordinary }, seed, F.Id(610), CompatibilityProfile.CrossGenerationCatalog, weather: Weather.Sandstorm, weatherTurns: 3);
            var crossOrder = F.Step(crossGeneration).Events.Single(e => e.Kind == BattleEventKind.Order).Actor;
            Assert.Equal(F.Id(1).ToString("N"), crossOrder);
        }

        [Fact]
        public void QuickFeetUsesGenFourStatusSpeedModifierInsteadOfParalysisPenalty()
        {
            var quickFeet = F.Mon(1, ability: "quick-feet", status: Condition.Paralysis, moves: new[] { F.Hit() });
            var ordinary = F.Mon(2, moves: new[] { F.Hit() });
            var state = F.State(quickFeet, ordinary, 911);
            var result = F.Step(state);
            Assert.Equal(F.Id(1).ToString("N"), result.Events.Single(e => e.Kind == BattleEventKind.Order).Actor);
        }
    }
}
