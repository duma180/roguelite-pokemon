using System;
using System.Collections.Generic;

namespace Roguelite.Gameplay
{
    public sealed class GeneratedPokemon
    {
        public PokemonRecord Pokemon { get; }
        public uint SourceSeed { get; }
        public uint FinalRandomState { get; }
        public long RandomDraws { get; }
        internal GeneratedPokemon(PokemonRecord pokemon, uint seed, BattleRandom random) { Pokemon = pokemon; SourceSeed = seed; FinalRandomState = random.State; RandomDraws = random.Draws; }
    }
    public static class PokemonFactory
    {
        public const string Version = "creature-factory-1.0.0";
        // Caller supplies individual identity and ability; generation and battle seeds are separate streams.
        // Draw order: HP/Atk/Def/SpAtk/SpDef/Speed IVs, then nature. EVs start at zero.
        public static GeneratedPokemon Create(Guid individualId, SpeciesDefinition species, int level, Ability ability, IEnumerable<MoveDefinition> moves, uint seed)
        {
            var random = new BattleRandom(seed);
            var ivs = new SixStats(random.Next(32), random.Next(32), random.Next(32), random.Next(32), random.Next(32), random.Next(32));
            var nature = (Nature)random.Next(25);
            var pokemon = new PokemonRecord(individualId, species, level, ivs, SixStats.Uniform(0), nature, ability, moves);
            return new GeneratedPokemon(pokemon, seed, random);
        }
    }
}
