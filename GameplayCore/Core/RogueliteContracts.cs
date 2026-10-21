using System;
using System.Collections.Generic;

namespace Roguelite.Gameplay.Planned
{
    // DESIGN CONTRACTS ONLY. No reward, reset, enhancement, PC or save service is implemented.
    public interface ICreatureProgression
    {
        Guid IndividualId { get; }
        IReadOnlyList<EnhancementGrant> Enhancements { get; }
        IReadOnlyList<EvolutionEntry> EvolutionHistory { get; }
    }
    public sealed class EnhancementGrant
    {
        public Guid GrantId { get; }
        public Stat Stat { get; }
        public string EarnedSpeciesId { get; }
        public int EarnedBaseValue { get; }
        public string PolicyVersion { get; }
        public EnhancementGrant(Guid grantId, Stat stat, string species, int basis, string version) { GrantId = grantId; Stat = stat; EarnedSpeciesId = species; EarnedBaseValue = basis; PolicyVersion = version; }
    }
    public sealed class EvolutionEntry
    {
        public string FromSpecies { get; }
        public string ToSpecies { get; }
        public string BonusPolicyVersion { get; }
        public EvolutionEntry(string from, string to, string policy) { FromSpecies = from; ToSpecies = to; BonusPolicyVersion = policy; }
    }
    public interface IEnhancedBaseStatPolicy
    {
        string Version { get; }
        SixStats EffectiveBaseStats(PokemonRecord pokemon, ICreatureProgression progression);
        // +5% grants and evolution inheritance use an explicit rational/rounding policy, never edit SpeciesDefinition.
    }
    public interface IZoneDifficultyPolicy
    {
        string Version { get; }
        SixStats OpponentBaseStats(SpeciesDefinition species, string geographicZoneId, int zoneTier);
        // Proposed factor (115/100)^tier; mapping and max tier require approval.
    }
    public interface IRunResetService
    {
        string Version { get; }
        ResetPlan PlanReset(RosterSnapshot roster); // level 5; preserve identities/species/form/moves/ability/nature, enhancements and protected PC.
    }
    public abstract class RosterSnapshot { } // Future versioned DTO incl. stable box/slot references and protected flags.
    public abstract class ResetPlan { } // Future atomic transaction: old/new hashes and preservation audit.
    public interface IDefeatRewardPolicy
    {
        string Version { get; }
        int PokePoints(string encounterClass); // Exactly one of 1, 10, 30. Encounter-class mapping is pending.
    }
    public interface IRewardLedger
    {
        bool CreditOnce(Guid encounterId, Guid defeatedIndividualId, int defeatOrdinal, int pokePoints);
        // Same defeat delivery/replay is deduplicated; a new encounter permits repeat rewards.
    }
    public interface ISaveRepository
    {
        string SchemaVersion { get; }
        void CommitAtomically(RosterSnapshot roster, ResetPlan? resetPlan, IRewardLedger ledger);
    }
}
