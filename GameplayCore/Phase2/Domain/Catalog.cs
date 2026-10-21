using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Roguelite.Gameplay;

namespace Roguelite.Domain
{
    public sealed class AbilityDefinition
    {
        public string Id { get; }
        public Ability? BattleAbility { get; }
        public AbilityDefinition(string id, Ability? battleAbility)
        {
            Id = Check.Id(id); if (battleAbility.HasValue) Check.Enum(battleAbility.Value); if (battleAbility == Ability.NoneFixture) throw new ArgumentException("Synthetic ability is not authoritative content.");
            if (battleAbility.HasValue && id != (battleAbility == Ability.RunAway ? "run-away" : battleAbility.Value.ToString().ToLowerInvariant())) throw new ArgumentException("Ability adapter ID mismatch."); BattleAbility = battleAbility;
        }
    }
    public sealed class ItemDefinition
    {
        public string Id { get; }
        public bool Holdable { get; }
        public bool CaptureBall { get; }
        public ItemDefinition(string id, bool holdable, bool captureBall = false) { Id = Check.Id(id); Holdable = holdable; CaptureBall = captureBall; }
    }
    public sealed class CatalogMove
    {
        public string Id { get; }
        public int BasePp { get; }
        public MoveDefinition? BattleDefinition { get; }
        public int SourceEffectId { get; }
        public Element Type { get; }
        public Category Category { get; }
        public int Power { get; }
        public int Accuracy { get; }
        public int Priority { get; }
        public CatalogMove(string id, int basePp, int sourceEffectId, MoveDefinition battleDefinition) : this(id, basePp, sourceEffectId, battleDefinition.Type, battleDefinition.Category, battleDefinition.Power, battleDefinition.Accuracy, battleDefinition.Priority, battleDefinition) { }
        public CatalogMove(string id, int basePp, int sourceEffectId, Element type, Category category, int power, int accuracy, int priority, MoveDefinition? battleDefinition = null)
        {
            Id = Check.Id(id); BasePp = Check.Range(basePp, 1, 40, nameof(basePp)); SourceEffectId = Check.Range(sourceEffectId, 0, 65535, nameof(sourceEffectId)); BattleDefinition = battleDefinition;
            Type = Check.Enum(type); Category = Check.Enum(category); Power = Check.Range(power, 0, 250, nameof(power)); Accuracy = Check.Range(accuracy, 0, 100, nameof(accuracy)); Priority = Check.Range(priority, -7, 5, nameof(priority));
            if (category == Category.Status ? power != 0 : power == 0) throw new ArgumentException("Invalid catalog move category/power.");
            if (battleDefinition != null && (battleDefinition.Id != id || battleDefinition.MaxPp != basePp || battleDefinition.Type != type || battleDefinition.Category != category || battleDefinition.Power != power || battleDefinition.Accuracy != accuracy || battleDefinition.Priority != priority)) throw new ArgumentException("Move descriptor mismatch.");
        }
        public int MaximumPp(int ppUps) => BasePp * (5 + Check.Range(ppUps, 0, 3, nameof(ppUps))) / 5;
    }
    public sealed class FormDefinition
    {
        public string Id { get; }
        public SpeciesDefinition Stats { get; }
        public int SourceFormIndex { get; }
        private readonly string[] abilities;
        public IReadOnlyList<string> Abilities => Array.AsReadOnly(abilities);
        public FormDefinition(string id, SpeciesDefinition stats, params string[] abilities) : this(id, stats, 0, abilities) { }
        public FormDefinition(string id, SpeciesDefinition stats, int sourceFormIndex, params string[] abilities)
        {
            Id = Check.Id(id); Stats = stats ?? throw new ArgumentNullException(nameof(stats)); SourceFormIndex = Check.Range(sourceFormIndex, 0, 31, nameof(sourceFormIndex)); this.abilities = abilities.ToArray();
            if (this.abilities.Length != 2) throw new ArgumentException("Gen IV ability slots require two entries, possibly identical."); foreach (string a in this.abilities) Check.Id(a);
            if (this.abilities[0] == "none") throw new ArgumentException("Primary ability cannot be absent.");
        }
        public string ResolveAbility(int slot) { Check.Range(slot, 0, 1, nameof(slot)); return abilities[slot] == "none" ? abilities[0] : abilities[slot]; }
    }
    public sealed class CatalogSpecies
    {
        public string Id { get; }
        public int NationalDex { get; }
        public GrowthRate Growth { get; }
        public int FemaleThreshold { get; } // 0 male-only; 254 female-only; 255 genderless; otherwise PID low byte < threshold.
        public int BaseFriendship { get; }
        private readonly FormDefinition[] forms;
        private readonly string[] evolutions;
        public IReadOnlyList<FormDefinition> Forms => Array.AsReadOnly(forms);
        public IReadOnlyList<string> Evolutions => Array.AsReadOnly(evolutions);
        public CatalogSpecies(string id, int dex, GrowthRate growth, int femaleThreshold, IEnumerable<FormDefinition> forms, int baseFriendship = 70, params string[] evolutions)
        {
            Id = Check.Id(id); NationalDex = Check.Range(dex, 1, 493, nameof(dex)); Growth = Check.Enum(growth); FemaleThreshold = Check.Range(femaleThreshold, 0, 255, nameof(femaleThreshold)); BaseFriendship = Check.Range(baseFriendship, 0, 255, nameof(baseFriendship)); this.forms = forms.ToArray(); this.evolutions = evolutions.ToArray();
            if (this.forms.Length == 0 || this.forms.Select(f => f.Id).Distinct().Count() != this.forms.Length) throw new ArgumentException("Empty/duplicate forms.");
            if (this.forms.Select(f => f.SourceFormIndex).Distinct().Count() != this.forms.Length) throw new ArgumentException("Duplicate source form index.");
            foreach (var f in this.forms) if (f.Stats.Id != id) throw new ArgumentException("Form species mismatch."); foreach (var e in this.evolutions) Check.Id(e);
            if (this.evolutions.Distinct().Count() != this.evolutions.Length || this.evolutions.Contains(id)) throw new ArgumentException("Invalid evolution links.");
        }
        public FormDefinition Form(string id) => forms.SingleOrDefault(f => f.Id == id) ?? throw new KeyNotFoundException("Unknown form: " + id);
        public Gender GenderFor(uint pid) => FemaleThreshold == 255 ? Gender.Genderless : FemaleThreshold == 254 ? Gender.Female : (pid & 255) < FemaleThreshold ? Gender.Female : Gender.Male;
    }
    public sealed class Catalog
    {
        public const string Schema = "catalog-1.0.0";
        public string Id { get; }
        public string Version { get; }
        public string ContentHash { get; }
        private readonly CatalogSpecies[] species;
        private readonly CatalogMove[] moves;
        private readonly AbilityDefinition[] abilities;
        private readonly ItemDefinition[] items;
        public IReadOnlyList<CatalogSpecies> Species => Array.AsReadOnly(species);
        public IReadOnlyList<CatalogMove> Moves => Array.AsReadOnly(moves);
        public IReadOnlyList<AbilityDefinition> Abilities => Array.AsReadOnly(abilities);
        public IReadOnlyList<ItemDefinition> Items => Array.AsReadOnly(items);
        public Catalog(string id, string version, IEnumerable<CatalogSpecies> species, IEnumerable<CatalogMove> moves, IEnumerable<AbilityDefinition> abilities, IEnumerable<ItemDefinition> items)
        {
            Id = Check.Id(id); Version = Check.Id(version);
            this.species = species.OrderBy(x => x.Id, StringComparer.Ordinal).ToArray(); this.moves = moves.OrderBy(x => x.Id, StringComparer.Ordinal).ToArray(); this.abilities = abilities.OrderBy(x => x.Id, StringComparer.Ordinal).ToArray(); this.items = items.OrderBy(x => x.Id, StringComparer.Ordinal).ToArray();
            void Unique(IEnumerable<string> ids) { var a = ids.ToArray(); if (a.Length == 0 || a.Distinct(StringComparer.Ordinal).Count() != a.Length || a.Any(x => x.StartsWith("fixture-", StringComparison.Ordinal))) throw new ArgumentException("Empty/duplicate/synthetic catalog IDs."); }
            Unique(this.species.Select(s => s.Id)); Unique(this.moves.Select(m => m.Id)); Unique(this.abilities.Select(a => a.Id)); Unique(this.items.Select(i => i.Id));
            if (this.species.Select(s => s.NationalDex).Distinct().Count() != this.species.Length) throw new ArgumentException("Species IDs must not alias one national-dex species; use forms.");
            foreach (var s in this.species) { foreach (var f in s.Forms) foreach (string a in f.Abilities) Ability(a); foreach (string e in s.Evolutions) SpeciesById(e); }
            var visiting = new HashSet<string>(); var visited = new HashSet<string>();
            void Visit(CatalogSpecies s) { if (visited.Contains(s.Id)) return; if (!visiting.Add(s.Id)) throw new ArgumentException("Evolution graph contains a cycle."); foreach (string e in s.Evolutions) Visit(SpeciesById(e)); visiting.Remove(s.Id); visited.Add(s.Id); }
            foreach (var s in this.species) Visit(s);
            if (Item("none").Holdable || Item("none").CaptureBall) throw new ArgumentException("Invalid none item.");
            ContentHash = Wire.Hash(CanonicalBytes());
        }
        public CatalogSpecies SpeciesById(string id) => species.SingleOrDefault(s => s.Id == id) ?? throw new KeyNotFoundException("Unknown species: " + id);
        public CatalogMove Move(string id) => moves.SingleOrDefault(m => m.Id == id) ?? throw new KeyNotFoundException("Unknown move: " + id);
        public AbilityDefinition Ability(string id) => abilities.SingleOrDefault(a => a.Id == id) ?? throw new KeyNotFoundException("Unknown ability: " + id);
        public ItemDefinition Item(string id) => items.SingleOrDefault(i => i.Id == id) ?? throw new KeyNotFoundException("Unknown item: " + id);
        public byte[] CanonicalBytes()
        {
            using var stream = new MemoryStream(); using var w = new BinaryWriter(stream, Wire.Utf8, true);
            Wire.Text(w, Schema); Wire.Text(w, Id); Wire.Text(w, Version); Wire.Text(w, HgssRules.Version);
            w.Write(species.Length); foreach (var s in species)
            {
                Wire.Text(w, s.Id); w.Write(s.NationalDex); w.Write((int)s.Growth); w.Write(s.FemaleThreshold); w.Write(s.BaseFriendship);
                var forms = s.Forms.OrderBy(f => f.Id, StringComparer.Ordinal).ToArray(); w.Write(forms.Length); foreach (var f in forms) { Wire.Text(w, f.Id); w.Write(f.SourceFormIndex); Wire.Text(w, Canonical.Species(f.Stats)); foreach (string a in f.Abilities) Wire.Text(w, a); }
                var es = s.Evolutions.OrderBy(e => e, StringComparer.Ordinal).ToArray(); w.Write(es.Length); foreach (string e in es) Wire.Text(w, e);
            }
            w.Write(moves.Length); foreach (var m in moves) { Wire.Text(w, m.Id); w.Write(m.BasePp); w.Write(m.SourceEffectId); w.Write((int)m.Type); w.Write((int)m.Category); w.Write(m.Power); w.Write(m.Accuracy); w.Write(m.Priority); w.Write(m.BattleDefinition != null); if (m.BattleDefinition != null) Wire.Text(w, Canonical.Move(m.BattleDefinition)); }
            w.Write(abilities.Length); foreach (var a in abilities) { Wire.Text(w, a.Id); w.Write(a.BattleAbility.HasValue ? (int)a.BattleAbility.Value : -1); }
            w.Write(items.Length); foreach (var i in items) { Wire.Text(w, i.Id); w.Write(i.Holdable); w.Write(i.CaptureBall); }
            w.Flush(); return stream.ToArray();
        }
    }
}
