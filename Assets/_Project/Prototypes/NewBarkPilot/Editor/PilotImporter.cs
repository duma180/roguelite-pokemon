using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PokemonRoguelite.NewBarkPilot.Editor
{
    public static class PilotImporter
    {
        public const string Version="1.2.0-schema-pilot";
        public const string Folder="Assets/_Project/Prototypes/NewBarkPilot";
        public const string InputPath=Folder+"/Data/new_bark_town_v1_2.json";
        public const string ScenePath=Folder+"/Scenes/NewBark_StructuralPilot_UNREVIEWED.unity";
        public static string EvidencePath => Path.Combine(Directory.GetParent(Application.dataPath).FullName,"PilotEvidence/NewBark/Phase1B3");
        static readonly string RootId="pilot:IPKE:r0:new-bark";

        public static PilotIr LoadApproved()
        {
            var ir=PilotIr.Load(InputPath);
            PilotCalibration.LoadBoundaryReview(); // Verify manifest-referenced evidence before any generation.
            return ir;
        }
        static T Ensure<T>(GameObject go) where T:Component
        {
            var component=go.GetComponent<T>();
            return component!=null?component:go.AddComponent<T>();
        }
        static void FolderAt(string path)
        {
            if(AssetDatabase.IsValidFolder(path)) return;
            string parent=Path.GetDirectoryName(path).Replace('\\','/'); FolderAt(parent);
            AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
        }
        static Mesh SaveMesh(string name,Mesh generated)
        {
            generated.name=name;
            string path=Folder+"/Generated/"+name+".asset";
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing==null) {AssetDatabase.CreateAsset(generated,path);return generated;}
            EditorUtility.CopySerialized(generated,existing); UnityEngine.Object.DestroyImmediate(generated); EditorUtility.SetDirty(existing);return existing;
        }
        static Material Material(string name,Color color,bool vertex=false)
        {
            string path=Folder+"/Generated/"+name+".mat";
            var result=AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader=Shader.Find(vertex?"NewBarkPilot/DiagnosticVertexColor":"Universal Render Pipeline/Unlit");
            PilotIr.Require(shader!=null,"Required diagnostic shader is unavailable.");
            if(result==null) {result=new Material(shader);AssetDatabase.CreateAsset(result,path);}
            result.name=name;result.shader=shader;if(!vertex) result.SetColor("_BaseColor",color);EditorUtility.SetDirty(result);return result;
        }

        sealed class Reconciler
        {
            readonly Scene scene;
            readonly Dictionary<string,PilotSourceRecord> prior;
            readonly HashSet<string> used=new HashSet<string>();
            public Reconciler(Scene scene)
            {
                this.scene=scene;
                var records=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<PilotSourceRecord>(true)).ToArray();
                PilotIr.Require(records.Select(r=>r.stableId).Distinct().Count()==records.Length,"Duplicate generated stable IDs; reconciliation refused.");
                prior=records.ToDictionary(r=>r.stableId);
            }
            public GameObject Use(string id,string name,Transform parent,string kind,JToken raw,string sourceId,string sourceRef,PrimitiveType? primitive=null)
            {
                PilotIr.Require(used.Add(id),"Generation plan contains duplicate ID "+id);
                if(!prior.TryGetValue(id,out var record))
                {
                    var created=primitive.HasValue?GameObject.CreatePrimitive(primitive.Value):new GameObject();
                    SceneManager.MoveGameObjectToScene(created,scene);
                    if(created.TryGetComponent<Collider>(out var collider)) UnityEngine.Object.DestroyImmediate(collider);
                    record=created.AddComponent<PilotSourceRecord>();
                }
                var go=record.gameObject;go.name=name;go.SetActive(true);go.transform.SetParent(parent,false);
                go.transform.localPosition=Vector3.zero;go.transform.localRotation=Quaternion.identity;go.transform.localScale=Vector3.one;
                record.stableId=id;record.sourceStableId=sourceId;record.sourceReference=sourceRef;record.kind=kind;
                record.rawRecordJson=raw==null?"{}":PilotIr.Compact(raw);record.geometryHash="";
                record.interpretation="Unreviewed structural diagnostic. Raw fields are authoritative; visual conversion is provisional.";
                return go;
            }
            public void Finish()
            {
                foreach(var row in prior.Where(p=>!used.Contains(p.Key)).OrderByDescending(p=>p.Key.Length))
                    if(row.Value!=null) UnityEngine.Object.DestroyImmediate(row.Value.gameObject);
            }
        }

        static void SetMesh(GameObject go,Mesh mesh,Material material,bool collider=false)
        {
            var filter=Ensure<MeshFilter>(go);filter.sharedMesh=mesh;
            var renderer=Ensure<MeshRenderer>(go);renderer.sharedMaterial=material;
            if(collider) {var c=Ensure<MeshCollider>(go);c.sharedMesh=null;c.sharedMesh=mesh;}
            go.GetComponent<PilotSourceRecord>().geometryHash=PilotGeometry.Hash(mesh);
        }
        static void Tint(GameObject go,Material material) { go.GetComponent<MeshRenderer>().sharedMaterial=material; }

        // All scene generation is reached only after LoadApproved has verified SHA-256 and schema.
        public static void Populate(Scene scene,PilotIr ir)
        {
            ir.VerifyUnchanged();
            PilotCalibration.LoadBoundaryReview();
            FolderAt(Folder+"/Generated");
            var vertex=Material("DiagnosticVertexColor",Color.white,true);
            var buildingMat=Material("BuildingProxy",new Color(.65f,.43f,.24f));
            var materials=new Dictionary<string,Material> {
                {"objects",Material("NPCMarker",new Color(.25f,.8f,1))},
                {"warps",Material("WarpMarker",new Color(.9f,.28f,.8f))},
                {"coordinateTriggers",Material("TriggerMarker",new Color(1,.65f,.15f))},
                {"backgrounds",Material("BackgroundMarker",new Color(1,1,.3f))}
            };
            var r=new Reconciler(scene);
            var root=r.Use(RootId,"NEW BARK STRUCTURAL PILOT — UNREVIEWED",null,"pilot-root",PilotCoordinates.Profile,"heartgold-us-rev0-new-bark-town",InputPath);
            var session=Ensure<PilotImportSession>(root);
            session.sourceIr=AssetDatabase.LoadAssetAtPath<TextAsset>(InputPath);session.schemaVersion=PilotIr.SchemaVersion;session.sourceIrSha256=ir.Hash;
            session.sourceRomSha256=PilotIr.RomHash;session.importerVersion=Version;session.coordinateProfileJson=PilotIr.Compact(PilotCoordinates.Profile);
            var data=r.Use(RootId+":data","Source records — raw values",root.transform,"source-data",null,"dataset",InputPath);
            var terrain=r.Use(RootId+":terrain","BDHC traversal surfaces — provisional Y",root.transform,"terrain-group",null,"dataset",InputPath);
            var buildings=r.Use(RootId+":buildings","18 building proxies — pivots uncalibrated",root.transform,"building-group",null,"dataset",InputPath);
            var markers=r.Use(RootId+":events","24 event markers — diagnostic Y=7",root.transform,"event-group",null,"dataset",InputPath);
            var typePanel=r.Use(RootId+":type","TYPE BYTE — raw palette",root.transform,"type-panel",null,"dataset",InputPath);
            typePanel.transform.localPosition=new Vector3(112,0,0);
            var collisionPanel=r.Use(RootId+":collision","COLLISION BYTE — raw grayscale",root.transform,"collision-panel",null,"dataset",InputPath);
            collisionPanel.transform.localPosition=new Vector3(224,0,0);
            var lands=ir.Lands.ToDictionary(l=>(int)l["landDataId"]);
            var plateMeshes=new Dictionary<string,Mesh>();var typeMeshes=new Dictionary<int,Mesh>();var collisionMeshes=new Dictionary<int,Mesh>();
            foreach(var pair in lands.OrderBy(p=>p.Key))
            {
                int landId=pair.Key;var land=pair.Value;string sid=(string)land["stableId"];
                var go=r.Use(sid,"Land "+landId+" — complete raw source",data.transform,"land-data",land,sid,(string)land["sourceMember"]);
                var permission=Ensure<PilotPermissionBlock>(go);
                permission.landDataId=landId;permission.sourceReference=(string)land["sourceMember"]+":permissions";
                permission.typeBytes=land["permissions"].Select(p=>(int)p["typeRaw"]).ToArray();permission.collisionBytes=land["permissions"].Select(p=>(int)p["collisionRaw"]).ToArray();
                permission.sourcePermissionSha256=PilotIr.Sha(PilotIr.PermissionBytes(land));
                PilotIr.Require(permission.ToSourceBytes().SequenceEqual(PilotIr.PermissionBytes(land)),"Permission round-trip failed.");
                foreach(var plate in land["bdhc"]["plates"].Cast<JObject>())
                    plateMeshes[(string)plate["stableId"]]=SaveMesh("Land"+landId+"_Plate"+(int)plate["index"],PilotGeometry.Plate((JObject)land["bdhc"],plate));
                typeMeshes[landId]=SaveMesh("Land"+landId+"_TypeBytes",PilotGeometry.Permissions(land,false));
                collisionMeshes[landId]=SaveMesh("Land"+landId+"_CollisionBytes",PilotGeometry.Permissions(land,true));
            }
            var firstCellForLand=new HashSet<int>();
            foreach(var cell in ir.Neighborhood.OrderBy(c=>(int)c["linearIndex"]))
            {
                int x=(int)cell["x"],z=(int)cell["z"],landId=(int)cell["landDataIdRaw"];
                var land=lands[landId];string cid=(string)cell["stableId"];
                var cellObject=r.Use(cid,"Cell "+x+","+z+" → land "+landId,terrain.transform,"matrix-cell",cell,cid,(string)cell["sourceMember"]);
                cellObject.transform.localPosition=PilotCoordinates.CellOrigin(x,z);
                foreach(var plate in land["bdhc"]["plates"].Cast<JObject>())
                {
                    string sid=(string)plate["stableId"];
                    var raw=new JObject { ["plate"]=plate.DeepClone(),["firstPoint"]=land["bdhc"]["points"][(int)plate["firstPointIndex"]].DeepClone(),["secondPoint"]=land["bdhc"]["points"][(int)plate["secondPointIndex"]].DeepClone(),["normal"]=land["bdhc"]["normals"][(int)plate["normalIndex"]].DeepClone(),["constant"]=land["bdhc"]["constants"][(int)plate["constantIndex"]].DeepClone() };
                    var p=r.Use(cid+":plate:"+(int)plate["index"],"Plate "+plate["index"],cellObject.transform,"bdhc-plate",raw,sid,(string)plate["sourceMember"]);
                    SetMesh(p,plateMeshes[sid],vertex,true);
                }
                var type=r.Use(cid+":type","Type grid "+x+","+z,typePanel.transform,"type-grid",cell,(string)land["stableId"],(string)land["sourceMember"]);
                type.transform.localPosition=PilotCoordinates.CellOrigin(x,z);SetMesh(type,typeMeshes[landId],vertex);
                var collision=r.Use(cid+":collision","Collision grid "+x+","+z,collisionPanel.transform,"collision-grid",cell,(string)land["stableId"],(string)land["sourceMember"]);
                collision.transform.localPosition=PilotCoordinates.CellOrigin(x,z);SetMesh(collision,collisionMeshes[landId],vertex);
                if(firstCellForLand.Add(landId)) foreach(var row in land["buildings"].Cast<JObject>())
                {
                    var b=r.Use((string)row["stableId"],"Building "+landId+":"+row["index"]+" model "+row["modelIdRaw"],buildings.transform,"building",row,(string)row["stableId"],(string)row["sourceMember"],PrimitiveType.Cube);
                    b.transform.localPosition=PilotCoordinates.CellOrigin(x,z)+PilotCoordinates.Building(row);b.transform.localScale=new Vector3(1.5f,2,1.5f);Tint(b,buildingMat);
                    b.GetComponent<PilotSourceRecord>().interpretation=PilotIr.Compact(PilotCalibration.BuildingInterpretation(row));
                }
            }
            foreach(var kind in new[]{"objects","warps","coordinateTriggers","backgrounds"}) foreach(var row in ir.Events[kind].Cast<JObject>())
            {
                var primitive=kind=="objects"?PrimitiveType.Capsule:kind=="warps"?PrimitiveType.Cylinder:PrimitiveType.Cube;
                var e=r.Use((string)row["stableId"],kind+" ["+row["index"]+"]",markers.transform,kind,row,(string)row["stableId"],(string)row["sourceMember"],primitive);
                e.transform.localPosition=PilotCoordinates.Event(row);e.transform.localScale=kind=="coordinateTriggers"?new Vector3(Math.Max(1,(int)row["widthRaw"]),.15f,Math.Max(1,(int)row["heightRaw"])):new Vector3(.65f,.65f,.65f);
                // Trigger dimensions extend from their origin tile; offset proxy to the rectangular area's center.
                if(kind=="coordinateTriggers") e.transform.localPosition+=new Vector3(((int)row["widthRaw"]-1)*.5f,0,((int)row["heightRaw"]-1)*.5f);
                Tint(e,materials[kind]);
            }
            AddLabel(r,root.transform,"terrain-title","BDHC SURFACES / BUILDINGS / EVENTS\nUNREVIEWED — vertical scale and pivots provisional",new Vector3(-30,10,-42),2.5f);
            string typeValues=string.Join(" ",ir.Lands.SelectMany(l=>l["permissions"]).Select(p=>(int)p["typeRaw"]).Distinct().OrderBy(v=>v).Select(v=>v.ToString("X2")));
            string collisionValues=string.Join(" ",ir.Lands.SelectMany(l=>l["permissions"]).Select(p=>(int)p["collisionRaw"]).Distinct().OrderBy(v=>v).Select(v=>v.ToString("X2")));
            AddLabel(r,root.transform,"type-title","TYPE BYTE / RUNTIME ORDER\nSchema 1.2.0: index = x + 32*z\nObserved hex: "+typeValues,new Vector3(82,2,-42),2.5f);
            AddLabel(r,root.transform,"collision-title","COLLISION BYTE / RUNTIME ORDER\nSchema 1.2.0: index = x + 32*z\nObserved hex: "+collisionValues,new Vector3(194,2,-42),2.5f);
            AddLabel(r,root.transform,"legend","CYAN NPC  |  MAGENTA WARP  |  ORANGE TRIGGER  |  YELLOW BACKGROUND\n1 Unity unit = 1 movement tile; +X east / +Z south",new Vector3(-30,10,69),1.4f);
            var cameraObject=r.Use(RootId+":camera","Pilot overview camera",root.transform,"camera",new JObject { ["orthographicSize"]=105 },"pilot-scaffold",ScenePath);
            var camera=Ensure<Camera>(cameraObject);camera.orthographic=true;camera.orthographicSize=105;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.035f,.05f,.075f);camera.nearClipPlane=.3f;camera.farClipPlane=1000;
            cameraObject.transform.position=new Vector3(128,230,-95);cameraObject.transform.LookAt(new Vector3(128,0,15));
            r.Finish();EditorSceneManager.MarkSceneDirty(scene);
            // Never flush unrelated dirty assets (e.g. Unity's in-memory URP migrations).
            foreach(string guid in AssetDatabase.FindAssets("",new[]{Folder+"/Generated"}))
            {
                var asset=AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                if(asset!=null) AssetDatabase.SaveAssetIfDirty(asset);
            }
        }

        static void AddLabel(Reconciler r,Transform parent,string id,string text,Vector3 position,float size)
        {
            var go=r.Use(RootId+":label:"+id,text.Split('\n')[0],parent,"label",new JObject { ["text"]=text },"pilot-scaffold",ScenePath);
            go.transform.localPosition=position;go.transform.localRotation=Quaternion.Euler(90,0,0);
            var label=Ensure<TextMesh>(go);label.text=text;label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.fontSize=64;label.characterSize=size*.25f;label.color=Color.white;label.anchor=TextAnchor.UpperLeft;
            go.GetComponent<MeshRenderer>().sharedMaterial=label.font.material;
        }

        static JArray Vector(Vector3 v) => new JArray(v.x,v.y,v.z);
        public static JObject Manifest(Scene scene)
        {
            var rows=new JArray();
            if(scene.IsValid()&&scene.isLoaded) foreach(var record in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<PilotSourceRecord>(true)).OrderBy(r=>r.stableId,StringComparer.Ordinal))
            {
                var t=record.transform;var permission=record.GetComponent<PilotPermissionBlock>();var filter=record.GetComponent<MeshFilter>();
                var parentRecord=t.parent!=null?t.parent.GetComponent<PilotSourceRecord>():null;
                var row=new JObject { ["stableId"]=record.stableId,["sourceStableId"]=record.sourceStableId,["sourceReference"]=record.sourceReference,["kind"]=record.kind,["rawRecordSha256"]=PilotIr.Sha(record.rawRecordJson),["parentId"]=parentRecord!=null?parentRecord.stableId:null,["position"]=Vector(t.localPosition),["rotation"]=new JArray(t.localRotation.x,t.localRotation.y,t.localRotation.z,t.localRotation.w),["scale"]=Vector(t.localScale),["geometrySha256"]=filter!=null&&filter.sharedMesh!=null?PilotGeometry.Hash(filter.sharedMesh):"",["permissionBytesSha256"]=permission!=null?PilotIr.Sha(permission.ToSourceBytes()):"" };
                var renderer=record.GetComponent<MeshRenderer>();
                row["interpretationSha256"]=PilotIr.Sha(record.interpretation);
                if(renderer!=null&&renderer.sharedMaterial!=null)
                {
                    var material=renderer.sharedMaterial;
                    row["material"]=new JObject { ["path"]=AssetDatabase.GetAssetPath(material),["shader"]=material.shader.name };
                    if(material.HasProperty("_BaseColor")) {var c=material.GetColor("_BaseColor");row["material"]["baseColor"]=new JArray(c.r,c.g,c.b,c.a);}
                }
                var camera=record.GetComponent<Camera>();
                if(camera!=null) row["camera"]=new JObject { ["orthographic"]=camera.orthographic,["orthographicSize"]=camera.orthographicSize,["near"]=camera.nearClipPlane,["far"]=camera.farClipPlane };
                var label=record.GetComponent<TextMesh>();
                if(label!=null) row["text"]=new JObject { ["value"]=label.text,["characterSize"]=label.characterSize,["fontSize"]=label.fontSize };
                rows.Add(row);
            }
            return new JObject { ["importerVersion"]=Version,["irSchemaVersion"]=PilotIr.SchemaVersion,["irSha256"]=PilotIr.ApprovedHash,["sourceRomSha256"]=PilotIr.RomHash,["boundaryReviewRegisterSha256"]=PilotCalibration.BoundaryReviewHash,["coordinateProfile"]=PilotCoordinates.Profile,["acceptedBaseline"]=false,["entities"]=rows };
        }
        public static string ManifestHash(JObject manifest) => PilotIr.Sha(PilotIr.Compact(manifest));
        static void WriteManifest(string name,JObject manifest)
        {
            Directory.CreateDirectory(EvidencePath);
            File.WriteAllText(Path.Combine(EvidencePath,name+".json"),new JObject { ["manifestSha256"]=ManifestHash(manifest),["manifest"]=manifest }.ToString(Formatting.Indented));
        }

        [MenuItem("Tools/New Bark Pilot/Import approved structural IR (UNREVIEWED)")]
        public static void ImportMenu() => Import("manual");
        public static string Import(string evidencePrefix)
        {
            var ir=LoadApproved(); // No scene/asset writes before this verifies SHA-256.
            using(var settings=new PilotSettingsSnapshot(Path.Combine(Directory.GetParent(Application.dataPath).FullName,"ProjectSettings/SceneTemplateSettings.json")))
            {
            var scene=SceneManager.GetSceneByPath(ScenePath);
            if(!scene.IsValid() && File.Exists(ScenePath)) scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
            WriteManifest(evidencePrefix+"-before",Manifest(scene));
            if(!scene.IsValid()) { FolderAt(Folder+"/Scenes");scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive); }
            Populate(scene,ir);SceneManager.SetActiveScene(scene);
            var after=Manifest(scene);string hash=ManifestHash(after);
            scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<PilotImportSession>(true)).Single().manifestSha256=hash;
            EditorSceneManager.SaveScene(scene,ScenePath);WriteManifest(evidencePrefix+"-after",after);
            Debug.Log("New Bark unreviewed pilot imported: "+hash);return hash;
            }
        }

        public static JObject RunReviewEvidence()
        {
            string first=Import("first-import");var scene=SceneManager.GetSceneByPath(ScenePath);
            var ids=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<PilotSourceRecord>(true)).ToDictionary(r=>r.stableId,r=>r.GetInstanceID());
            string second=Import("reimport");var records=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<PilotSourceRecord>(true)).ToArray();
            var result=new JObject { ["firstManifestSha256"]=first,["reimportManifestSha256"]=second,["idempotentManifest"]=first==second,["existingInstancesRetained"]=records.All(r=>ids.TryGetValue(r.stableId,out int id)&&id==r.GetInstanceID()),["uniqueGeneratedStableIds"]=records.Select(r=>r.stableId).Distinct().Count()==records.Length,["generatedEntityCount"]=records.Length,["counts"]=JObject.FromObject(records.GroupBy(r=>r.kind).ToDictionary(g=>g.Key,g=>g.Count())),["permissionRoundTrip"]=records.Where(r=>r.kind=="land-data").All(r=>r.GetComponent<PilotPermissionBlock>().sourcePermissionSha256==PilotIr.Sha(r.GetComponent<PilotPermissionBlock>().ToSourceBytes())),["calibrationStatus"]="PENDING REVIEW: vertical scale, building pivots/transforms, legal visual reference and boundary topology",["acceptedBaseline"]=false };
            File.WriteAllText(Path.Combine(EvidencePath,"scene-reimport-results.json"),result.ToString(Formatting.Indented));return result;
        }
    }
}
