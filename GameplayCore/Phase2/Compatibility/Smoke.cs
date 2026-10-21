using System;
using System.IO;
using Roguelite.Domain;
using Roguelite.Persistence;
using Roguelite.Phase2.Fixtures;

internal static class Smoke
{
    private static void Assert(bool condition) { if (!condition) throw new Exception("Compatibility smoke assertion failed."); }
    private static int Main(string[] args)
    {
        var c = DomainCatalog.Create(); var snapshot = PilotWorld.World(c); var bytes = SaveCodec.Encode(snapshot);
        Assert(Wire.Hash(bytes) == Wire.Hash(SaveCodec.Encode(SaveCodec.Decode(bytes, c).Snapshot)));
        Console.WriteLine("catalog=" + c.ContentHash); Console.WriteLine("save=" + Wire.Hash(bytes)); Console.WriteLine("pointerBytes=" + IntPtr.Size);
        string directory = Path.GetFullPath(args[0]); if (Directory.Exists(directory) && Directory.GetFileSystemEntries(directory).Length != 0) throw new Exception("Fresh compatibility directory required.");
        var store = new AtomicSaveStore(directory, snapshot.SaveId, c); store.Commit(snapshot, -1); var next = snapshot.PrepareCommit(PilotWorld.Id(4)); store.Commit(next, 0); Assert(store.Load()!.Snapshot.Revision == 1);
        var bad = File.ReadAllBytes(store.PrimaryPath); bad[40] ^= 1; File.WriteAllBytes(store.PrimaryPath, bad); Assert(store.Load()!.FromBackup); store.RepairFromBackup(PilotWorld.Id(77), snapshot.CommitId); Assert(!store.Load()!.FromBackup);
        var migrated = SaveCodec.Decode(SaveCodec.EncodeLegacyFixture(snapshot), c); Assert(migrated.SourceSchema == 1 && Wire.Hash(SaveCodec.Encode(migrated.Snapshot)) == Wire.Hash(bytes));
        var validBattle = BattleProjection.Create(PilotWorld.Plain(c), c); Assert(validBattle.Record.IndividualId == PilotWorld.Id(10));
        Console.WriteLine("roundTrip=pass;replace=pass;flush=pass;recovery=pass;migration=pass;projection=pass"); return 0;
    }
}
