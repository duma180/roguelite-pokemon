using System;
using System.Collections.Generic;
using System.Linq;
using Roguelite.Domain;
using Roguelite.Gameplay;
using Roguelite.Persistence;
using Roguelite.Phase2.Fixtures;
using Xunit;

namespace Roguelite.Phase2.Tests
{
    public sealed class IntegrationTests
    {
        [Fact] public void BattleResultsPersistOnlyHpPpAndStatusWithoutLosingIndividualMetadata()
        {
            var s = PilotWorld.World(rich: false); var p = s.Creature(PilotWorld.Id(10)); var opponent = s.Creature(PilotWorld.Id(11));
            var battle = new BattleState(new[] { BattleProjection.Create(p, s.Catalog) }, new[] { BattleProjection.Create(opponent, s.Catalog) }, 2026, PilotWorld.Id(9000)); var step = BattleEngine.ResolveTurn(battle, BattleAction.Move(0), BattleAction.Move(0));
            var changed = BattleProjection.ApplyResult(p, step.State.Active(Side.Player), s.Catalog); var after = s.ReplaceCreature(changed); var loaded = SaveCodec.Decode(SaveCodec.Encode(after), s.Catalog).Snapshot.Creature(p.IndividualId);
            Assert.Equal(p.IndividualId, loaded.IndividualId); Assert.Equal(step.State.Active(Side.Player).Hp, loaded.Export().CurrentHp); Assert.Equal(step.State.Active(Side.Player).Pp[0], loaded.Export().Moves[0].Pp); Assert.Equal(p.Export().Moves[0].SlotId, loaded.Export().Moves[0].SlotId); Assert.Equal(p.Export().Personality, loaded.Export().Personality); Assert.Equal(p.Export().OriginalTrainer.TrainerId, loaded.Export().OriginalTrainer.TrainerId); Assert.Equal(p.Export().CurrentHp, s.Creature(p.IndividualId).Export().CurrentHp);
        }
        [Fact] public void BattleResultWithChangedIdentityOrDefinitionRejects()
        {
            var c = DomainCatalog.Create(); var p = PilotWorld.Plain(c); var other = BattleProjection.Create(PilotWorld.Plain(c, 11, "rattata", pid: 100), c); Assert.Throws<InvalidOperationException>(() => BattleProjection.ApplyResult(p, other, c)); var expected = BattleProjection.Create(p, c).Record; var changed = new BattlePokemon(new PokemonRecord(expected.IndividualId, expected.Species, 51, expected.Ivs, expected.Evs, expected.Nature, expected.Ability, expected.Moves)); Assert.Throws<InvalidOperationException>(() => BattleProjection.ApplyResult(p, changed, c));
        }
        [Fact] public void MultipleCatalogVersionsResolveExactlyAndRejectRebinding()
        {
            var a = DomainCatalog.Create(); var b = new Catalog(a.Id, "2.1.0", a.Species, a.Moves, a.Abilities, a.Items); var entries = new[] { a, b }; var registry = new CatalogRegistry(entries); Assert.Same(a, registry.Resolve(a.Id, a.Version, a.ContentHash)); Assert.Same(b, registry.Resolve(b.Id, b.Version, b.ContentHash)); Assert.Equal(registry.ManifestHash, new CatalogRegistry(entries.Reverse()).ManifestHash); entries[0] = b; Assert.Same(a, registry.Resolve(a.Id, a.Version, a.ContentHash));
            Assert.Throws<ArgumentException>(() => new CatalogRegistry(new[] { a, a })); Assert.Throws<KeyNotFoundException>(() => registry.Resolve(a.Id, "999", a.ContentHash)); Assert.Throws<CatalogMismatchException>(() => registry.Resolve(a.Id, a.Version, new string('0', 64)));
        }
        [Fact] public void RosterRejectsForeignHandlerAndRevisionOverflow()
        {
            var s = PilotWorld.World(); var d = s.Export(); d.Creatures[0].HandlerId = PilotWorld.Id(9000); Assert.Throws<ArgumentException>(() => new SaveSnapshot(d, s.Catalog)); d = s.Export(); d.Revision = long.MaxValue; var max = new SaveSnapshot(d, s.Catalog); Assert.Throws<OverflowException>(() => max.PrepareCommit(PilotWorld.Id(4)));
        }
        [Fact] public void PersistentFormSwitchRetainsIdentityAndSelectsCorrectImmutableFormStats()
        {
            var c = DomainCatalog.Create(); var p = PilotWorld.Plain(c, 12, "deoxys", "normal", 1000); var d = p.Export(); d.FormId = "attack"; var attack = new PersistentPokemon(d, c); Assert.Equal(p.IndividualId, attack.IndividualId); Assert.True(attack.Stats[Stat.Attack] > p.Stats[Stat.Attack]); Assert.Equal(p.Stats[Stat.Hp], attack.Stats[Stat.Hp]); Assert.Equal("normal", p.FormId); Assert.Throws<NotSupportedException>(() => BattleProjection.Create(attack, c));
        }
        [Fact] public void MissingDraftFieldsRejectClearlyInsteadOfPublishingPartialRecords()
        {
            var c = DomainCatalog.Create(); var d = PilotWorld.Plain(c).Export(); d.Moves = null!; Assert.Throws<ArgumentException>(() => new PersistentPokemon(d, c)); var s = PilotWorld.World(c).Export(); s.Boxes[0].Slots = null!; Assert.Throws<ArgumentException>(() => new SaveSnapshot(s, c));
        }
    }
}
