using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Roguelite.Domain;
using Roguelite.Gameplay;
using Roguelite.Persistence;
using Roguelite.Phase2.Fixtures;
using Xunit;

namespace Roguelite.Phase2.Tests
{
    public sealed class DomainTests
    {
        private static readonly JsonDocument References = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "references.json")));
        public static IEnumerable<object[]> GrowthVectors() => References.RootElement.GetProperty("growth").EnumerateArray().Select(r => new object[] { r.GetProperty("rate").GetInt32(), r.GetProperty("level").GetInt32(), r.GetProperty("experience").GetInt64() });
        [Theory, MemberData(nameof(GrowthVectors))] public void All600GrowthThresholdsMatchPinnedHgssTable(int rate, int level, long expected) => Assert.Equal(expected, ExperienceCurve.Threshold((GrowthRate)rate, level));
        [Fact] public void GrowthIsMonotoneAndLevelExperienceBoundariesReject()
        {
            foreach (GrowthRate r in Enum.GetValues(typeof(GrowthRate))) { for (int l = 1; l < 100; l++) { long min = ExperienceCurve.Threshold(r, l); long next = ExperienceCurve.Threshold(r, l + 1); Assert.True(next > min); ExperienceCurve.Validate(r, l, min); ExperienceCurve.Validate(r, l, next - 1); Assert.Throws<ArgumentException>(() => ExperienceCurve.Validate(r, l, next)); } ExperienceCurve.Validate(r, 100, ExperienceCurve.Threshold(r, 100)); Assert.Throws<ArgumentException>(() => ExperienceCurve.Validate(r, 100, ExperienceCurve.Threshold(r, 100) + 1)); }
        }
        [Fact] public void CatalogIsOrderIndependentCultureIndependentAndDefensivelyCopied()
        {
            var c = DomainCatalog.Create(); var array = c.Species.Reverse().ToArray(); var copy = new Catalog(c.Id, c.Version, array, c.Moves.Reverse(), c.Abilities.Reverse(), c.Items.Reverse()); Assert.Equal(c.ContentHash, copy.ContentHash); array[0] = array[1]; Assert.Equal(c.ContentHash, copy.ContentHash);
            var old = CultureInfo.CurrentCulture; try { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR"); Assert.Equal(c.ContentHash, DomainCatalog.Create().ContentHash); } finally { CultureInfo.CurrentCulture = old; }
        }
        [Fact] public void CatalogFactsMatchSourceStatsGrowthAndRawAbilitySlots()
        {
            var c = DomainCatalog.Create(); foreach (var s in c.Species) foreach (var f in s.Forms)
            {
                string key = s.Id.ToUpperInvariant() + (f.Id == "attack" ? "_ATK" : ""); var source = References.RootElement.GetProperty("species").GetProperty(key);
                Assert.Equal(new[] { "hp", "atk", "def", "spatk", "spdef", "speed" }.Select(k => source.GetProperty(k).GetInt32()), f.Stats.BaseStats.ToArray());
                Assert.Equal(source.GetProperty("friendship").GetInt32(), s.BaseFriendship);
                Assert.Equal(source.GetProperty("abilities").EnumerateArray().Select(a => a.GetString()!.Replace("ABILITY_", "").ToLowerInvariant().Replace('_', '-')), f.Abilities);
                Assert.Equal(source.GetProperty("growthRate").GetString()!.Replace("_", "").ToLowerInvariant(), s.Growth.ToString().ToLowerInvariant());
            }
            Assert.Equal("none", c.SpeciesById("bulbasaur").Form("base").Abilities[1]); Assert.Equal("overgrow", c.SpeciesById("bulbasaur").Form("base").ResolveAbility(1));
        }
        [Fact] public void CatalogValidationRejectsDuplicateIdsMissingReferencesAndCycles()
        {
            var c = DomainCatalog.Create(); Assert.Throws<ArgumentException>(() => new Catalog(c.Id, c.Version, c.Species.Concat(new[] { c.Species[0] }), c.Moves, c.Abilities, c.Items)); Assert.Throws<KeyNotFoundException>(() => new Catalog(c.Id, c.Version, c.Species, c.Moves, c.Abilities.Where(a => a.Id != "overgrow"), c.Items));
            var f = new FormDefinition("base", new SpeciesDefinition("alpha", "test", SixStats.Uniform(50), Element.Normal), "overgrow", "none"); var g = new FormDefinition("base", new SpeciesDefinition("beta", "test", SixStats.Uniform(50), Element.Normal), "overgrow", "none");
            var a = new CatalogSpecies("alpha", 20, GrowthRate.Fast, 0, new[] { f }, 70, "beta"); var b = new CatalogSpecies("beta", 21, GrowthRate.Fast, 0, new[] { g }, 70, "alpha"); Assert.Throws<ArgumentException>(() => new Catalog("test", "1", new[] { a, b }, c.Moves, c.Abilities, c.Items));
            Assert.Throws<ArgumentException>(() => new AbilityDefinition("pressure", Ability.Overgrow)); Assert.Throws<ArgumentException>(() => new AbilityDefinition("fake", Ability.NoneFixture));
        }
        [Fact] public void CatalogContentChangesInvalidateSavePinEvenIfVersionStringMatches()
        {
            var c = DomainCatalog.Create(); var alternate = new Catalog(c.Id, c.Version, c.Species, c.Moves, c.Abilities, c.Items.Concat(new[] { new ItemDefinition("new-item", true) })); Assert.NotEqual(c.ContentHash, alternate.ContentHash); Assert.Throws<CatalogMismatchException>(() => new SaveSnapshot(PilotWorld.World(c).Export(), alternate));
        }
        [Fact] public void CreatureCopyProtectsEveryNestedMutableArrayAndObject()
        {
            var c = DomainCatalog.Create(); var p = PilotWorld.Rich(c); var snapshot = PilotWorld.World(c); string before = Wire.Hash(SaveCodec.Encode(snapshot)); var d = p.Export();
            d.Ivs[0] = 0; d.Evs[0] = 0; d.Moves[0].Pp = 1; d.OriginalTrainer.Name = "changed"; d.ContestStats[0] = 255; d.RibbonBits[0] = 0; d.Extensions[0].Payload[0] = 77;
            d.MailData[0] = 255; d.CapsuleSeals[0] = 0; Assert.Equal(0, p.Export().MailData[0]); Assert.Equal(255, p.Export().CapsuleSeals[0]);
            Assert.Equal(31, p.Export().Ivs[0]); Assert.Equal(255, p.Export().Evs[0]); Assert.Equal(0, p.Export().Moves[0].Pp); Assert.Equal("Pilot", p.Export().OriginalTrainer.Name); Assert.Equal(1, p.Export().ContestStats[0]); Assert.Equal(255, p.Export().RibbonBits[0]); Assert.NotEqual(77, p.Export().Extensions[0].Payload[0]); Assert.Equal(before, Wire.Hash(SaveCodec.Encode(snapshot)));
        }
        [Fact] public void StableCreatureBoxAndSlotIdsSurviveTransfersSwapsAndReload()
        {
            var s = PilotWorld.World(); var d = s.Export(); var moved = Storage.Transfer(s, d.Party[0].SlotId, d.Boxes[0].Slots[1].SlotId); Assert.Null(moved.Export().Party[0].CreatureId); Assert.Equal(PilotWorld.Id(10), moved.Export().Boxes[0].Slots[1].CreatureId);
            var swapped = Storage.Transfer(moved, d.Boxes[0].Slots[1].SlotId, d.Party[1].SlotId, true); var read = SaveCodec.Decode(SaveCodec.Encode(swapped), s.Catalog).Snapshot;
            Assert.Equal(PilotWorld.Id(10), read.Export().Party[1].CreatureId); Assert.Equal(PilotWorld.Id(11), read.Export().Boxes[0].Slots[1].CreatureId); Assert.Equal(d.Boxes[0].BoxId, read.Export().Boxes[0].BoxId); Assert.Equal(d.Boxes[0].Slots.Select(x => x.SlotId), read.Export().Boxes[0].Slots.Select(x => x.SlotId)); Assert.Equal(PilotWorld.Id(10), s.Export().Party[0].CreatureId);
        }
        [Fact] public void StorageRejectsDuplicatePlacementDanglingOrphanAndSlotIds()
        {
            var s = PilotWorld.World(); var d = s.Export(); d.Party[2].CreatureId = d.Party[0].CreatureId; Assert.Throws<ArgumentException>(() => new SaveSnapshot(d, s.Catalog));
            d = s.Export(); d.Party[0].CreatureId = null; Assert.Throws<ArgumentException>(() => new SaveSnapshot(d, s.Catalog)); d = s.Export(); d.Party[0].CreatureId = PilotWorld.Id(999); Assert.Throws<ArgumentException>(() => new SaveSnapshot(d, s.Catalog)); d = s.Export(); d.Party[1].SlotId = d.Party[0].SlotId; Assert.Throws<ArgumentException>(() => new SaveSnapshot(d, s.Catalog));
            Assert.Throws<InvalidOperationException>(() => Storage.Transfer(s, s.Export().Party[0].SlotId, s.Export().Party[1].SlotId)); Assert.Throws<InvalidOperationException>(() => Storage.Transfer(s, s.Export().Party[2].SlotId, s.Export().Party[3].SlotId));
        }
        [Fact] public void ReservedBoxPolicyRoundTripsAndRequiresResolverWithoutInventingProtectionRules()
        {
            var s = PilotWorld.World(); var d = s.Export(); d.Boxes[1].ProtectionPolicyReference = "unreviewed.protection-policy"; var reserved = new SaveSnapshot(d, s.Catalog); var r = SaveCodec.Decode(SaveCodec.Encode(reserved), s.Catalog).Snapshot; Assert.Equal(d.Boxes[1].ProtectionPolicyReference, r.Export().Boxes[1].ProtectionPolicyReference); Assert.Throws<NotSupportedException>(() => Storage.Transfer(r, d.Party[0].SlotId, d.Boxes[1].Slots[0].SlotId));
        }
        [Fact] public void EvolutionPreservesIdentityMovesTraitsMetadataAndOpaqueBonuses()
        {
            var c = DomainCatalog.Create(); var p = PilotWorld.Rich(c); string catalogHash = c.ContentHash; var q = p.RecordEvolution("ivysaur", "base", PilotWorld.Id(6000), "approved-event-fixture"); var a = p.Export(); var b = q.Export(); Assert.Equal(a.IndividualId, b.IndividualId); Assert.Equal(a.Personality, b.Personality); Assert.Equal(a.Nature, b.Nature); Assert.Equal(a.Experience, b.Experience); Assert.Equal(a.Level, b.Level); Assert.Equal(a.Moves.Select(x => x.PpUps), b.Moves.Select(x => x.PpUps)); Assert.Equal(a.Moves.Select(x => x.SlotId), b.Moves.Select(x => x.SlotId)); Assert.Equal(a.Extensions[0].Payload, b.Extensions[0].Payload); Assert.Equal(a.OriginalTrainer.TrainerId, b.OriginalTrainer.TrainerId); Assert.Equal("ivysaur", b.SpeciesId); Assert.Equal(a.CurrentHp + q.Stats[Stat.Hp] - p.Stats[Stat.Hp], b.CurrentHp); Assert.Equal(catalogHash, c.ContentHash); Assert.Empty(p.Export().EvolutionHistory);
            var s = PilotWorld.World(c).ReplaceCreature(q); var r = SaveCodec.Decode(SaveCodec.Encode(s), c).Snapshot.Creature(p.IndividualId); Assert.Equal(b.EvolutionHistory[0].EventId, r.Export().EvolutionHistory[0].EventId);
        }
        [Fact] public void InvalidEvolutionHistoryOrUnapprovedTransitionRejects()
        {
            var c = DomainCatalog.Create(); var p = PilotWorld.Plain(c); Assert.Throws<NotSupportedException>(() => p.RecordEvolution("charmander", "base", PilotWorld.Id(6000), "test")); var d = p.Export(); d.EvolutionHistory = new[] { new EvolutionData { EventId = PilotWorld.Id(6000), FromSpecies = "bulbasaur", FromForm = "base", ToSpecies = "ivysaur", ToForm = "base", EvidenceId = "test", Level = 50 } }; Assert.Throws<ArgumentException>(() => new PersistentPokemon(d, c));
        }
        [Fact] public void EnhancementsCannotMutateStatsAndUnknownExtensionBytesAreRetained()
        {
            var c = DomainCatalog.Create(); var p = PilotWorld.Plain(c); var d = p.Export(); d.Extensions = new[] { new ExtensionData { Key = "roguelite.enhancements", Schema = "pending-policy-1", Payload = new byte[] { 255, 42, 0 } } }; var q = new PersistentPokemon(d, c); Assert.Equal(p.Stats.ToArray(), q.Stats.ToArray()); Assert.Equal(p.Export().Ivs, q.Export().Ivs); Assert.Equal(p.Export().Evs, q.Export().Evs);
        }
        [Fact] public void PpUpsHeldItemFormsAndOtherUnsupportedBattleDataRemainStorableButFailProjection()
        {
            var c = DomainCatalog.Create(); Assert.Throws<NotSupportedException>(() => BattleProjection.Create(PilotWorld.Rich(c), c)); Assert.Throws<NotSupportedException>(() => BattleProjection.Create(PilotWorld.Plain(c, 12, "deoxys", "attack"), c));
            void Reject(Action<CreatureData> change) { var d = PilotWorld.Plain(c).Export(); change(d); Assert.Throws<NotSupportedException>(() => BattleProjection.Create(new PersistentPokemon(d, c), c)); }
            Reject(d => { d.Moves[0].PpUps = 1; }); Reject(d => { d.Moves[0].MoveId = "toxic"; d.Moves[0].Pp = 10; }); Reject(d => { d.HeldItemId = "oran-berry"; }); Reject(d => { d.Status = PersistentStatus.Sleep; d.SleepTurns = 2; });
        }
        [Fact] public void SupportedBattleProjectionRetainsIdentityHpPpAndStatsWithoutAlteringPhaseOne()
        {
            var c = DomainCatalog.Create(); var p = PilotWorld.Plain(c); var b = BattleProjection.Create(p, c); Assert.Equal(p.IndividualId, b.Record.IndividualId); Assert.Equal(p.Stats.ToArray(), b.Stats.ToArray()); Assert.Equal(p.Export().CurrentHp, b.Hp); Assert.Equal(p.Export().Moves[0].Pp, b.Pp[0]);
        }
        [Fact] public void PersonalityGenderNatureShinyAndAbilitySlotValidation()
        {
            var c = DomainCatalog.Create(); var p = PilotWorld.Plain(c); Assert.Equal((Nature)(p.Export().Personality % 25), p.Export().Nature); var d = p.Export(); d.Nature = Nature.Quirky; Assert.Throws<ArgumentException>(() => new PersistentPokemon(d, c)); d = p.Export(); d.Gender = Gender.Male; Assert.Throws<ArgumentException>(() => new PersistentPokemon(d, c));
            d = p.Export(); d.AbilityId = "blaze"; Assert.Throws<ArgumentException>(() => new PersistentPokemon(d, c)); d = p.Export(); d.OriginalTrainer.TrainerId = d.Personality; Assert.True(new PersistentPokemon(d, c).IsShiny); Assert.False(p.IsShiny);
        }
        [Fact] public void RichRecordBoundsAndPidLinkedDataRejectRatherThanRepair()
        {
            var c = DomainCatalog.Create(); void Bad(Action<CreatureData> change) { var d = PilotWorld.Rich(c).Export(); change(d); Assert.ThrowsAny<ArgumentException>(() => new PersistentPokemon(d, c)); }
            Bad(d => d.Evs[2] = 1); Bad(d => d.Ivs[0] = 32); Bad(d => d.Experience = 0); Bad(d => d.CurrentHp = int.MaxValue); Bad(d => d.Moves[0].Pp = 57); Bad(d => d.Moves[0].PpUps = 4); Bad(d => d.Friendship = 256); Bad(d => d.MetDate = "2026-02-30"); Bad(d => d.Status = PersistentStatus.Healthy); Bad(d => d.ContestStats = new byte[5]); Bad(d => d.Markings = 64); Bad(d => d.PokerusStrain = 0); Bad(d => d.OriginalTrainer.Gender = Gender.Genderless); Bad(d => d.Moves[1].SlotId = d.Moves[0].SlotId); Bad(d => d.Extensions[1].Key = d.Extensions[0].Key);
            Bad(d => d.ShinyLeaves = 64); Bad(d => d.Mood = -129); Bad(d => d.MailData = new byte[55]); Bad(d => d.CapsuleSeals = new byte[25]); Bad(d => d.LegacyBallCode = 256);
        }
        [Fact] public void EggDataIsPreservedButNoEggEvolutionOrBattleIsInvented()
        {
            var c = DomainCatalog.Create(); var d = PilotWorld.Plain(c).Export(); d.IsEgg = true; d.EggCyclesRemaining = 20; d.Moves = Array.Empty<MoveData>(); var egg = new PersistentPokemon(d, c); Assert.Equal(20, egg.Export().EggCyclesRemaining); Assert.Throws<NotSupportedException>(() => BattleProjection.Create(egg, c)); Assert.Throws<InvalidOperationException>(() => egg.RecordEvolution("ivysaur", "base", PilotWorld.Id(6000), "test"));
        }
    }
}
