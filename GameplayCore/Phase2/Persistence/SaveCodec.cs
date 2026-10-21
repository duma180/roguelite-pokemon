using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Roguelite.Domain;
using Roguelite.Gameplay;

namespace Roguelite.Persistence
{
    public sealed class CorruptSaveException : IOException { public CorruptSaveException(string message, Exception? inner = null) : base(message, inner) { } }
    public sealed class UnsupportedSaveException : NotSupportedException { public UnsupportedSaveException(string message) : base(message) { } }
    public sealed class DecodedSave
    {
        public SaveSnapshot Snapshot { get; }
        public int SourceSchema { get; }
        public string Migration => SourceSchema == 1 ? "1.0.0->2.0.0:add-empty-description" : "none";
        internal DecodedSave(SaveSnapshot snapshot, int schema) { Snapshot = snapshot; SourceSchema = schema; }
    }
    public static class SaveCodec
    {
        public const int CurrentSchema = 2;
        public const int MaxPayload = 8 * 1024 * 1024;
        public const int EnvelopeBytes = 52;
        private static readonly byte[] Magic = Wire.Utf8.GetBytes("PKRGSAVE");
        private sealed class LimitedStream : Stream
        {
            private readonly MemoryStream inner = new MemoryStream();
            public override bool CanRead => false; public override bool CanSeek => false; public override bool CanWrite => true; public override long Length => inner.Length; public override long Position { get => inner.Position; set => throw new NotSupportedException(); }
            public override void Flush() => inner.Flush(); public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException(); public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException(); public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) { if (Length + count > MaxPayload) throw new InvalidDataException("Save exceeds payload limit."); inner.Write(buffer, offset, count); }
            public override void WriteByte(byte value) { if (Length == MaxPayload) throw new InvalidDataException("Save exceeds payload limit."); inner.WriteByte(value); }
            public byte[] Bytes() => inner.ToArray(); protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
        }
        public static byte[] Encode(SaveSnapshot snapshot) => EncodeVersion(snapshot, CurrentSchema);
        // A deliberately introduced predecessor for migration tests, not a claim that Phase 1 had native saves.
        public static byte[] EncodeLegacyFixture(SaveSnapshot snapshot)
        {
            if (snapshot.Export().Description != "") throw new InvalidOperationException("Legacy export cannot discard a current description."); return EncodeVersion(snapshot, 1);
        }
        private static byte[] EncodeVersion(SaveSnapshot snapshot, int schema)
        {
            using var payload = new LimitedStream(); using (var w = new BinaryWriter(payload, Wire.Utf8, true)) WriteSave(w, snapshot.Export(), schema);
            byte[] body = payload.Bytes(); using var stream = new MemoryStream(); using (var w = new BinaryWriter(stream, Wire.Utf8, true)) { w.Write(Magic); w.Write(1); w.Write(schema); w.Write(body.Length); w.Write(body); }
            byte[] prefix = stream.ToArray(); using var sha = SHA256.Create(); byte[] digest = sha.ComputeHash(prefix); stream.Write(digest, 0, digest.Length); return stream.ToArray();
        }
        public static DecodedSave Decode(byte[] bytes, Catalog catalog)
        {
            if (bytes == null || bytes.Length < EnvelopeBytes || bytes.Length > MaxPayload + EnvelopeBytes) throw new CorruptSaveException("Save length is outside limits.");
            using var sha = SHA256.Create(); var expected = sha.ComputeHash(bytes, 0, bytes.Length - 32); int different = 0; for (int i = 0; i < 32; i++) different |= expected[i] ^ bytes[bytes.Length - 32 + i]; if (different != 0) throw new CorruptSaveException("Envelope integrity mismatch.");
            try
            {
                using var stream = new MemoryStream(bytes, false); using var r = new BinaryReader(stream, Wire.Utf8, true);
                if (!Wire.Exact(r, 8).SequenceEqual(Magic)) throw new CorruptSaveException("Wrong save magic."); int envelope = r.ReadInt32(), schema = r.ReadInt32(), length = r.ReadInt32();
                if (length < 0 || length > MaxPayload || length != bytes.Length - EnvelopeBytes) throw new CorruptSaveException("Invalid payload length.");
                if (envelope != 1 || schema != 1 && schema != CurrentSchema) throw new UnsupportedSaveException("Unrecognized envelope/schema; automatic downgrade forbidden.");
                var data = ReadSave(r, schema); if (stream.Position != bytes.Length - 32) throw new CorruptSaveException("Trailing or missing payload fields.");
                if (data.Ruleset != HgssRules.Version) throw new UnsupportedSaveException("Unsupported ruleset; explicit migration required.");
                return new DecodedSave(new SaveSnapshot(data, catalog), schema);
            }
            catch (Exception e) when (e is ArgumentException || e is System.Collections.Generic.KeyNotFoundException || e is EndOfStreamException || e is InvalidDataException || e is OverflowException)
            { throw new CorruptSaveException("Save schema/domain validation failed.", e); }
        }
        private static void Extensions(BinaryWriter w, ExtensionData[] items)
        {
            w.Write(items.Length); foreach (var e in items.OrderBy(e => e.Key, StringComparer.Ordinal)) { Wire.Text(w, e.Key); Wire.Text(w, e.Schema); Wire.Blob(w, e.Payload); }
        }
        private static ExtensionData[] Extensions(BinaryReader r)
        {
            var a = new ExtensionData[Wire.Count(r, 64)]; for (int i = 0; i < a.Length; i++) a[i] = new ExtensionData { Key = Wire.Text(r, 128), Schema = Wire.Text(r, 128), Payload = Wire.Blob(r) }; return a;
        }
        private static void Trainer(BinaryWriter w, TrainerData t) { w.Write(t.TrainerId); Wire.Text(w, t.Name); w.Write((int)t.Gender); w.Write((int)t.Language); }
        private static TrainerData Trainer(BinaryReader r) => new TrainerData { TrainerId = r.ReadUInt32(), Name = Wire.Text(r, 64), Gender = (Gender)r.ReadInt32(), Language = (GameLanguage)r.ReadInt32() };
        private static void Creature(BinaryWriter w, CreatureData p)
        {
            Wire.Id(w, p.IndividualId); Wire.Text(w, p.SpeciesId); Wire.Text(w, p.FormId); w.Write(p.Personality); w.Write((int)p.Gender); w.Write((int)p.Nature); Wire.Text(w, p.AbilityId); w.Write(p.AbilitySlot);
            foreach (int x in p.Ivs) w.Write(x); foreach (int x in p.Evs) w.Write(x); w.Write(p.Level); w.Write(p.Experience);
            w.Write(p.Moves.Length); foreach (var m in p.Moves) { Wire.Id(w, m.SlotId); Wire.Text(w, m.MoveId); w.Write(m.Pp); w.Write(m.PpUps); }
            Wire.Text(w, p.HeldItemId); w.Write(p.Friendship); Trainer(w, p.OriginalTrainer); Wire.Id(w, p.HandlerId); w.Write((int)p.Language); Wire.Text(w, p.Nickname); w.Write(p.IsNicknamed); w.Write(p.CurrentHp); w.Write((int)p.Status); w.Write(p.SleepTurns); w.Write(p.IsEgg); w.Write(p.EggCyclesRemaining);
            w.Write(p.OriginGame); w.Write(p.MetLocation); w.Write(p.MetLevel); Wire.Text(w, p.MetDate); w.Write(p.EggMetLocation); Wire.Text(w, p.EggMetDate); Wire.Text(w, p.CaptureBallId); w.Write(p.FatefulEncounter); w.Write(p.PokerusStrain); w.Write(p.PokerusDays); w.Write(p.Markings); Wire.Blob(w, p.ContestStats); Wire.Blob(w, p.RibbonBits);
            w.Write(p.ShinyLeaves); w.Write(p.Mood); w.Write(p.MetTerrain); w.Write(p.LegacyEggMetLocation); w.Write(p.LegacyMetLocation); w.Write(p.LegacyBallCode); w.Write(p.BallCapsuleId); Wire.Blob(w, p.MailData); Wire.Blob(w, p.CapsuleSeals);
            w.Write(p.EvolutionHistory.Length); foreach (var e in p.EvolutionHistory) { Wire.Id(w, e.EventId); Wire.Text(w, e.FromSpecies); Wire.Text(w, e.FromForm); Wire.Text(w, e.ToSpecies); Wire.Text(w, e.ToForm); Wire.Text(w, e.EvidenceId); w.Write(e.Level); }
            Extensions(w, p.Extensions);
        }
        private static CreatureData Creature(BinaryReader r)
        {
            var p = new CreatureData { IndividualId = Wire.Id(r), SpeciesId = Wire.Text(r, 128), FormId = Wire.Text(r, 128), Personality = r.ReadUInt32(), Gender = (Gender)r.ReadInt32(), Nature = (Nature)r.ReadInt32(), AbilityId = Wire.Text(r, 128), AbilitySlot = r.ReadInt32() };
            p.Ivs = Enumerable.Range(0, 6).Select(_ => r.ReadInt32()).ToArray(); p.Evs = Enumerable.Range(0, 6).Select(_ => r.ReadInt32()).ToArray(); p.Level = r.ReadInt32(); p.Experience = r.ReadInt64();
            p.Moves = new MoveData[Wire.Count(r, 4)]; for (int i = 0; i < p.Moves.Length; i++) p.Moves[i] = new MoveData { SlotId = Wire.Id(r), MoveId = Wire.Text(r, 128), Pp = r.ReadInt32(), PpUps = r.ReadInt32() };
            p.HeldItemId = Wire.Text(r, 128); p.Friendship = r.ReadInt32(); p.OriginalTrainer = Trainer(r); p.HandlerId = Wire.Id(r); p.Language = (GameLanguage)r.ReadInt32(); p.Nickname = Wire.Text(r, 256); p.IsNicknamed = Wire.Bool(r); p.CurrentHp = r.ReadInt32(); p.Status = (PersistentStatus)r.ReadInt32(); p.SleepTurns = r.ReadInt32(); p.IsEgg = Wire.Bool(r); p.EggCyclesRemaining = r.ReadInt32();
            p.OriginGame = r.ReadInt32(); p.MetLocation = r.ReadInt32(); p.MetLevel = r.ReadInt32(); p.MetDate = Wire.Text(r, 10); p.EggMetLocation = r.ReadInt32(); p.EggMetDate = Wire.Text(r, 10); p.CaptureBallId = Wire.Text(r, 128); p.FatefulEncounter = Wire.Bool(r); p.PokerusStrain = r.ReadInt32(); p.PokerusDays = r.ReadInt32(); p.Markings = r.ReadInt32(); p.ContestStats = Wire.Blob(r, 6); p.RibbonBits = Wire.Blob(r, 32);
            p.ShinyLeaves = r.ReadInt32(); p.Mood = r.ReadInt32(); p.MetTerrain = r.ReadInt32(); p.LegacyEggMetLocation = r.ReadInt32(); p.LegacyMetLocation = r.ReadInt32(); p.LegacyBallCode = r.ReadInt32(); p.BallCapsuleId = r.ReadInt32(); p.MailData = Wire.Blob(r, 56); p.CapsuleSeals = Wire.Blob(r, 24);
            p.EvolutionHistory = new EvolutionData[Wire.Count(r, 100)]; for (int i = 0; i < p.EvolutionHistory.Length; i++) p.EvolutionHistory[i] = new EvolutionData { EventId = Wire.Id(r), FromSpecies = Wire.Text(r, 128), FromForm = Wire.Text(r, 128), ToSpecies = Wire.Text(r, 128), ToForm = Wire.Text(r, 128), EvidenceId = Wire.Text(r, 128), Level = r.ReadInt32() };
            p.Extensions = Extensions(r); return p;
        }
        private static void Slots(BinaryWriter w, SlotData[] slots) { w.Write(slots.Length); foreach (var s in slots) { Wire.Id(w, s.SlotId); w.Write(s.CreatureId.HasValue); if (s.CreatureId.HasValue) Wire.Id(w, s.CreatureId.Value); } }
        private static SlotData[] Slots(BinaryReader r, int max) { var a = new SlotData[Wire.Count(r, max)]; for (int i = 0; i < a.Length; i++) { a[i] = new SlotData { SlotId = Wire.Id(r) }; if (Wire.Bool(r)) a[i].CreatureId = Wire.Id(r); } return a; }
        private static void WriteSave(BinaryWriter w, SaveData d, int schema)
        {
            Wire.Id(w, d.SaveId); Wire.Id(w, d.CommitId); w.Write(d.Revision); Wire.Id(w, d.HandlerId); Trainer(w, d.Player); Wire.Text(w, d.CatalogId); Wire.Text(w, d.CatalogVersion); Wire.Text(w, d.CatalogHash); Wire.Text(w, d.Ruleset);
            if (schema >= 2) Wire.Text(w, d.Description);
            w.Write(d.Creatures.Length); foreach (var p in d.Creatures.OrderBy(p => p.IndividualId.ToString("N"), StringComparer.Ordinal)) Creature(w, p);
            Slots(w, d.Party); w.Write(d.Boxes.Length); foreach (var box in d.Boxes) { Wire.Id(w, box.BoxId); Wire.Text(w, box.Label); Wire.Text(w, box.ProtectionPolicyReference); Slots(w, box.Slots); }
            Extensions(w, d.Extensions);
        }
        private static SaveData ReadSave(BinaryReader r, int schema)
        {
            var d = new SaveData { SaveId = Wire.Id(r), CommitId = Wire.Id(r), Revision = r.ReadInt64(), HandlerId = Wire.Id(r), Player = Trainer(r), CatalogId = Wire.Text(r, 128), CatalogVersion = Wire.Text(r, 128), CatalogHash = Wire.Text(r, 64), Ruleset = Wire.Text(r, 128), Description = schema >= 2 ? Wire.Text(r, 1024) : "" };
            d.Creatures = new CreatureData[Wire.Count(r, 546)]; for (int i = 0; i < d.Creatures.Length; i++) d.Creatures[i] = Creature(r);
            d.Party = Slots(r, 6); d.Boxes = new BoxData[Wire.Count(r, 18)]; for (int i = 0; i < d.Boxes.Length; i++) d.Boxes[i] = new BoxData { BoxId = Wire.Id(r), Label = Wire.Text(r, 128), ProtectionPolicyReference = Wire.Text(r, 128), Slots = Slots(r, 30) };
            d.Extensions = Extensions(r); return d;
        }
    }
}
