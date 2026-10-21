using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PokemonRoguelite.NewBarkPilot.Editor
{
    public static class PilotReview
    {
        public static string Capture(string name,Vector3 target,float size)
        {
            var scene=SceneManager.GetSceneByPath(PilotImporter.ScenePath);
            var camera=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>()).Single();
            var oldPosition=camera.transform.position;var oldRotation=camera.transform.rotation;float oldSize=camera.orthographicSize,oldAspect=camera.aspect;
            var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;
            var rt=new RenderTexture(1920,1080,24);var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);
            try
            {
                camera.transform.position=target+new Vector3(0,150,-95);camera.transform.LookAt(target);camera.orthographicSize=size;camera.aspect=1920f/1080;
                camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();
                Directory.CreateDirectory(PilotImporter.EvidencePath);string path=Path.Combine(PilotImporter.EvidencePath,name+".png");File.WriteAllBytes(path,image.EncodeToPNG());return path;
            }
            finally
            {
                camera.targetTexture=oldTarget;RenderTexture.active=oldActive;camera.transform.SetPositionAndRotation(oldPosition,oldRotation);camera.orthographicSize=oldSize;camera.aspect=oldAspect;
                UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);
            }
        }

        static List<float> EdgeHeights(JObject land,float x,float z)
        {
            var ys=new List<float>();var b=(JObject)land["bdhc"];
            foreach(var plate in b["plates"].Cast<JObject>())
            {
                var v=PilotGeometry.PlateVertices(b,plate);
                if(x>=v[0].x-1e-5&&x<=v[2].x+1e-5&&z>=v[0].z-1e-5&&z<=v[2].z+1e-5)
                    ys.Add(v[0].y+(v[1].y-v[0].y)*(x-v[0].x)/(v[1].x-v[0].x)+(v[3].y-v[0].y)*(z-v[0].z)/(v[3].z-v[0].z));
            }
            return ys;
        }
        public static JObject BoundaryDiagnostics()
        {
            var ir=PilotImporter.LoadApproved();var lands=ir.Lands.ToDictionary(l=>(int)l["landDataId"]);var cells=ir.Neighborhood.ToArray();var edges=new JArray();
            foreach(var cell in cells) foreach(bool east in new[]{true,false})
            {
                var other=cells.FirstOrDefault(c=>(int)c["x"]==(int)cell["x"]+(east?1:0)&&(int)c["z"]==(int)cell["z"]+(east?0:1));if(other==null)continue;
                int missing=0,steps=0;float maxDelta=0;
                for(int i=0;i<32;i++)
                {
                    var a=EdgeHeights(lands[(int)cell["landDataIdRaw"]],east?32:i+.5f,east?i+.5f:32);
                    var b=EdgeHeights(lands[(int)other["landDataIdRaw"]],east?0:i+.5f,east?i+.5f:0);
                    if(a.Count==0||b.Count==0){missing++;continue;}float delta=a.SelectMany(y=>b.Select(q=>Math.Abs(y-q))).Min();maxDelta=Math.Max(maxDelta,delta);if(delta>1e-4)steps++;
                }
                edges.Add(new JObject { ["from"]=(string)cell["stableId"],["to"]=(string)other["stableId"],["missingSurfaceSamples"]=missing,["differentHeightSamples"]=steps,["maxNearestHeightDeltaUnity"]=maxDelta });
            }
            var report=new JObject { ["samplesPerEdge"]=32,["toleranceUnity"]=0.0001,["edges"]=edges,["interpretation"]="Geometric aggregate retained for comparison. Matrix altitude belongs to omitted map-model rendering, not BDHC traversal heights. Use calibration-analysis.json for the exhaustive version2 boundary policy, static traversal candidates, explicit exception evidence and unresolved visual cases. No filler geometry or visual-fidelity claim." };
            File.WriteAllText(Path.Combine(PilotImporter.EvidencePath,"boundary-diagnostics.json"),report.ToString());return report;
        }
    }
}
