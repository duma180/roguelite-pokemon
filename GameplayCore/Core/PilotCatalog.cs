using System;
using System.Linq;

namespace Roguelite.Gameplay
{
    // Small factual HGSS fixture catalog, not a bulk database or learnset validator.
    public static class PilotCatalog
    {
        public const string DataVersion = "hgss-facts-pilot-1";
        public static SpeciesDefinition Bulbasaur { get; } = new SpeciesDefinition("bulbasaur", DataVersion, new SixStats(45, 49, 49, 65, 65, 45), Element.Grass, Element.Poison);
        public static SpeciesDefinition Charmander { get; } = new SpeciesDefinition("charmander", DataVersion, new SixStats(39, 52, 43, 60, 50, 65), Element.Fire);
        public static SpeciesDefinition Squirtle { get; } = new SpeciesDefinition("squirtle", DataVersion, new SixStats(44, 48, 65, 50, 64, 43), Element.Water);
        public static SpeciesDefinition Rattata { get; } = new SpeciesDefinition("rattata", DataVersion, new SixStats(30, 56, 35, 25, 35, 72), Element.Normal);
        public static SpeciesDefinition Magnemite { get; } = new SpeciesDefinition("magnemite", DataVersion, new SixStats(25, 35, 70, 95, 55, 45), Element.Electric, Element.Steel);
        public static MoveDefinition Tackle { get; } = new MoveDefinition("tackle", Element.Normal, Category.Physical, 35, 95, 35);
        public static MoveDefinition QuickAttack { get; } = new MoveDefinition("quick-attack", Element.Normal, Category.Physical, 40, 100, 30, priority: 1);
        public static MoveDefinition Ember { get; } = new MoveDefinition("ember", Element.Fire, Category.Special, 40, 100, 25, burnChance: 10);
        public static MoveDefinition WaterGun { get; } = new MoveDefinition("water-gun", Element.Water, Category.Special, 40, 100, 25);
        public static MoveDefinition RazorLeaf { get; } = new MoveDefinition("razor-leaf", Element.Grass, Category.Physical, 55, 95, 25, criticalStage: 1);
        public static MoveDefinition Slash { get; } = new MoveDefinition("slash", Element.Normal, Category.Physical, 70, 100, 20, criticalStage: 1);
        public static MoveDefinition Growl { get; } = new MoveDefinition("growl", Element.Normal, Category.Status, 0, 100, 40, effect: Effect.LowerAttack);
        public static MoveDefinition ThunderWave { get; } = new MoveDefinition("thunder-wave", Element.Electric, Category.Status, 0, 100, 20, effect: Effect.Paralyze);
        public static MoveDefinition WillOWisp { get; } = new MoveDefinition("will-o-wisp", Element.Fire, Category.Status, 0, 75, 15, effect: Effect.Burn);
        public static MoveDefinition Swift { get; } = new MoveDefinition("swift", Element.Normal, Category.Special, 60, 0, 20);
        internal static void ValidateForBattle(PokemonRecord p)
        {
            // Research-only fixture namespace is explicit. Unknown real content must not silently become generic damage.
            var species = new[] { Bulbasaur, Charmander, Squirtle, Rattata, Magnemite }.SingleOrDefault(s => s.Id == p.Species.Id);
            if (species == null) { if (!p.Species.Id.StartsWith("fixture-", StringComparison.Ordinal)) throw new NotSupportedException("Species is not in the approved pilot catalog."); }
            else if (Canonical.Species(species) != Canonical.Species(p.Species)) throw new ArgumentException("Authoritative species definition was overridden.");
            foreach (var move in p.Moves)
            {
                var known = new[] { Tackle, QuickAttack, Ember, WaterGun, RazorLeaf, Slash, Growl, ThunderWave, WillOWisp, Swift }.SingleOrDefault(m => m.Id == move.Id);
                if (known == null) { if (!move.Id.StartsWith("fixture-", StringComparison.Ordinal)) throw new NotSupportedException("Move semantics are not approved for this subset."); }
                else if (Canonical.Move(known) != Canonical.Move(move)) throw new ArgumentException("Authoritative move definition was overridden.");
            }
        }
    }
}
