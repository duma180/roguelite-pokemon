using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace PokemonRoguelite.NewBarkPilot.Editor
{
    // Experimental source-camera diagnostics only. Never applies a camera to the saved pilot.
    public static class PilotSourceCamera
    {
        public const string ProfileHash="0b9cacf5af382b77f9bd0b3eca8d7d4a70418f49e86aaed36faadb2177e268f7";
        public static string ResearchPath=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../../Phase1A-Research/phase1b3"));
        public static string EvidencePath=>Path.Combine(Directory.GetParent(Application.dataPath).FullName,"PilotEvidence/NewBark/Phase1B3");
        public static JObject Load()
        {
            var bytes=File.ReadAllBytes(Path.Combine(ResearchPath,"source-camera-profile.json"));
            PilotIr.Require(PilotIr.Sha(bytes)==ProfileHash,"Unapproved source-camera profile bytes.");
            var p=JObject.Parse(System.Text.Encoding.UTF8.GetString(bytes));
            PilotIr.Require((string)p["sourceIrSha256"]==PilotIr.ApprovedHash && (string)p["sourceRomSha256"]==PilotIr.RomHash,"Camera source identity mismatch.");
            return p;
        }
        public static Vector3 Offset(JObject p)=>new Vector3((float)p["cameraOffsetUnity"][0],(float)p["cameraOffsetUnity"][1],(float)p["cameraOffsetUnity"][2]);
        public static Matrix4x4 View(JObject p,Vector3 target)
        {
            var eye=target+Offset(p);var k=Offset(p).normalized;var right=Vector3.Cross(Vector3.up,k).normalized;var up=Vector3.Cross(k,right);
            var m=Matrix4x4.identity;
            m.SetRow(0,new Vector4(right.x,right.y,right.z,-Vector3.Dot(right,eye)));
            m.SetRow(1,new Vector4(up.x,up.y,up.z,-Vector3.Dot(up,eye)));
            m.SetRow(2,new Vector4(k.x,k.y,k.z,-Vector3.Dot(k,eye)));return m;
        }
        public static Matrix4x4 Projection(JObject p)
        {
            float f=(float)p["sdkTrigSelectedRaw"]["halfFovCos"]/(float)p["sdkTrigSelectedRaw"]["halfFovSin"];
            float a=(int)p["aspectRawFx32"]/4096f,n=(float)p["nearUnity"],z=(float)p["farUnity"];
            var m=new Matrix4x4();m[0,0]=f/a;m[1,1]=f;m[2,2]=-(z+n)/(z-n);m[2,3]=-2*z*n/(z-n);m[3,2]=-1;return m;
        }
        public static Vector2 Pixel(JObject p,Vector3 target,Vector3 point)
        {
            var q=Projection(p)*View(p,target)*new Vector4(point.x,point.y,point.z,1);
            return new Vector2(128*(q.x/q.w+1),96*(1-q.y/q.w));
        }
        static JArray Vec(Vector3 v)=>new JArray(v.x,v.y,v.z);
        public static JObject Capture(string name,Vector3 target)
        {
            var p=Load();var ir=PilotImporter.LoadApproved();var active=SceneManager.GetActiveScene();var before=PilotImporter.ManifestHash(PilotImporter.Manifest(active));
            var scene=EditorSceneManager.NewPreviewScene();var prior=RenderTexture.active;
            var rt=new RenderTexture(256,192,24);var image=new Texture2D(256,192,TextureFormat.RGB24,false);
            var disposable=new List<UnityEngine.Object>();var points=new JArray();
            try
            {
                Func<Color,Material> mat=color=>{var m=new Material(Shader.Find("Universal Render Pipeline/Unlit"));m.SetColor("_BaseColor",color);m.SetFloat("_Cull",0);disposable.Add(m);return m;};
                var surface=mat(new Color(.16f,.42f,.32f));var proxy=mat(new Color(.6f,.37f,.18f));var anchor=mat(Color.red);var eventMat=mat(Color.magenta);
                Action<string,Vector3,Vector3,Material> box=(label,pos,size,material)=>{var g=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(g,scene);g.name=label;g.transform.position=pos;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=material;};
                var lands=ir.Lands.ToDictionary(l=>(int)l["landDataId"]);
                foreach(var cell in ir.Neighborhood)
                {
                    var land=lands[(int)cell["landDataIdRaw"]];var origin=PilotCoordinates.CellOrigin((int)cell["x"],(int)cell["z"]);var b=(JObject)land["bdhc"];
                    foreach(var plate in b["plates"].Cast<JObject>())
                    {
                        var g=new GameObject((string)plate["stableId"]);SceneManager.MoveGameObjectToScene(g,scene);g.transform.position=origin;
                        var mesh=PilotGeometry.Plate(b,plate);disposable.Add(mesh);g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=surface;
                    }
                    foreach(var row in land["buildings"].Cast<JObject>())
                    {
                        var pos=origin+PilotCoordinates.BuildingAnchor(row);box("Arbitrary proxy, not footprint",pos+Vector3.up,new Vector3(1.5f,2,1.5f),proxy);
                        box((string)row["stableId"],pos,new Vector3(.16f,.16f,.16f),anchor);
                    }
                }
                foreach(var row in ir.Events["warps"].Cast<JObject>())
                {
                    var pos=PilotCoordinates.Event(row);pos.y=1; // Explicit hypothetical visible floor height, separate from raw event Y and saved diagnostic Y=7.
                    box((string)row["stableId"],pos,new Vector3(.25f,.08f,.25f),eventMat);var pixel=Pixel(p,target,pos);
                    points.Add(new JObject {["sourceId"]=(string)row["stableId"],["pointUnity"]=Vec(pos),["pixelTopLeft"]=new JArray(pixel.x,pixel.y),["heightStatus"]="Hypothetical visible floor Y=1; not inferred from raw event Y=0"});
                }
                var cg=new GameObject("ROM source-camera candidate; disposable");SceneManager.MoveGameObjectToScene(cg,scene);var c=cg.AddComponent<Camera>();c.scene=scene;
                c.transform.position=target+Offset(p);c.transform.LookAt(target);c.nearClipPlane=(float)p["nearUnity"];c.farClipPlane=(float)p["farUnity"];
                c.aspect=(int)p["aspectRawFx32"]/4096f;c.fieldOfView=(float)p["fullVerticalFovDegreesDerived"];c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.025f,.035f,.05f);c.targetTexture=rt;
                c.worldToCameraMatrix=View(p,target);c.projectionMatrix=Projection(p);c.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,256,192),0,0);image.Apply();
                Directory.CreateDirectory(EvidencePath);string path=Path.Combine(EvidencePath,name+".png");File.WriteAllBytes(path,image.EncodeToPNG());
                var result=new JObject {["image"]=path,["imageSha256"]=PilotIr.Sha(File.ReadAllBytes(path)),["cameraProfileSha256"]=ProfileHash,["targetUnity"]=Vec(target),["projectionPoints"]=points,["originalManifestSha256"]=before,["limitations"]="Independent BDHC surfaces and arbitrary cuboids only. Cull disabled in disposable materials for explicit source right-handed view. No original graphics or footprint inference.",["accepted"]=false};
                File.WriteAllText(Path.Combine(EvidencePath,name+".json"),result.ToString());return result;
            }
            finally
            {
                RenderTexture.active=prior;EditorSceneManager.ClosePreviewScene(scene);
                foreach(var o in disposable)UnityEngine.Object.DestroyImmediate(o);UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);
                PilotIr.Require(before==PilotImporter.ManifestHash(PilotImporter.Manifest(active)),"Preview changed the saved pilot manifest.");
            }
        }
    }
}
