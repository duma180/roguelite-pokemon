using System;
using System.Collections.Generic;
using System.Linq;
using Roguelite.Gameplay;

namespace Roguelite.Domain
{
    public sealed class SlotData
    {
        public Guid SlotId;
        public Guid? CreatureId;
        public SlotData Copy() => (SlotData)MemberwiseClone();
    }
    public sealed class BoxData
    {
        public Guid BoxId;
        public string Label = "";
        public string ProtectionPolicyReference = ""; // Reserved, uninterpreted; not a boolean or final policy.
        public SlotData[] Slots = Array.Empty<SlotData>();
        public BoxData Copy()
        {
            if (Slots == null || Slots.Any(s => s == null)) throw new ArgumentException("Missing box slots."); return new BoxData { BoxId = BoxId, Label = Label, ProtectionPolicyReference = ProtectionPolicyReference, Slots = Slots.Select(s => s.Copy()).ToArray() };
        }
    }
    public sealed class SaveData
    {
        public Guid SaveId;
        public Guid CommitId;
        public long Revision;
        public Guid HandlerId;
        public TrainerData Player = new TrainerData();
        public string CatalogId = "";
        public string CatalogVersion = "";
        public string CatalogHash = "";
        public string Ruleset = HgssRules.Version;
        public string Description = ""; // Introduced in save schema 2.0; schema 1.0 migration sets empty text.
        public CreatureData[] Creatures = Array.Empty<CreatureData>();
        public SlotData[] Party = Array.Empty<SlotData>();
        public BoxData[] Boxes = Array.Empty<BoxData>();
        public ExtensionData[] Extensions = Array.Empty<ExtensionData>();
        public SaveData Copy()
        {
            if (Player == null || Creatures == null || Creatures.Any(p => p == null) || Party == null || Party.Any(s => s == null) || Boxes == null || Boxes.Any(b => b == null) || Extensions == null || Extensions.Any(e => e == null)) throw new ArgumentException("Missing roster field.");
            var c = (SaveData)MemberwiseClone(); c.Player = Player.Copy(); c.Creatures = Creatures.Select(p => p.Copy()).ToArray(); c.Party = Party.Select(s => s.Copy()).ToArray(); c.Boxes = Boxes.Select(b => b.Copy()).ToArray(); c.Extensions = Extensions.Select(e => e.Copy()).ToArray(); return c;
        }
    }
    public sealed class CatalogMismatchException : Exception { public CatalogMismatchException(string message) : base(message) { } }
    public sealed class SaveSnapshot
    {
        private readonly SaveData data;
        private readonly PersistentPokemon[] creatures;
        public Catalog Catalog { get; }
        public Guid SaveId => data.SaveId;
        public Guid CommitId => data.CommitId;
        public long Revision => data.Revision;
        public IReadOnlyList<PersistentPokemon> Creatures => Array.AsReadOnly(creatures);
        public SaveSnapshot(SaveData draft, Catalog catalog)
        {
            if (draft == null) throw new ArgumentNullException(nameof(draft)); Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            data = draft.Copy();
            if (data.CatalogId != catalog.Id || data.CatalogVersion != catalog.Version || data.CatalogHash != catalog.ContentHash) throw new CatalogMismatchException("Save catalog identity/version/hash differs; explicit catalog migration is required.");
            if (data.Ruleset != HgssRules.Version) throw new NotSupportedException("Unsupported save ruleset.");
            Check.Identity(data.SaveId); Check.Identity(data.CommitId); Check.Identity(data.HandlerId); Check.Range(data.Revision, 0, long.MaxValue, "revision"); data.Player.Validate(); Check.Text(data.Description, 1024);
            Check.Range(data.Creatures.Length, 0, 546, "creatures"); creatures = data.Creatures.Select(p => new PersistentPokemon(p, catalog)).ToArray();
            if (data.Party.Length != 6 || data.Boxes.Length < 1 || data.Boxes.Length > 18) throw new ArgumentException("HGSS storage profile: six party slots, one to eighteen 30-slot boxes.");
            var ids = new HashSet<Guid>(); void Unique(Guid id) { Check.Identity(id); if (!ids.Add(id)) throw new ArgumentException("Duplicate domain identity."); }
            Unique(data.SaveId); Unique(data.CommitId); Unique(data.HandlerId); foreach (var p in creatures) { var record = p.Export(); if (record.HandlerId != data.HandlerId) throw new ArgumentException("Creature handler differs from roster owner."); Unique(p.IndividualId); foreach (var m in record.Moves) Unique(m.SlotId); foreach (var e in record.EvolutionHistory) Unique(e.EventId); }
            foreach (var slot in data.Party) Unique(slot.SlotId);
            foreach (var box in data.Boxes)
            {
                Unique(box.BoxId); Check.Text(box.Label, 128); if (box.ProtectionPolicyReference != "") Check.Id(box.ProtectionPolicyReference);
                if (box.Slots.Length != 30) throw new ArgumentException("Box must have thirty stable slots."); foreach (var slot in box.Slots) Unique(slot.SlotId);
            }
            var placed = data.Party.Concat(data.Boxes.SelectMany(b => b.Slots)).Where(s => s.CreatureId.HasValue).Select(s => s.CreatureId!.Value).ToArray();
            if (placed.Distinct().Count() != placed.Length || !placed.OrderBy(x => x).SequenceEqual(creatures.Select(p => p.IndividualId).OrderBy(x => x))) throw new ArgumentException("Every creature must occupy exactly one valid location.");
            DomainValidation.Extensions(data.Extensions);
        }
        public PersistentPokemon Creature(Guid id) => creatures.SingleOrDefault(p => p.IndividualId == id) ?? throw new KeyNotFoundException("Unknown individual.");
        public SaveData Export() => data.Copy();
        public SaveSnapshot PrepareCommit(Guid commitId) { var c = Export(); c.CommitId = Check.Identity(commitId); c.Revision = checked(c.Revision + 1); return new SaveSnapshot(c, Catalog); }
        public SaveSnapshot ReplaceCreature(PersistentPokemon creature)
        {
            if (creature.CatalogHash != Catalog.ContentHash) throw new CatalogMismatchException("Creature catalog differs."); var c = Export(); int index = Array.FindIndex(c.Creatures, p => p.IndividualId == creature.IndividualId); if (index < 0) throw new KeyNotFoundException(); c.Creatures[index] = creature.Export(); return new SaveSnapshot(c, Catalog);
        }
    }
    public static class Storage
    {
        public static SaveSnapshot Transfer(SaveSnapshot snapshot, Guid sourceSlot, Guid targetSlot, bool swap = false)
        {
            if (sourceSlot == targetSlot) throw new InvalidOperationException("Source and target must differ."); var d = snapshot.Export(); var slots = d.Party.Concat(d.Boxes.SelectMany(b => b.Slots)).ToArray();
            var source = slots.SingleOrDefault(s => s.SlotId == sourceSlot) ?? throw new KeyNotFoundException(); var target = slots.SingleOrDefault(s => s.SlotId == targetSlot) ?? throw new KeyNotFoundException();
            if (!source.CreatureId.HasValue || target.CreatureId.HasValue && !swap) throw new InvalidOperationException("Empty source or occupied destination.");
            foreach (var b in d.Boxes) if (b.ProtectionPolicyReference != "" && b.Slots.Any(s => s.SlotId == sourceSlot || s.SlotId == targetSlot)) throw new NotSupportedException("Reserved box policy requires a reviewed policy resolver.");
            var other = target.CreatureId; target.CreatureId = source.CreatureId; source.CreatureId = other; return new SaveSnapshot(d, snapshot.Catalog);
        }
    }
    public static class CreatureFactory
    {
        public static PersistentPokemon Create(Catalog catalog, Guid id, string speciesId, string formId, int level, uint personality, TrainerData trainer, Guid handlerId, int[] ivs, int[] evs, params MoveData[] moves)
        {
            var species = catalog.SpeciesById(speciesId); var form = species.Form(formId); int slot = (int)(personality & 1);
            var draft = new CreatureData { IndividualId = id, SpeciesId = speciesId, FormId = formId, Level = level, Experience = ExperienceCurve.Threshold(species.Growth, level), Personality = personality, Gender = species.GenderFor(personality), Nature = (Nature)(personality % 25), AbilitySlot = slot, AbilityId = form.ResolveAbility(slot), OriginalTrainer = trainer.Copy(), HandlerId = handlerId, Language = trainer.Language, Ivs = (int[])ivs.Clone(), Evs = (int[])evs.Clone(), Moves = moves.Select(m => m.Copy()).ToArray(), Friendship = species.BaseFriendship };
            var provisional = new PersistentPokemon(draft, catalog); draft.CurrentHp = provisional.Stats[Stat.Hp]; return new PersistentPokemon(draft, catalog);
        }
    }
    public static class BattleProjection
    {
        public static BattlePokemon Create(PersistentPokemon pokemon, Catalog catalog)
        {
            if (pokemon.CatalogHash != catalog.ContentHash) throw new CatalogMismatchException("Projection catalog differs."); var p = pokemon.Export();
            if (p.IsEgg || p.HeldItemId != "none" || p.Status != PersistentStatus.Healthy && p.Status != PersistentStatus.Burn && p.Status != PersistentStatus.Paralysis) throw new NotSupportedException("Egg/item/status is outside Phase 1 battle support.");
            var ability = catalog.Ability(p.AbilityId).BattleAbility ?? throw new NotSupportedException("Ability is storable, not battle-supported.");
            var moves = p.Moves.Select(m => catalog.Move(m.MoveId).BattleDefinition ?? throw new NotSupportedException("Move is storable, not battle-supported.")).ToArray();
            if (p.Moves.Any(m => m.PpUps != 0)) throw new NotSupportedException("Phase 1 PP capacity does not implement PP Ups; no truncation permitted.");
            var definition = catalog.SpeciesById(p.SpeciesId).Form(p.FormId).Stats;
            var record = new PokemonRecord(p.IndividualId, definition, p.Level, DomainValidation.Six(p.Ivs, 31), DomainValidation.Six(p.Evs, 255), p.Nature, ability, moves);
            var supported = new[] { PilotCatalog.Bulbasaur, PilotCatalog.Charmander, PilotCatalog.Squirtle, PilotCatalog.Rattata, PilotCatalog.Magnemite }.SingleOrDefault(s => s.Id == definition.Id);
            if (p.FormId != "base" || supported == null || Canonical.Species(definition) != Canonical.Species(supported)) throw new NotSupportedException("Species/form is outside Phase 1 battle support.");
            var supportedMoves = new[] { PilotCatalog.Tackle, PilotCatalog.QuickAttack, PilotCatalog.Ember, PilotCatalog.WaterGun, PilotCatalog.RazorLeaf, PilotCatalog.Slash, PilotCatalog.Growl, PilotCatalog.ThunderWave, PilotCatalog.WillOWisp, PilotCatalog.Swift };
            foreach (var move in moves) if (!supportedMoves.Any(m => Canonical.Move(m) == Canonical.Move(move))) throw new NotSupportedException("Move semantics differ from Phase 1 support.");
            var status = p.Status == PersistentStatus.Burn ? MajorStatus.Burn : p.Status == PersistentStatus.Paralysis ? MajorStatus.Paralysis : MajorStatus.Healthy;
            var mon = new BattlePokemon(record, p.CurrentHp, status, p.Moves.Select(m => m.Pp));
            return mon;
        }
        public static PersistentPokemon ApplyResult(PersistentPokemon baseline, BattlePokemon result, Catalog catalog)
        {
            var expected = Create(baseline, catalog); var a = expected.Record; var b = result.Record;
            if (a.IndividualId != b.IndividualId || a.Level != b.Level || a.Nature != b.Nature || a.Ability != b.Ability || Canonical.Species(a.Species) != Canonical.Species(b.Species) || !a.Ivs.ToArray().SequenceEqual(b.Ivs.ToArray()) || !a.Evs.ToArray().SequenceEqual(b.Evs.ToArray()) || !a.Moves.Select(Canonical.Move).SequenceEqual(b.Moves.Select(Canonical.Move))) throw new InvalidOperationException("Battle result identity/definition differs from its baseline projection.");
            var d = baseline.Export(); d.CurrentHp = result.Hp; d.Status = result.Status == MajorStatus.Burn ? PersistentStatus.Burn : result.Status == MajorStatus.Paralysis ? PersistentStatus.Paralysis : PersistentStatus.Healthy; d.SleepTurns = 0;
            for (int i = 0; i < d.Moves.Length; i++) d.Moves[i].Pp = result.Pp[i]; return new PersistentPokemon(d, catalog);
        }
    }
    public sealed class CatalogRegistry
    {
        private readonly Dictionary<string, Catalog> catalogs = new Dictionary<string, Catalog>(StringComparer.Ordinal);
        public string ManifestHash { get; }
        public CatalogRegistry(IEnumerable<Catalog> entries)
        {
            foreach (var c in entries) { string key = c.Id + "/" + c.Version; if (catalogs.ContainsKey(key)) throw new ArgumentException("Catalog version already registered; rebinding to different content is forbidden."); catalogs.Add(key, c); }
            if (catalogs.Count == 0) throw new ArgumentException("Empty catalog registry.");
            using var stream = new System.IO.MemoryStream(); using var writer = new System.IO.BinaryWriter(stream, Wire.Utf8, true); Wire.Text(writer, "catalog-registry-1.0.0"); foreach (var entry in catalogs.OrderBy(e => e.Key, StringComparer.Ordinal)) { Wire.Text(writer, entry.Key); Wire.Text(writer, entry.Value.ContentHash); } writer.Flush(); ManifestHash = Wire.Hash(stream.ToArray());
        }
        public Catalog Resolve(string id, string version, string expectedHash)
        {
            Check.Id(id); Check.Id(version); if (!catalogs.TryGetValue(id + "/" + version, out var result)) throw new KeyNotFoundException("Catalog version is unavailable; no latest-version fallback."); if (result.ContentHash != expectedHash) throw new CatalogMismatchException("Catalog content hash differs."); return result;
        }
    }
}
