using System;
using System.Linq;
using Roguelite.Domain;

namespace Roguelite.Phase2.Fixtures
{
    public static class PilotWorld
    {
        public static Guid Id(int n) => Guid.Parse("20000000-0000-0000-0000-" + n.ToString("D12", System.Globalization.CultureInfo.InvariantCulture));
        public static TrainerData Trainer() => new TrainerData { TrainerId = 0x12345678, Name = "Pilot", Gender = Gender.Female, Language = GameLanguage.Japanese };
        public static PersistentPokemon Plain(Catalog c, int id = 10, string species = "bulbasaur", string form = "base", uint pid = 27)
        {
            return CreatureFactory.Create(c, Id(id), species, form, 50, pid, Trainer(), Id(3), new[] { 31, 0, 20, 5, 14, 28 }, new[] { 255, 255, 0, 0, 0, 0 }, new MoveData { SlotId = Id(50000 + id * 10), MoveId = "tackle", Pp = 35 });
        }
        public static PersistentPokemon Rich(Catalog c)
        {
            var p = Plain(c).Export(); p.Experience += 100; p.Language = GameLanguage.English; p.HeldItemId = "everstone"; p.Friendship = 212; p.Nickname = "芽🌱"; p.IsNicknamed = true; p.CurrentHp--; p.Status = PersistentStatus.Sleep; p.SleepTurns = 3;
            p.Moves = new[] { new MoveData { SlotId = Id(50100), MoveId = "tackle", Pp = 0, PpUps = 3 }, new MoveData { SlotId = Id(50101), MoveId = "razor-leaf", Pp = 17, PpUps = 1 }, new MoveData { SlotId = Id(50102), MoveId = "growl", Pp = 40 }, new MoveData { SlotId = Id(50103), MoveId = "toxic", Pp = 5, PpUps = 2 } };
            p.OriginGame = 7; p.MetLocation = 23; p.MetLevel = 5; p.MetDate = "2026-10-21"; p.EggMetLocation = 222; p.EggMetDate = "2025-12-31"; p.FatefulEncounter = true; p.PokerusStrain = 3; p.PokerusDays = 1; p.Markings = 63; p.ContestStats = new byte[] { 1, 2, 3, 4, 5, 6 }; p.RibbonBits = new byte[] { 255, 170, 0 };
            p.ShinyLeaves = 47; p.Mood = -27; p.MetTerrain = 3; p.LegacyEggMetLocation = 44; p.LegacyMetLocation = 45; p.LegacyBallCode = 4; p.BallCapsuleId = 2; p.MailData = Enumerable.Range(0, 56).Select(i => (byte)i).ToArray(); p.CapsuleSeals = Enumerable.Range(0, 24).Select(i => (byte)(255 - i)).ToArray();
            p.Extensions = new[] { new ExtensionData { Key = "roguelite.enhancements", Schema = "unreviewed-opaque-1", Payload = new byte[] { 1, 2, 3 } }, new ExtensionData { Key = "future.annotation", Schema = "unknown-9", Payload = new byte[] { 255, 0, 17 } } };
            return new PersistentPokemon(p, c);
        }
        public static SaveSnapshot World(Catalog? c = null, bool rich = true)
        {
            c ??= DomainCatalog.Create(); var a = rich ? Rich(c) : Plain(c); var b = Plain(c, 11, "rattata", pid: 100); var d = Plain(c, 12, "deoxys", "attack", 1000);
            var draft = new SaveData { SaveId = Id(1), CommitId = Id(2), HandlerId = Id(3), Player = Trainer(), CatalogId = c.Id, CatalogVersion = c.Version, CatalogHash = c.ContentHash, Creatures = new[] { a.Export(), b.Export(), d.Export() }, Party = Enumerable.Range(0, 6).Select(n => new SlotData { SlotId = Id(1000 + n) }).ToArray(), Boxes = Enumerable.Range(0, 2).Select(bi => new BoxData { BoxId = Id(2000 + bi), Label = bi == 0 ? "Research" : "保管", Slots = Enumerable.Range(0, 30).Select(i => new SlotData { SlotId = Id(3000 + bi * 30 + i) }).ToArray() }).ToArray(), Extensions = new[] { new ExtensionData { Key = "roguelite.poke-point-ledger", Schema = "unreviewed-opaque-1", Payload = new byte[] { 9, 8, 7 } } } };
            draft.Party[0].CreatureId = a.IndividualId; draft.Party[1].CreatureId = b.IndividualId; draft.Boxes[0].Slots[0].CreatureId = d.IndividualId; return new SaveSnapshot(draft, c);
        }
    }
}
