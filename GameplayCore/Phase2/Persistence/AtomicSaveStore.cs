using System;
using System.IO;
using Roguelite.Domain;

namespace Roguelite.Persistence
{
    public enum CommitStage { BeforeTempWrite, MidTempWrite, AfterTempFlush, BeforeAtomicReplace, AfterAtomicReplace }
    public sealed class RevisionConflictException : InvalidOperationException { public RevisionConflictException() : base("Save revision/transaction conflicts with committed state.") { } }
    public sealed class RecoveryRequiredException : InvalidOperationException { public RecoveryRequiredException() : base("Explicit backup recovery is required before writing.") { } }
    public sealed class SaveIdentityMismatchException : InvalidOperationException { public SaveIdentityMismatchException() : base("Save belongs to a different profile.") { } }
    public sealed class CommitUncertainException : IOException { public CommitUncertainException(Exception inner) : base("Atomic publication was attempted and its acknowledgement is uncertain. Reload and reconcile CommitId; do not blindly retry a new transaction.", inner) { } }
    public sealed class LoadedSave
    {
        public SaveSnapshot Snapshot { get; }
        public bool FromBackup { get; }
        public int SourceSchema { get; }
        public string Migration { get; }
        internal LoadedSave(DecodedSave decoded, bool backup) { Snapshot = decoded.Snapshot; FromBackup = backup; SourceSchema = decoded.SourceSchema; Migration = decoded.Migration; }
    }
    public sealed class CommitResult
    {
        public bool AlreadyCommitted { get; }
        public long Revision { get; }
        internal CommitResult(bool already, long revision) { AlreadyCommitted = already; Revision = revision; }
    }
    public sealed class AtomicSaveStore
    {
        private readonly string directory;
        private readonly Guid saveId;
        private readonly Catalog catalog;
        private readonly Action<CommitStage>? fault;
        public string PrimaryPath => Path.Combine(directory, "roster.save");
        public string BackupPath => Path.Combine(directory, "roster.save.bak");
        public string TemporaryPath => Path.Combine(directory, "roster.save.tmp");
        public AtomicSaveStore(string directory, Guid saveId, Catalog catalog, Action<CommitStage>? faultInjector = null)
        {
            this.directory = Path.GetFullPath(directory ?? throw new ArgumentNullException(nameof(directory))); this.saveId = Check.Identity(saveId); this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog)); fault = faultInjector; Directory.CreateDirectory(this.directory);
        }
        private FileStream Acquire() => new FileStream(Path.Combine(directory, "roster.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        private DecodedSave Read(string path)
        {
            long length = new FileInfo(path).Length; if (length < SaveCodec.EnvelopeBytes || length > SaveCodec.MaxPayload + SaveCodec.EnvelopeBytes) throw new CorruptSaveException("Committed file has invalid size.");
            var decoded = SaveCodec.Decode(File.ReadAllBytes(path), catalog); if (decoded.Snapshot.SaveId != saveId) throw new SaveIdentityMismatchException(); return decoded;
        }
        private LoadedSave? LoadInternal()
        {
            CorruptSaveException? badPrimary = null;
            if (File.Exists(PrimaryPath)) { try { return new LoadedSave(Read(PrimaryPath), false); } catch (CorruptSaveException e) { badPrimary = e; } }
            if (File.Exists(BackupPath)) return new LoadedSave(Read(BackupPath), true);
            if (badPrimary != null) throw badPrimary;
            return null; // A .tmp file is never a committed save, even when its checksum is valid.
        }
        public LoadedSave? Load() { using var handle = Acquire(); return LoadInternal(); }
        private void WriteTemp(string path, byte[] bytes)
        {
            fault?.Invoke(CommitStage.BeforeTempWrite);
            using var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            int half = bytes.Length / 2; file.Write(bytes, 0, half); fault?.Invoke(CommitStage.MidTempWrite); file.Write(bytes, half, bytes.Length - half); file.Flush(true); fault?.Invoke(CommitStage.AfterTempFlush);
        }
        public CommitResult Commit(SaveSnapshot proposed, long expectedRevision)
        {
            if (proposed.SaveId != saveId) throw new SaveIdentityMismatchException();
            if (proposed.Catalog.ContentHash != catalog.ContentHash) throw new CatalogMismatchException("Store catalog differs.");
            Check.Range(expectedRevision, -1, long.MaxValue, nameof(expectedRevision)); byte[] bytes = SaveCodec.Encode(proposed);
            if (expectedRevision == long.MaxValue || proposed.Revision != expectedRevision + 1) throw new RevisionConflictException();
            using var handle = Acquire(); var current = LoadInternal();
            if (current?.FromBackup == true) throw new RecoveryRequiredException();
            if (current != null && current.Snapshot.CommitId == proposed.CommitId)
            {
                if (Wire.Hash(SaveCodec.Encode(current.Snapshot)) != Wire.Hash(bytes)) throw new RevisionConflictException();
                return new CommitResult(true, current.Snapshot.Revision);
            }
            long actual = current?.Snapshot.Revision ?? -1;
            if (actual != expectedRevision) throw new RevisionConflictException();
            WriteTemp(TemporaryPath, bytes);
            // Verify the flushed candidate before publishing; never implement a delete/copy fallback for Replace.
            Read(TemporaryPath); fault?.Invoke(CommitStage.BeforeAtomicReplace);
            try
            {
                if (current == null) File.Move(TemporaryPath, PrimaryPath); else File.Replace(TemporaryPath, PrimaryPath, BackupPath);
                fault?.Invoke(CommitStage.AfterAtomicReplace); return new CommitResult(false, proposed.Revision);
            }
            catch (Exception e) { throw new CommitUncertainException(e); } // Native publication errors also require reconciliation.
        }
        public LoadedSave RepairFromBackup(Guid recoveryId, Guid expectedBackupCommitId)
        {
            Check.Identity(recoveryId); using var handle = Acquire(); var loaded = LoadInternal(); if (loaded == null || !loaded.FromBackup) throw new InvalidOperationException("No backup recovery is pending.");
            if (loaded.Snapshot.CommitId != expectedBackupCommitId) throw new RevisionConflictException();
            string quarantine = Path.Combine(directory, "quarantine-" + recoveryId.ToString("N") + ".save"); if (File.Exists(quarantine)) throw new IOException("Recovery quarantine already exists; refusing overwrite.");
            byte[] backup = File.ReadAllBytes(BackupPath); string temp = Path.Combine(directory, "roster.repair.tmp"); WriteTemp(temp, backup); Read(temp); fault?.Invoke(CommitStage.BeforeAtomicReplace);
            try
            {
                if (File.Exists(PrimaryPath)) File.Replace(temp, PrimaryPath, quarantine); else File.Move(temp, PrimaryPath);
                fault?.Invoke(CommitStage.AfterAtomicReplace); return LoadInternal() ?? throw new CorruptSaveException("Recovered primary missing.");
            }
            catch (Exception e) { throw new CommitUncertainException(e); }
        }
    }
}
