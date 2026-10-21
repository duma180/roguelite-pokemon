using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelite.Gameplay
{
    public enum Stat { Hp, Attack, Defense, SpecialAttack, SpecialDefense, Speed }
    public enum Element { Normal, Fire, Water, Electric, Grass, Ice, Fighting, Poison, Ground, Flying, Psychic, Bug, Rock, Ghost, Dragon, Dark, Steel }
    public enum Nature { Hardy, Lonely, Brave, Adamant, Naughty, Bold, Docile, Relaxed, Impish, Lax, Timid, Hasty, Serious, Jolly, Naive, Modest, Mild, Quiet, Bashful, Rash, Calm, Gentle, Sassy, Careful, Quirky }
    public enum Ability { NoneFixture, Overgrow, Blaze, Torrent, RunAway, Sturdy }
    public enum Category { Physical, Special, Status }
    public enum Effect { Damage, Burn, Paralyze, LowerAttack }
    public enum MajorStatus { Healthy, Burn, Paralysis }
    public enum Stage { Attack, Defense, SpecialAttack, SpecialDefense, Speed, Accuracy, Evasion }

    internal static class Guard
    {
        public static int Range(int x, int min, int max, string name) { if (x < min || x > max) throw new ArgumentOutOfRangeException(name); return x; }
        public static T EnumValue<T>(T x) where T : struct { if (!Enum.IsDefined(typeof(T), x)) throw new ArgumentOutOfRangeException(nameof(x)); return x; }
        public static string Id(string x) { if (string.IsNullOrWhiteSpace(x) || x.Any(c => !(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '.' && c != '-' && c != '_')) throw new ArgumentException("IDs use lowercase ASCII letters, digits, dot, dash or underscore."); return x; }
    }

    public sealed class SixStats
    {
        private readonly int[] values;
        public SixStats(int hp, int attack, int defense, int specialAttack, int specialDefense, int speed)
        { values = new[] { hp, attack, defense, specialAttack, specialDefense, speed }; }
        public int this[Stat stat] => values[(int)Guard.EnumValue(stat)];
        public int[] ToArray() => (int[])values.Clone();
        public static SixStats Uniform(int x) => new SixStats(x, x, x, x, x, x);
        internal void Validate(int min, int max, string name) { foreach (var v in values) Guard.Range(v, min, max, name); }
    }

    public sealed class SpeciesDefinition
    {
        public string Id { get; }
        public string DataVersion { get; }
        public SixStats BaseStats { get; }
        public Element Type1 { get; }
        public Element? Type2 { get; }
        public bool OneHp { get; }
        public SpeciesDefinition(string id, string dataVersion, SixStats stats, Element type1, Element? type2 = null, bool oneHp = false)
        {
            Id = Guard.Id(id); DataVersion = Guard.Id(dataVersion); stats.Validate(1, 255, nameof(stats)); BaseStats = stats;
            Type1 = Guard.EnumValue(type1); if (type2.HasValue) Guard.EnumValue(type2.Value);
            if (type1 == type2) throw new ArgumentException("Duplicate types are represented as a single type."); Type2 = type2; OneHp = oneHp;
        }
        public bool HasType(Element type) => Type1 == type || Type2 == type;
    }

    public sealed class MoveDefinition
    {
        public string Id { get; }
        public Element Type { get; }
        public Category Category { get; }
        public Effect Effect { get; }
        public int Power { get; }
        public int Accuracy { get; } // 0 means bypass the accuracy check, as in HGSS data.
        public int MaxPp { get; }
        public int Priority { get; }
        public int CriticalStage { get; }
        public int BurnChance { get; }
        public MoveDefinition(string id, Element type, Category category, int power, int accuracy, int pp, int priority = 0, int criticalStage = 0, Effect effect = Effect.Damage, int burnChance = 0)
        {
            Id = Guard.Id(id); Type = Guard.EnumValue(type); Category = Guard.EnumValue(category); Effect = Guard.EnumValue(effect);
            Power = Guard.Range(power, 0, 250, nameof(power)); Accuracy = Guard.Range(accuracy, 0, 100, nameof(accuracy)); MaxPp = Guard.Range(pp, 1, 64, nameof(pp));
            Priority = Guard.Range(priority, -7, 5, nameof(priority)); CriticalStage = Guard.Range(criticalStage, 0, 4, nameof(criticalStage)); BurnChance = Guard.Range(burnChance, 0, 100, nameof(burnChance));
            if ((category == Category.Status) != (effect != Effect.Damage) || (category == Category.Status ? power != 0 || burnChance != 0 || criticalStage != 0 : power == 0)) throw new ArgumentException("Unsupported move definition combination.");
        }
    }

    // Persistent identity is not a slot, species ID, level, or a newly generated battle ID.
    public sealed class PokemonRecord
    {
        public Guid IndividualId { get; }
        public SpeciesDefinition Species { get; }
        public int Level { get; }
        public SixStats Ivs { get; }
        public SixStats Evs { get; }
        public Nature Nature { get; }
        public Ability Ability { get; }
        private readonly MoveDefinition[] moves;
        public IReadOnlyList<MoveDefinition> Moves => Array.AsReadOnly(moves);
        public PokemonRecord(Guid id, SpeciesDefinition species, int level, SixStats ivs, SixStats evs, Nature nature, Ability ability, IEnumerable<MoveDefinition> moves)
        {
            if (id == Guid.Empty) throw new ArgumentException("Individual ID cannot be empty.");
            IndividualId = id; Species = species ?? throw new ArgumentNullException(nameof(species)); Level = Guard.Range(level, 1, 100, nameof(level));
            ivs.Validate(0, 31, nameof(ivs)); evs.Validate(0, 255, nameof(evs)); if (evs.ToArray().Sum() > 510) throw new ArgumentException("Gen IV total EV cap is 510.");
            Ivs = ivs; Evs = evs; Nature = Guard.EnumValue(nature); Ability = Guard.EnumValue(ability);
            this.moves = moves.ToArray(); if (this.moves.Length < 1 || this.moves.Length > 4 || this.moves.Any(m => m == null) || this.moves.Select(m => m.Id).Distinct().Count() != this.moves.Length) throw new ArgumentException("One to four distinct supported moves required.");
        }
        public PokemonRecord WithSpeciesAndLevel(SpeciesDefinition species, int level) => new PokemonRecord(IndividualId, species, level, Ivs, Evs, Nature, Ability, moves);
    }
}
