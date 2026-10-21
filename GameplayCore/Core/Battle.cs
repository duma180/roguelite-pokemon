using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelite.Gameplay
{
    public enum EncounterKind { Wild, Trainer }
    public enum Side { Player, Opponent }
    public enum BattleOutcome { Ongoing, PlayerWin, OpponentWin, Draw }
    public enum ActionKind { Move, Switch }
    public enum EventKind { TurnStarted, Order, RandomRoll, Switched, MoveUsed, Missed, Immune, Critical, Damage, StatusChanged, StatusBlocked, StageChanged, UnableToAct, Fainted, ReplacementRequired, BattleEnded }

    public sealed class BattlePokemon
    {
        public PokemonRecord Record { get; }
        public SixStats Stats { get; }
        public int Hp { get; internal set; }
        public MajorStatus Status { get; internal set; }
        private readonly int[] pp;
        private readonly int[] stages;
        public IReadOnlyList<int> Pp => Array.AsReadOnly(pp);
        public IReadOnlyList<int> Stages => Array.AsReadOnly(stages);
        public BattlePokemon(PokemonRecord record, int? hp = null, MajorStatus status = MajorStatus.Healthy, IEnumerable<int>? pp = null, IEnumerable<int>? stages = null)
        {
            Record = record; Stats = HgssRules.CalculateStats(record); Hp = Guard.Range(hp ?? Stats[Stat.Hp], 0, Stats[Stat.Hp], nameof(hp)); Status = Guard.EnumValue(status);
            if (status == MajorStatus.Burn && record.Species.HasType(Element.Fire)) throw new ArgumentException("Fire types cannot be burned in this supported ruleset.");
            this.pp = pp?.ToArray() ?? record.Moves.Select(m => m.MaxPp).ToArray(); this.stages = stages?.ToArray() ?? new int[7];
            if (this.pp.Length != record.Moves.Count || this.stages.Length != 7) throw new ArgumentException("Snapshot array length mismatch.");
            for (int i = 0; i < this.pp.Length; i++) Guard.Range(this.pp[i], 0, record.Moves[i].MaxPp, nameof(pp));
            foreach (int s in this.stages) Guard.Range(s, -6, 6, nameof(stages));
        }
        public int StageValue(Stage stage) => stages[(int)Guard.EnumValue(stage)];
        internal void Consume(int slot) { pp[slot]--; }
        internal int LowerAttack() { int old = stages[0]; stages[0] = Math.Max(-6, old - 1); return stages[0] - old; }
        internal void ResetStages() { Array.Clear(stages, 0, stages.Length); }
        public BattlePokemon DeepCopy() => new BattlePokemon(Record, Hp, Status, pp, stages);
    }

    public sealed class BattleState
    {
        private readonly BattlePokemon[][] teams;
        private readonly int[] active;
        public string Ruleset { get; }
        public Guid EncounterId { get; }
        public EncounterKind Kind { get; }
        public uint RandomState { get; internal set; }
        public long RandomDraws { get; internal set; }
        public int Turn { get; internal set; }
        public BattleOutcome Outcome => teams[0].All(p => p.Hp == 0) ? teams[1].All(p => p.Hp == 0) ? BattleOutcome.Draw : BattleOutcome.OpponentWin : teams[1].All(p => p.Hp == 0) ? BattleOutcome.PlayerWin : BattleOutcome.Ongoing;
        public BattleState(IEnumerable<BattlePokemon> player, IEnumerable<BattlePokemon> opponent, uint seed, Guid encounterId, EncounterKind kind = EncounterKind.Trainer, int playerActive = 0, int opponentActive = 0, string ruleset = HgssRules.Version, int turn = 0, long draws = 0)
        {
            if (ruleset != HgssRules.Version) throw new NotSupportedException("Unsupported ruleset; no implicit migration.");
            if (encounterId == Guid.Empty) throw new ArgumentException("Encounter identity is caller-owned and must be stable."); EncounterId = encounterId;
            Ruleset = ruleset; Kind = Guard.EnumValue(kind); var rng = new BattleRandom(seed, draws); RandomState = rng.State; RandomDraws = draws; Turn = Guard.Range(turn, 0, int.MaxValue, nameof(turn));
            teams = new[] { player.Select(p => p.DeepCopy()).ToArray(), opponent.Select(p => p.DeepCopy()).ToArray() };
            Guard.Range(teams[0].Length, 1, 6, nameof(player)); Guard.Range(teams[1].Length, 1, kind == EncounterKind.Wild ? 1 : 6, nameof(opponent));
            active = new[] { Guard.Range(playerActive, 0, teams[0].Length - 1, nameof(playerActive)), Guard.Range(opponentActive, 0, teams[1].Length - 1, nameof(opponentActive)) };
            var all = teams.SelectMany(t => t).ToArray(); if (all.Select(p => p.Record.IndividualId).Distinct().Count() != all.Length) throw new ArgumentException("Duplicate individual identity across battle parties.");
            foreach (var p in all) PilotCatalog.ValidateForBattle(p.Record);
            foreach (var g in all.GroupBy(p => p.Record.Species.Id)) if (g.Select(p => Canonical.Species(p.Record.Species)).Distinct().Count() != 1) throw new ArgumentException("Conflicting species definitions under one ID.");
            foreach (var g in all.SelectMany(p => p.Record.Moves).GroupBy(m => m.Id)) if (g.Select(Canonical.Move).Distinct().Count() != 1) throw new ArgumentException("Conflicting move definitions under one ID.");
        }
        public IReadOnlyList<BattlePokemon> Team(Side side) => Array.AsReadOnly(teams[(int)Guard.EnumValue(side)]);
        public int ActiveIndex(Side side) => active[(int)Guard.EnumValue(side)];
        public BattlePokemon Active(Side side) => teams[(int)Guard.EnumValue(side)][ActiveIndex(side)];
        public bool NeedsReplacement(Side side) => Outcome == BattleOutcome.Ongoing && Active(side).Hp == 0;
        internal void Switch(Side side, int index) { Active(side).ResetStages(); active[(int)side] = index; Active(side).ResetStages(); }
        public BattleState DeepCopy() => new BattleState(teams[0], teams[1], RandomState, EncounterId, Kind, active[0], active[1], Ruleset, Turn, RandomDraws);
    }

    public sealed class BattleAction
    {
        public ActionKind Kind { get; }
        public int Index { get; }
        private BattleAction(ActionKind kind, int index) { Kind = kind; Index = index; }
        public static BattleAction Move(int slot) => new BattleAction(ActionKind.Move, slot);
        public static BattleAction Switch(int partyIndex) => new BattleAction(ActionKind.Switch, partyIndex);
    }
    public sealed class BattleEvent
    {
        public int Turn { get; }
        public EventKind Kind { get; }
        public string Actor { get; }
        public string Target { get; }
        public string Detail { get; }
        public int Amount { get; }
        public int Roll { get; }
        internal BattleEvent(int turn, EventKind kind, string actor, string target, string detail, int amount = 0, int roll = -1) { Turn = turn; Kind = kind; Actor = actor; Target = target; Detail = detail; Amount = amount; Roll = roll; }
    }
    public sealed class StepResult
    {
        public BattleState State { get; }
        public IReadOnlyList<BattleEvent> Events { get; }
        public string StateHash => Canonical.Hash(Canonical.State(State));
        internal StepResult(BattleState state, List<BattleEvent> events) { State = state; Events = events.AsReadOnly(); }
    }

    public static class BattleEngine
    {
        private static string Id(BattlePokemon p) => p.Record.IndividualId.ToString("N");
        private static Side Other(Side side) => side == Side.Player ? Side.Opponent : Side.Player;
        private static void ValidateSwitch(BattleState s, Side side, int index)
        {
            Guard.Range(index, 0, s.Team(side).Count - 1, nameof(index)); if (index == s.ActiveIndex(side) || s.Team(side)[index].Hp == 0) throw new InvalidOperationException("Cannot switch to active or fainted individual.");
        }
        private static void Validate(BattleState s, Side side, BattleAction action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (action.Kind == ActionKind.Switch) ValidateSwitch(s, side, action.Index);
            else { Guard.Range(action.Index, 0, s.Active(side).Record.Moves.Count - 1, nameof(action.Index)); if (s.Active(side).Pp[action.Index] == 0) throw new InvalidOperationException("No PP; Struggle is outside this prototype."); }
        }
        public static StepResult ReplaceFainted(BattleState input, Side side, int index)
        {
            Guard.EnumValue(side); if (!input.NeedsReplacement(side)) throw new InvalidOperationException("No replacement pending."); ValidateSwitch(input, side, index);
            var s = input.DeepCopy(); s.Switch(side, index); return new StepResult(s, new List<BattleEvent> { new BattleEvent(s.Turn, EventKind.Switched, Id(s.Active(side)), "", "forced-replacement") });
        }
        public static StepResult ResolveTurn(BattleState input, BattleAction player, BattleAction opponent)
        {
            if (input.Outcome != BattleOutcome.Ongoing || input.NeedsReplacement(Side.Player) || input.NeedsReplacement(Side.Opponent)) throw new InvalidOperationException("Battle ended or explicit replacement required.");
            Validate(input, Side.Player, player); Validate(input, Side.Opponent, opponent);
            var s = input.DeepCopy(); s.Turn = checked(s.Turn + 1); var rng = new BattleRandom(s.RandomState, s.RandomDraws); var events = new List<BattleEvent>();
            void Emit(EventKind kind, BattlePokemon? a = null, BattlePokemon? t = null, string detail = "", int amount = 0, int roll = -1) => events.Add(new BattleEvent(s.Turn, kind, a == null ? "" : Id(a), t == null ? "" : Id(t), detail, amount, roll));
            int Roll(int bound, string purpose, BattlePokemon? a = null) { int x = rng.Next(bound); Emit(EventKind.RandomRoll, a, detail: purpose, amount: bound, roll: x); return x; }
            void Hurt(BattlePokemon target, int amount, BattlePokemon? source, string cause, int roll = -1)
            {
                int dealt = Math.Min(target.Hp, amount); target.Hp -= dealt; Emit(EventKind.Damage, source, target, cause, dealt, roll);
                if (target.Hp == 0) Emit(EventKind.Fainted, target, detail: cause);
            }
            void ApplyStatus(BattlePokemon a, BattlePokemon t, MajorStatus status)
            {
                if (t.Status != MajorStatus.Healthy || status == MajorStatus.Burn && t.Record.Species.HasType(Element.Fire)) Emit(EventKind.StatusBlocked, a, t, "already-statused-or-type-immunity");
                else { t.Status = status; Emit(EventKind.StatusChanged, a, t, status.ToString()); }
            }
            void Act(Side side, BattleAction action)
            {
                var a = s.Active(side); var t = s.Active(Other(side));
                if (a.Hp == 0) { Emit(EventKind.UnableToAct, a, detail: "fainted"); return; }
                if (action.Kind == ActionKind.Switch) { s.Switch(side, action.Index); Emit(EventKind.Switched, s.Active(side), a, "voluntary"); return; }
                if (t.Hp == 0) { Emit(EventKind.UnableToAct, a, detail: "no-living-target"); return; }
                if (a.Status == MajorStatus.Paralysis && Roll(4, "full-paralysis", a) == 0) { Emit(EventKind.UnableToAct, a, detail: "full-paralysis"); return; }
                var move = a.Record.Moves[action.Index]; a.Consume(action.Index); Emit(EventKind.MoveUsed, a, t, move.Id);
                int accuracy = HgssRules.AccuracyThreshold(move.Accuracy, a.StageValue(Stage.Accuracy), t.StageValue(Stage.Evasion));
                if (move.Accuracy != 0 && Roll(100, "accuracy", a) >= accuracy) { Emit(EventKind.Missed, a, t, move.Id); return; }
                bool immune = move.Category != Category.Status && (TypeChart.HalfUnits(move.Type, t.Record.Species.Type1) == 0 || t.Record.Species.Type2.HasValue && TypeChart.HalfUnits(move.Type, t.Record.Species.Type2.Value) == 0)
                    || move.Effect == Effect.Paralyze && move.Type == Element.Electric && t.Record.Species.HasType(Element.Ground);
                if (immune) { Emit(EventKind.Immune, a, t, move.Id); return; }
                if (move.Category != Category.Status)
                {
                    bool critical = Roll(HgssRules.CriticalDenominator(move.CriticalStage), "critical", a) == 0;
                    if (critical) Emit(EventKind.Critical, a, t, move.Id, 2);
                    int range = 100 - Roll(16, "damage-range", a); int damage = HgssRules.Damage(a, t, move, critical, range);
                    Hurt(t, damage, a, move.Id, range);
                    if (t.Hp > 0 && move.BurnChance > 0 && t.Status == MajorStatus.Healthy && !t.Record.Species.HasType(Element.Fire) && Roll(100, "secondary-burn", a) < move.BurnChance) ApplyStatus(a, t, MajorStatus.Burn);
                }
                else if (move.Effect == Effect.LowerAttack) Emit(EventKind.StageChanged, a, t, "attack", t.LowerAttack());
                else ApplyStatus(a, t, move.Effect == Effect.Burn ? MajorStatus.Burn : MajorStatus.Paralysis);
            }
            int Priority(Side side, BattleAction action) => action.Kind == ActionKind.Switch ? 6 : s.Active(side).Record.Moves[action.Index].Priority;
            Side Faster(string purpose)
            {
                int a = HgssRules.EffectiveSpeed(s.Active(Side.Player)), b = HgssRules.EffectiveSpeed(s.Active(Side.Opponent));
                return a == b ? Roll(2, purpose) == 0 ? Side.Player : Side.Opponent : a > b ? Side.Player : Side.Opponent;
            }
            Emit(EventKind.TurnStarted);
            int pp = Priority(Side.Player, player), op = Priority(Side.Opponent, opponent);
            Side first = pp == op ? Faster("order-speed-tie") : pp > op ? Side.Player : Side.Opponent;
            Emit(EventKind.Order, s.Active(first), s.Active(Other(first)), "priority-then-start-turn-speed");
            Act(first, first == Side.Player ? player : opponent);
            if (s.Outcome == BattleOutcome.Ongoing) Act(Other(first), first == Side.Player ? opponent : player);
            if (s.Outcome == BattleOutcome.Ongoing && (s.Active(Side.Player).Status == MajorStatus.Burn || s.Active(Side.Opponent).Status == MajorStatus.Burn))
            {
                Side residualFirst = Faster("residual-speed-tie");
                foreach (Side side in new[] { residualFirst, Other(residualFirst) })
                {
                    var p = s.Active(side); if (p.Hp > 0 && p.Status == MajorStatus.Burn) Hurt(p, Math.Max(1, p.Stats[Stat.Hp] / 8), null, "burn-residual");
                    if (s.Outcome != BattleOutcome.Ongoing) break;
                }
            }
            if (s.Outcome != BattleOutcome.Ongoing) Emit(EventKind.BattleEnded, detail: s.Outcome.ToString());
            else foreach (Side side in new[] { Side.Player, Side.Opponent }) if (s.NeedsReplacement(side)) Emit(EventKind.ReplacementRequired, s.Active(side), detail: side.ToString());
            s.RandomState = rng.State; s.RandomDraws = rng.Draws; return new StepResult(s, events);
        }
    }
}
