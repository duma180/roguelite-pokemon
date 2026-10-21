using System.Linq;
using Roguelite.Gameplay;

namespace Roguelite.Domain
{
    public static class DomainCatalog
    {
        // Deliberately bounded factual catalog. Deoxys forms and Toxic are storable, not battle-supported.
        public static Catalog Create()
        {
            FormDefinition Form(SpeciesDefinition stats, string ability) => new FormDefinition("base", stats, ability, "none");
            var ivysaur = new SpeciesDefinition("ivysaur", "hgss-facts-domain-2", new SixStats(60, 62, 63, 80, 80, 60), Element.Grass, Element.Poison);
            var dn = new SpeciesDefinition("deoxys", "hgss-facts-domain-2", new SixStats(50, 150, 50, 150, 50, 150), Element.Psychic);
            var da = new SpeciesDefinition("deoxys", "hgss-facts-domain-2", new SixStats(50, 180, 20, 180, 20, 150), Element.Psychic);
            var species = new[] {
                new CatalogSpecies("bulbasaur", 1, GrowthRate.MediumSlow, 31, new[] { Form(PilotCatalog.Bulbasaur, "overgrow") }, 70, "ivysaur"),
                new CatalogSpecies("ivysaur", 2, GrowthRate.MediumSlow, 31, new[] { Form(ivysaur, "overgrow") }),
                new CatalogSpecies("charmander", 4, GrowthRate.MediumSlow, 31, new[] { Form(PilotCatalog.Charmander, "blaze") }),
                new CatalogSpecies("squirtle", 7, GrowthRate.MediumSlow, 31, new[] { Form(PilotCatalog.Squirtle, "torrent") }),
                new CatalogSpecies("rattata", 19, GrowthRate.MediumFast, 127, new[] { new FormDefinition("base", PilotCatalog.Rattata, "run-away", "guts") }),
                new CatalogSpecies("magnemite", 81, GrowthRate.MediumFast, 255, new[] { new FormDefinition("base", PilotCatalog.Magnemite, "magnet-pull", "sturdy") }),
                new CatalogSpecies("deoxys", 386, GrowthRate.Slow, 255, new[] { new FormDefinition("normal", dn, "pressure", "none"), new FormDefinition("attack", da, 1, "pressure", "none") }, 0) };
            var definitions = new[] { PilotCatalog.Tackle, PilotCatalog.QuickAttack, PilotCatalog.Ember, PilotCatalog.WaterGun, PilotCatalog.RazorLeaf, PilotCatalog.Slash, PilotCatalog.Growl, PilotCatalog.ThunderWave, PilotCatalog.WillOWisp, PilotCatalog.Swift };
            int Effect(MoveDefinition m) => m.Id == "quick-attack" ? 103 : m.Id == "ember" ? 4 : m.Id == "razor-leaf" || m.Id == "slash" ? 43 : m.Id == "growl" ? 18 : m.Id == "thunder-wave" ? 67 : m.Id == "will-o-wisp" ? 167 : m.Id == "swift" ? 17 : 0;
            var moves = definitions.Select(m => new CatalogMove(m.Id, m.MaxPp, Effect(m), m)).Concat(new[] { new CatalogMove("toxic", 10, 33, Element.Poison, Category.Status, 0, 85, 0) });
            var abilities = new[] { new AbilityDefinition("overgrow", Ability.Overgrow), new AbilityDefinition("blaze", Ability.Blaze), new AbilityDefinition("torrent", Ability.Torrent), new AbilityDefinition("run-away", Ability.RunAway), new AbilityDefinition("sturdy", Ability.Sturdy), new AbilityDefinition("pressure", null), new AbilityDefinition("guts", null), new AbilityDefinition("magnet-pull", null), new AbilityDefinition("none", null) };
            return new Catalog("hgss-domain-pilot", "2.0.0", species, moves, abilities, new[] { new ItemDefinition("none", false), new ItemDefinition("poke-ball", true, true), new ItemDefinition("oran-berry", true), new ItemDefinition("everstone", true) });
        }
    }
}
