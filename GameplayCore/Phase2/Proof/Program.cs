using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Roguelite.Domain;
using Roguelite.Persistence;
using Roguelite.Phase2.Fixtures;

namespace Roguelite.Phase2.Proof
{
    internal static class Program
    {
        private static void Output(object result) => Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        private static void Fresh(string directory)
        {
            if (Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any()) throw new IOException("Proof requires a fresh, empty disposable directory."); Directory.CreateDirectory(directory); File.WriteAllText(Path.Combine(directory, ".phase2-disposable"), "phase2-research-only");
        }
        private static void Owned(string directory) { if (!File.Exists(Path.Combine(directory, ".phase2-disposable"))) throw new IOException("Disposable proof marker missing; refusing to touch directory."); }
        private static SaveSnapshot Next(SaveSnapshot snapshot)
        {
            var d = snapshot.PrepareCommit(PilotWorld.Id(4)).Export(); d.Description = "child-transaction"; return new SaveSnapshot(d, snapshot.Catalog);
        }
        private static int Main(string[] args)
        {
            var c = DomainCatalog.Create(); var s = PilotWorld.World(c);
            if (args.Length == 1 && args[0] == "vectors") { Output(new { catalogHash = c.ContentHash, saveHash = Wire.Hash(SaveCodec.Encode(s)), saveBytes = SaveCodec.Encode(s).Length, schema = SaveCodec.CurrentSchema, is64BitProcess = Environment.Is64BitProcess }); return 0; }
            if (args.Length < 2) throw new ArgumentException("Usage: vectors | init DIR | inspect DIR | interrupt DIR STAGE | demo DIR"); string directory = Path.GetFullPath(args[1]);
            if (args[0] == "init" || args[0] == "demo") Fresh(directory); else Owned(directory);
            var store = new AtomicSaveStore(directory, s.SaveId, c);
            if (args[0] == "init") { store.Commit(s, -1); Output(new { revision = 0, hash = Wire.Hash(SaveCodec.Encode(s)) }); return 0; }
            if (args[0] == "interrupt")
            {
                var stage = (CommitStage)Enum.Parse(typeof(CommitStage), args[2]); var interrupted = new AtomicSaveStore(directory, s.SaveId, c, at => { if (at == stage) Environment.Exit(73); }); interrupted.Commit(Next(s), 0); throw new Exception("Interruption point was not reached.");
            }
            if (args[0] == "inspect") { var loaded = store.Load() ?? throw new Exception("No committed save"); Output(new { revision = loaded.Snapshot.Revision, commitId = loaded.Snapshot.CommitId, fromBackup = loaded.FromBackup, schema = loaded.SourceSchema, hash = Wire.Hash(SaveCodec.Encode(loaded.Snapshot)) }); return 0; }
            if (args[0] != "demo") throw new ArgumentException("Unknown proof command.");
            store.Commit(s, -1); var moved = Storage.Transfer(s, s.Export().Party[0].SlotId, s.Export().Boxes[1].Slots[0].SlotId);
            var evolved = moved.Creature(PilotWorld.Id(10)).RecordEvolution("ivysaur", "base", PilotWorld.Id(6000), "approved-event-fixture"); var d = moved.ReplaceCreature(evolved).PrepareCommit(PilotWorld.Id(4)).Export(); d.Description = "Persistent identity / transfer / evolution fixture"; d.Boxes[1].ProtectionPolicyReference = "reserved.no-policy";
            var next = new SaveSnapshot(d, c); store.Commit(next, 0); var committed = store.Load()!; byte[] committedBytes = File.ReadAllBytes(store.PrimaryPath); string committedHash = Wire.Hash(committedBytes);
            if (committed.Snapshot.Export().Boxes[1].Slots[0].CreatureId != evolved.IndividualId || committed.Snapshot.Creature(evolved.IndividualId).SpeciesId != "ivysaur") throw new Exception("Identity/transfer/evolution proof failed.");
            committedBytes[40] ^= 1; File.WriteAllBytes(store.PrimaryPath, committedBytes); var recovered = store.Load()!; if (!recovered.FromBackup || recovered.Snapshot.Revision != 0) throw new Exception("Recovery proof failed."); store.RepairFromBackup(PilotWorld.Id(77), recovered.Snapshot.CommitId);
            string migrationDirectory = Path.Combine(directory, "migration"); Directory.CreateDirectory(migrationDirectory); var migrationStore = new AtomicSaveStore(migrationDirectory, s.SaveId, c); File.WriteAllBytes(migrationStore.PrimaryPath, SaveCodec.EncodeLegacyFixture(s)); var legacy = migrationStore.Load()!; migrationStore.Commit(Next(legacy.Snapshot), 0);
            var before = s.Export(); var after = committed.Snapshot.Export();
            bool slotsPreserved = before.Party.Select(x => x.SlotId).SequenceEqual(after.Party.Select(x => x.SlotId)) && before.Boxes.Select(x => x.BoxId).SequenceEqual(after.Boxes.Select(x => x.BoxId)) && before.Boxes.SelectMany(x => x.Slots).Select(x => x.SlotId).SequenceEqual(after.Boxes.SelectMany(x => x.Slots).Select(x => x.SlotId));
            bool enhancementsPreserved = s.Creature(evolved.IndividualId).Export().Extensions.Single(x => x.Key == "roguelite.enhancements").Payload.SequenceEqual(committed.Snapshot.Creature(evolved.IndividualId).Export().Extensions.Single(x => x.Key == "roguelite.enhancements").Payload);
            if (!slotsPreserved || !enhancementsPreserved) throw new Exception("Durable preservation check failed.");
            Output(new { version = "phase2-proof-1", catalogHash = c.ContentHash, initialHash = Wire.Hash(SaveCodec.Encode(s)), committedHash, individualId = evolved.IndividualId, speciesAfterEvolution = evolved.SpeciesId, partyAndPcSlotIdsPreserved = slotsPreserved, enhancementBytesPreserved = enhancementsPreserved, committedRevision = committed.Snapshot.Revision, recoveredRevision = recovered.Snapshot.Revision, corruptGenerationQuarantined = File.Exists(Path.Combine(directory, "quarantine-" + PilotWorld.Id(77).ToString("N") + ".save")), recoveredStateHash = Wire.Hash(SaveCodec.Encode(store.Load()!.Snapshot)), legacySchemaRead = legacy.SourceSchema, explicitMigration = legacy.Migration, upgradedSchema = migrationStore.Load()!.SourceSchema, is64BitProcess = Environment.Is64BitProcess }); return 0;
        }
    }
}
