using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using PokemonRoguelite.NewBarkPilot.Editor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PokemonRoguelite.NewBarkPilot.Tests
{
    public sealed class PilotImportTests
    {
        static void WithScene(Action<Scene,PilotIr> test)
        {
            var prior=SceneManager.GetActiveScene();
            var scene=EditorSceneManager.NewPreviewScene();
            try {test(scene,PilotImporter.LoadApproved());}
            finally {EditorSceneManager.ClosePreviewScene(scene);if(prior.IsValid()&&prior.isLoaded)SceneManager.SetActiveScene(prior);}
        }
        static PilotSourceRecord[] Records(Scene scene) => scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<PilotSourceRecord>(true)).ToArray();

        [Test] public void SupersededSchemaAndWrongRomAreRejected()
        {
            var root=(JObject)PilotImporter.LoadApproved().Root.DeepClone();root["schemaVersion"]="1.0.0";
            Assert.Throws<InvalidDataException>(()=>PilotIr.ValidateIdentity(root));
            root["schemaVersion"]="1.1.0";
            Assert.Throws<InvalidDataException>(()=>PilotIr.ValidateIdentity(root));
            root["schemaVersion"]="9.0.0";
            Assert.Throws<InvalidDataException>(()=>PilotIr.ValidateIdentity(root));
            root["schemaVersion"]="1.2.0";root["sourceRom"]["gameCode"]="IPGE";
            Assert.Throws<InvalidDataException>(()=>PilotIr.ValidateIdentity(root));
        }
        [Test] public void TamperedBytesAreRejectedBeforeSceneMutation()
        {
            int count=SceneManager.sceneCount;string path=Path.GetTempFileName();
            try {File.WriteAllText(path,File.ReadAllText(PilotImporter.InputPath)+" ");Assert.Throws<InvalidDataException>(()=>PilotIr.Load(path));Assert.AreEqual(count,SceneManager.sceneCount);}
            finally {File.Delete(path);}
        }
        [Test] public void SceneTemplateSnapshotRestoresExactBytesAndAbsence()
        {
            string path=Path.GetTempFileName();File.WriteAllText(path,"user settings");
            try
            {
                using(new PilotSettingsSnapshot(path))File.WriteAllText(path,"Unity generated defaults");
                Assert.AreEqual("user settings",File.ReadAllText(path));File.Delete(path);
                using(new PilotSettingsSnapshot(path))File.WriteAllText(path,"Unity generated defaults");
                Assert.IsFalse(File.Exists(path));
            }
            finally {if(File.Exists(path))File.Delete(path);}
        }
        [Test] public void CoordinateProfileMapsGlobalTilesAndRawBuildingFx32()
        {
            var e=new JObject { ["xRaw"]=682,["zRaw"]=391 };
            Assert.AreEqual(new Vector3(10.5f,7,7.5f),PilotCoordinates.Event(e));
            foreach(var row in PilotImporter.LoadApproved().Lands.SelectMany(l=>l["buildings"].Cast<JObject>()))
            {
                byte[] raw=Enumerable.Range(0,48).Select(i=>Convert.ToByte(((string)row["rawHex"]).Substring(i*2,2),16)).ToArray();
                var p=PilotCoordinates.Building(row);
                Assert.AreEqual(16+BitConverter.ToInt32(raw,4)/65536f,p.x,1e-5);
                Assert.AreEqual(1+BitConverter.ToInt32(raw,8)/65536f,p.y,1e-5);
                Assert.AreEqual(16+BitConverter.ToInt32(raw,12)/65536f,p.z,1e-5);
            }
        }
        [Test] public void InMemoryDocumentMutationIsRejectedBeforeGeneration()
        {
            WithScene((scene,ir)=> {
                ir.Root["schemaVersion"]="1.0.0";
                Assert.Throws<InvalidDataException>(()=>PilotImporter.Populate(scene,ir));
                Assert.AreEqual(0,scene.rootCount);
            });
        }
        [Test] public void EveryPlateVertexSatisfiesRawPlaneAndWinding()
        {
            foreach(var land in PilotImporter.LoadApproved().Lands)
            {
                var b=(JObject)land["bdhc"];
                foreach(var plate in b["plates"].Cast<JObject>())
                {
                    var vertices=PilotGeometry.PlateVertices(b,plate);var n=b["normals"][(int)plate["normalIndex"]]["rawFx32"];
                    double nx=(long)n[0]/4096.0,ny=(long)n[1]/4096.0,nz=(long)n[2]/4096.0,d=(long)b["constants"][(int)plate["constantIndex"]]["rawFx32"]/4096.0;
                    foreach(var v in vertices) Assert.Less(Math.Abs(nx*(v.x-16)*16+ny*v.y*16+nz*(v.z-16)*16+d),.001,"Plane residual exceeds 0.001 source units.");
                    Assert.Greater(Vector3.Cross(vertices[2]-vertices[0],vertices[1]-vertices[0]).y,0,"Upward winding required.");
                }
            }
        }
        [Test] public void SixPermissionBlocksRoundTripAllSourcePairs()
        {
            WithScene((scene,ir)=> {
                PilotImporter.Populate(scene,ir);
                var blocks=Records(scene).Where(r=>r.kind=="land-data").Select(r=>r.GetComponent<PilotPermissionBlock>()).ToArray();
                Assert.AreEqual(6,blocks.Length);
                foreach(var block in blocks)
                {
                    var land=ir.Lands.Single(l=>(int)l["landDataId"]==block.landDataId);
                    CollectionAssert.AreEqual(PilotIr.PermissionBytes(land),block.ToSourceBytes());
                    foreach(var p in land["permissions"]) Assert.AreEqual((int)p["x"]+32*(int)p["z"],(int)p["index"]);
                }
            });
        }
        [Test] public void AllBuildingsAndEventsRetainExactRawRecords()
        {
            WithScene((scene,ir)=> {
                PilotImporter.Populate(scene,ir);var records=Records(scene).ToDictionary(r=>r.stableId);
                var source=ir.Lands.SelectMany(l=>l["buildings"].Cast<JObject>()).Concat(new[]{"objects","warps","coordinateTriggers","backgrounds"}.SelectMany(k=>ir.Events[k].Cast<JObject>())).ToArray();
                Assert.AreEqual(42,source.Length);
                foreach(var row in source) Assert.AreEqual(PilotIr.Compact(row),records[(string)row["stableId"]].rawRecordJson);
                foreach(var land in ir.Lands) Assert.AreEqual(PilotIr.Compact(land),records[(string)land["stableId"]].rawRecordJson,"Complete raw land record retains unknown fields.");
            });
        }
        [Test] public void ReimportRetainsInstancesAndRemovesOnlyStaleGeneratedRecords()
        {
            WithScene((scene,ir)=> {
                PilotImporter.Populate(scene,ir);var first=Records(scene).ToDictionary(r=>r.stableId,r=>r.GetInstanceID());
                string hash=PilotImporter.ManifestHash(PilotImporter.Manifest(scene));
                var stale=new GameObject("stale-generated");SceneManager.MoveGameObjectToScene(stale,scene);stale.AddComponent<PilotSourceRecord>().stableId="pilot:stale";
                var unowned=new GameObject("user-owned-test-object");SceneManager.MoveGameObjectToScene(unowned,scene);
                PilotImporter.Populate(scene,ir);var after=Records(scene);
                Assert.AreEqual(144,after.Length);Assert.IsTrue(after.All(r=>first[r.stableId]==r.GetInstanceID()));
                Assert.IsTrue(unowned!=null);Assert.IsTrue(stale==null);Assert.AreEqual(hash,PilotImporter.ManifestHash(PilotImporter.Manifest(scene)));
            });
        }
        [Test] public void IndependentCleanScenesProduceIdenticalManifestsAndGeometry()
        {
            string first=null;
            WithScene((scene,ir)=> {PilotImporter.Populate(scene,ir);first=PilotImporter.ManifestHash(PilotImporter.Manifest(scene));});
            WithScene((scene,ir)=> {PilotImporter.Populate(scene,ir);Assert.AreEqual(first,PilotImporter.ManifestHash(PilotImporter.Manifest(scene)));});
        }
        [Test] public void EveryEventOriginSelectsNewBarkAndMarkersKeepRawY()
        {
            WithScene((scene,ir)=> {
                PilotImporter.Populate(scene,ir);var records=Records(scene).ToDictionary(r=>r.stableId);
                foreach(var kind in new[]{"objects","warps","coordinateTriggers","backgrounds"}) foreach(var row in ir.Events[kind].Cast<JObject>())
                {
                    Assert.AreEqual(21,(int)row["xRaw"]/32);Assert.AreEqual(12,(int)row["zRaw"]/32);
                    var marker=records[(string)row["stableId"]];Assert.AreEqual(7,marker.transform.localPosition.y);
                    Assert.AreEqual(row["yRaw"],JObject.Parse(marker.rawRecordJson)["yRaw"]);
                }
            });
        }
    }
}
