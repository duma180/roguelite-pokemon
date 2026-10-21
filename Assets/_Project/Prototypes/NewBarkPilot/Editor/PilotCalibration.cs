using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace PokemonRoguelite.NewBarkPilot.Editor
{
    // Derived research views only. Never rewrites the reviewed IR or moves source geometry.
    public static class PilotCalibration
    {
        public const string BoundaryReviewHash="ce56d2023fcd890ecee68ebe109ee45b3c2221c56b700d437cc965450d0796d9";
        public static JObject LoadBoundaryReview(string path=null)
        {
            var bytes=File.ReadAllBytes(path??(PilotImporter.Folder+"/Data/boundary-review-register.json"));
            PilotIr.Require(PilotIr.Sha(bytes)==BoundaryReviewHash,"Boundary review register hash mismatch.");
            return JObject.Parse(System.Text.Encoding.UTF8.GetString(bytes));
        }
        public static JObject BoundaryDecision(JObject sample,JObject register)
        {
            bool blocked=(bool)sample["fromPermission"]["blockedBit15"]||(bool)sample["toPermission"]["blockedBit15"];
            bool gap=sample["nearestDeltaUnity"].Type==JTokenType.Null;
            var entry=register["entries"].Cast<JObject>().SingleOrDefault(e=>(string)e["id"]==(string)sample["id"]);
            bool match=entry!=null&&new[]{"from","to","fromSurfaces","toSurfaces","nearestDeltaUnity"}.All(k=>JToken.DeepEquals(entry[k],sample[k]))
                &&(bool)entry["fromBlocked"]==(bool)sample["fromPermission"]["blockedBit15"]&&(bool)entry["toBlocked"]==(bool)sample["toPermission"]["blockedBit15"];
            string category,status;bool exemption=false,required=!blocked;
            if(gap)
            {
                category="source-bdhc-coverage-gap";
                exemption=blocked&&match&&(string)entry["decision"]=="document-source-coverage-gap";
                status=exemption?"documented-gap; visual-unresolved":"unresolved-gap; investigate";
            }
            else if(!blocked)
            {
                category="traversable-boundary";
                bool unambiguous=sample["fromSurfaces"].Count()==1&&sample["toSurfaces"].Count()==1;
                status=(float)sample["nearestDeltaUnity"]<=.0001f&&unambiguous?"pass-static-continuity":"FAIL-or-unresolved-traversable-boundary";
            }
            else if((float)sample["nearestDeltaUnity"]<=.0001f)
            {category="blocked-continuous-boundary";status="measured-continuous; no-visual-fidelity-claim";}
            else if(match&&(string)entry["decision"]=="document-blocked-source-elevation-step")
            {category="documented-blocked-elevation-discontinuity";status="documented-exemption; visual-unresolved";exemption=true;}
            else {category="unresolved-boundary";status="unregistered-or-changed-blocked-step; investigate";}
            return new JObject { ["category"]=category,["status"]=status,["traversableContinuityRequired"]=required,["explicitDocumentedExemption"]=exemption,["reviewEvidenceMatches"]=match,["reason"]=match?(string)entry["reason"]:"No matching exception evidence; blocked state alone does not validate a discontinuity.",["faithfulVisualTopologyValidated"]=false };
        }
        public static int RuntimePermissionIndex(int x,int z)
        {
            PilotIr.Require(x>=0&&x<32&&z>=0&&z<32,"Local tile outside land block.");
            return x+32*z; // ROM sub_020547D8; DSPRE MapEditor also indexes [y,x].
        }
        public static float ModelRenderAltitude(int altitudeRaw) => altitudeRaw*.5f;
        static byte[] Raw(JObject row) => Enumerable.Range(0,48).Select(i=>Convert.ToByte(((string)row["rawHex"]).Substring(i*2,2),16)).ToArray();
        public static JObject BuildingInterpretation(JObject row)
        {
            var bytes=Raw(row);
            Func<int,JArray> vector=offset=>new JArray(Enumerable.Range(0,3).Select(i=>BitConverter.ToInt32(bytes,offset+4*i)));
            return new JObject {
                ["translationRawFx32"]=vector(4),["rotationRaw32"]=vector(16),["scaleRawFx32"]=vector(28),
                ["padding40Through47Hex"]=BitConverter.ToString(bytes,40,8).Replace("-","").ToLowerInvariant(),
                ["scaleRuntime"]=new JArray(Enumerable.Range(0,3).Select(i=>BitConverter.ToInt32(bytes,28+4*i)/4096f)),
                ["sourceAnchorUnityMapLocal"]=new JArray(16+BitConverter.ToInt32(bytes,4)/65536f,BitConverter.ToInt32(bytes,8)/65536f,16+BitConverter.ToInt32(bytes,12)/65536f),
                ["placeholderOffsetUnity"]=new JArray(0,1,0),
                ["placeholderDiagnosticSizeUnity"]=new JArray(1.5,2,1.5),
                ["rotationPolicy"]="Outdoor MapPropManager draw path uses identity rotation; signed raw 32-bit vector retained. No generic Euler conversion is inferred.",
                ["scalePolicy"]="Schema 1.2.0 scale VecFx32 begins at byte28; preserved and validated against rawHex. Placeholder cuboid size is diagnostic, not a source footprint.",
                ["pivotPolicy"]="Source model origin, not calibrated building footprint. Cuboid bottom lifted by proxy half-height only; no original model imported.",
                ["matrixAltitudePolicy"]="Outdoor props add map-center X/Z, not matrix altitude Y. BDHC also uses its own unshifted Y."
            };
        }
        static JArray Hits(JObject land,float x,float z)
        {
            var hits=new JArray();var b=(JObject)land["bdhc"];
            foreach(var p in b["plates"].Cast<JObject>())
            {
                var v=PilotGeometry.PlateVertices(b,p);
                if(x<v[0].x-1e-5||x>v[2].x+1e-5||z<v[0].z-1e-5||z>v[2].z+1e-5)continue;
                float y=v[0].y+(v[1].y-v[0].y)*(x-v[0].x)/(v[1].x-v[0].x)+(v[3].y-v[0].y)*(z-v[0].z)/(v[3].z-v[0].z);
                hits.Add(new JObject { ["plate"]=(int)p["index"],["yUnity"]=y });
            }
            return hits;
        }
        static JObject Permission(JObject land,int x,int z)
        {
            var p=land["permissions"][RuntimePermissionIndex(x,z)];
            return new JObject { ["runtimeIndex"]=RuntimePermissionIndex(x,z),["schemaIndex"]=(int)p["index"],["typeRaw"]=(int)p["typeRaw"],["collisionRaw"]=(int)p["collisionRaw"],["blockedBit15"]=((int)p["collisionRaw"]&128)!=0 };
        }
        public static JObject Analyze(PilotIr ir)
        {
            ir.VerifyUnchanged();var lands=ir.Lands.ToDictionary(l=>(int)l["landDataId"]);var cells=ir.Neighborhood.ToArray();var samples=new JArray();var review=LoadBoundaryReview();
            PilotIr.Require((string)review["romSha256"]==PilotIr.RomHash,"Boundary register ROM mismatch.");
            foreach(var land in ir.Lands)PilotIr.Require((string)review["landSourceSha256"][((int)land["landDataId"]).ToString()]==(string)land["rawSha256"],"Boundary evidence source block mismatch.");
            foreach(var a in cells.OrderBy(c=>(int)c["x"]).ThenBy(c=>(int)c["z"]))foreach(bool east in new[]{true,false})
            {
                int x=(int)a["x"],z=(int)a["z"];
                var b=cells.FirstOrDefault(c=>(int)c["x"]==x+(east?1:0)&&(int)c["z"]==z+(east?0:1));if(b==null)continue;
                for(int i=0;i<32;i++)
                {
                    var al=lands[(int)a["landDataIdRaw"]];var bl=lands[(int)b["landDataIdRaw"]];
                    var ah=Hits(al,east?32:i+.5f,east?i+.5f:32);var bh=Hits(bl,east?0:i+.5f,east?i+.5f:0);
                    float? delta=ah.Count==0||bh.Count==0?(float?)null:ah.SelectMany(p=>bh.Select(q=>Math.Abs((float)p["yUnity"]-(float)q["yUnity"]))).Min();
                    string classification=!delta.HasValue?"unresolved source BDHC coverage gap":delta.Value<.0001f?"continuous":"probable legitimate elevation discontinuity";
                    samples.Add(new JObject { ["id"]=x+","+z+":"+(east?"east":"south")+":"+i.ToString("D2"),["from"]=(string)a["stableId"],["to"]=(string)b["stableId"],["sample"]=i,["fromSurfaces"]=ah,["toSurfaces"]=bh,["nearestDeltaUnity"]=delta,["fromPermission"]=Permission(al,east?31:i,east?i:31),["toPermission"]=Permission(bl,east?0:i,east?i:0),["classification"]=classification });
                }
            }
            foreach(var sample in samples.Cast<JObject>())sample["validation"]=BoundaryDecision(sample,review);
            var report=new JObject { ["irSha256"]=ir.Hash,["sampleCount"]=samples.Count,["counts"]=JObject.FromObject(samples.GroupBy(s=>(string)s["classification"]).ToDictionary(g=>g.Key,g=>g.Count())),["samples"]=samples,["buildingInterpretations"]=new JArray(ir.Lands.SelectMany(l=>l["buildings"].Cast<JObject>()).Select(r=>new JObject { ["stableId"]=(string)r["stableId"],["derived"]=BuildingInterpretation(r) })),["acceptedBaseline"]=false };
            report["boundaryPolicyVersion"]="2.0.0";report["reviewRegisterSha256"]=BoundaryReviewHash;
            report["categoryCounts"]=JObject.FromObject(samples.GroupBy(s=>(string)s["validation"]["category"]).ToDictionary(g=>g.Key,g=>g.Count()));
            return report;
        }
        public static JObject WriteEvidence()
        {
            var result=Analyze(PilotImporter.LoadApproved());Directory.CreateDirectory(PilotImporter.EvidencePath);
            File.WriteAllText(Path.Combine(PilotImporter.EvidencePath,"calibration-analysis.json"),result.ToString());return (JObject)result["counts"];
        }
    }
}
