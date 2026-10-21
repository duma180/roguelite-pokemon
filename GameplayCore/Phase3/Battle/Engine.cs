using System;
using System.Collections.Generic;
using System.Linq;
using Roguelite.Gameplay;

namespace Roguelite.Gameplay.Phase3
{
    /// <summary>Explicit single-battle subset layered over the frozen Phase 1 arithmetic.</summary>
    public static class AdvancedBattleEngine
    {
        private static Side Other(Side x) => x == Side.Player ? Side.Opponent : Side.Player;
        private static string Id(AdvancedPokemon p) => p.Record.IndividualId.ToString("N");
        private static bool Has(AdvancedPokemon p, string id) => !p.AbilitySuppressed && p.AbilityId == id;
        private static bool WeatherEffective(AdvancedBattleState s) => !Has(s.Active(Side.Player), "air-lock") && !Has(s.Active(Side.Player), "cloud-nine") && !Has(s.Active(Side.Opponent), "air-lock") && !Has(s.Active(Side.Opponent), "cloud-nine");
        private static string EffectKey(BattleMoveEffect effect) => effect == BattleMoveEffect.Damage ? "damage" : effect == BattleMoveEffect.Pursuit ? "pursuit" : effect == BattleMoveEffect.RapidSpin ? "rapid-spin" : effect == BattleMoveEffect.Bind ? "binding" : effect == BattleMoveEffect.Ingrain || effect == BattleMoveEffect.AquaRing ? "ingrain-aqua-ring" : effect == BattleMoveEffect.HealBlock ? "heal-block" : effect == BattleMoveEffect.ForceSwitch ? "force-switch" : effect == BattleMoveEffect.Disable || effect == BattleMoveEffect.Encore || effect == BattleMoveEffect.Taunt || effect == BattleMoveEffect.Imprison ? "disable-encore-taunt-imprison" : effect == BattleMoveEffect.Struggle ? "struggle" : effect == BattleMoveEffect.Protect ? "protect" : effect == BattleMoveEffect.SetRain || effect == BattleMoveEffect.SetSun || effect == BattleMoveEffect.SetSandstorm || effect == BattleMoveEffect.SetHail ? "weather" : effect == BattleMoveEffect.SetSpikes || effect == BattleMoveEffect.SetToxicSpikes || effect == BattleMoveEffect.SetStealthRock ? "entry-hazards" : effect == BattleMoveEffect.SetReflect || effect == BattleMoveEffect.SetLightScreen ? "screens" : effect == BattleMoveEffect.InflictSleep ? "sleep" : effect == BattleMoveEffect.InflictFreeze ? "freeze" : effect == BattleMoveEffect.InflictPoison ? "poison" : effect == BattleMoveEffect.InflictBadPoison ? "bad-poison" : effect == BattleMoveEffect.InflictBurn || effect == BattleMoveEffect.InflictParalysis ? "burn-paralysis" : effect == BattleMoveEffect.LeechSeed ? "leech-seed" : "volatile";
        private static int Roll(BattleRandom r, int n, string purpose, AdvancedPokemon p, int turn, List<BattleEvent3> ev) { int x = r.Next(n); ev.Add(new BattleEvent3(turn, BattleEventKind.Random, Id(p), detail: purpose, amount: n, roll: x)); return x; }
        private static void Hurt(AdvancedPokemon p, int amount, AdvancedPokemon? source, string cause, int turn, List<BattleEvent3> ev, int roll = -1)
        {
            if (p.Hp <= 0) return; int dealt = Math.Min(p.Hp, Math.Max(0, amount)); p.Hp -= dealt; ev.Add(new BattleEvent3(turn, BattleEventKind.Damage, source == null ? "" : Id(source), Id(p), cause, dealt, roll));
            if (p.Hp == 0) ev.Add(new BattleEvent3(turn, BattleEventKind.Fainted, Id(p), detail: cause));
        }
        private static int Speed(AdvancedBattleState s, AdvancedPokemon p)
        {
            long speed = HgssRules.ApplyStage(HgssRules.CalculateStats(p.Record)[Stat.Speed], p.Stages[(int)Stage.Speed]);
            if (p.Condition != Condition.Healthy && Has(p, "quick-feet")) speed = speed * 3 / 2;
            else if (p.Condition == Condition.Paralysis) speed /= 4;
            if (WeatherEffective(s) && (s.Weather == Weather.Rain && Has(p, "swift-swim") || s.Weather == Weather.Sun && Has(p, "chlorophyll") || s.Ruleset == CompatibilityProfile.CrossGenerationCatalog && s.Weather == Weather.Sandstorm && Has(p, "sand-rush"))) speed = Math.Min(int.MaxValue, speed * 2);
            return (int)speed;
        }
        private static Side Faster(AdvancedBattleState s, BattleRandom rng, string purpose, List<BattleEvent3> ev)
        {
            int a = Speed(s, s.Active(Side.Player)), b = Speed(s, s.Active(Side.Opponent));
            return a == b ? Roll(rng, 2, purpose, s.Active(Side.Player), s.Turn, ev) == 0 ? Side.Player : Side.Opponent : a > b ? Side.Player : Side.Opponent;
        }
        private static void Validate(AdvancedBattleState s, Side side, BattleAction3 a)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (a.Kind == ActionKind.Switch) { if (a.Index < 0 || a.Index >= s.Team(side).Count) throw new ArgumentOutOfRangeException(nameof(a.Index)); if (a.Index == s.ActiveIndex(side) || s.Team(side)[a.Index].Hp == 0) throw new InvalidOperationException("Switch target is active or fainted."); if (s.Active(side).SwitchLocked || s.Active(side).BindingTurns > 0 || s.Active(side).Ingrained || TrappedByAbility(s, side)) throw new InvalidOperationException("Trapping prevents voluntary switching."); }
            else
            {
                var p = s.Active(side);
                if (a.Index < 0 || a.Index >= p.Moves.Count) throw new ArgumentOutOfRangeException(nameof(a.Index));
                if (!p.Pp.All(x => x == 0) && p.Pp[a.Index] == 0) throw new InvalidOperationException("Selected move has no PP; use another move or automatic Struggle.");
            }
        }
        private static string RestrictionReason(AdvancedPokemon p, AdvancedPokemon foe, BattleMove move)
        {
            if (p.DisableTurns > 0 && p.DisabledMoveId == move.Id) return "Disabled move cannot be selected.";
            if (p.TauntTurns > 0 && move.Category == Category.Status) return "Taunt prevents status moves.";
            if (p.EncoreTurns > 0 && p.EncoreMoveId != move.Id) return "Encore restricts move selection.";
            if (foe.Imprisoning && foe.Moves.Any(m => m.Id == move.Id)) return "Imprison prevents selecting a shared move.";
            return string.Empty;
        }
        public static BattleStep3 ReplaceFainted(AdvancedBattleState input, Side side, int index)
        {
            if (!input.NeedsReplacement(side)) throw new InvalidOperationException("No replacement is pending.");
            if (index < 0 || index >= input.Team(side).Count || index == input.ActiveIndex(side) || input.Team(side)[index].Hp == 0) throw new InvalidOperationException("Replacement must be a living non-active individual.");
            var s = input.DeepCopy(); var outgoing = s.Active(side); s.Switch(side, index); var events = new List<BattleEvent3> { new BattleEvent3(s.Turn, BattleEventKind.Switched, Id(s.Active(side)), Id(outgoing), "forced-replacement") };
            ApplyEntry(s, side, events); return new BattleStep3(s, events);
        }
        public static BattleStep3 ResolveTurn(AdvancedBattleState input, BattleAction3 player, BattleAction3 opponent)
        {
            if (input.Outcome != BattleOutcome.Ongoing || input.NeedsReplacement(Side.Player) || input.NeedsReplacement(Side.Opponent)) throw new InvalidOperationException("Battle ended or replacement is pending.");
            Validate(input, Side.Player, player); Validate(input, Side.Opponent, opponent);
            var s = input.DeepCopy(); s.Turn = checked(s.Turn + 1); var rng = new BattleRandom(s.RandomState, s.RandomDraws); var ev = new List<BattleEvent3>();
            foreach (var p in s.RawTeams.SelectMany(x => x)) { p.Protecting = false; p.FlinchTurns = 0; }
            ev.Add(new BattleEvent3(s.Turn, BattleEventKind.TurnStarted));
            string[] startingActors = { Id(s.Active(Side.Player)), Id(s.Active(Side.Opponent)) };
            // HGSS keeps a distinct speed-sorted battler order for end-of-turn processing;
            // it is not necessarily the priority-sorted move execution order.
            Side residualFirst = Faster(s, rng, "residual-speed-order-tie", ev);
            int Priority(Side side, BattleAction3 a) => a.Kind == ActionKind.Switch ? 6 : s.Active(side).Moves[a.Index].Priority;
            int pp = Priority(Side.Player, player), po = Priority(Side.Opponent, opponent);
            Side first = pp == po ? Faster(s, rng, "order-speed-tie", ev) : pp > po ? Side.Player : Side.Opponent;
            ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Order, Id(s.Active(first)), Id(s.Active(Other(first))), "priority-then-start-turn-speed"));
            bool pursuitInterception = player.Kind == ActionKind.Switch && opponent.Kind == ActionKind.Move && s.Active(Side.Opponent).Moves[opponent.Index].Effect == BattleMoveEffect.Pursuit
                || opponent.Kind == ActionKind.Switch && player.Kind == ActionKind.Move && s.Active(Side.Player).Moves[player.Index].Effect == BattleMoveEffect.Pursuit;
            if (pursuitInterception)
            {
                Side pursuer = player.Kind == ActionKind.Switch ? Side.Opponent : Side.Player;
                Side switching = Other(pursuer);
                var pursuitAction = pursuer == Side.Player ? player : opponent;
                var switchAction = switching == Side.Player ? player : opponent;
                Act(pursuer, pursuitAction, true);
                if (s.Active(switching).Hp > 0) Act(switching, switchAction);
            }
            else
            {
                Act(first, first == Side.Player ? player : opponent); if (s.Outcome == BattleOutcome.Ongoing) Act(Other(first), first == Side.Player ? opponent : player);
            }
            if (s.Outcome == BattleOutcome.Ongoing) EndTurn();
            foreach (Side side in new[] { Side.Player, Side.Opponent }) if (s.NeedsReplacement(side)) ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Replacement, Id(s.Active(side)), detail: side.ToString()));
            if (s.Outcome != BattleOutcome.Ongoing) ev.Add(new BattleEvent3(s.Turn, BattleEventKind.BattleEnded, detail: s.Outcome.ToString()));
            s.RandomState = rng.State; s.RandomDraws = rng.Draws; return new BattleStep3(s, ev);

            void Act(Side side, BattleAction3 action, bool pursuitBoost = false)
            {
                var a = s.Active(side); var t = s.Active(Other(side));
                if (a.Hp == 0) return;
                if (action.Kind == ActionKind.Switch) { int outgoing = s.ActiveIndex(side); foreach (var mon in s.Team(Other(side))) if (mon.InfatuatedBy == outgoing) mon.InfatuatedBy = -1; s.Switch(side, action.Index); ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Switched, Id(s.Active(side)), Id(s.Team(side)[outgoing]), "voluntary")); ApplyEntry(s, side, ev); return; }
                if (Id(a) != startingActors[(int)side] || s.NeedsReplacement(side)) return;
                if (t.Hp == 0) return;
                if (a.FlinchTurns > 0) { a.FlinchTurns = 0; ev.Add(new BattleEvent3(s.Turn, BattleEventKind.UnableToAct, Id(a), detail: "flinched")); a.ProtectChain = 0; return; }
                if (a.Condition == Condition.Sleep && (Has(a, "insomnia") || Has(a, "vital-spirit"))) { a.Condition = Condition.Healthy; a.ConditionCounter = 0; ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Status, Id(a), detail: "sleep-immunity-cured")); }
                if (a.Condition == Condition.Sleep)
                {
                    a.ConditionCounter = Math.Max(0, a.ConditionCounter - 1); ev.Add(new BattleEvent3(s.Turn, BattleEventKind.UnableToAct, Id(a), detail: "sleep")); a.ProtectChain = 0;
                    if (a.ConditionCounter == 0) { a.Condition = Condition.Healthy; ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Status, Id(a), detail: "woke-after-sleep")); }
                    return;
                }
                if (a.Condition == Condition.Freeze && Has(a, "magma-armor")) { a.Condition = Condition.Healthy; ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Status, Id(a), detail: "freeze-immunity-cured")); }
                if (a.Condition == Condition.Freeze)
                {
                    if (Roll(rng, 5, "freeze-thaw", a, s.Turn, ev) == 0) { a.Condition = Condition.Healthy; ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Status, Id(a), detail: "thawed")); }
                    else { ev.Add(new BattleEvent3(s.Turn, BattleEventKind.UnableToAct, Id(a), detail: "freeze")); a.ProtectChain = 0; return; }
                }
                if (a.ConfusionTurns > 0 && Has(a, "own-tempo")) { a.ConfusionTurns = 0; ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Volatile, Id(a), detail: "confusion-immunity-cured")); }
                if (a.ConfusionTurns > 0)
                {
                    a.ConfusionTurns--; if (Roll(rng, 2, "confusion-self-hit", a, s.Turn, ev) == 0)
                    { int range = 100 - Roll(rng, 16, "confusion-damage-range", a, s.Turn, ev); int damage = SelfDamage(a, range); Hurt(a, damage, a, "confusion-self-hit", s.Turn, ev, range); ev.Add(new BattleEvent3(s.Turn, BattleEventKind.UnableToAct, Id(a), detail: "confusion")); a.ProtectChain = 0; return; }
                    if (a.ConfusionTurns == 0) ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Volatile, Id(a), detail: "confusion-ended"));
                }
                if (a.InfatuatedBy >= 0 && Has(a, "oblivious")) { a.InfatuatedBy = -1; ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Volatile, Id(a), detail: "infatuation-immunity-cured")); }
                if (a.InfatuatedBy >= 0 && Roll(rng, 2, "infatuation", a, s.Turn, ev) == 0) { ev.Add(new BattleEvent3(s.Turn, BattleEventKind.UnableToAct, Id(a), detail: "infatuation")); a.ProtectChain = 0; return; }
                if (a.Condition == Condition.Paralysis && Roll(rng, 4, "full-paralysis", a, s.Turn, ev) == 0) { ev.Add(new BattleEvent3(s.Turn, BattleEventKind.UnableToAct, Id(a), detail: "full-paralysis")); a.ProtectChain = 0; return; }
                bool struggle = a.Pp.All(x => x == 0); BattleMove move = struggle ? Struggle : a.Moves[action.Index]; int slot = struggle ? -1 : action.Index;
                if (!struggle && a.Pp[slot] == 0) throw new InvalidOperationException("Selected move has no PP; use another move or automatic Struggle.");
                if (!struggle)
                {
                    string restriction = RestrictionReason(a, t, move);
                    if (restriction.Length != 0) { ev.Add(new BattleEvent3(s.Turn, BattleEventKind.MoveFailed, Id(a), Id(t), "restriction:" + restriction)); a.ProtectChain = 0; return; }
                }
                EffectRegistry.Foundational.Require(EffectKey(move.Effect), s.Ruleset);
                if (!struggle)
                {
                    a.LastMoveId = move.Id;
                    int pressureCost = 1 + (Has(t, "pressure") ? 1 : 0);
                    a.RawPp[slot] = Math.Max(0, a.RawPp[slot] - pressureCost);
                    ev.Add(new BattleEvent3(s.Turn, BattleEventKind.MoveUsed, Id(a), Id(t), move.Id, pressureCost));
                }
                else ev.Add(new BattleEvent3(s.Turn, BattleEventKind.MoveUsed, Id(a), Id(t), "struggle"));
                if (move.Effect == BattleMoveEffect.Protect)
                {
                    bool success = true; int baseChance = s.Ruleset == CompatibilityProfile.Hgss ? 2 : 3; int cap = s.Ruleset == CompatibilityProfile.Hgss ? 3 : 6;
                    for (int i = 0; i < Math.Min(a.ProtectChain, cap) && success; i++) success = Roll(rng, baseChance, "protect-success-chain", a, s.Turn, ev) == 0;
                    if (success) { a.ProtectChain = Math.Min(cap, a.ProtectChain + 1); a.Protecting = true; ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Protected, Id(a), detail: "protect")); }
                    else { a.ProtectChain = 0; a.Protecting = false; ev.Add(new BattleEvent3(s.Turn, BattleEventKind.MoveFailed, Id(a), detail: "protect-failed")); }
                    return;
                }
                a.Protecting = false;
                if (a.Condition == Condition.Healthy || a.Condition == Condition.Burn || a.Condition == Condition.Paralysis || a.Condition == Condition.Poison || a.Condition == Condition.BadPoison) a.ProtectChain = 0;
                int threshold = HgssRules.AccuracyThreshold(move.Accuracy, a.Stages[(int)Stage.Accuracy], t.Stages[(int)Stage.Evasion]);
                if (move.Accuracy > 0 && Roll(rng, 100, "accuracy", a, s.Turn, ev) >= threshold) { ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Missed, Id(a), Id(t), move.Id)); return; }
                if (t.Hp == 0) return;
                if (IsProtected(t) && (move.Category != Category.Status || TargetsOpponent(move.Effect))) { ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Protected, Id(a), Id(t), move.Id)); return; }
                if (move.Category == Category.Status) { ExecuteStatus(a, t, move, side); return; }
                if (move.Effect != BattleMoveEffect.Struggle && move.Type == Element.Ground && Has(t, "levitate")) { ev.Add(new BattleEvent3(s.Turn, BattleEventKind.MoveFailed, Id(a), Id(t), "levitate-immunity")); return; }
                if (move.Effect != BattleMoveEffect.Struggle && (TypeChart.HalfUnits(move.Type, t.Record.Species.Type1) == 0 || t.Record.Species.Type2.HasValue && TypeChart.HalfUnits(move.Type, t.Record.Species.Type2.Value) == 0)) { ev.Add(new BattleEvent3(s.Turn, BattleEventKind.MoveFailed, Id(a), Id(t), "type-immunity")); return; }
                bool crit = Roll(rng, HgssRules.CriticalDenominator(move.CriticalStage), "critical", a, s.Turn, ev) == 0;
                if (crit) ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Random, Id(a), Id(t), "critical-hit", HgssRules.CriticalDenominator(move.CriticalStage), 0));
                int random = 100 - Roll(rng, 16, "damage-range", a, s.Turn, ev); int dmg = Damage(a, t, move, crit, random, s.Weather, WeatherEffective(s), s.SideCondition(Other(side)), pursuitBoost);
                Hurt(t, dmg, a, struggle ? "struggle" : move.Id, s.Turn, ev, random);
                if (move.Effect == BattleMoveEffect.RapidSpin && dmg > 0)
                {
                    var own = s.SideCondition(side);
                    // The HGSS command returns after clearing the first applicable
                    // condition in this order: binding, Leech Seed, Spikes,
                    // Toxic Spikes, then Stealth Rock.
                    if (a.BindingTurns > 0) { ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Volatile, Id(a), detail: "rapid-spin-cleared-binding", amount: a.BindingTurns)); a.BindingTurns = 0; a.BindingSourceSide = -1; a.BindingSourceIndividualId = Guid.Empty; a.BindingMoveId = string.Empty; a.SwitchLocked = false; }
                    else if (a.LeechSeedSourceSide >= 0) { a.LeechSeedSourceSide = -1; ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Volatile, Id(a), detail: "rapid-spin-cleared-leech-seed")); }
                    else if (own.SpikesLayers > 0) { own.SpikesLayers = 0; ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Hazard, Id(a), detail: "rapid-spin-cleared-spikes")); }
                    else if (own.ToxicSpikesLayers > 0) { own.ToxicSpikesLayers = 0; ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Hazard, Id(a), detail: "rapid-spin-cleared-toxic-spikes")); }
                    else if (own.StealthRock) { own.StealthRock = false; ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Hazard, Id(a), detail: "rapid-spin-cleared-stealth-rock")); }
                }
                if (struggle && a.Hp > 0) Hurt(a, Math.Max(1, HgssRules.CalculateStats(a.Record)[Stat.Hp] / 4), a, "struggle-recoil", s.Turn, ev);
                if (move.EffectChance > 0 && t.Hp > 0 && Roll(rng, 100, "secondary-effect", a, s.Turn, ev) < move.EffectChance) ApplySecondary(a, t, move, side);
                if (t.Hp > 0 && t.Condition == Condition.Freeze && move.Type == Element.Fire) { t.Condition = Condition.Healthy; t.ConditionCounter = 0; ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Status, Id(a), Id(t), "thawed-by-fire-move")); }
            }
            void ExecuteStatus(AdvancedPokemon a, AdvancedPokemon t, BattleMove move, Side side)
            {
                switch (move.Effect)
                {
                    case BattleMoveEffect.InflictSleep: ApplyCondition(t, move.Effect, 1 + Roll(rng, 3, "sleep-duration", a, s.Turn, ev), a, ev); break;
                    case BattleMoveEffect.InflictFreeze: ApplyCondition(t, move.Effect, 0, a, ev); break;
                    case BattleMoveEffect.InflictPoison: case BattleMoveEffect.InflictBadPoison: case BattleMoveEffect.InflictBurn: case BattleMoveEffect.InflictParalysis: ApplyCondition(t, move.Effect, 0, a, ev); break;
                    case BattleMoveEffect.LeechSeed:
                        if (t.Record.Species.HasType(Element.Grass)) ev.Add(new BattleEvent3(s.Turn, BattleEventKind.MoveFailed, Id(a), Id(t), "type-immunity"));
                        else if (t.LeechSeedSourceSide >= 0) ev.Add(new BattleEvent3(s.Turn, BattleEventKind.MoveFailed, Id(a), Id(t), "already-seeded"));
                        else { t.LeechSeedSourceSide = (int)side; ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Volatile, Id(a), Id(t), "leech-seed")); }
                        break;
                    case BattleMoveEffect.Ingrain:
                        if (a.Ingrained) ev.Add(new BattleEvent3(s.Turn, BattleEventKind.MoveFailed, Id(a), detail: "ingrain-already-active"));
                        else { a.Ingrained = true; ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Volatile, Id(a), detail: "ingrain")); }
                        break;
                    case BattleMoveEffect.AquaRing:
                        if (a.AquaRing) ev.Add(new BattleEvent3(s.Turn, BattleEventKind.MoveFailed, Id(a), detail: "aqua-ring-already-active"));
                        else { a.AquaRing = true; ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Volatile, Id(a), detail: "aqua-ring")); }
                        break;
                    case BattleMoveEffect.HealBlock:
                        if (t.HealBlockTurns > 0) ev.Add(new BattleEvent3(s.Turn, BattleEventKind.MoveFailed, Id(a), Id(t), "heal-block-already-active"));
                        else { t.HealBlockTurns = 5; ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Volatile, Id(a), Id(t), "heal-block", 5)); }
                        break;
                    case BattleMoveEffect.SetRain: case BattleMoveEffect.SetSun: case BattleMoveEffect.SetSandstorm: case BattleMoveEffect.SetHail:
                        s.Weather = move.Effect == BattleMoveEffect.SetRain ? Weather.Rain : move.Effect == BattleMoveEffect.SetSun ? Weather.Sun : move.Effect == BattleMoveEffect.SetSandstorm ? Weather.Sandstorm : Weather.Hail;
                        s.WeatherTurns = 5; ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Weather, Id(a), detail: s.Weather.ToString(), amount: 5)); break;
                    case BattleMoveEffect.SetSpikes: case BattleMoveEffect.SetToxicSpikes: case BattleMoveEffect.SetStealthRock:
                        var c = s.SideCondition(Other(side)); if (move.Effect == BattleMoveEffect.SetSpikes && c.SpikesLayers < 3) c.SpikesLayers++; else if (move.Effect == BattleMoveEffect.SetToxicSpikes && c.ToxicSpikesLayers < 2) c.ToxicSpikesLayers++; else if (move.Effect == BattleMoveEffect.SetStealthRock && !c.StealthRock) c.StealthRock = true; else { ev.Add(new BattleEvent3(s.Turn, BattleEventKind.MoveFailed, Id(a), detail: "hazard-already-maxed")); break; }
                        ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Hazard, Id(a), detail: move.Effect.ToString())); break;
                    case BattleMoveEffect.SetReflect: case BattleMoveEffect.SetLightScreen:
                        var sideConditions = s.SideCondition(side); bool isReflect = move.Effect == BattleMoveEffect.SetReflect; if (isReflect ? sideConditions.ReflectTurns > 0 : sideConditions.LightScreenTurns > 0) ev.Add(new BattleEvent3(s.Turn, BattleEventKind.MoveFailed, Id(a), detail: "screen-already-active")); else { if (isReflect) sideConditions.ReflectTurns = 5; else sideConditions.LightScreenTurns = 5; ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Screen, Id(a), detail: move.Effect.ToString(), amount: 5)); } break;
                    case BattleMoveEffect.Disable:
                        int disabledSlot = IndexOf(t, t.LastMoveId);
                        if (t.DisableTurns > 0 || disabledSlot < 0 || t.Pp[disabledSlot] == 0) ev.Add(new BattleEvent3(s.Turn, BattleEventKind.MoveFailed, Id(a), Id(t), "disable-no-eligible-last-move"));
                        else { t.DisabledMoveId = t.LastMoveId; t.DisableTurns = 3 + Roll(rng, 4, "disable-duration", a, s.Turn, ev); ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Volatile, Id(a), Id(t), "disable:" + t.DisabledMoveId, t.DisableTurns)); } break;
                    case BattleMoveEffect.Encore:
                        int encoredSlot = IndexOf(t, t.LastMoveId);
                        if (t.EncoreTurns > 0 || encoredSlot < 0 || t.Pp[encoredSlot] == 0 || new[] { "transform", "mimic", "sketch", "mirror-move", "encore", "struggle" }.Contains(t.LastMoveId)) ev.Add(new BattleEvent3(s.Turn, BattleEventKind.MoveFailed, Id(a), Id(t), "encore-no-eligible-last-move"));
                        else { t.EncoreMoveId = t.LastMoveId; t.EncoreTurns = 3 + Roll(rng, 5, "encore-duration", a, s.Turn, ev); ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Volatile, Id(a), Id(t), "encore:" + t.EncoreMoveId, t.EncoreTurns)); } break;
                    case BattleMoveEffect.Taunt:
                        if (t.TauntTurns > 0) ev.Add(new BattleEvent3(s.Turn, BattleEventKind.MoveFailed, Id(a), Id(t), "taunt-already-active"));
                        else { t.TauntTurns = 3; ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Volatile, Id(a), Id(t), "taunt", 3)); } break;
                    case BattleMoveEffect.Imprison:
                        bool sharesMove = a.Moves.Any(ownMove => t.Moves.Any(targetMove => ownMove.Id == targetMove.Id));
                        if (a.Imprisoning || !sharesMove) ev.Add(new BattleEvent3(s.Turn, BattleEventKind.MoveFailed, Id(a), Id(t), a.Imprisoning ? "imprison-already-active" : "imprison-no-shared-move"));
                        else { a.Imprisoning = true; ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Volatile, Id(a), Id(t), "imprison")); } break;
                    case BattleMoveEffect.ForceSwitch:
                        var reserves = s.Team(Other(side)).Select((p, i) => new { p, i }).Where(x => x.i != s.ActiveIndex(Other(side)) && x.p.Hp > 0).ToArray();
                        if (reserves.Length == 0 || Has(t, "suction-cups") || t.BindingTurns > 0 || t.Ingrained || t.SwitchLocked) ev.Add(new BattleEvent3(s.Turn, BattleEventKind.MoveFailed, Id(a), Id(t), Has(t, "suction-cups") ? "suction-cups-blocked" : reserves.Length == 0 ? "no-living-reserve" : "trapping-prevents-forced-switch"));
                        else ev.Add(new BattleEvent3(s.Turn, BattleEventKind.MoveFailed, Id(a), Id(t), "replacement-selection-policy-not-supplied")); break;
                    case BattleMoveEffect.Confuse: if (t.ConfusionTurns > 0 || Has(t, "own-tempo")) ev.Add(new BattleEvent3(s.Turn, BattleEventKind.MoveFailed, Id(a), detail: Has(t, "own-tempo") ? "ability-immunity" : "already-confused")); else { t.ConfusionTurns = 2 + Roll(rng, 4, "confusion-duration", a, s.Turn, ev); ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Volatile, Id(a), Id(t), "confusion", t.ConfusionTurns)); } break;
                    case BattleMoveEffect.Flinch: t.FlinchTurns = 1; ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Volatile, Id(a), Id(t), "flinch", 1)); break;
                    case BattleMoveEffect.Infatuate: if (a.IsGenderless || t.IsGenderless || a.IsMale == t.IsMale || Has(t, "oblivious")) ev.Add(new BattleEvent3(s.Turn, BattleEventKind.MoveFailed, Id(a), detail: Has(t, "oblivious") ? "ability-immunity" : "infatuation-incompatible")); else { t.InfatuatedBy = s.ActiveIndex(side); ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Volatile, Id(a), Id(t), "infatuation")); } break;
                    case BattleMoveEffect.TrapSwitch: if (t.SwitchLocked) ev.Add(new BattleEvent3(s.Turn, BattleEventKind.MoveFailed, Id(a), detail: "already-trapped")); else { t.SwitchLocked = true; ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Volatile, Id(a), Id(t), "switch-locked")); } break;
                    default: throw new NotSupportedException("Unimplemented status effect: " + move.Effect);
                }
            }
            void ApplyCondition(AdvancedPokemon t, BattleMoveEffect effect, int duration, AdvancedPokemon source, List<BattleEvent3> targetEvents)
            {
                if (t.Condition != Condition.Healthy) { targetEvents.Add(new BattleEvent3(s.Turn, BattleEventKind.MoveFailed, Id(source), Id(t), "already-statused")); return; }
                Condition next = effect == BattleMoveEffect.InflictSleep ? Condition.Sleep : effect == BattleMoveEffect.InflictFreeze ? Condition.Freeze : effect == BattleMoveEffect.InflictPoison ? Condition.Poison : effect == BattleMoveEffect.InflictBadPoison ? Condition.BadPoison : effect == BattleMoveEffect.InflictBurn ? Condition.Burn : Condition.Paralysis;
                if (next == Condition.Freeze && t.Record.Species.HasType(Element.Ice) || (next == Condition.Poison || next == Condition.BadPoison) && (t.Record.Species.HasType(Element.Poison) || t.Record.Species.HasType(Element.Steel)) || next == Condition.Burn && t.Record.Species.HasType(Element.Fire)) { targetEvents.Add(new BattleEvent3(s.Turn, BattleEventKind.MoveFailed, Id(source), Id(t), "type-immunity")); return; }
                if (next == Condition.Sleep && (Has(t, "insomnia") || Has(t, "vital-spirit")) || next == Condition.Freeze && Has(t, "magma-armor") || (next == Condition.Poison || next == Condition.BadPoison) && Has(t, "immunity") || next == Condition.Burn && Has(t, "water-veil") || next == Condition.Paralysis && Has(t, "limber")) { targetEvents.Add(new BattleEvent3(s.Turn, BattleEventKind.MoveFailed, Id(source), Id(t), "ability-immunity")); return; }
                t.Condition = next; t.ConditionCounter = duration; t.ToxicCounter = next == Condition.BadPoison ? 1 : 0; targetEvents.Add(new BattleEvent3(s.Turn, BattleEventKind.Status, Id(source), Id(t), next.ToString(), duration));
            }
            void ApplySecondary(AdvancedPokemon a, AdvancedPokemon t, BattleMove move, Side side)
            {
                switch (move.Effect)
                {
                    case BattleMoveEffect.Bind:
                        if (t.BindingTurns > 0) { ev.Add(new BattleEvent3(s.Turn, BattleEventKind.MoveFailed, Id(a), Id(t), "already-bound")); break; }
                        t.BindingTurns = 2 + Roll(rng, 4, "binding-duration", a, s.Turn, ev); t.BindingSourceSide = (int)side; t.BindingSourceIndividualId = a.Record.IndividualId; t.BindingMoveId = move.Id; t.SwitchLocked = true;
                        ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Volatile, Id(a), Id(t), "binding:" + move.Id, t.BindingTurns)); break;
                    case BattleMoveEffect.InflictSleep: ApplyCondition(t, move.Effect, 1 + Roll(rng, 3, "sleep-duration", a, s.Turn, ev), a, ev); break;
                    case BattleMoveEffect.InflictFreeze: case BattleMoveEffect.InflictPoison: case BattleMoveEffect.InflictBadPoison: case BattleMoveEffect.InflictBurn: case BattleMoveEffect.InflictParalysis: ApplyCondition(t, move.Effect, 0, a, ev); break;
                    case BattleMoveEffect.Confuse: if (t.ConfusionTurns == 0 && !Has(t, "own-tempo")) { t.ConfusionTurns = 2 + Roll(rng, 4, "confusion-duration", a, s.Turn, ev); ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Volatile, Id(a), Id(t), "confusion", t.ConfusionTurns)); } break;
                    case BattleMoveEffect.Flinch: t.FlinchTurns = 1; ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Volatile, Id(a), Id(t), "flinch", 1)); break;
                    case BattleMoveEffect.Infatuate: if (!a.IsGenderless && !t.IsGenderless && a.IsMale != t.IsMale && !Has(t, "oblivious")) { t.InfatuatedBy = s.ActiveIndex(side); ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Volatile, Id(a), Id(t), "infatuation")); } break;
                    case BattleMoveEffect.TrapSwitch: t.SwitchLocked = true; ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Volatile, Id(a), Id(t), "switch-locked")); break;
                }
            }
            void EndTurn()
            {
                // The cartridge updates side conditions before individual end-turn states.
                foreach (Side side in new[] { Side.Player, Side.Opponent })
                {
                    var c = s.SideCondition(side);
                    if (c.ReflectTurns > 0 && --c.ReflectTurns == 0) ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Screen, detail: side + ":reflect-ended"));
                    if (c.LightScreenTurns > 0 && --c.LightScreenTurns == 0) ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Screen, detail: side + ":light-screen-ended"));
                }
                foreach (Side side in new[] { residualFirst, Other(residualFirst) })
                {
                    var p = s.Active(side); if (p.Hp == 0) continue;
                    int maxHp = HgssRules.CalculateStats(p.Record)[Stat.Hp];

                    // HGSS UpdateMonCondition processes ability effects before major-status damage.
                    if (Has(p, "speed-boost") && p.RawStages[(int)Stage.Speed] < 6)
                    {
                        p.RawStages[(int)Stage.Speed]++;
                        ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Volatile, Id(p), detail: "speed-boost", amount: p.RawStages[(int)Stage.Speed]));
                    }
                    if (p.Condition != Condition.Healthy && Has(p, "shed-skin") && Roll(rng, 10, "shed-skin", p, s.Turn, ev) < 3)
                    {
                        p.Condition = Condition.Healthy; p.ConditionCounter = 0; p.ToxicCounter = 0;
                        ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Status, Id(p), detail: "shed-skin-cured"));
                    }

                    if (p.Hp > 0 && p.Ingrained && p.HealBlockTurns == 0) Heal(p, Math.Max(1, maxHp / 16) * (p.HeldItemId == "big-root" ? 130 : 100) / 100, "ingrain-heal");
                    if (p.Hp > 0 && p.AquaRing && p.HealBlockTurns == 0) Heal(p, Math.Max(1, maxHp / 16) * (p.HeldItemId == "big-root" ? 130 : 100) / 100, "aqua-ring-heal");
                    if (p.Hp > 0 && WeatherEffective(s)) ApplyWeatherTurnEffect(p, maxHp);
                    if (p.Hp > 0 && p.LeechSeedSourceSide >= 0 && p.LeechSeedSourceSide != (int)side && !Has(p, "magic-guard"))
                    {
                        Side sourceSide = (Side)p.LeechSeedSourceSide;
                        var source = s.Active(sourceSide);
                        if (source.Hp > 0)
                        {
                            int before = p.Hp;
                            Hurt(p, Math.Max(1, maxHp / 8), null, "leech-seed-drain", s.Turn, ev);
                            int drained = before - p.Hp;
                            if (drained > 0)
                            {
                                if (Has(p, "liquid-ooze"))
                                {
                                    if (!Has(source, "magic-guard")) Hurt(source, drained, p, "liquid-ooze", s.Turn, ev);
                                    ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Volatile, Id(p), Id(source), "leech-seed-liquid-ooze", drained));
                                }
                                else if (source.HealBlockTurns == 0) Heal(source, drained * (source.HeldItemId == "big-root" ? 130 : 100) / 100, "leech-seed-heal");
                            }
                        }
                    }
                    if (p.Hp > 0)
                    {
                        int amount = p.Condition == Condition.BadPoison ? Math.Max(1, maxHp * Math.Max(1, p.ToxicCounter) / 16) : p.Condition == Condition.Poison || p.Condition == Condition.Burn ? Math.Max(1, maxHp / 8) : 0;
                        if (amount > 0 && !Has(p, "magic-guard"))
                        {
                            Hurt(p, amount, null, p.Condition.ToString().ToLowerInvariant() + "-residual", s.Turn, ev);
                            if (p.Condition == Condition.BadPoison) p.ToxicCounter = Math.Min(15, p.ToxicCounter + 1);
                        }
                    }
                    if (p.BindingTurns > 0 && p.Hp > 0)
                    {
                        p.BindingTurns--;
                        if (p.BindingTurns == 0) { p.BindingSourceSide = -1; p.BindingSourceIndividualId = Guid.Empty; p.BindingMoveId = string.Empty; p.SwitchLocked = false; ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Volatile, Id(p), detail: "binding-ended")); }
                        else if (!Has(p, "magic-guard")) Hurt(p, Math.Max(1, maxHp / 16), s.Active((Side)p.BindingSourceSide), "binding-residual:" + p.BindingMoveId, s.Turn, ev);
                    }
                    if (p.DisableTurns > 0 && --p.DisableTurns == 0) { ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Volatile, Id(p), detail: "disable-ended:" + p.DisabledMoveId)); p.DisabledMoveId = string.Empty; }
                    if (p.EncoreTurns > 0 && --p.EncoreTurns == 0) { ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Volatile, Id(p), detail: "encore-ended:" + p.EncoreMoveId)); p.EncoreMoveId = string.Empty; }
                    if (p.TauntTurns > 0 && --p.TauntTurns == 0) ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Volatile, Id(p), detail: "taunt-ended"));
                    if (p.HealBlockTurns > 0 && --p.HealBlockTurns == 0) ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Volatile, Id(p), detail: "heal-block-ended"));
                }
                if (s.WeatherTurns > 0 && --s.WeatherTurns == 0) { s.Weather = Weather.Clear; ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Weather, detail: "cleared")); }
                foreach (Side side in new[] { Side.Player, Side.Opponent }) foreach (var p in s.Team(side)) if (p.InfatuatedBy >= 0 && s.Team(Other(side))[p.InfatuatedBy].Hp == 0) p.InfatuatedBy = -1;
            }

            void ApplyWeatherTurnEffect(AdvancedPokemon p, int maxHp)
            {
                bool rock = p.Record.Species.HasType(Element.Rock), ground = p.Record.Species.HasType(Element.Ground), steel = p.Record.Species.HasType(Element.Steel), ice = p.Record.Species.HasType(Element.Ice);
                if (s.Weather == Weather.Sandstorm)
                {
                    if (!Has(p, "magic-guard") && !rock && !ground && !steel && !Has(p, "sand-veil")) Hurt(p, Math.Max(1, maxHp / 16), null, "sandstorm-residual", s.Turn, ev);
                }
                else if (s.Weather == Weather.Hail)
                {
                    if (Has(p, "ice-body")) Heal(p, Math.Max(1, maxHp / 16), "ice-body-weather-heal");
                    else if (!Has(p, "magic-guard") && !ice && !Has(p, "snow-cloak")) Hurt(p, Math.Max(1, maxHp / 16), null, "hail-residual", s.Turn, ev);
                }
                else if (s.Weather == Weather.Rain)
                {
                    if (Has(p, "rain-dish")) Heal(p, Math.Max(1, maxHp / 16), "rain-dish-weather-heal");
                    else if (Has(p, "dry-skin")) Heal(p, Math.Max(1, maxHp / 8), "dry-skin-weather-heal");
                    if (Has(p, "hydration") && p.Condition != Condition.Healthy)
                    {
                        p.Condition = Condition.Healthy; p.ConditionCounter = 0; p.ToxicCounter = 0;
                        ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Status, Id(p), detail: "hydration-cured"));
                    }
                }
                else if (s.Weather == Weather.Sun)
                {
                    if (!Has(p, "magic-guard") && Has(p, "dry-skin")) Hurt(p, Math.Max(1, maxHp / 8), null, "dry-skin-sun-damage", s.Turn, ev);
                    else if (!Has(p, "magic-guard") && Has(p, "solar-power")) Hurt(p, Math.Max(1, maxHp / 8), null, "solar-power-sun-damage", s.Turn, ev);
                }
            }
            void Heal(AdvancedPokemon p, int amount, string cause)
            {
                if (p.Hp <= 0) return; int restored = Math.Min(amount, HgssRules.CalculateStats(p.Record)[Stat.Hp] - p.Hp); if (restored <= 0) return;
                p.Hp += restored; ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Status, Id(p), detail: cause, amount: restored));
            }
        }
        private static int SelfDamage(AdvancedPokemon p, int randomPercent)
        {
            var stats = HgssRules.CalculateStats(p.Record); int attack = HgssRules.ApplyStage(stats[Stat.Attack], p.Stages[(int)Stage.Attack]); int defense = HgssRules.ApplyStage(stats[Stat.Defense], p.Stages[(int)Stage.Defense]);
            return HgssRules.Damage(p.Record.Level, 40, attack, defense, p.Condition == Condition.Burn, false, randomPercent, false, Element.Normal, Element.Normal, Element.Normal);
        }
        private static int Damage(AdvancedPokemon a, AdvancedPokemon t, BattleMove move, bool critical, int random, Weather weather, bool weatherEffective, SideConditions targetSide, bool pursuitBoost = false)
        {
            var stat = HgssRules.CalculateStats(a.Record); var def = HgssRules.CalculateStats(t.Record);
            bool physical = move.Category == Category.Physical; int attackStage = a.Stages[physical ? (int)Stage.Attack : (int)Stage.SpecialAttack], defenseStage = t.Stages[physical ? (int)Stage.Defense : (int)Stage.SpecialDefense];
            if (critical) { attackStage = Math.Max(0, attackStage); defenseStage = Math.Min(0, defenseStage); }
            int attack = HgssRules.ApplyStage(stat[physical ? Stat.Attack : Stat.SpecialAttack], attackStage);
            int defense = HgssRules.ApplyStage(def[physical ? Stat.Defense : Stat.SpecialDefense], defenseStage);
            if (move.Effect == BattleMoveEffect.Struggle) return HgssRules.Damage(a.Record.Level, 50, attack, defense, a.Condition == Condition.Burn, critical, random, false, Element.Normal, Element.Normal, null);
            if (!physical && weatherEffective && weather == Weather.Sandstorm && t.Record.Species.HasType(Element.Rock)) defense = checked(defense * 3 / 2);
            int power = move.Power * (pursuitBoost ? 2 : 1); if (weatherEffective && weather == Weather.Rain && move.Type == Element.Water) power = power * 3 / 2; else if (weatherEffective && weather == Weather.Rain && move.Type == Element.Fire) power /= 2;
            if (weatherEffective && weather == Weather.Sun && move.Type == Element.Fire) power = power * 3 / 2; else if (weatherEffective && weather == Weather.Sun && move.Type == Element.Water) power /= 2;
            if (power == 0) return 0;
                bool boost = (Has(a, "overgrow") && move.Type == Element.Grass || Has(a, "blaze") && move.Type == Element.Fire || Has(a, "torrent") && move.Type == Element.Water) && a.Hp <= HgssRules.CalculateStats(a.Record)[Stat.Hp] / 3;
            if (boost) power = power * 3 / 2;
            int damage = HgssRules.Damage(a.Record.Level, power, attack, defense, a.Condition == Condition.Burn && physical, critical, random, a.Record.Species.HasType(move.Type), move.Type, t.Record.Species.Type1, t.Record.Species.Type2);
            if (!critical && (physical && targetSide.ReflectTurns > 0 || !physical && targetSide.LightScreenTurns > 0)) damage = Math.Max(1, damage / 2);
            return damage;
        }
        private static readonly BattleMove Struggle = new BattleMove("struggle", Element.Normal, Category.Physical, 50, 0, 1, BattleMoveEffect.Struggle);
        private static bool IsProtected(AdvancedPokemon p) => p.Protecting;
        private static int IndexOf(AdvancedPokemon p, string id) { for (int i = 0; i < p.Moves.Count; i++) if (p.Moves[i].Id == id) return i; return -1; }
        private static bool TargetsOpponent(BattleMoveEffect effect) => effect == BattleMoveEffect.InflictSleep || effect == BattleMoveEffect.InflictFreeze || effect == BattleMoveEffect.InflictPoison || effect == BattleMoveEffect.InflictBadPoison || effect == BattleMoveEffect.InflictBurn || effect == BattleMoveEffect.InflictParalysis || effect == BattleMoveEffect.LeechSeed || effect == BattleMoveEffect.HealBlock || effect == BattleMoveEffect.Confuse || effect == BattleMoveEffect.Flinch || effect == BattleMoveEffect.Infatuate || effect == BattleMoveEffect.TrapSwitch || effect == BattleMoveEffect.Disable || effect == BattleMoveEffect.Encore || effect == BattleMoveEffect.Taunt || effect == BattleMoveEffect.ForceSwitch;
        private static void ApplyEntry(AdvancedBattleState s, Side side, List<BattleEvent3> ev)
        {
            var p = s.Active(side); if (p.Hp == 0) return; int max = HgssRules.CalculateStats(p.Record)[Stat.Hp]; var c = s.SideCondition(side);
            if (!Has(p, "magic-guard") && c.StealthRock) { int h = TypeChart.HalfUnits(Element.Rock, p.Record.Species.Type1); if (p.Record.Species.Type2.HasValue) h = h * TypeChart.HalfUnits(Element.Rock, p.Record.Species.Type2.Value) / 2; if (h > 0) Hurt(p, Math.Max(1, max * h / 16), null, "stealth-rock", s.Turn, ev); }
            if (p.Hp > 0 && !Has(p, "magic-guard") && c.SpikesLayers > 0 && !p.Record.Species.HasType(Element.Flying) && !Has(p, "levitate")) { int denom = c.SpikesLayers == 1 ? 8 : c.SpikesLayers == 2 ? 6 : 4; Hurt(p, Math.Max(1, max / denom), null, "spikes", s.Turn, ev); }
            if (p.Hp > 0 && c.ToxicSpikesLayers > 0)
            {
                bool grounded = !p.Record.Species.HasType(Element.Flying) && !Has(p, "levitate");
                if (grounded && p.Record.Species.HasType(Element.Poison)) { c.ToxicSpikesLayers = 0; ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Hazard, Id(p), detail: "toxic-spikes-absorbed")); }
                else if (grounded && !p.Record.Species.HasType(Element.Steel) && p.Condition == Condition.Healthy && !Has(p, "immunity")) { p.Condition = c.ToxicSpikesLayers == 1 ? Condition.Poison : Condition.BadPoison; p.ToxicCounter = p.Condition == Condition.BadPoison ? 1 : 0; ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Status, detail: "toxic-spikes-" + p.Condition)); }
            }
            if (p.Hp == 0) return;
            var foe = s.Active(Other(side));
            // HGSS send-out state order: Trace, weather setters, then Intimidate.
            if (Has(p, "trace") && !p.AbilitySuppressed && foe.Hp > 0)
            {
                if (AbilityRegistry.IsSupported(foe.AbilityId)) { p.AbilityId = foe.AbilityId; ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Volatile, Id(p), Id(foe), "trace-copied:" + p.AbilityId)); }
                else ev.Add(new BattleEvent3(s.Turn, BattleEventKind.MoveFailed, Id(p), Id(foe), "trace-unsupported-target-ability:" + foe.AbilityId));
            }
            if (p.AbilitySuppressed) return;
            Weather setterWeather = p.AbilityId == "drizzle" ? Weather.Rain : p.AbilityId == "drought" ? Weather.Sun : p.AbilityId == "sand-stream" ? Weather.Sandstorm : p.AbilityId == "snow-warning" ? Weather.Hail : Weather.Clear;
            if (setterWeather != Weather.Clear && (s.Weather != setterWeather || s.WeatherTurns > 0))
            {
                s.Weather = setterWeather; s.WeatherTurns = 0; ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Weather, Id(p), detail: "entry-ability:" + p.AbilityId + ":permanent"));
            }
            if (p.AbilityId == "intimidate" && foe.Hp > 0 && foe.Stages[(int)Stage.Attack] > -6 && !Has(foe, "clear-body") && !Has(foe, "hyper-cutter") && !Has(foe, "white-smoke"))
            {
                foe.RawStages[(int)Stage.Attack]--; ev.Add(new BattleEvent3(s.Turn, BattleEventKind.Volatile, Id(p), Id(foe), "intimidate-attack-drop", foe.Stages[(int)Stage.Attack]));
            }
        }
        private static bool TrappedByAbility(AdvancedBattleState s, Side side)
        {
            var a = s.Active(side); var opponent = s.Active(Other(side));
            if (Has(opponent, "shadow-tag") && !Has(a, "shadow-tag")) return true;
            if (Has(opponent, "arena-trap") && !a.Record.Species.HasType(Element.Flying) && !Has(a, "levitate")) return true;
            return Has(opponent, "magnet-pull") && a.Record.Species.HasType(Element.Steel);
        }
    }
}
