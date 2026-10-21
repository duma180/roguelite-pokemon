using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using PokemonRoguelite.NewBarkPilot.Editor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PokemonRoguelite.NewBarkPilot.Tests
{
    public sealed class PilotSchemaTests
    {
        [Test] public void AlteredBoundaryRegisterCannotBeLoaded()
        {
            string path=Path.GetTempFileName();int scenes=SceneManager.sceneCount;
            try
            {
                File.WriteAllText(path,File.ReadAllText(PilotImporter.Folder+"/Data/boundary-review-register.json")+" ");
                Assert.Throws<InvalidDataException>(()=>PilotCalibration.LoadBoundaryReview(path));Assert.AreEqual(scenes,SceneManager.sceneCount);
            }
            finally{File.Delete(path);}
        }
        [Test] public void SupersededInputFileIsRejectedBeforeSceneMutation()
        {
            int scenes=SceneManager.sceneCount;
            Assert.Throws<InvalidDataException>(()=>PilotIr.Load(PilotImporter.Folder+"/Data/new_bark_town_v1_1.json"));
            Assert.AreEqual(scenes,SceneManager.sceneCount);
        }
        [Test] public void EveryPermissionQuadMatchesRuntimePositionAndBytes()
        {
            foreach(var land in PilotImporter.LoadApproved().Lands)foreach(bool collision in new[]{false,true})
            {
                var mesh=PilotGeometry.Permissions(land,collision);
                try
                {
                    var vertices=mesh.vertices;var colors=mesh.colors;Assert.AreEqual(4096,vertices.Length);
                    for(int i=0;i<1024;i++)
                    {
                        int x=i%32,z=i/32;Assert.AreEqual(new Vector3(x,0,z),vertices[i*4]);Assert.AreEqual(new Vector3(x+1,0,z+1),vertices[i*4+2]);
                        int value=(int)land["permissions"][x+32*z][collision?"collisionRaw":"typeRaw"];
                        var expected=collision?new Color(.12f+value/255f*.85f,.12f+value/255f*.85f,.16f+value/255f*.8f):Color.HSVToRGB(value/256f,.68f,value==0?.32f:.95f);
                        Assert.AreEqual(expected,colors[i*4]);
                    }
                }
                finally{UnityEngine.Object.DestroyImmediate(mesh);}
            }
        }
        [Test] public void PermissionSpatialAndSourceHashTamperingAreRejected()
        {
            var l=(JObject)PilotImporter.LoadApproved().Lands.First().DeepClone();l["permissions"][1]["x"]=0;l["permissions"][1]["z"]=1;
            Assert.Throws<InvalidDataException>(()=>PilotIr.PermissionBytes(l));
            l["permissions"][1]["x"]=1;l["permissions"][1]["z"]=0;l["permissionRawSha256"]=new string('0',64);
            Assert.Throws<InvalidDataException>(()=>PilotIr.PermissionBytes(l));
        }
        [Test] public void SignedNonDefaultBuildingVectorsAndUnknownBytesRoundTrip()
        {
            var row=(JObject)PilotImporter.LoadApproved().Lands.First()["buildings"][0].DeepClone();byte[] raw;
            using(var stream=new MemoryStream())using(var w=new BinaryWriter(stream))
            {w.Write(123u);foreach(int n in new[]{-32768,98304,int.MinValue,0x4000,-0x4000,0x14000,8192,2048,-4096})w.Write(n);w.Write(new byte[]{1,35,69,103,137,171,205,239});raw=stream.ToArray();}
            row["modelIdRaw"]=123;row["rawHex"]=BitConverter.ToString(raw).Replace("-","").ToLowerInvariant();row["rawSha256"]=PilotIr.Sha(raw);
            row["translationRawFx32"]=new JArray(-32768,98304,int.MinValue);row["rotationRaw32"]=new JArray(0x4000,-0x4000,0x14000);row["scaleRawFx32"]=new JArray(8192,2048,-4096);
            row["translationRawHex"]=((string)row["rawHex"]).Substring(8,24);row["rotationRawHex"]=((string)row["rawHex"]).Substring(32,24);row["scaleRawHex"]=((string)row["rawHex"]).Substring(56,24);row["unknownPaddingHex"]="0123456789abcdef";
            row["positionRaw"]=new JObject { ["xWhole"]=-1,["xFraction"]=32768,["yWhole"]=1,["yFraction"]=32768,["zWhole"]=-32768,["zFraction"]=0 };
            PilotIr.ValidateBuildingRecord(row);var derived=PilotCalibration.BuildingInterpretation(row);
            CollectionAssert.AreEqual(new[]{16384,-16384,81920},derived["rotationRaw32"].Select(v=>(int)v));
            CollectionAssert.AreEqual(new[]{2f,.5f,-1f},derived["scaleRuntime"].Select(v=>(float)v));
            Assert.AreEqual(new Vector3(15.5f,1.5f,-32752),PilotCoordinates.BuildingAnchor(row));
            Assert.AreEqual(PilotCoordinates.BuildingAnchor(row)+new Vector3(0,1,0),PilotCoordinates.Building(row));
            Assert.AreEqual("0123456789abcdef",(string)derived["padding40Through47Hex"]);
        }
        [Test] public void MisalignedBuildingScaleAndCorruptRecordHashAreRejected()
        {
            var row=(JObject)PilotImporter.LoadApproved().Lands.First()["buildings"][0].DeepClone();string raw=(string)row["rawHex"];
            row["scaleRawHex"]=raw.Substring(58,24);Assert.Throws<InvalidDataException>(()=>PilotIr.ValidateBuildingRecord(row));
            row["scaleRawHex"]=raw.Substring(56,24);row["scaleRawFx32"][0]=8192;Assert.Throws<InvalidDataException>(()=>PilotIr.ValidateBuildingRecord(row));
            row["scaleRawFx32"][0]=4096;row["rawSha256"]=new string('0',64);Assert.Throws<InvalidDataException>(()=>PilotIr.ValidateBuildingRecord(row));
        }
        [Test] public void BlockedStateAloneNeverExemptsAnUnregisteredOrChangedStep()
        {
            var report=PilotCalibration.Analyze(PilotImporter.LoadApproved());var review=PilotCalibration.LoadBoundaryReview();
            var step=(JObject)report["samples"].First(s=>(string)s["validation"]["category"]=="documented-blocked-elevation-discontinuity").DeepClone();
            Assert.IsTrue((bool)PilotCalibration.BoundaryDecision(step,review)["explicitDocumentedExemption"]);
            var changed=(JObject)step.DeepClone();changed["nearestDeltaUnity"]=7;
            Assert.AreEqual("unresolved-boundary",(string)PilotCalibration.BoundaryDecision(changed,review)["category"]);
            step["id"]="new-unreviewed-step";Assert.IsFalse((bool)PilotCalibration.BoundaryDecision(step,review)["explicitDocumentedExemption"]);
            step["fromPermission"]["blockedBit15"]=false;step["toPermission"]["blockedBit15"]=false;
            StringAssert.StartsWith("FAIL",(string)PilotCalibration.BoundaryDecision(step,review)["status"]);
            var gap=(JObject)report["samples"].First(s=>(string)s["validation"]["category"]=="source-bdhc-coverage-gap").DeepClone();
            gap["fromPermission"]["blockedBit15"]=false;gap["toPermission"]["blockedBit15"]=false;
            Assert.IsFalse((bool)PilotCalibration.BoundaryDecision(gap,review)["explicitDocumentedExemption"]);
        }
        [Test] public void TraversableContinuityAndAllExplicitBoundaryCategoriesAreValidated()
        {
            var r=PilotCalibration.Analyze(PilotImporter.LoadApproved());var counts=r["categoryCounts"];
            Assert.AreEqual(12,(int)counts["traversable-boundary"]);Assert.AreEqual(180,(int)counts["blocked-continuous-boundary"]);
            Assert.AreEqual(182,(int)counts["documented-blocked-elevation-discontinuity"]);Assert.AreEqual(10,(int)counts["source-bdhc-coverage-gap"]);
            foreach(var s in r["samples"])
            {
                var v=s["validation"];Assert.IsFalse((bool)v["faithfulVisualTopologyValidated"]);
                if((bool)v["traversableContinuityRequired"])Assert.AreEqual("pass-static-continuity",(string)v["status"]);
                if((bool)v["explicitDocumentedExemption"]){Assert.IsTrue((bool)v["reviewEvidenceMatches"]);Assert.IsNotEmpty((string)v["reason"]);}
            }
        }
    }
}
