using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace PokemonRoguelite.NewBarkPilot.Editor
{
    public static class PilotGeometry
    {
        public static readonly int[] QuadIndices = { 0, 2, 1, 0, 3, 2 };
        public static Vector3[] PlateVertices(JObject bdhc, JObject plate)
        {
            int a = (int)plate["firstPointIndex"], b = (int)plate["secondPointIndex"], n = (int)plate["normalIndex"], c = (int)plate["constantIndex"];
            PilotIr.Require(a >= 0 && b >= 0 && a < bdhc["points"].Count() && b < bdhc["points"].Count() && n >= 0 && n < bdhc["normals"].Count() && c >= 0 && c < bdhc["constants"].Count(), "BDHC plate references are invalid.");
            double x0 = (long)bdhc["points"][a]["xRawFx32"] / 4096.0, z0 = (long)bdhc["points"][a]["zRawFx32"] / 4096.0;
            double x1 = (long)bdhc["points"][b]["xRawFx32"] / 4096.0, z1 = (long)bdhc["points"][b]["zRawFx32"] / 4096.0;
            var normal = bdhc["normals"][n]["rawFx32"];
            double nx = (long)normal[0] / 4096.0, ny = (long)normal[1] / 4096.0, nz = (long)normal[2] / 4096.0;
            double d = (long)bdhc["constants"][c]["rawFx32"] / 4096.0;
            PilotIr.Require(Math.Abs(ny) > 1e-12 && x1 > x0 && z1 > z0, "Unsupported vertical or degenerate BDHC plate; no record may be dropped.");
            var corners = new[] { (x0,z0), (x1,z0), (x1,z1), (x0,z1) };
            return corners.Select(p => new Vector3((float)(16 + p.Item1 / 16), (float)(-(nx*p.Item1+nz*p.Item2+d)/ny/16), (float)(16+p.Item2/16))).ToArray();
        }

        public static Mesh Plate(JObject bdhc, JObject plate)
        {
            var mesh = new Mesh { name = "BDHC plate " + plate["index"] };
            mesh.vertices = PlateVertices(bdhc, plate); mesh.triangles = QuadIndices;
            mesh.colors = Enumerable.Repeat(new Color(.22f,.58f,.42f),4).ToArray();
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }

        public static Mesh Permissions(JObject land, bool collision)
        {
            var vertices = new List<Vector3>(); var colors = new List<Color>(); var indices = new List<int>();
            foreach (var row in land["permissions"].Cast<JObject>().OrderBy(r=>(int)r["index"]))
            {
                int x=(int)row["x"], z=(int)row["z"], value=(int)row[collision?"collisionRaw":"typeRaw"];
                int start=vertices.Count;
                vertices.AddRange(new[] { new Vector3(x,0,z),new Vector3(x+1,0,z),new Vector3(x+1,0,z+1),new Vector3(x,0,z+1) });
                var color=collision?new Color(.12f+value/255f*.85f,.12f+value/255f*.85f,.16f+value/255f*.8f):Color.HSVToRGB(value/256f,.68f,value==0?.32f:.95f);
                colors.AddRange(Enumerable.Repeat(color,4)); indices.AddRange(QuadIndices.Select(i=>i+start));
            }
            var mesh=new Mesh { name=collision?"Raw collision byte grid":"Raw type byte grid" };
            mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(indices,0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }

        public static string Hash(Mesh mesh)
        {
            using(var stream=new MemoryStream()) using(var writer=new BinaryWriter(stream))
            {
                foreach(var p in mesh.vertices) { writer.Write(p.x);writer.Write(p.y);writer.Write(p.z); }
                foreach(var i in mesh.triangles) writer.Write(i);
                foreach(var c in mesh.colors) {writer.Write(c.r);writer.Write(c.g);writer.Write(c.b);writer.Write(c.a);}
                writer.Flush(); return PilotIr.Sha(stream.ToArray());
            }
        }
    }
}
