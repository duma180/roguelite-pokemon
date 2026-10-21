using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Roguelite.Domain
{
    public static class Check
    {
        public static int Range(int value, int min, int max, string name) { if (value < min || value > max) throw new ArgumentOutOfRangeException(name); return value; }
        public static long Range(long value, long min, long max, string name) { if (value < min || value > max) throw new ArgumentOutOfRangeException(name); return value; }
        public static Guid Identity(Guid id) { if (id == Guid.Empty) throw new ArgumentException("Empty identity."); return id; }
        public static T Enum<T>(T value) where T : struct { if (!System.Enum.IsDefined(typeof(T), value)) throw new ArgumentException("Unknown enum value."); return value; }
        public static string Id(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 128) throw new ArgumentException("Invalid identifier.");
            foreach (char c in value) if (!(c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '.' || c == '-' || c == '_')) throw new ArgumentException("Identifier must use lowercase ASCII.");
            return value;
        }
        public static string Text(string value, int max = 256) { if (value == null || new UTF8Encoding(false, true).GetByteCount(value) > max) throw new ArgumentException("Invalid text length/Unicode."); return value; }
    }
    public static class Wire
    {
        public static readonly Encoding Utf8 = new UTF8Encoding(false, true);
        public static void Text(BinaryWriter w, string value) { byte[] b = Utf8.GetBytes(value); w.Write(b.Length); w.Write(b); }
        public static string Text(BinaryReader r, int max = 4096) { int n = Count(r, max); return Utf8.GetString(Exact(r, n)); }
        public static void Blob(BinaryWriter w, byte[] value) { w.Write(value.Length); w.Write(value); }
        public static byte[] Blob(BinaryReader r, int max = 65536) => Exact(r, Count(r, max));
        public static void Id(BinaryWriter w, Guid value) => w.Write(value.ToByteArray());
        public static Guid Id(BinaryReader r) => new Guid(Exact(r, 16));
        public static int Count(BinaryReader r, int max) => Check.Range(r.ReadInt32(), 0, max, "wire count");
        public static bool Bool(BinaryReader r) { byte b = r.ReadByte(); if (b > 1) throw new InvalidDataException("Noncanonical boolean."); return b == 1; }
        public static byte[] Exact(BinaryReader r, int count) { var b = r.ReadBytes(count); if (b.Length != count) throw new EndOfStreamException(); return b; }
        public static string Hash(byte[] value) { using var h = SHA256.Create(); return Hex(h.ComputeHash(value)); }
        public static string Hex(byte[] value) => BitConverter.ToString(value).Replace("-", "").ToLowerInvariant();
    }
    public enum GrowthRate { MediumFast, Erratic, Fluctuating, MediumSlow, Fast, Slow }
    public enum Gender { Male, Female, Genderless }
    public enum PersistentStatus { Healthy, Sleep, Poison, Burn, Freeze, Paralysis, Toxic }
    public enum GameLanguage { Japanese = 1, English = 2, French = 3, Italian = 4, German = 5, Spanish = 7, Korean = 8 }
    public static class ExperienceCurve
    {
        public static long Threshold(GrowthRate rate, int level)
        {
            Check.Enum(rate); Check.Range(level, 1, 100, nameof(level)); if (level == 1) return 0;
            long n = level, cube = n * n * n;
            switch (rate)
            {
                case GrowthRate.MediumFast: return cube;
                case GrowthRate.Fast: return cube * 4 / 5;
                case GrowthRate.Slow: return cube * 5 / 4;
                case GrowthRate.MediumSlow: return Math.Max(0, cube * 6 / 5 - 15 * n * n + 100 * n - 140);
                case GrowthRate.Erratic: return n <= 50 ? cube * (100 - n) / 50 : n <= 68 ? cube * (150 - n) / 100 : n <= 98 ? cube * ((1911 - 10 * n) / 3) / 500 : cube * (160 - n) / 100;
                case GrowthRate.Fluctuating: return n <= 15 ? cube * ((n + 1) / 3 + 24) / 50 : n <= 36 ? cube * (n + 14) / 50 : cube * (n / 2 + 32) / 50;
                default: throw new NotSupportedException();
            }
        }
        public static void Validate(GrowthRate rate, int level, long experience)
        {
            if (experience < Threshold(rate, level) || experience > Threshold(rate, 100) || level < 100 && experience >= Threshold(rate, level + 1)) throw new ArgumentException("Level and experience disagree.");
        }
    }
}
