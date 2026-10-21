using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using PokemonRoguelite.NewBarkPilot.Editor;

namespace PokemonRoguelite.NewBarkPilot.Tests
{
    public sealed class PilotCalibrationTests
    {
        [Test] public void RuntimePermissionViewIsSeparateFromApprovedSerialization()
        {
            Assert.AreEqual(227,PilotCalibration.RuntimePermissionIndex(3,7));
            Assert.AreNotEqual(3*32+7,PilotCalibration.RuntimePermissionIndex(3,7));
            foreach(var land in PilotImporter.LoadApproved().Lands)
            {
                var bytes=PilotIr.PermissionBytes(land);
                for(int x=0;x<32;x++)for(int z=0;z<32;z++)
                {
                    int runtime=PilotCalibration.RuntimePermissionIndex(x,z);
                    Assert.AreEqual((int)land["permissions"][runtime]["typeRaw"],bytes[2*runtime]);
                    Assert.AreEqual(x,(int)land["permissions"][runtime]["x"]);
                    Assert.AreEqual(z,(int)land["permissions"][runtime]["z"],"Schema 1.2.0 spatial labels must match the runtime.");
                }
            }
        }
        [Test] public void BuildingScaleUsesAlignedVecFx32AtByte28()
        {
            var ir=PilotImporter.LoadApproved();
            foreach(var row in ir.Lands.SelectMany(l=>l["buildings"].Cast<JObject>()))
            {
                var d=PilotCalibration.BuildingInterpretation(row);
                CollectionAssert.AreEqual(new[]{4096,4096,4096},d["scaleRawFx32"].Select(v=>(int)v));
                CollectionAssert.AreEqual(new[]{0,0,0},d["rotationRaw32"].Select(v=>(int)v));
            }
            var synthetic=(JObject)ir.Lands.First()["buildings"][0].DeepClone();
            var raw=Enumerable.Range(0,48).Select(i=>Convert.ToByte(((string)synthetic["rawHex"]).Substring(i*2,2),16)).ToArray();
            Array.Copy(BitConverter.GetBytes(8192),0,raw,28,4);
            synthetic["rawHex"]=BitConverter.ToString(raw).Replace("-","").ToLowerInvariant();
            Assert.AreEqual(2,(float)PilotCalibration.BuildingInterpretation(synthetic)["scaleRuntime"][0]);
        }
        [Test] public void MatrixRenderAltitudeDoesNotOffsetBdhcOrPropPositions()
        {
            Assert.AreEqual(1,PilotCalibration.ModelRenderAltitude(2));Assert.AreEqual(2,PilotCalibration.ModelRenderAltitude(4));
            var ir=PilotImporter.LoadApproved();
            foreach(var cell in ir.Neighborhood)Assert.AreEqual(0,PilotCoordinates.CellOrigin((int)cell["x"],(int)cell["z"]).y);
            var filler=ir.Lands.Single(l=>(int)l["landDataId"]==210);var b=(JObject)filler["bdhc"];
            foreach(var v in PilotGeometry.PlateVertices(b,(JObject)b["plates"][0]))Assert.AreEqual(0,v.y);
        }
        [Test] public void EveryOriginalBoundaryAnomalyIsEnumeratedAndHasBlockedSide()
        {
            var r=PilotCalibration.Analyze(PilotImporter.LoadApproved());var samples=r["samples"].ToArray();
            Assert.AreEqual(384,samples.Length);Assert.AreEqual(384,samples.Select(s=>(string)s["id"]).Distinct().Count());
            Assert.AreEqual(192,(int)r["counts"]["continuous"]);
            Assert.AreEqual(182,(int)r["counts"]["probable legitimate elevation discontinuity"]);
            Assert.AreEqual(10,(int)r["counts"]["unresolved source BDHC coverage gap"]);
            foreach(var s in samples.Where(s=>(string)s["classification"]!="continuous"))
                Assert.IsTrue((bool)s["fromPermission"]["blockedBit15"]||(bool)s["toPermission"]["blockedBit15"],(string)s["id"]);
        }
        [Test] public void MissingCoverageIsExactlyTenNorthernRoute27Samples()
        {
            var samples=PilotCalibration.Analyze(PilotImporter.LoadApproved())["samples"];
            var missing=samples.Where(s=>s["nearestDeltaUnity"].Type==JTokenType.Null).ToArray();
            CollectionAssert.AreEqual(Enumerable.Range(22,10),missing.Select(s=>(int)s["sample"]));
            foreach(var s in missing)
            {
                StringAssert.StartsWith("22,11:south:",(string)s["id"]);
                Assert.AreEqual(0,s["toSurfaces"].Count());
                Assert.IsTrue((bool)s["fromPermission"]["blockedBit15"]&&(bool)s["toPermission"]["blockedBit15"]);
            }
        }
    }
}
