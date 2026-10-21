using System;

namespace Roguelite.Gameplay
{
    public static class HgssRules
    {
        public const string Version = "hgss-gen4-subset-1.0.0";
        public const string RandomVersion = "xorshift32-rejection-1";
        private static readonly Stat[] natureStats = { Stat.Attack, Stat.Defense, Stat.Speed, Stat.SpecialAttack, Stat.SpecialDefense };
        public static SixStats CalculateStats(PokemonRecord p)
        {
            int Calc(Stat stat)
            {
                if (stat == Stat.Hp && p.Species.OneHp) return 1;
                long basis = checked((2L * p.Species.BaseStats[stat] + p.Ivs[stat] + p.Evs[stat] / 4) * p.Level / 100);
                if (stat == Stat.Hp) return checked((int)(basis + p.Level + 10));
                int n = (int)p.Nature; int factor = n / 5 == n % 5 ? 100 : natureStats[n / 5] == stat ? 110 : natureStats[n % 5] == stat ? 90 : 100;
                return checked((int)((basis + 5) * factor / 100));
            }
            return new SixStats(Calc(Stat.Hp), Calc(Stat.Attack), Calc(Stat.Defense), Calc(Stat.SpecialAttack), Calc(Stat.SpecialDefense), Calc(Stat.Speed));
        }
        public static int ApplyStage(int value, int stage)
        {
            Guard.Range(value, 1, 1000000, nameof(value)); Guard.Range(stage, -6, 6, nameof(stage));
            return Math.Max(1, checked((int)((long)value * (stage >= 0 ? 2 + stage : 2) / (stage >= 0 ? 2 : 2 - stage))));
        }
        public static int AccuracyThreshold(int accuracy, int accuracyStage, int evasionStage)
        {
            Guard.Range(accuracy, 0, 100, nameof(accuracy)); Guard.Range(accuracyStage, -6, 6, nameof(accuracyStage)); Guard.Range(evasionStage, -6, 6, nameof(evasionStage));
            // HGSS uses a quantized table, not exact thirds (e.g. 70 at +1 => 93).
            int index = Math.Max(-6, Math.Min(6, accuracyStage - evasionStage)) + 6;
            int[] percent = { 33, 36, 43, 50, 60, 75, 100, 133, 166, 200, 233, 266, 300 };
            return accuracy == 0 ? 100 : Math.Min(100, accuracy * percent[index] / 100);
        }
        public static int CriticalDenominator(int stage) { Guard.Range(stage, 0, 4, nameof(stage)); return new[] { 16, 8, 4, 3, 2 }[stage]; }
        public static int EffectiveSpeed(BattlePokemon p) => ApplyStage(p.Stats[Stat.Speed], p.StageValue(Stage.Speed)) / (p.Status == MajorStatus.Paralysis ? 4 : 1);
        public static int Damage(int level, int power, int attack, int defense, bool burn, bool critical, int randomPercent, bool stab, Element moveType, Element type1, Element? type2 = null)
        {
            Guard.Range(level, 1, 100, nameof(level)); Guard.Range(power, 1, 375, nameof(power)); Guard.Range(attack, 1, 1000000, nameof(attack)); Guard.Range(defense, 1, 1000000, nameof(defense)); Guard.Range(randomPercent, 85, 100, nameof(randomPercent));
            int e1 = TypeChart.HalfUnits(moveType, type1), e2 = type2.HasValue ? TypeChart.HalfUnits(moveType, type2.Value) : 2;
            if (e1 == 0 || e2 == 0) return 0;
            long d = checked((2L * level / 5 + 2) * power * attack) / defense / 50;
            if (burn) d /= 2;
            d = checked((d + 2) * (critical ? 2 : 1) * randomPercent) / 100;
            if (stab) d = checked(d * 3) / 2;
            // HGSS applies non-neutral matchups in chart-entry order, with DamageDivide's nonzero floor.
            d = TypeChart.ApplyOrdered(d, moveType, type1, type2);
            return Math.Max(1, checked((int)d));
        }
        public static int Damage(BattlePokemon attacker, BattlePokemon target, MoveDefinition move, bool critical, int randomPercent)
        {
            if (move.Category == Category.Status) throw new ArgumentException("Status moves have no damage formula.");
            var a = move.Category == Category.Physical ? Stat.Attack : Stat.SpecialAttack; var d = move.Category == Category.Physical ? Stat.Defense : Stat.SpecialDefense;
            var astage = move.Category == Category.Physical ? Stage.Attack : Stage.SpecialAttack; var dstage = move.Category == Category.Physical ? Stage.Defense : Stage.SpecialDefense;
            int aStage = attacker.StageValue(astage), dStage = target.StageValue(dstage);
            if (critical) { aStage = Math.Max(0, aStage); dStage = Math.Min(0, dStage); }
            int power = move.Power;
            bool boost = attacker.Record.Ability == Ability.Overgrow && move.Type == Element.Grass || attacker.Record.Ability == Ability.Blaze && move.Type == Element.Fire || attacker.Record.Ability == Ability.Torrent && move.Type == Element.Water;
            if (boost && attacker.Hp <= attacker.Stats[Stat.Hp] / 3) power = power * 3 / 2;
            return Damage(attacker.Record.Level, power, ApplyStage(attacker.Stats[a], aStage), ApplyStage(target.Stats[d], dStage), attacker.Status == MajorStatus.Burn && move.Category == Category.Physical, critical, randomPercent, attacker.Record.Species.HasType(move.Type), move.Type, target.Record.Species.Type1, target.Record.Species.Type2);
        }
    }

    public sealed class BattleRandom
    {
        public uint State { get; private set; }
        public long Draws { get; private set; }
        public BattleRandom(uint state, long draws = 0) { if (state == 0 || draws < 0) throw new ArgumentOutOfRangeException(nameof(state)); State = state; Draws = draws; }
        public uint NextUInt()
        {
            unchecked { uint x = State; x ^= x << 13; x ^= x >> 17; x ^= x << 5; State = x; }
            Draws = checked(Draws + 1); return State;
        }
        public int Next(int bound)
        {
            Guard.Range(bound, 1, int.MaxValue, nameof(bound));
            // xorshift32 visits [1, 2^32-1]; subtract 1, reject its incomplete final bucket.
            ulong size = uint.MaxValue, limit = size - size % (uint)bound;
            uint sample; do { sample = NextUInt() - 1; } while ((ulong)sample >= limit);
            return (int)(sample % (uint)bound);
        }
    }

    public static class TypeChart
    {
        // Factual Gen IV chart, encoded independently as sparse rows: resistance / weakness / immunity.
        private static readonly string[] resist = { "Rock Steel", "Fire Water Rock Dragon", "Water Grass Dragon", "Electric Grass Dragon", "Fire Grass Poison Flying Bug Dragon Steel", "Fire Water Ice Steel", "Poison Flying Psychic Bug", "Poison Ground Rock Ghost", "Grass Bug", "Electric Rock Steel", "Psychic Steel", "Fire Fighting Poison Flying Ghost Steel", "Fighting Ground Steel", "Dark Steel", "Steel", "Fighting Dark Steel", "Fire Water Electric Steel" };
        private static readonly string[] weak = { "", "Grass Ice Bug Steel", "Fire Ground Rock", "Water Flying", "Water Ground Rock", "Grass Ground Flying Dragon", "Normal Ice Rock Dark Steel", "Grass", "Fire Electric Poison Rock Steel", "Grass Fighting Bug", "Fighting Poison", "Grass Psychic Dark", "Fire Ice Flying Bug", "Psychic Ghost", "Dragon", "Psychic Ghost", "Ice Rock" };
        private static readonly string[] immune = { "Ghost", "", "", "Ground", "", "", "Ghost", "Steel", "Flying", "", "Dark", "", "", "Normal", "", "", "" };
        private static readonly string[] order = { "Rock Steel Ghost", "Fire Water Grass Ice Bug Rock Dragon Steel", "Fire Water Grass Ground Rock Dragon", "Water Electric Grass Ground Flying Dragon", "Fire Water Grass Poison Ground Flying Bug Rock Dragon Steel", "Water Grass Ice Ground Flying Dragon Steel Fire", "Normal Ice Poison Flying Psychic Bug Rock Dark Steel Ghost", "Grass Poison Ground Rock Ghost Steel", "Fire Electric Grass Poison Flying Bug Rock Steel", "Electric Grass Fighting Bug Rock Steel", "Fighting Poison Psychic Dark Steel", "Fire Grass Fighting Poison Flying Psychic Ghost Dark Steel", "Fire Ice Fighting Ground Flying Bug Steel", "Normal Psychic Dark Steel Ghost", "Dragon Steel", "Fighting Psychic Ghost Dark Steel", "Fire Water Electric Ice Rock Steel" };
        internal static long ApplyOrdered(long damage, Element attack, Element type1, Element? type2)
        {
            foreach (string name in order[(int)attack].Split(' '))
            {
                var type = (Element)Enum.Parse(typeof(Element), name);
                if (type == type1 || type == type2) { int effect = HalfUnits(attack, type); if (effect == 0) return 0; damage = Math.Max(1L, checked(damage * effect) / 2); }
            }
            return damage;
        }
        public static int HalfUnits(Element attack, Element defense)
        {
            Guard.EnumValue(attack); Guard.EnumValue(defense); string d = defense.ToString(); int a = (int)attack;
            bool Has(string row) => Array.IndexOf(row.Split(' '), d) >= 0;
            return Has(immune[a]) ? 0 : Has(weak[a]) ? 4 : Has(resist[a]) ? 1 : 2;
        }
    }
}
