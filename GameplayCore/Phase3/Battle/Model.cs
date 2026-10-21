using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Roguelite.Gameplay;

namespace Roguelite.Gameplay.Phase3
{
    public enum Condition { Healthy, Sleep, Freeze, Poison, BadPoison, Burn, Paralysis }
    public enum Weather { Clear, Rain, Sun, Sandstorm, Hail }
    public enum BattleMoveEffect { Damage, Pursuit, RapidSpin, ForceSwitch, Disable, Encore, Taunt, Imprison, InflictSleep, InflictFreeze, InflictPoison, InflictBadPoison, InflictBurn, InflictParalysis, LeechSeed, Bind, Ingrain, AquaRing, HealBlock, Protect, SetRain, SetSun, SetSandstorm, SetHail, SetSpikes, SetToxicSpikes, SetStealthRock, SetReflect, SetLightScreen, Confuse, Flinch, Infatuate, TrapSwitch, Struggle }
    public enum CompatibilityDisposition { Native, ExplicitlyPorted, Unsupported }
    public enum BattleEventKind { TurnStarted, Order, Random, Switched, MoveUsed, MoveFailed, Missed, Protected, Damage, Status, Volatile, Hazard, Weather, Screen, UnableToAct, Fainted, Replacement, BattleEnded }

    public sealed class CompatibilityProfile
    {
        public const string Hgss = "hgss-gen4-singles-1.0.0";
        public const string CrossGenerationCatalog = "hgss-combat-gen1-9-content-1.0.0";
        public string Ruleset { get; }
        public string ContentGeneration { get; }
        public CompatibilityProfile(string ruleset, string contentGeneration)
        {
            if (ruleset != Hgss && ruleset != CrossGenerationCatalog) throw new NotSupportedException("Unknown compatibility ruleset.");
            if (contentGeneration != "gen1-9") throw new NotSupportedException("This data registry accepts an explicit Gen I-IX catalog version only.");
            Ruleset = ruleset; ContentGeneration = contentGeneration;
        }
    }

    public sealed class EffectRegistration
    {
        public string MoveId { get; }
        public int GenerationIntroduced { get; }
        public CompatibilityDisposition Disposition { get; }
        public string Ruleset { get; }
        public EffectRegistration(string moveId, int generationIntroduced, CompatibilityDisposition disposition, string ruleset)
        {
            MoveId = Id(moveId); if (generationIntroduced < 1 || generationIntroduced > 9) throw new ArgumentOutOfRangeException(nameof(generationIntroduced));
            GenerationIntroduced = generationIntroduced; Disposition = disposition; Ruleset = ruleset;
        }
        private static string Id(string x) { if (string.IsNullOrWhiteSpace(x) || x.Any(c => !(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '-')) throw new ArgumentException("Move IDs use lowercase ASCII."); return x; }
    }

    public sealed class EffectRegistry
    {
        private readonly Dictionary<string, EffectRegistration> entries;
        public string Version { get; }
        public string ContentHash { get; }
        public EffectRegistry(string version, IEnumerable<EffectRegistration> registrations)
        {
            Version = version ?? throw new ArgumentNullException(nameof(version)); entries = new Dictionary<string, EffectRegistration>(StringComparer.Ordinal);
            foreach (var e in registrations) { string key = Key(e.MoveId, e.Ruleset); if (entries.ContainsKey(key)) throw new ArgumentException("Duplicate effect registration " + key); entries.Add(key, e); }
            string canonical = string.Join("\n", entries.OrderBy(x => x.Key).Select(x => x.Value.MoveId + "|" + x.Value.GenerationIntroduced + "|" + x.Value.Disposition + "|" + x.Value.Ruleset));
            using (var sha = SHA256.Create()) ContentHash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(Version + "\n" + canonical))).Replace("-", "").ToLowerInvariant();
        }
        public EffectRegistration Require(string moveId, string ruleset)
        {
            if (!entries.TryGetValue(Key(moveId, ruleset), out var entry) || entry.Disposition == CompatibilityDisposition.Unsupported)
                throw new NotSupportedException("Move effect has no validated implementation under " + ruleset + ": " + moveId);
            return entry;
        }
        private static string Key(string id, string ruleset) => id + "|" + ruleset;
        public static EffectRegistry Foundational { get; } = new EffectRegistry("phase3-effects-1.3.0", new[] {
            new EffectRegistration("damage", 1, CompatibilityDisposition.Native, CompatibilityProfile.Hgss), new EffectRegistration("damage", 1, CompatibilityDisposition.ExplicitlyPorted, CompatibilityProfile.CrossGenerationCatalog),
            new EffectRegistration("sleep", 1, CompatibilityDisposition.Native, CompatibilityProfile.Hgss), new EffectRegistration("sleep", 1, CompatibilityDisposition.ExplicitlyPorted, CompatibilityProfile.CrossGenerationCatalog),
            new EffectRegistration("freeze", 1, CompatibilityDisposition.Native, CompatibilityProfile.Hgss), new EffectRegistration("freeze", 1, CompatibilityDisposition.ExplicitlyPorted, CompatibilityProfile.CrossGenerationCatalog),
            new EffectRegistration("poison", 1, CompatibilityDisposition.Native, CompatibilityProfile.Hgss), new EffectRegistration("poison", 1, CompatibilityDisposition.ExplicitlyPorted, CompatibilityProfile.CrossGenerationCatalog),
            new EffectRegistration("bad-poison", 2, CompatibilityDisposition.Native, CompatibilityProfile.Hgss), new EffectRegistration("bad-poison", 2, CompatibilityDisposition.ExplicitlyPorted, CompatibilityProfile.CrossGenerationCatalog),
            new EffectRegistration("burn-paralysis", 1, CompatibilityDisposition.Native, CompatibilityProfile.Hgss), new EffectRegistration("burn-paralysis", 1, CompatibilityDisposition.ExplicitlyPorted, CompatibilityProfile.CrossGenerationCatalog),
            new EffectRegistration("volatile", 1, CompatibilityDisposition.Native, CompatibilityProfile.Hgss), new EffectRegistration("volatile", 1, CompatibilityDisposition.ExplicitlyPorted, CompatibilityProfile.CrossGenerationCatalog),
            new EffectRegistration("protect", 2, CompatibilityDisposition.Native, CompatibilityProfile.Hgss), new EffectRegistration("protect", 2, CompatibilityDisposition.ExplicitlyPorted, CompatibilityProfile.CrossGenerationCatalog),
            new EffectRegistration("weather", 2, CompatibilityDisposition.Native, CompatibilityProfile.Hgss), new EffectRegistration("weather", 2, CompatibilityDisposition.ExplicitlyPorted, CompatibilityProfile.CrossGenerationCatalog),
            new EffectRegistration("entry-hazards", 2, CompatibilityDisposition.Native, CompatibilityProfile.Hgss), new EffectRegistration("entry-hazards", 2, CompatibilityDisposition.ExplicitlyPorted, CompatibilityProfile.CrossGenerationCatalog),
            new EffectRegistration("screens", 1, CompatibilityDisposition.Native, CompatibilityProfile.Hgss), new EffectRegistration("screens", 1, CompatibilityDisposition.ExplicitlyPorted, CompatibilityProfile.CrossGenerationCatalog),
            new EffectRegistration("struggle", 1, CompatibilityDisposition.Native, CompatibilityProfile.Hgss), new EffectRegistration("struggle", 1, CompatibilityDisposition.ExplicitlyPorted, CompatibilityProfile.CrossGenerationCatalog),
            new EffectRegistration("gen9-signature-effect", 9, CompatibilityDisposition.Unsupported, CompatibilityProfile.CrossGenerationCatalog),
            new EffectRegistration("pursuit", 2, CompatibilityDisposition.Native, CompatibilityProfile.Hgss), new EffectRegistration("rapid-spin", 2, CompatibilityDisposition.Native, CompatibilityProfile.Hgss),
            new EffectRegistration("force-switch", 1, CompatibilityDisposition.Native, CompatibilityProfile.Hgss), new EffectRegistration("disable-encore-taunt-imprison", 1, CompatibilityDisposition.Native, CompatibilityProfile.Hgss),
            new EffectRegistration("leech-seed", 1, CompatibilityDisposition.Native, CompatibilityProfile.Hgss), new EffectRegistration("leech-seed", 1, CompatibilityDisposition.ExplicitlyPorted, CompatibilityProfile.CrossGenerationCatalog),
            new EffectRegistration("binding", 1, CompatibilityDisposition.Native, CompatibilityProfile.Hgss), new EffectRegistration("binding", 1, CompatibilityDisposition.ExplicitlyPorted, CompatibilityProfile.CrossGenerationCatalog),
            new EffectRegistration("ingrain-aqua-ring", 3, CompatibilityDisposition.Native, CompatibilityProfile.Hgss), new EffectRegistration("ingrain-aqua-ring", 3, CompatibilityDisposition.ExplicitlyPorted, CompatibilityProfile.CrossGenerationCatalog),
            new EffectRegistration("heal-block", 4, CompatibilityDisposition.Native, CompatibilityProfile.Hgss), new EffectRegistration("heal-block", 4, CompatibilityDisposition.Unsupported, CompatibilityProfile.CrossGenerationCatalog),
            new EffectRegistration("entry-abilities", 3, CompatibilityDisposition.Native, CompatibilityProfile.Hgss), new EffectRegistration("entry-abilities", 3, CompatibilityDisposition.Unsupported, CompatibilityProfile.CrossGenerationCatalog)
        });
    }

    public static class AbilityRegistry
    {
        private static readonly IReadOnlyDictionary<string, int> Supported = new System.Collections.ObjectModel.ReadOnlyDictionary<string, int>(new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["none"] = 1, ["overgrow"] = 3, ["blaze"] = 3, ["torrent"] = 3, ["levitate"] = 3, ["magic-guard"] = 3, ["air-lock"] = 3, ["cloud-nine"] = 3,
            ["insomnia"] = 3, ["vital-spirit"] = 3, ["magma-armor"] = 3, ["limber"] = 3, ["immunity"] = 3, ["water-veil"] = 3, ["own-tempo"] = 3, ["oblivious"] = 3,
            ["arena-trap"] = 3, ["shadow-tag"] = 3, ["magnet-pull"] = 3, ["suction-cups"] = 3, ["pressure"] = 3, ["swift-swim"] = 3, ["chlorophyll"] = 3, ["sand-rush"] = 5,
            ["speed-boost"] = 3, ["shed-skin"] = 3, ["quick-feet"] = 4, ["sand-veil"] = 3, ["snow-cloak"] = 4, ["rain-dish"] = 3, ["dry-skin"] = 4, ["ice-body"] = 4, ["hydration"] = 4, ["solar-power"] = 4, ["liquid-ooze"] = 3,
            ["intimidate"] = 3, ["trace"] = 3, ["drizzle"] = 3, ["drought"] = 3, ["sand-stream"] = 3, ["snow-warning"] = 4,
            ["clear-body"] = 3, ["hyper-cutter"] = 3, ["white-smoke"] = 3, ["suction-cups"] = 3
        });
        public static string Version => "hgss-ability-hooks-1.4.0";
        public static string ContentHash
        {
            get { string canonical = string.Join("\n", Supported.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => x.Key + "|" + x.Value)); using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(Version + "\n" + canonical))).Replace("-", "").ToLowerInvariant(); }
        }
        public static int Require(string id) { if (!Supported.TryGetValue(id, out int generation)) throw new NotSupportedException("Ability has no validated Phase 3 hook: " + id); return generation; }
        internal static bool IsSupported(string id) => Supported.ContainsKey(id);
    }

    public sealed class BattleMove
    {
        public string Id { get; }
        public Element Type { get; }
        public Category Category { get; }
        public int Power { get; }
        public int Accuracy { get; }
        public int BasePp { get; }
        public int Priority { get; }
        public BattleMoveEffect Effect { get; }
        public int EffectChance { get; }
        public int CriticalStage { get; }
        public int GenerationIntroduced { get; }
        public int MaxPp => BasePp;
        public BattleMove(string id, Element type, Category category, int power, int accuracy, int pp, BattleMoveEffect effect = BattleMoveEffect.Damage, int priority = 0, int effectChance = 0, int criticalStage = 0, int generationIntroduced = 4)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Any(c => !(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '-')) throw new ArgumentException("Move IDs use lowercase ASCII.");
            if (!Enum.IsDefined(typeof(Element), type) || !Enum.IsDefined(typeof(Category), category) || !Enum.IsDefined(typeof(BattleMoveEffect), effect)) throw new ArgumentOutOfRangeException(nameof(type));
            if (power < 0 || power > 250 || accuracy < 0 || accuracy > 100 || pp < 1 || pp > 64 || priority < -7 || priority > 5 || effectChance < 0 || effectChance > 100 || criticalStage < 0 || criticalStage > 4 || generationIntroduced < 1 || generationIntroduced > 9) throw new ArgumentOutOfRangeException(nameof(power));
            Id = id; Type = type; Category = category; Power = power; Accuracy = accuracy; BasePp = pp; Effect = effect; Priority = priority; EffectChance = effectChance; CriticalStage = criticalStage; GenerationIntroduced = generationIntroduced;
            if (effect == BattleMoveEffect.Pursuit && power > 125) throw new ArgumentOutOfRangeException(nameof(power), "Doubled HGSS Pursuit power must remain within the validated damage API range.");
            if (effect == BattleMoveEffect.LeechSeed && Type != Element.Grass) throw new ArgumentException("Leech Seed is a Grass-type effect.");
            bool conditionEffect = effect == BattleMoveEffect.InflictSleep || effect == BattleMoveEffect.InflictFreeze || effect == BattleMoveEffect.InflictPoison || effect == BattleMoveEffect.InflictBadPoison || effect == BattleMoveEffect.InflictBurn || effect == BattleMoveEffect.InflictParalysis;
            bool selfUtility = effect == BattleMoveEffect.Protect || effect == BattleMoveEffect.Ingrain || effect == BattleMoveEffect.AquaRing || effect == BattleMoveEffect.SetRain || effect == BattleMoveEffect.SetSun || effect == BattleMoveEffect.SetSandstorm || effect == BattleMoveEffect.SetHail || effect == BattleMoveEffect.SetSpikes || effect == BattleMoveEffect.SetToxicSpikes || effect == BattleMoveEffect.SetStealthRock || effect == BattleMoveEffect.SetReflect || effect == BattleMoveEffect.SetLightScreen || effect == BattleMoveEffect.Imprison;
            bool statusOnly = selfUtility || effect == BattleMoveEffect.LeechSeed || effect == BattleMoveEffect.HealBlock || effect == BattleMoveEffect.Confuse || effect == BattleMoveEffect.Infatuate || effect == BattleMoveEffect.TrapSwitch || effect == BattleMoveEffect.Disable || effect == BattleMoveEffect.Encore || effect == BattleMoveEffect.Taunt || effect == BattleMoveEffect.ForceSwitch;
            if (category == Category.Status ? power != 0 || (!conditionEffect && !statusOnly && effect != BattleMoveEffect.Flinch) || effectChance != 0 : power == 0 || statusOnly || effect == BattleMoveEffect.Struggle && category != Category.Physical || effect == BattleMoveEffect.Pursuit && Type != Element.Dark || effect == BattleMoveEffect.RapidSpin && Type != Element.Normal) throw new ArgumentException("Move category/effect/power combination is unsupported.");
        }
    }

    public sealed class AdvancedPokemon
    {
        public PokemonRecord Record { get; }
        public int Hp { get; internal set; }
        public Condition Condition { get; internal set; }
        public int ConditionCounter { get; internal set; }
        public int ToxicCounter { get; internal set; }
        public int ConfusionTurns { get; internal set; }
        public int FlinchTurns { get; internal set; }
        public int InfatuatedBy { get; internal set; }
        public bool SwitchLocked { get; internal set; }
        public string LastMoveId { get; internal set; }
        public string DisabledMoveId { get; internal set; }
        public int DisableTurns { get; internal set; }
        public string EncoreMoveId { get; internal set; }
        public int EncoreTurns { get; internal set; }
        public int TauntTurns { get; internal set; }
        public bool Imprisoning { get; internal set; }
        /// <summary>Source battle side for Leech Seed, or -1 when this individual is not seeded.</summary>
        public int LeechSeedSourceSide { get; internal set; }
        public int BindingTurns { get; internal set; }
        public int BindingSourceSide { get; internal set; }
        public Guid BindingSourceIndividualId { get; internal set; }
        public string BindingMoveId { get; internal set; }
        public bool Ingrained { get; internal set; }
        public bool AquaRing { get; internal set; }
        public int HealBlockTurns { get; internal set; }
        public bool AbilitySuppressed { get; internal set; }
        public string BaseAbilityId { get; }
        public string HeldItemId { get; }
        public int ProtectChain { get; internal set; }
        public bool Protecting { get; internal set; }
        private readonly int[] pp;
        private readonly int[] stages;
        public IReadOnlyList<int> Pp => Array.AsReadOnly(pp);
        public IReadOnlyList<int> Stages => Array.AsReadOnly(stages);
        internal int[] RawPp => pp;
        internal int[] RawStages => stages;
        public bool IsMale { get; }
        public bool IsGenderless { get; }
        public string AbilityId { get; internal set; }
        public IReadOnlyList<BattleMove> Moves { get; }
        public AdvancedPokemon(PokemonRecord record, IEnumerable<BattleMove> moves, bool isMale = true, bool isGenderless = false, string? abilityId = null, int? hp = null, Condition condition = Condition.Healthy, int conditionCounter = 0, int toxicCounter = 0, int confusionTurns = 0, int flinchTurns = 0, int infatuatedBy = -1, bool switchLocked = false, int protectChain = 0, IEnumerable<int>? pp = null, IEnumerable<int>? stages = null, string? lastMoveId = null, string? disabledMoveId = null, int disableTurns = 0, string? encoreMoveId = null, int encoreTurns = 0, int tauntTurns = 0, bool imprisoning = false, int leechSeedSourceSide = -1, int bindingTurns = 0, int bindingSourceSide = -1, Guid bindingSourceIndividualId = default, string? bindingMoveId = null, bool ingrained = false, bool aquaRing = false, int healBlockTurns = 0, bool abilitySuppressed = false, string? heldItemId = null, string? currentAbilityId = null)
        {
            Record = record ?? throw new ArgumentNullException(nameof(record)); var ms = moves?.ToArray() ?? throw new ArgumentNullException(nameof(moves));
            if (ms.Length < 1 || ms.Length > 4 || ms.Select(m => m.Id).Distinct().Count() != ms.Length) throw new ArgumentException("One to four distinct battle moves required.");
            Moves = Array.AsReadOnly(ms); IsMale = isMale; IsGenderless = isGenderless; BaseAbilityId = abilityId ?? (record.Ability == Ability.NoneFixture ? "none" : record.Ability == Ability.RunAway ? "run-away" : record.Ability.ToString().ToLowerInvariant()); AbilityId = currentAbilityId ?? BaseAbilityId; HeldItemId = heldItemId ?? "none";
            AbilityRegistry.Require(BaseAbilityId); AbilityRegistry.Require(AbilityId);
            if (HeldItemId != "none" && HeldItemId != "big-root") throw new NotSupportedException("Held item has no validated Phase 3 battle hook: " + HeldItemId);
            int maxHp = HgssRules.CalculateStats(record)[Stat.Hp]; Hp = hp ?? maxHp; if (Hp < 0 || Hp > maxHp) throw new ArgumentOutOfRangeException(nameof(hp));
            if (!Enum.IsDefined(typeof(Condition), condition)) throw new ArgumentOutOfRangeException(nameof(condition)); Condition = condition;
            ConditionCounter = Range(conditionCounter, 0, 7); ToxicCounter = Range(toxicCounter, 0, 15); ConfusionTurns = Range(confusionTurns, 0, 5); FlinchTurns = Range(flinchTurns, 0, 1); InfatuatedBy = Range(infatuatedBy, -1, 5); SwitchLocked = switchLocked; ProtectChain = Range(protectChain, 0, 6);
            if (condition != Condition.Healthy && condition != Condition.BadPoison && toxicCounter != 0) throw new ArgumentException("Toxic counter exists only for bad poison.");
            this.pp = pp?.ToArray() ?? ms.Select(m => m.MaxPp).ToArray(); this.stages = stages?.ToArray() ?? new int[7];
            if (this.pp.Length != ms.Length || this.stages.Length != 7 || this.pp.Where((x, i) => x < 0 || x > ms[i].MaxPp).Any() || this.stages.Any(x => x < -6 || x > 6)) throw new ArgumentException("PP/stage snapshot invalid.");
            LastMoveId = ValidateMoveReference(lastMoveId, ms, nameof(lastMoveId));
            DisabledMoveId = ValidateMoveReference(disabledMoveId, ms, nameof(disabledMoveId)); DisableTurns = Range(disableTurns, 0, 6);
            EncoreMoveId = ValidateMoveReference(encoreMoveId, ms, nameof(encoreMoveId)); EncoreTurns = Range(encoreTurns, 0, 7); TauntTurns = Range(tauntTurns, 0, 3); Imprisoning = imprisoning; LeechSeedSourceSide = Range(leechSeedSourceSide, -1, 1);
            BindingTurns = Range(bindingTurns, 0, 5); BindingSourceSide = Range(bindingSourceSide, -1, 1); BindingSourceIndividualId = bindingSourceIndividualId; BindingMoveId = bindingMoveId ?? string.Empty; Ingrained = ingrained; AquaRing = aquaRing; HealBlockTurns = Range(healBlockTurns, 0, 5); AbilitySuppressed = abilitySuppressed;
            if ((BindingTurns == 0) != (BindingSourceSide == -1) || (BindingTurns == 0) != (BindingSourceIndividualId == Guid.Empty) || (BindingTurns == 0) != (BindingMoveId.Length == 0)) throw new ArgumentException("Binding source and duration must be set together.");
            if ((DisableTurns == 0) != (DisabledMoveId.Length == 0) || (EncoreTurns == 0) != (EncoreMoveId.Length == 0)) throw new ArgumentException("Move restriction ID and duration must be set together.");
            if (condition == Condition.Freeze && record.Species.HasType(Element.Ice) || (condition == Condition.Poison || condition == Condition.BadPoison) && (record.Species.HasType(Element.Poison) || record.Species.HasType(Element.Steel)) || condition == Condition.Burn && record.Species.HasType(Element.Fire)) throw new ArgumentException("Initial condition conflicts with a Gen IV type immunity.");
        }
        private static int Range(int x, int lo, int hi) { if (x < lo || x > hi) throw new ArgumentOutOfRangeException(); return x; }
        private static string ValidateMoveReference(string? id, BattleMove[] moves, string parameter)
        { if (string.IsNullOrEmpty(id)) return string.Empty; if (!moves.Any(m => m.Id == id)) throw new ArgumentException("Move restriction references an unknown move.", parameter); return id; }
        public AdvancedPokemon DeepCopy() => new AdvancedPokemon(Record, Moves, IsMale, IsGenderless, BaseAbilityId, Hp, Condition, ConditionCounter, ToxicCounter, ConfusionTurns, FlinchTurns, InfatuatedBy, SwitchLocked, ProtectChain, Pp, Stages, LastMoveId, DisabledMoveId, DisableTurns, EncoreMoveId, EncoreTurns, TauntTurns, Imprisoning, LeechSeedSourceSide, BindingTurns, BindingSourceSide, BindingSourceIndividualId, BindingMoveId, Ingrained, AquaRing, HealBlockTurns, AbilitySuppressed, HeldItemId, AbilityId);
        public static int RestorePp(int current, int maximum, int amount)
        { if (current < 0 || maximum < current || amount < 0) throw new ArgumentOutOfRangeException(nameof(current)); return (int)Math.Min(maximum, (long)current + amount); }
    }

    public sealed class SideConditions
    {
        public int SpikesLayers { get; internal set; }
        public int ToxicSpikesLayers { get; internal set; }
        public bool StealthRock { get; internal set; }
        public int ReflectTurns { get; internal set; }
        public int LightScreenTurns { get; internal set; }
        public SideConditions(int spikesLayers = 0, int toxicSpikesLayers = 0, bool stealthRock = false, int reflectTurns = 0, int lightScreenTurns = 0)
        {
            if (spikesLayers < 0 || spikesLayers > 3 || toxicSpikesLayers < 0 || toxicSpikesLayers > 2 || reflectTurns < 0 || reflectTurns > 5 || lightScreenTurns < 0 || lightScreenTurns > 5) throw new ArgumentOutOfRangeException(nameof(spikesLayers));
            SpikesLayers = spikesLayers; ToxicSpikesLayers = toxicSpikesLayers; StealthRock = stealthRock; ReflectTurns = reflectTurns; LightScreenTurns = lightScreenTurns;
        }
        public SideConditions DeepCopy() => new SideConditions { SpikesLayers = SpikesLayers, ToxicSpikesLayers = ToxicSpikesLayers, StealthRock = StealthRock, ReflectTurns = ReflectTurns, LightScreenTurns = LightScreenTurns };
    }

    public sealed class AdvancedBattleState
    {
        private readonly AdvancedPokemon[][] teams; private readonly int[] active;
        public string Ruleset { get; } public Guid EncounterId { get; } public uint RandomState { get; internal set; } public long RandomDraws { get; internal set; } public int Turn { get; internal set; }
        public Weather Weather { get; internal set; } public int WeatherTurns { get; internal set; }
        private readonly SideConditions[] sides;
        public SideConditions Conditions(Side side) => sides[(int)side].DeepCopy();
        public IReadOnlyList<AdvancedPokemon> Team(Side side) => Array.AsReadOnly(teams[(int)side]);
        public int ActiveIndex(Side side) => active[(int)side]; public AdvancedPokemon Active(Side side) => teams[(int)side][active[(int)side]];
        public bool NeedsReplacement(Side side) => Outcome == BattleOutcome.Ongoing && Active(side).Hp == 0;
        public BattleOutcome Outcome => teams[0].All(p => p.Hp == 0) ? teams[1].All(p => p.Hp == 0) ? BattleOutcome.Draw : BattleOutcome.OpponentWin : teams[1].All(p => p.Hp == 0) ? BattleOutcome.PlayerWin : BattleOutcome.Ongoing;
        public AdvancedBattleState(IEnumerable<AdvancedPokemon> player, IEnumerable<AdvancedPokemon> opponent, uint seed, Guid encounterId, string ruleset = CompatibilityProfile.Hgss, int playerActive = 0, int opponentActive = 0, int turn = 0, Weather weather = Weather.Clear, int weatherTurns = 0, SideConditions? playerConditions = null, SideConditions? opponentConditions = null)
        {
            if (ruleset != CompatibilityProfile.Hgss && ruleset != CompatibilityProfile.CrossGenerationCatalog) throw new NotSupportedException("Unsupported ruleset.");
            if (encounterId == Guid.Empty || seed == 0) throw new ArgumentException("Stable encounter identity and nonzero seed required.");
            Ruleset = ruleset; EncounterId = encounterId; RandomState = seed; Turn = turn; Weather = weather; WeatherTurns = weatherTurns;
            if (!Enum.IsDefined(typeof(Weather), weather) || turn < 0 || weatherTurns < 0 || weatherTurns > 8) throw new ArgumentOutOfRangeException(nameof(weather));
            teams = new[] { player.Select(p => p.DeepCopy()).ToArray(), opponent.Select(p => p.DeepCopy()).ToArray() };
            if (teams.Any(t => t.Length < 1 || t.Length > 6) || teams.SelectMany(t => t).Select(p => p.Record.IndividualId).Distinct().Count() != teams.Sum(t => t.Length)) throw new ArgumentException("Teams need one to six uniquely identified individuals.");
            active = new[] { Range(playerActive, teams[0].Length), Range(opponentActive, teams[1].Length) };
            sides = new[] { playerConditions?.DeepCopy() ?? new SideConditions(), opponentConditions?.DeepCopy() ?? new SideConditions() };
            foreach (var c in sides) if (c.SpikesLayers < 0 || c.SpikesLayers > 3 || c.ToxicSpikesLayers < 0 || c.ToxicSpikesLayers > 2 || c.ReflectTurns < 0 || c.ReflectTurns > 5 || c.LightScreenTurns < 0 || c.LightScreenTurns > 5) throw new ArgumentOutOfRangeException(nameof(playerConditions));
        }
        private static int Range(int x, int count) { if (x < 0 || x >= count) throw new ArgumentOutOfRangeException(); return x; }
        internal SideConditions SideCondition(Side side) => sides[(int)side];
        internal void Switch(Side side, int index)
        {
            var outgoing = Active(side); Guid outgoingId = outgoing.Record.IndividualId;
            foreach (var p in teams.SelectMany(t => t)) if (p.BindingSourceIndividualId == outgoingId) ClearBinding(p);
            ResetOnSwitch(outgoing); active[(int)side] = index; ResetOnSwitch(Active(side)); Active(side).AbilityId = Active(side).BaseAbilityId;
        }
        private static void ClearBinding(AdvancedPokemon p) { p.BindingTurns = 0; p.BindingSourceSide = -1; p.BindingSourceIndividualId = Guid.Empty; p.BindingMoveId = string.Empty; }
        private static void ResetOnSwitch(AdvancedPokemon p)
        {
            Array.Clear(p.RawStages, 0, p.RawStages.Length); p.ConfusionTurns = p.FlinchTurns = p.InfatuatedBy = p.ProtectChain = 0; p.Protecting = false; p.SwitchLocked = false; p.LeechSeedSourceSide = -1; ClearBinding(p); p.Ingrained = p.AquaRing = false; p.HealBlockTurns = 0; p.AbilitySuppressed = false;
            p.DisabledMoveId = p.EncoreMoveId = string.Empty; p.DisableTurns = p.EncoreTurns = p.TauntTurns = 0; p.Imprisoning = false;
            if (p.Condition == Condition.BadPoison) p.ToxicCounter = 0;
        }
        public AdvancedBattleState DeepCopy()
        {
            var s = new AdvancedBattleState(teams[0], teams[1], RandomState, EncounterId, Ruleset, active[0], active[1], Turn, Weather, WeatherTurns, sides[0], sides[1]); s.RandomDraws = RandomDraws; return s;
        }
        internal AdvancedPokemon[][] RawTeams => teams;
    }

    public sealed class BattleAction3
    {
        public ActionKind Kind { get; } public int Index { get; }
        private BattleAction3(ActionKind kind, int index) { Kind = kind; Index = index; }
        public static BattleAction3 Move(int slot) => new BattleAction3(ActionKind.Move, slot);
        public static BattleAction3 Switch(int partyIndex) => new BattleAction3(ActionKind.Switch, partyIndex);
    }
    public sealed class BattleEvent3
    {
        public int Turn { get; } public BattleEventKind Kind { get; } public string Actor { get; } public string Target { get; } public string Detail { get; } public int Amount { get; } public int Roll { get; }
        internal BattleEvent3(int turn, BattleEventKind kind, string actor = "", string target = "", string detail = "", int amount = 0, int roll = -1) { Turn = turn; Kind = kind; Actor = actor; Target = target; Detail = detail; Amount = amount; Roll = roll; }
    }
    public sealed class BattleStep3
    {
        public AdvancedBattleState State { get; } public IReadOnlyList<BattleEvent3> Events { get; } public string StateHash { get; }
        internal BattleStep3(AdvancedBattleState state, List<BattleEvent3> events) { State = state; Events = events.AsReadOnly(); StateHash = BattleCanonical.Hash(state, events); }
    }
    public static class BattleCanonical
    {
        public static string Hash(AdvancedBattleState s, IEnumerable<BattleEvent3>? events = null)
        {
            var b = new StringBuilder().Append("phase3.4-snapshot-1|").Append(s.Ruleset).Append('|').Append(HgssRules.RandomVersion).Append('|').Append(s.EncounterId.ToString("N")).Append('|').Append(s.Turn).Append('|').Append(s.RandomState).Append('|').Append(s.RandomDraws).Append('|').Append(s.Weather).Append('|').Append(s.WeatherTurns);
            foreach (Side side in new[] { Side.Player, Side.Opponent }) { b.Append('|').Append(side).Append(':').Append(s.ActiveIndex(side)); var c = s.Conditions(side); b.Append(':').Append(c.SpikesLayers).Append(':').Append(c.ToxicSpikesLayers).Append(':').Append(c.StealthRock).Append(':').Append(c.ReflectTurns).Append(':').Append(c.LightScreenTurns); foreach (var p in s.Team(side)) { b.Append(';').Append(p.Record.IndividualId.ToString("N")).Append(',').Append(p.Record.Species.Id).Append(',').Append(p.Record.Level).Append(',').Append(p.AbilityId).Append(',').Append(p.BaseAbilityId).Append(',').Append(p.HeldItemId).Append(',').Append(p.AbilitySuppressed).Append(',').Append(p.IsMale).Append(',').Append(p.IsGenderless).Append(',').Append(p.Hp).Append(',').Append(p.Condition).Append(',').Append(p.ConditionCounter).Append(',').Append(p.ToxicCounter).Append(',').Append(p.ConfusionTurns).Append(',').Append(p.FlinchTurns).Append(',').Append(p.InfatuatedBy).Append(',').Append(p.LeechSeedSourceSide).Append(',').Append(p.BindingTurns).Append(',').Append(p.BindingSourceSide).Append(',').Append(p.BindingSourceIndividualId.ToString("N")).Append(',').Append(p.BindingMoveId).Append(',').Append(p.Ingrained).Append(',').Append(p.AquaRing).Append(',').Append(p.HealBlockTurns).Append(',').Append(p.SwitchLocked).Append(',').Append(p.LastMoveId).Append(',').Append(p.DisabledMoveId).Append(',').Append(p.DisableTurns).Append(',').Append(p.EncoreMoveId).Append(',').Append(p.EncoreTurns).Append(',').Append(p.TauntTurns).Append(',').Append(p.Imprisoning).Append(',').Append(p.ProtectChain).Append(',').Append(p.Protecting).Append(',').Append(string.Join(".", p.Pp)).Append(',').Append(string.Join(".", p.Stages)); foreach (var m in p.Moves) b.Append('~').Append(m.Id).Append(':').Append(m.GenerationIntroduced).Append(':').Append(m.Type).Append(':').Append(m.Category).Append(':').Append(m.Power).Append(':').Append(m.Accuracy).Append(':').Append(m.BasePp).Append(':').Append(m.Priority).Append(':').Append(m.Effect).Append(':').Append(m.EffectChance).Append(':').Append(m.CriticalStage); } }
            if (events != null) foreach (var e in events) b.Append('|').Append(e.Turn).Append(',').Append(e.Kind).Append(',').Append(e.Actor).Append(',').Append(e.Target).Append(',').Append(e.Detail).Append(',').Append(e.Amount).Append(',').Append(e.Roll);
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(b.ToString()))).Replace("-", "").ToLowerInvariant();
        }
    }
}
