using System;
using System.Globalization;
using System.Linq;
using Roguelite.Gameplay;

namespace Roguelite.Domain
{
    // Mutable drafts are transport/edit buffers. Published domain objects own a validated deep copy.
    public sealed class ExtensionData
    {
        public string Key = "";
        public string Schema = "";
        public byte[] Payload = Array.Empty<byte>();
        public ExtensionData Copy() => new ExtensionData { Key = Key, Schema = Schema, Payload = (byte[])(Payload ?? throw new ArgumentException("Missing extension payload.")).Clone() };
    }
    public sealed class TrainerData
    {
        public uint TrainerId;
        public string Name = "";
        public Gender Gender;
        public GameLanguage Language;
        public TrainerData Copy() => (TrainerData)MemberwiseClone(); // Scalars and immutable strings only.
        public void Validate() { Check.Text(Name, 64); if (Name.Length == 0 || Check.Enum(Gender) == Gender.Genderless) throw new ArgumentException("Invalid original trainer."); Check.Enum(Language); }
    }
    public sealed class MoveData
    {
        public Guid SlotId;
        public string MoveId = "";
        public int Pp;
        public int PpUps;
        public MoveData Copy() => (MoveData)MemberwiseClone();
    }
    public sealed class EvolutionData
    {
        public Guid EventId;
        public string FromSpecies = "";
        public string FromForm = "";
        public string ToSpecies = "";
        public string ToForm = "";
        public string EvidenceId = "";
        public int Level;
        public EvolutionData Copy() => (EvolutionData)MemberwiseClone();
    }
    public sealed class CreatureData
    {
        public Guid IndividualId;
        public string SpeciesId = "";
        public string FormId = "";
        public uint Personality;
        public Gender Gender;
        public Nature Nature;
        public string AbilityId = "";
        public int AbilitySlot;
        public int[] Ivs = new int[6];
        public int[] Evs = new int[6];
        public int Level;
        public long Experience;
        public MoveData[] Moves = Array.Empty<MoveData>();
        public string HeldItemId = "none";
        public int Friendship;
        public TrainerData OriginalTrainer = new TrainerData();
        public Guid HandlerId;
        public GameLanguage Language;
        public string Nickname = "";
        public bool IsNicknamed;
        public int CurrentHp;
        public PersistentStatus Status;
        public int SleepTurns;
        public bool IsEgg;
        public int EggCyclesRemaining;
        public int OriginGame;
        public int MetLocation;
        public int MetLevel;
        public string MetDate = "";
        public int EggMetLocation;
        public string EggMetDate = "";
        public string CaptureBallId = "poke-ball";
        public bool FatefulEncounter;
        public int PokerusStrain;
        public int PokerusDays;
        public int Markings;
        public int ShinyLeaves;
        public int Mood;
        public int MetTerrain;
        public int LegacyEggMetLocation;
        public int LegacyMetLocation;
        public int LegacyBallCode;
        public int BallCapsuleId;
        public byte[] MailData = Array.Empty<byte>(); // Optional 56-byte HGSS metadata; uninterpreted, not letter rendering.
        public byte[] CapsuleSeals = Array.Empty<byte>(); // Optional 24-byte HGSS seal metadata, no graphics.
        public byte[] ContestStats = new byte[6];
        public byte[] RibbonBits = Array.Empty<byte>();
        public EvolutionData[] EvolutionHistory = Array.Empty<EvolutionData>();
        public ExtensionData[] Extensions = Array.Empty<ExtensionData>();
        public CreatureData Copy()
        {
            if (Ivs == null || Evs == null || Moves == null || Moves.Any(m => m == null) || OriginalTrainer == null || ContestStats == null || RibbonBits == null || MailData == null || CapsuleSeals == null || EvolutionHistory == null || EvolutionHistory.Any(e => e == null) || Extensions == null || Extensions.Any(e => e == null)) throw new ArgumentException("Missing creature field.");
            var c = (CreatureData)MemberwiseClone(); c.Ivs = (int[])Ivs.Clone(); c.Evs = (int[])Evs.Clone(); c.Moves = Moves.Select(m => m.Copy()).ToArray(); c.OriginalTrainer = OriginalTrainer.Copy(); c.ContestStats = (byte[])ContestStats.Clone(); c.RibbonBits = (byte[])RibbonBits.Clone(); c.MailData = (byte[])MailData.Clone(); c.CapsuleSeals = (byte[])CapsuleSeals.Clone(); c.EvolutionHistory = EvolutionHistory.Select(e => e.Copy()).ToArray(); c.Extensions = Extensions.Select(e => e.Copy()).ToArray(); return c;
        }
    }
    public static class DomainValidation
    {
        public static void Extensions(ExtensionData[] entries)
        {
            Check.Range(entries.Length, 0, 64, nameof(entries)); if (entries.Select(e => e.Key).Distinct(StringComparer.Ordinal).Count() != entries.Length) throw new ArgumentException("Duplicate extensions.");
            foreach (var e in entries) { Check.Id(e.Key); Check.Id(e.Schema); Check.Range(e.Payload.Length, 0, 65536, "extension length"); }
        }
        public static void Date(string date) { if (date != "" && (!DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) || d.Year < 2000 || d.Year > 2099)) throw new ArgumentException("Invalid HGSS date."); }
        public static SixStats Six(int[] a, int max) { if (a.Length != 6 || a.Any(x => x < 0 || x > max)) throw new ArgumentException("Invalid six-stat array."); return new SixStats(a[0], a[1], a[2], a[3], a[4], a[5]); }
    }
    public sealed class PersistentPokemon
    {
        private readonly CreatureData data;
        private readonly Catalog catalog;
        public Guid IndividualId => data.IndividualId;
        public string SpeciesId => data.SpeciesId;
        public string FormId => data.FormId;
        public int Level => data.Level;
        public long Experience => data.Experience;
        public SixStats Stats { get; }
        public bool IsShiny => ((data.OriginalTrainer.TrainerId & 65535) ^ (data.OriginalTrainer.TrainerId >> 16) ^ (data.Personality & 65535) ^ (data.Personality >> 16)) < 8;
        public string CatalogHash => catalog.ContentHash;
        public PersistentPokemon(CreatureData draft, Catalog catalog)
        {
            if (draft == null) throw new ArgumentNullException(nameof(draft)); this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog)); data = draft.Copy();
            Check.Identity(data.IndividualId); Check.Identity(data.HandlerId); var species = catalog.SpeciesById(data.SpeciesId); var form = species.Form(data.FormId);
            Check.Enum(data.Gender); Check.Enum(data.Nature); Check.Enum(data.Language);
            if (data.Gender != species.GenderFor(data.Personality) || (int)data.Nature != data.Personality % 25) throw new ArgumentException("HGSS PID, gender and nature disagree.");
            if (form.ResolveAbility(data.AbilitySlot) != data.AbilityId) throw new ArgumentException("Ability does not match the explicit species/form slot."); catalog.Ability(data.AbilityId);
            var ivs = DomainValidation.Six(data.Ivs, 31); var evs = DomainValidation.Six(data.Evs, 255); if (data.Evs.Sum() > 510) throw new ArgumentException("EV sum exceeds 510.");
            Check.Range(data.Level, 1, 100, "level"); ExperienceCurve.Validate(species.Growth, data.Level, data.Experience);
            Check.Range(data.Moves.Length, data.IsEgg ? 0 : 1, 4, "moves");
            if (data.Moves.Select(m => m.SlotId).Distinct().Count() != data.Moves.Length || data.Moves.Select(m => m.MoveId).Distinct(StringComparer.Ordinal).Count() != data.Moves.Length) throw new ArgumentException("Duplicate move IDs/slots.");
            foreach (var m in data.Moves) { Check.Identity(m.SlotId); var def = catalog.Move(m.MoveId); Check.Range(m.Pp, 0, def.MaximumPp(m.PpUps), "PP"); }
            var item = catalog.Item(data.HeldItemId); if (item.Id != "none" && !item.Holdable || data.IsEgg && item.Id != "none") throw new ArgumentException("Item is not holdable here.");
            if (!catalog.Item(data.CaptureBallId).CaptureBall) throw new ArgumentException("Capture ball reference is invalid.");
            Check.Range(data.Friendship, 0, 255, "friendship"); data.OriginalTrainer.Validate(); Check.Text(data.Nickname, 256); if (data.IsNicknamed && data.Nickname.Length == 0) throw new ArgumentException("Missing nickname.");
            Check.Enum(data.Status); Check.Range(data.SleepTurns, data.Status == PersistentStatus.Sleep ? 1 : 0, data.Status == PersistentStatus.Sleep ? 7 : 0, "sleep");
            if (data.Status == PersistentStatus.Burn && form.Stats.HasType(Element.Fire) || (data.Status == PersistentStatus.Poison || data.Status == PersistentStatus.Toxic) && (form.Stats.HasType(Element.Poison) || form.Stats.HasType(Element.Steel)) || data.Status == PersistentStatus.Freeze && form.Stats.HasType(Element.Ice)) throw new ArgumentException("Status is incompatible with this static form.");
            Check.Range(data.EggCyclesRemaining, 0, 65535, "egg cycles"); if (!data.IsEgg && data.EggCyclesRemaining != 0) throw new ArgumentException("Non-egg has egg cycles.");
            Check.Range(data.OriginGame, 0, 255, "origin game"); Check.Range(data.MetLocation, 0, 65535, "met location"); Check.Range(data.EggMetLocation, 0, 65535, "egg location"); Check.Range(data.MetLevel, 0, 100, "met level"); DomainValidation.Date(data.MetDate); DomainValidation.Date(data.EggMetDate);
            Check.Range(data.ShinyLeaves, 0, 63, "shiny leaves"); Check.Range(data.Mood, -128, 127, "mood"); Check.Range(data.MetTerrain, 0, 255, "met terrain"); Check.Range(data.LegacyEggMetLocation, 0, 65535, "legacy egg location"); Check.Range(data.LegacyMetLocation, 0, 65535, "legacy location"); Check.Range(data.LegacyBallCode, 0, 255, "legacy ball"); Check.Range(data.BallCapsuleId, 0, 255, "capsule");
            if (data.MailData.Length != 0 && data.MailData.Length != 56 || data.CapsuleSeals.Length != 0 && data.CapsuleSeals.Length != 24) throw new ArgumentException("Invalid native metadata length.");
            Check.Range(data.PokerusStrain, 0, 15, "pokerus strain"); Check.Range(data.PokerusDays, 0, 15, "pokerus days"); if (data.PokerusStrain == 0 && data.PokerusDays != 0) throw new ArgumentException("Invalid Pokerus state."); Check.Range(data.Markings, 0, 63, "markings"); if (data.ContestStats.Length != 6 || data.RibbonBits.Length > 32) throw new ArgumentException("Invalid contest/ribbon data.");
            DomainValidation.Extensions(data.Extensions); Check.Range(data.EvolutionHistory.Length, 0, 100, "evolution history"); if (data.EvolutionHistory.Select(e => e.EventId).Distinct().Count() != data.EvolutionHistory.Length) throw new ArgumentException("Duplicate evolution event.");
            string? previousSpecies = null; string? previousForm = null;
            foreach (var e in data.EvolutionHistory)
            {
                Check.Identity(e.EventId); var from = catalog.SpeciesById(e.FromSpecies); from.Form(e.FromForm); catalog.SpeciesById(e.ToSpecies).Form(e.ToForm); Check.Id(e.EvidenceId); Check.Range(e.Level, 1, 100, "evolution level");
                if (!from.Evolutions.Contains(e.ToSpecies) || previousSpecies != null && (e.FromSpecies != previousSpecies || e.FromForm != previousForm)) throw new ArgumentException("Invalid evolution history chain."); previousSpecies = e.ToSpecies; previousForm = e.ToForm;
            }
            if (previousSpecies != null && (previousSpecies != data.SpeciesId || previousForm != data.FormId)) throw new ArgumentException("Evolution history does not end at current form.");
            // Stats-only projection: ability and move have no role in this unchanged Phase 1 formula.
            var statInput = new PokemonRecord(data.IndividualId, form.Stats, data.Level, ivs, evs, data.Nature, Ability.NoneFixture, new[] { PilotCatalog.Tackle });
            Stats = HgssRules.CalculateStats(statInput); Check.Range(data.CurrentHp, 0, Stats[Stat.Hp], "HP");
        }
        public CreatureData Export() => data.Copy();
        public PersistentPokemon RecordEvolution(string targetSpecies, string targetForm, Guid eventId, string evidenceId)
        {
            if (data.IsEgg) throw new InvalidOperationException("Eggs cannot evolve."); var target = catalog.SpeciesById(targetSpecies); var source = catalog.SpeciesById(data.SpeciesId);
            if (!source.Evolutions.Contains(targetSpecies) || target.Growth != source.Growth) throw new NotSupportedException("Unapproved evolution/growth transition.");
            var draft = Export(); draft.SpeciesId = targetSpecies; draft.FormId = targetForm; draft.AbilityId = target.Form(targetForm).ResolveAbility(draft.AbilitySlot);
            draft.EvolutionHistory = draft.EvolutionHistory.Concat(new[] { new EvolutionData { EventId = eventId, FromSpecies = SpeciesId, FromForm = FormId, ToSpecies = targetSpecies, ToForm = targetForm, EvidenceId = evidenceId, Level = Level } }).ToArray();
            draft.CurrentHp = 0; var provisional = new PersistentPokemon(draft, catalog); draft.CurrentHp = data.CurrentHp == 0 ? 0 : Math.Min(provisional.Stats[Stat.Hp], data.CurrentHp + provisional.Stats[Stat.Hp] - Stats[Stat.Hp]); return new PersistentPokemon(draft, catalog);
        }
    }
}
