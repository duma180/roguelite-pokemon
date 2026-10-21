using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace PokemonRoguelite.NewBarkPilot.Editor
{
    public sealed class PilotIr
    {
        public const string ApprovedHash = "6bfd469abf6f7f440836bfdf96e4855a6a7907276260e0846bc3b2e8e77b23e6";
        public const string SchemaVersion = "1.2.0";
        public const string RomHash = "65f02a56842b75aa92d775d56d657a56fe3fa993550b04dc20704ab82d760105";
        public readonly JObject Root;
        public readonly string Hash;
        readonly string parsedContentHash;
        public IEnumerable<JObject> Lands => Root["outdoorGeography"]["landDataRecords"].Cast<JObject>();
        public IEnumerable<JObject> Neighborhood => Root["outdoorGeography"]["matrix"]["cells"].Cast<JObject>()
            .Where(c => Math.Abs((int)c["x"] - 21) <= 1 && Math.Abs((int)c["z"] - 12) <= 1);
        public JObject Events => (JObject)Root["events"]["raw"];

        PilotIr(JObject root, string hash) { Root = root; Hash = hash; parsedContentHash=Sha(Compact(root)); }
        public void VerifyUnchanged()
        {
            Require(Hash==ApprovedHash && Sha(Compact(Root))==parsedContentHash,"Validated IR was modified in memory; generation refused.");
            ValidateIdentity(Root);
        }

        public static string Sha(byte[] bytes)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
        public static string Sha(string text) => Sha(Encoding.UTF8.GetBytes(text));
        public static string Compact(JToken token) => token.ToString(Formatting.None);
        public static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }

        public static void ValidateIdentity(JObject root)
        {
            Require((string)root["schema"] == "org.pokemon-roguelite.hgss-world-structure", "Wrong IR schema.");
            Require((string)root["schemaVersion"] == SchemaVersion, "Only corrected IR 1.2.0 is accepted; 1.0.0 and 1.1.0 are superseded. No implicit migration is implemented.");
            Require((string)root["sourceRom"]["gameCode"] == "IPKE" && (int)root["sourceRom"]["revision"] == 0, "Wrong ROM identity.");
            Require((string)root["sourceRom"]["sha256"] == RomHash, "Wrong source ROM fingerprint.");
        }

        public static PilotIr Load(string path)
        {
            // This guard runs before ANY asset or scene mutation.
            byte[] bytes = File.ReadAllBytes(path);
            string hash = Sha(bytes);
            Require(hash == ApprovedHash, "IR SHA-256 does not match the authoritative 1.2.0 input.");
            var result = new PilotIr(JObject.Parse(Encoding.UTF8.GetString(bytes)), hash);
            ValidateIdentity(result.Root);
            var ids = result.Root.DescendantsAndSelf().OfType<JObject>().Where(o => o["stableId"] != null).ToArray();
            Require(ids.Select(o => (string)o["stableId"]).Distinct().Count() == ids.Length, "Duplicate source stable IDs.");
            Require(result.Lands.Count() == 6 && result.Neighborhood.Count() == 9, "Unexpected pilot scope.");
            Require(result.Lands.Sum(l => l["buildings"].Count()) == 18, "Expected 18 building placements.");
            var expected = new Dictionary<string, int> { {"objects",10}, {"warps",5}, {"coordinateTriggers",4}, {"backgrounds",5} };
            foreach (var pair in expected) Require(result.Events[pair.Key].Count() == pair.Value, "Unexpected " + pair.Key + " count.");
            foreach (var land in result.Lands)
            {
                PermissionBytes(land);
                foreach(var row in land["buildings"].Cast<JObject>()) ValidateBuildingRecord(row);
                var b = (JObject)land["bdhc"];
                foreach (var p in b["plates"].Cast<JObject>()) PilotGeometry.PlateVertices(b, p);
                foreach (var access in b["accessList"]) Require((int)access["plateIndexRaw"] < b["plates"].Count(), "BDHC access index out of range.");
                foreach (var strip in b["strips"]) Require((int)strip["accessListStartIndex"] + (int)strip["accessListElementCount"] <= b["accessList"].Count(), "BDHC strip range out of bounds.");
            }
            return result;
        }

        public static byte[] PermissionBytes(JObject land)
        {
            var rows = land["permissions"].Cast<JObject>().OrderBy(r => (int)r["index"]).ToArray();
            Require(rows.Length == 1024, "Incomplete permission block.");
            var result = new byte[2048];
            for (int i = 0; i < rows.Length; i++)
            {
                Require((int)rows[i]["index"] == i && (int)rows[i]["x"] == i % 32 && (int)rows[i]["z"] == i / 32 && i==(int)rows[i]["x"]+32*(int)rows[i]["z"], "Permission spatial position must match runtime x+32*z.");
                result[i * 2] = checked((byte)(int)rows[i]["typeRaw"]);
                result[i * 2 + 1] = checked((byte)(int)rows[i]["collisionRaw"]);
            }
            Require(Sha(result)==(string)land["permissionRawSha256"],"Permission bytes do not match the source section SHA-256.");
            return result;
        }

        public static void ValidateBuildingRecord(JObject row)
        {
            string hex=(string)row["rawHex"];Require(hex!=null&&hex.Length==96,"Expected complete 48-byte building record.");
            var raw=Enumerable.Range(0,48).Select(i=>Convert.ToByte(hex.Substring(i*2,2),16)).ToArray();
            Require(Sha(raw)==(string)row["rawSha256"],"Building record SHA-256 mismatch.");
            Require(BitConverter.ToUInt32(raw,0)==(uint)row["modelIdRaw"],"Model ID differs from raw record.");
            var names=new[]{"translationRawFx32","rotationRaw32","scaleRawFx32"};var offsets=new[]{4,16,28};
            for(int n=0;n<3;n++)
            {
                var values=row[names[n]] as JArray;Require(values!=null&&values.Count==3,"Missing aligned building vector.");
                for(int i=0;i<3;i++)Require((int)values[i]==BitConverter.ToInt32(raw,offsets[n]+4*i),"Building vector differs from aligned raw bytes: "+names[n]);
            }
            foreach(var pair in new[]{("translationRawHex",4,12),("rotationRawHex",16,12),("scaleRawHex",28,12),("unknownPaddingHex",40,8)})
                Require((string)row[pair.Item1]==hex.Substring(pair.Item2*2,pair.Item3*2),"Building byte slice mismatch: "+pair.Item1);
            for(int i=0;i<3;i++)
            {
                string axis=new[]{"x","y","z"}[i];int value=BitConverter.ToInt32(raw,4+4*i);
                Require((int)row["positionRaw"][axis+"Whole"]==(value>>16)&&(int)row["positionRaw"][axis+"Fraction"]==(value&65535),"Signed translation split mismatch.");
            }
        }
    }

    public static class PilotCoordinates
    {
        public static JObject Profile => new JObject {
            ["id"]="new-bark-schema-1.2-profile", ["axes"]="Unity +X east, +Z south, +Y up",
            ["unityUnitsPerMovementTile"]=1, ["sourceModelUnitsPerTile"]=16,
            ["globalTileOriginX"]=672, ["globalTileOriginZ"]=384, ["sourceMapCenteredOriginOffset"]=256,
            ["horizontalModelToUnityScale"]=0.0625, ["provisionalVerticalModelToUnityScale"]=0.0625,
            ["verticalOrigin"]=0, ["eventDiagnosticY"]=7,
            ["verified"]="matrix floor division by 32; global event tile X/Z; tile centers 16*n+8; raw fixed-point coefficients retained",
            ["provisional"]="Building model pivots and original visual geometry remain uncalibrated. Event marker display lift is artificial; no visual fidelity approval.",
            ["permissionSpatialOrder"]="Schema 1.2.0: index=x+32*z; x varies fastest. Every position and source-byte SHA-256 validated; stable IDs remain keyed by raw index.",
            ["buildingProxy"]="fixed 1.5 x 2 x 1.5 Unity cuboid; source rotation and scale preserved but not applied",
            ["buildingPositionConversion"]="signed int32 at record offsets 4/8/12 is fx32 /4096 model units, then /16 Unity scale; equivalent to signed high16 + low16/65536; add map center +16 to X/Z and proxy half-height +1 to Y",
            ["eventYPolicy"]="raw Y retained; all marker bases use diagnostic display plane Y=7, without gameplay placement claims",
            ["triggerProxyCenterOffset"]="visual rectangle center adds ((widthRaw-1)/2,0,(heightRaw-1)/2) to the raw origin tile center; raw origin and dimensions remain unchanged",
            ["matrixAltitudePolicy"]="Runtime ov01_021F5FB8 sets map MODEL Y=altitudeRaw*8 source units (altitudeRaw/2 Unity). sub_02054654 queries BDHC without altitude addition; outdoor MapProp render adds only map X/Z. Traversal surfaces therefore remain unshifted. Omitted visual model is not reconstructed.",
            ["buildingTransformValidation"]="Schema 1.2.0 validates signed translation at4, rotation vector at16, scale VecFx32 at28, unknown padding40..47 against complete48-byte record and SHA-256. Source anchors and diagnostic offsets are separate; pivots remain unresolved.",
            ["typePalette"]="HSV(rawByte/256,0.68,rawByte==0?0.32:0.95)",
            ["collisionPalette"]="RGB(0.12+rawByte/255*0.85,0.12+rawByte/255*0.85,0.16+rawByte/255*0.8)",
            ["bdhcEquation"]="Y=-(nx*X+nz*Z+d)/ny; coefficients and points decoded from fx32 /4096"
        };
        public static Vector3 CellOrigin(int x, int z) => new Vector3((x - 21) * 32, 0, (z - 12) * 32);
        public static Vector3 Event(JObject row) => new Vector3((int)row["xRaw"] - 672 + .5f, 7, (int)row["zRaw"] - 384 + .5f);
        public static Vector3 BuildingAnchor(JObject row)
        {
            var p=row["translationRawFx32"];
            return new Vector3(16+(int)p[0]/65536f,(int)p[1]/65536f,16+(int)p[2]/65536f);
        }
        public static Vector3 BuildingPlaceholderOffset => new Vector3(0,1,0);
        public static Vector3 Building(JObject row) => BuildingAnchor(row)+BuildingPlaceholderOffset;
    }
}
