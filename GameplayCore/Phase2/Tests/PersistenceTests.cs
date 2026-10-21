using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Roguelite.Domain;
using Roguelite.Persistence;
using Roguelite.Phase2.Fixtures;
using Xunit;

namespace Roguelite.Phase2.Tests
{
    public sealed class Scratch : IDisposable
    {
        private static readonly string Root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "PokemonRoguelite.Phase2.Tests"));
        public string DirectoryPath { get; }
        public Scratch() { DirectoryPath = Path.Combine(Root, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(DirectoryPath); }
        public void Dispose()
        {
            string full = Path.GetFullPath(DirectoryPath); if (Path.GetDirectoryName(full) != Root || Path.GetFileName(full).Length != 32) throw new InvalidOperationException("Unsafe scratch cleanup."); Directory.Delete(full, true);
        }
    }
    public static class SaveFixture
    {
        public static byte[] Reseal(byte[] bytes) { using var sha = SHA256.Create(); var hash = sha.ComputeHash(bytes, 0, bytes.Length - 32); hash.CopyTo(bytes, bytes.Length - 32); return bytes; }
        public static byte[] HeaderInt(byte[] bytes, int offset, int value) { var copy = (byte[])bytes.Clone(); BitConverter.GetBytes(value).CopyTo(copy, offset); return Reseal(copy); }
        public static SaveSnapshot Next(SaveSnapshot old, int transaction = 4, string label = "next") { var d = old.PrepareCommit(PilotWorld.Id(transaction)).Export(); d.Description = label; return new SaveSnapshot(d, old.Catalog); }
        public static void ChangeText(byte[] bytes, string old, string replacement)
        {
            var a = Wire.Utf8.GetBytes(old); var b = Wire.Utf8.GetBytes(replacement); if (a.Length != b.Length) throw new ArgumentException();
            for (int i = 20; i < bytes.Length - 32 - a.Length; i++) if (bytes.Skip(i).Take(a.Length).SequenceEqual(a)) { b.CopyTo(bytes, i); Reseal(bytes); return; }
            throw new Exception("Pattern not found.");
        }
        public static int CreatureCountOffset(byte[] bytes)
        {
            using var r = new BinaryReader(new MemoryStream(bytes), Wire.Utf8); r.BaseStream.Position = 20; Wire.Id(r); Wire.Id(r); r.ReadInt64(); Wire.Id(r); r.ReadUInt32(); Wire.Text(r); r.ReadInt32(); r.ReadInt32(); for (int i = 0; i < 5; i++) Wire.Text(r); return checked((int)r.BaseStream.Position);
        }
    }
    public sealed class CodecTests
    {
        [Fact] public void ReviewedCandidateCatalogAndSaveEncodingHaveFrozenGoldenHashes()
        {
            var s = PilotWorld.World(); Assert.Equal("30caffe2074f3145b432cf8aed2a48c0384d530b2db3a587904660eac7676003", s.Catalog.ContentHash); var bytes = SaveCodec.Encode(s); Assert.Equal(2925, bytes.Length); Assert.Equal("9109e08bf01998ce189b9c2a3432f5a4a1554d49a6d9cb96f353e329837ff657", Wire.Hash(bytes));
        }
        [Fact] public void RichSchemaRoundTripIsLosslessAndCanonical()
        {
            var s = PilotWorld.World(); byte[] bytes = SaveCodec.Encode(s); var r = SaveCodec.Decode(bytes, s.Catalog); Assert.Equal(bytes, SaveCodec.Encode(r.Snapshot)); Assert.Equal(2, r.SourceSchema); Assert.Equal("none", r.Migration);
            var p = r.Snapshot.Creature(PilotWorld.Id(10)).Export(); var original = s.Creature(PilotWorld.Id(10)).Export(); Assert.Equal(original.Nickname, p.Nickname); Assert.Equal(original.Moves.Select(m => m.PpUps), p.Moves.Select(m => m.PpUps)); Assert.Equal(original.RibbonBits, p.RibbonBits); Assert.Equal(original.ContestStats, p.ContestStats); Assert.Equal(original.MetDate, p.MetDate); Assert.Equal(original.EggMetDate, p.EggMetDate); Assert.Equal(original.OriginalTrainer.Language, p.OriginalTrainer.Language); Assert.Equal(original.FatefulEncounter, p.FatefulEncounter); Assert.Equal(original.PokerusDays, p.PokerusDays); Assert.Equal(original.Markings, p.Markings); Assert.Equal(original.HeldItemId, p.HeldItemId); Assert.Equal(original.Friendship, p.Friendship); Assert.Equal(original.Status, p.Status); Assert.Equal(original.SleepTurns, p.SleepTurns);
            Assert.Equal(original.ShinyLeaves, p.ShinyLeaves); Assert.Equal(original.Mood, p.Mood); Assert.Equal(original.MetTerrain, p.MetTerrain); Assert.Equal(original.LegacyMetLocation, p.LegacyMetLocation); Assert.Equal(original.LegacyEggMetLocation, p.LegacyEggMetLocation); Assert.Equal(original.LegacyBallCode, p.LegacyBallCode); Assert.Equal(original.BallCapsuleId, p.BallCapsuleId); Assert.Equal(original.MailData, p.MailData); Assert.Equal(original.CapsuleSeals, p.CapsuleSeals);
        }
        [Theory, InlineData(PersistentStatus.Healthy), InlineData(PersistentStatus.Sleep), InlineData(PersistentStatus.Poison), InlineData(PersistentStatus.Burn), InlineData(PersistentStatus.Freeze), InlineData(PersistentStatus.Paralysis), InlineData(PersistentStatus.Toxic)]
        public void EveryPersistentStatusRoundTripsWithoutInventingBattleSupport(PersistentStatus status)
        {
            var s = PilotWorld.World(rich: false); var d = s.Export(); var p = d.Creatures.Single(p => p.SpeciesId == "rattata"); p.Status = status; p.SleepTurns = status == PersistentStatus.Sleep ? 7 : 0; var read = SaveCodec.Decode(SaveCodec.Encode(new SaveSnapshot(d, s.Catalog)), s.Catalog).Snapshot; Assert.Equal(status, read.Creature(p.IndividualId).Export().Status);
        }
        [Fact] public void EggFieldsAndZeroMovesRoundTrip()
        {
            var s = PilotWorld.World(rich: false); var d = s.Export(); d.Creatures[0].IsEgg = true; d.Creatures[0].EggCyclesRemaining = 65535; d.Creatures[0].Moves = Array.Empty<MoveData>(); var r = SaveCodec.Decode(SaveCodec.Encode(new SaveSnapshot(d, s.Catalog)), s.Catalog).Snapshot; Assert.True(r.Creature(d.Creatures[0].IndividualId).Export().IsEgg); Assert.Equal(65535, r.Creature(d.Creatures[0].IndividualId).Export().EggCyclesRemaining); Assert.Empty(r.Creature(d.Creatures[0].IndividualId).Export().Moves);
        }
        [Fact] public void CreatureCollectionAndExtensionOrderingDoNotChangeCanonicalEncoding()
        {
            var s = PilotWorld.World(); var d = s.Export(); Array.Reverse(d.Creatures); foreach (var p in d.Creatures) Array.Reverse(p.Extensions); Assert.Equal(SaveCodec.Encode(s), SaveCodec.Encode(new SaveSnapshot(d, s.Catalog)));
        }
        [Fact] public void LegacyMigrationAddsOnlyDescriptionAndNeverMutatesInput()
        {
            var s = PilotWorld.World(); byte[] legacy = SaveCodec.EncodeLegacyFixture(s); string before = Wire.Hash(legacy); var r = SaveCodec.Decode(legacy, s.Catalog); Assert.Equal(1, r.SourceSchema); Assert.Equal("1.0.0->2.0.0:add-empty-description", r.Migration); Assert.Equal("", r.Snapshot.Export().Description); Assert.Equal(SaveCodec.Encode(s), SaveCodec.Encode(r.Snapshot)); Assert.Equal(before, Wire.Hash(legacy)); Assert.Equal(2, SaveCodec.Decode(SaveCodec.Encode(r.Snapshot), s.Catalog).SourceSchema);
            Assert.Throws<InvalidOperationException>(() => SaveCodec.EncodeLegacyFixture(SaveFixture.Next(s)));
        }
        [Fact] public void EveryByteFlipIsDetectedIncludingHeaderAndChecksum()
        {
            var s = PilotWorld.World(); byte[] original = SaveCodec.Encode(s); for (int i = 0; i < original.Length; i++) { var copy = (byte[])original.Clone(); copy[i] ^= 1; Assert.Throws<CorruptSaveException>(() => SaveCodec.Decode(copy, s.Catalog)); }
        }
        [Fact] public void EveryTruncationOfOneWholeSaveIsRejected()
        {
            var s = PilotWorld.World(); var original = SaveCodec.Encode(s); for (int length = 0; length < original.Length; length++) Assert.Throws<CorruptSaveException>(() => SaveCodec.Decode(original.Take(length).ToArray(), s.Catalog));
        }
        [Theory, InlineData(-1), InlineData(547), InlineData(int.MaxValue)] public void MaliciousCollectionLengthsRejectBeforeAllocation(int count)
        {
            var s = PilotWorld.World(); var bytes = SaveCodec.Encode(s); var corrupt = SaveFixture.HeaderInt(bytes, SaveFixture.CreatureCountOffset(bytes), count); Assert.Throws<CorruptSaveException>(() => SaveCodec.Decode(corrupt, s.Catalog));
        }
        [Fact] public void UnknownSchemaEnvelopeRulesetAndCatalogFailExplicitly()
        {
            var s = PilotWorld.World(); var bytes = SaveCodec.Encode(s); Assert.Throws<UnsupportedSaveException>(() => SaveCodec.Decode(SaveFixture.HeaderInt(bytes, 12, 999), s.Catalog)); Assert.Throws<UnsupportedSaveException>(() => SaveCodec.Decode(SaveFixture.HeaderInt(bytes, 8, 999), s.Catalog));
            var copy = (byte[])bytes.Clone(); SaveFixture.ChangeText(copy, s.Catalog.ContentHash, new string('0', 64)); Assert.Throws<CatalogMismatchException>(() => SaveCodec.Decode(copy, s.Catalog)); copy = (byte[])bytes.Clone(); SaveFixture.ChangeText(copy, "hgss-gen4-subset-1.0.0", "xxxx-gen4-subset-1.0.0"); Assert.Throws<UnsupportedSaveException>(() => SaveCodec.Decode(copy, s.Catalog));
        }
        [Fact] public void TrailingFieldsMalformedUtf8AndResealedInvalidDomainDataReject()
        {
            var s = PilotWorld.World(); var bytes = SaveCodec.Encode(s); var extra = new byte[bytes.Length + 1]; Array.Copy(bytes, 0, extra, 0, bytes.Length - 32); BitConverter.GetBytes(bytes.Length - SaveCodec.EnvelopeBytes + 1).CopyTo(extra, 16); SaveFixture.Reseal(extra); Assert.Throws<CorruptSaveException>(() => SaveCodec.Decode(extra, s.Catalog));
            var utf = (byte[])bytes.Clone(); SaveFixture.ChangeText(utf, "芽🌱", "芽🌱"); int index = Array.IndexOf(utf, (byte)0xE8, 20); utf[index] = 255; SaveFixture.Reseal(utf); Assert.Throws<CorruptSaveException>(() => SaveCodec.Decode(utf, s.Catalog));
        }
        [Fact] public void BoundedResealedMutationCorpusCannotEscapeValidationOrCrashParser()
        {
            var s = PilotWorld.World(); var original = SaveCodec.Encode(s); var rng = new Random(2026);
            for (int i = 0; i < 500; i++)
            {
                var bytes = (byte[])original.Clone(); bytes[rng.Next(20, bytes.Length - 32)] ^= (byte)rng.Next(1, 256); SaveFixture.Reseal(bytes);
                try { var parsed = SaveCodec.Decode(bytes, s.Catalog); Assert.Equal(SaveCodec.Encode(parsed.Snapshot), SaveCodec.Encode(SaveCodec.Decode(SaveCodec.Encode(parsed.Snapshot), s.Catalog).Snapshot)); }
                catch (Exception e) when (e is CorruptSaveException || e is UnsupportedSaveException || e is CatalogMismatchException) { }
            }
        }
        [Fact] public void SnapshotAndCodecReferencesRemainDeepCopyIndependent()
        {
            var s = PilotWorld.World(); var bytes = SaveCodec.Encode(s); var r = SaveCodec.Decode(bytes, s.Catalog).Snapshot; var draft = r.Export(); draft.Player.Name = "changed"; draft.Party[0].CreatureId = null; draft.Boxes[0].Label = "changed"; draft.Creatures[0].OriginalTrainer.Name = "changed"; draft.Extensions[0].Payload[0] = 0; Array.Clear(bytes, 0, bytes.Length); Assert.Equal(SaveCodec.Encode(s), SaveCodec.Encode(r));
        }
    }
    public sealed class AtomicStoreTests
    {
        public static IEnumerable<object[]> Stages() => Enum.GetValues(typeof(CommitStage)).Cast<CommitStage>().Select(s => new object[] { s });
        private static AtomicSaveStore Store(Scratch d, SaveSnapshot s, Action<CommitStage>? fault = null) => new AtomicSaveStore(d.DirectoryPath, s.SaveId, s.Catalog, fault);
        [Fact] public void InitialAndSubsequentCommitsAreAtomicAndKeepPreviousGeneration()
        {
            using var d = new Scratch(); var s = PilotWorld.World(); var store = Store(d, s); Assert.Null(store.Load()); Assert.False(store.Commit(s, -1).AlreadyCommitted); var next = SaveFixture.Next(s); store.Commit(next, 0); Assert.Equal(1, store.Load()!.Snapshot.Revision); Assert.Equal(0, SaveCodec.Decode(File.ReadAllBytes(store.BackupPath), s.Catalog).Snapshot.Revision); Assert.False(File.Exists(store.TemporaryPath));
        }
        [Theory, MemberData(nameof(Stages))] public void InterruptedUpdatesHaveExactlyOldOrNewCommittedState(CommitStage stage)
        {
            using var d = new Scratch(); var s = PilotWorld.World(); var store = Store(d, s); store.Commit(s, -1); var next = SaveFixture.Next(s); var faulty = Store(d, s, at => { if (at == stage) throw new IOException("Injected interruption"); });
            if (stage == CommitStage.AfterAtomicReplace) Assert.Throws<CommitUncertainException>(() => faulty.Commit(next, 0)); else Assert.Throws<IOException>(() => faulty.Commit(next, 0));
            var loaded = store.Load()!; Assert.False(loaded.FromBackup); Assert.Equal(stage == CommitStage.AfterAtomicReplace ? 1 : 0, loaded.Snapshot.Revision); Assert.Equal(SaveCodec.Encode(stage == CommitStage.AfterAtomicReplace ? next : s), SaveCodec.Encode(loaded.Snapshot));
        }
        [Theory, MemberData(nameof(Stages))] public void InterruptedInitialSaveNeverPromotesTempOnLoad(CommitStage stage)
        {
            using var d = new Scratch(); var s = PilotWorld.World(); var faulty = Store(d, s, at => { if (at == stage) throw new IOException("Injected interruption"); }); Assert.ThrowsAny<IOException>(() => faulty.Commit(s, -1)); var store = Store(d, s);
            if (stage == CommitStage.AfterAtomicReplace) Assert.Equal(0, store.Load()!.Snapshot.Revision); else Assert.Null(store.Load()); store.Commit(s, -1); Assert.Equal(0, store.Load()!.Snapshot.Revision);
        }
        [Fact] public void IdempotentRetryAndConflictingWriterRevisionAreExplicit()
        {
            using var d = new Scratch(); var s = PilotWorld.World(); var store = Store(d, s); store.Commit(s, -1); Assert.True(store.Commit(s, -1).AlreadyCommitted); Assert.Throws<RevisionConflictException>(() => store.Commit(s, 100));
            var a = SaveFixture.Next(s, 4); store.Commit(a, 0); Assert.True(store.Commit(a, 0).AlreadyCommitted); Assert.Throws<RevisionConflictException>(() => store.Commit(SaveFixture.Next(s, 5), 0)); var changed = a.Export(); changed.Description = "different"; Assert.Throws<RevisionConflictException>(() => store.Commit(new SaveSnapshot(changed, s.Catalog), 0)); Assert.Equal(a.CommitId, store.Load()!.Snapshot.CommitId);
        }
        [Fact] public async Task ConcurrentWritersCannotLoseAnUpdate()
        {
            using var d = new Scratch(); var s = PilotWorld.World(); Store(d, s).Commit(s, -1); using var start = new ManualResetEventSlim(); var proposals = new[] { SaveFixture.Next(s, 4, "a"), SaveFixture.Next(s, 5, "b") };
            var tasks = proposals.Select(p => Task.Run(() => { start.Wait(); try { Store(d, s).Commit(p, 0); return true; } catch (Exception e) when (e is IOException || e is RevisionConflictException) { return false; } })).ToArray(); start.Set(); var results = await Task.WhenAll(tasks); Assert.Single(results, success => success); Assert.Equal(1, Store(d, s).Load()!.Snapshot.Revision);
        }
        [Fact] public void LockIsExclusiveAndNoStaleLockFileBlocksLaterAccess()
        {
            using var d = new Scratch(); var s = PilotWorld.World(); var store = Store(d, s); store.Commit(s, -1); using (var handle = new FileStream(Path.Combine(d.DirectoryPath, "roster.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None)) Assert.Throws<IOException>(() => store.Load()); Assert.NotNull(store.Load());
        }
        [Fact] public void NativePublicationFailureIsReportedAsUncertainAndReconciledByReload()
        {
            using var d = new Scratch(); var s = PilotWorld.World(); var store = Store(d, s); store.Commit(s, -1); var one = SaveFixture.Next(s); store.Commit(one, 0); var two = SaveFixture.Next(one, 5);
            using (var locked = new FileStream(store.BackupPath, FileMode.Open, FileAccess.Read, FileShare.None)) Assert.Throws<CommitUncertainException>(() => store.Commit(two, 1)); var current = store.Load()!; Assert.InRange(current.Snapshot.Revision, 1, 2); Assert.False(current.FromBackup);
        }
        [Fact] public void CorruptPrimaryUsesBackupWithoutWritingAndExplicitRecoveryPreservesForensics()
        {
            using var d = new Scratch(); var s = PilotWorld.World(); var store = Store(d, s); store.Commit(s, -1); store.Commit(SaveFixture.Next(s), 0); byte[] bad = File.ReadAllBytes(store.PrimaryPath); bad[30] ^= 1; File.WriteAllBytes(store.PrimaryPath, bad); var loaded = store.Load()!; Assert.True(loaded.FromBackup); Assert.Equal(0, loaded.Snapshot.Revision); Assert.Equal(bad, File.ReadAllBytes(store.PrimaryPath));
            Assert.Throws<RecoveryRequiredException>(() => store.Commit(SaveFixture.Next(loaded.Snapshot, 5), 0)); Assert.Throws<RevisionConflictException>(() => store.RepairFromBackup(PilotWorld.Id(77), PilotWorld.Id(999)));
            var repaired = store.RepairFromBackup(PilotWorld.Id(77), loaded.Snapshot.CommitId); Assert.False(repaired.FromBackup); Assert.Equal(bad, File.ReadAllBytes(Path.Combine(d.DirectoryPath, "quarantine-" + PilotWorld.Id(77).ToString("N") + ".save"))); Assert.Equal(SaveCodec.Encode(s), File.ReadAllBytes(store.BackupPath)); store.Commit(SaveFixture.Next(repaired.Snapshot, 6), 0); Assert.Equal(1, store.Load()!.Snapshot.Revision);
        }
        [Fact] public void MissingPrimaryCanRecoverAndCorruptBackupDoesNotBecomeNewSave()
        {
            using var d = new Scratch(); var s = PilotWorld.World(); var store = Store(d, s); store.Commit(s, -1); store.Commit(SaveFixture.Next(s), 0); File.Delete(store.PrimaryPath); Assert.True(store.Load()!.FromBackup); store.RepairFromBackup(PilotWorld.Id(77), s.CommitId); Assert.False(store.Load()!.FromBackup);
            File.WriteAllBytes(store.PrimaryPath, new byte[] { 1 }); File.WriteAllBytes(store.BackupPath, new byte[] { 2 }); Assert.Throws<CorruptSaveException>(() => store.Load()); Assert.Throws<CorruptSaveException>(() => store.Commit(SaveFixture.Next(s), 0));
        }
        [Fact] public void RecoveryNeverOverwritesExistingQuarantineEvidence()
        {
            using var d = new Scratch(); var s = PilotWorld.World(); var store = Store(d, s); store.Commit(s, -1); store.Commit(SaveFixture.Next(s), 0); File.WriteAllBytes(store.PrimaryPath, new byte[] { 1 }); var q = Path.Combine(d.DirectoryPath, "quarantine-" + PilotWorld.Id(77).ToString("N") + ".save"); File.WriteAllBytes(q, new byte[] { 7, 8 }); Assert.Throws<IOException>(() => store.RepairFromBackup(PilotWorld.Id(77), s.CommitId)); Assert.Equal(new byte[] { 7, 8 }, File.ReadAllBytes(q)); Assert.True(store.Load()!.FromBackup);
        }
        [Theory, MemberData(nameof(Stages))] public void RecoveryInterruptedAtEveryStageKeepsAtLeastOneValidCommittedGeneration(CommitStage stage)
        {
            using var d = new Scratch(); var s = PilotWorld.World(); var store = Store(d, s); store.Commit(s, -1); store.Commit(SaveFixture.Next(s), 0); File.WriteAllBytes(store.PrimaryPath, new byte[] { 1, 2 });
            var faulty = Store(d, s, at => { if (at == stage) throw new IOException("Interrupted recovery"); }); Assert.ThrowsAny<IOException>(() => faulty.RepairFromBackup(PilotWorld.Id(77), s.CommitId)); var loaded = store.Load()!; Assert.Equal(0, loaded.Snapshot.Revision); Assert.Equal(stage != CommitStage.AfterAtomicReplace, loaded.FromBackup); Assert.Equal(0, SaveCodec.Decode(File.ReadAllBytes(store.BackupPath), s.Catalog).Snapshot.Revision);
        }
        [Fact] public void UncommittedValidOrCorruptTempFilesAreNeverAutoRecovered()
        {
            using var d = new Scratch(); var s = PilotWorld.World(); var store = Store(d, s); File.WriteAllBytes(store.TemporaryPath, SaveCodec.Encode(s)); Assert.Null(store.Load()); File.WriteAllBytes(store.TemporaryPath, new byte[] { 1 }); Assert.Null(store.Load()); store.Commit(s, -1); File.WriteAllBytes(store.TemporaryPath, SaveCodec.Encode(SaveFixture.Next(s))); Assert.Equal(0, store.Load()!.Snapshot.Revision);
        }
        [Theory, InlineData("schema"), InlineData("envelope"), InlineData("catalog"), InlineData("ruleset"), InlineData("profile")]
        public void FutureOrForeignPrimaryNeverSilentlyRollsBackToBackup(string kind)
        {
            using var d = new Scratch(); var s = PilotWorld.World(); var store = Store(d, s); store.Commit(s, -1); var next = SaveFixture.Next(s); store.Commit(next, 0); var bytes = File.ReadAllBytes(store.PrimaryPath);
            if (kind == "schema") bytes = SaveFixture.HeaderInt(bytes, 12, 99); else if (kind == "envelope") bytes = SaveFixture.HeaderInt(bytes, 8, 99); else if (kind == "catalog") SaveFixture.ChangeText(bytes, s.Catalog.ContentHash, new string('0', 64)); else if (kind == "ruleset") SaveFixture.ChangeText(bytes, "hgss-gen4-subset-1.0.0", "xxxx-gen4-subset-1.0.0"); else { var other = next.Export(); other.SaveId = PilotWorld.Id(999); bytes = SaveCodec.Encode(new SaveSnapshot(other, s.Catalog)); }
            File.WriteAllBytes(store.PrimaryPath, bytes); var exception = Record.Exception(() => store.Load()); Assert.NotNull(exception); Assert.False(exception is CorruptSaveException); Assert.Equal(bytes, File.ReadAllBytes(store.PrimaryPath));
        }
        [Fact] public void LegacyReadDoesNotRewriteAndNextExplicitCommitMigratesAtomically()
        {
            using var d = new Scratch(); var s = PilotWorld.World(); var store = Store(d, s); var bytes = SaveCodec.EncodeLegacyFixture(s); File.WriteAllBytes(store.PrimaryPath, bytes); var read = store.Load()!; Assert.Equal(1, read.SourceSchema); Assert.Equal(bytes, File.ReadAllBytes(store.PrimaryPath)); store.Commit(SaveFixture.Next(read.Snapshot), 0); Assert.Equal(2, store.Load()!.SourceSchema); Assert.Equal(bytes, File.ReadAllBytes(store.BackupPath));
        }
        [Fact] public void OversizedOrTamperedSnapshotsNeverOverwriteExistingSaves()
        {
            using var d = new Scratch(); var s = PilotWorld.World(); var store = Store(d, s); store.Commit(s, -1); var draft = s.PrepareCommit(PilotWorld.Id(4)).Export(); ExtensionData[] Big() => Enumerable.Range(0, 64).Select(i => new ExtensionData { Key = "future.x" + i, Schema = "future-1", Payload = new byte[65536] }).ToArray(); draft.Extensions = Big(); draft.Creatures[0].Extensions = Big(); var huge = new SaveSnapshot(draft, s.Catalog); Assert.Throws<InvalidDataException>(() => store.Commit(huge, 0)); Assert.Equal(SaveCodec.Encode(s), File.ReadAllBytes(store.PrimaryPath));
        }
    }
}
