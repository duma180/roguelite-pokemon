using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Roguelite.Gameplay
{
    public static class Canonical
    {
        private sealed class Writer
        {
            private readonly StringBuilder b = new StringBuilder();
            public void Add(object value) { string x = Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""; b.Append(x.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(x); }
            public override string ToString() => b.ToString();
        }
        public static string Species(SpeciesDefinition s)
        {
            var w = new Writer(); w.Add(s.Id); w.Add(s.DataVersion); foreach (var x in s.BaseStats.ToArray()) w.Add(x); w.Add((int)s.Type1); w.Add(s.Type2.HasValue ? (int)s.Type2.Value : -1); w.Add(s.OneHp ? 1 : 0); return w.ToString();
        }
        public static string Move(MoveDefinition m)
        {
            var w = new Writer(); w.Add(m.Id); w.Add((int)m.Type); w.Add((int)m.Category); w.Add((int)m.Effect); w.Add(m.Power); w.Add(m.Accuracy); w.Add(m.MaxPp); w.Add(m.Priority); w.Add(m.CriticalStage); w.Add(m.BurnChance); return w.ToString();
        }
        public static string State(BattleState s)
        {
            var w = new Writer(); w.Add("battle-snapshot-1.0.0"); w.Add(s.Ruleset); w.Add(s.EncounterId.ToString("N")); w.Add(HgssRules.RandomVersion); w.Add((int)s.Kind); w.Add(s.Turn); w.Add(s.RandomState); w.Add(s.RandomDraws);
            foreach (Side side in new[] { Side.Player, Side.Opponent })
            {
                w.Add(s.ActiveIndex(side)); w.Add(s.Team(side).Count);
                foreach (var p in s.Team(side))
                {
                    var r = p.Record; w.Add(r.IndividualId.ToString("N")); w.Add(Species(r.Species)); w.Add(r.Level); w.Add((int)r.Nature); w.Add((int)r.Ability);
                    foreach (int x in r.Ivs.ToArray()) w.Add(x); foreach (int x in r.Evs.ToArray()) w.Add(x);
                    w.Add(r.Moves.Count); foreach (var m in r.Moves) w.Add(Move(m)); foreach (int x in p.Pp) w.Add(x); foreach (int x in p.Stages) w.Add(x); w.Add(p.Hp); w.Add((int)p.Status);
                }
            }
            return w.ToString();
        }
        public static string Events(System.Collections.Generic.IEnumerable<BattleEvent> events)
        {
            var w = new Writer(); w.Add("battle-events-1.0.0"); foreach (var e in events) { w.Add(e.Turn); w.Add((int)e.Kind); w.Add(e.Actor); w.Add(e.Target); w.Add(e.Detail); w.Add(e.Amount); w.Add(e.Roll); } return w.ToString();
        }
        public static string Hash(string canonical) { using var sha = SHA256.Create(); var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical)); return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant(); }
        public static string TransitionHash(BattleState before, BattleAction player, BattleAction opponent, StepResult after)
        {
            var w = new Writer(); w.Add("battle-transition-1.0.0"); w.Add(State(before)); w.Add((int)player.Kind); w.Add(player.Index); w.Add((int)opponent.Kind); w.Add(opponent.Index); w.Add(State(after.State)); w.Add(Events(after.Events)); return Hash(w.ToString());
        }
    }
}
