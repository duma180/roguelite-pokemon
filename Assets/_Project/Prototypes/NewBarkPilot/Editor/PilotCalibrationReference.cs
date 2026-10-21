using System.IO;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PokemonRoguelite.NewBarkPilot.Editor
{
    public static class PilotCalibrationReference
    {
        // Independently authored unit reference in a disposable preview scene; never fills terrain gaps.
        public static string Capture()
        {
            var scene=EditorSceneManager.NewPreviewScene();var prior=RenderTexture.active;
            var rt=new RenderTexture(1600,1000,24);var image=new Texture2D(1600,1000,TextureFormat.RGB24,false);
            var materials=new System.Collections.Generic.List<Material>();
            try
            {
                System.Action<string,Vector3,Vector3,Color> box=(name,p,s,color)=> {
                    var g=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(g,scene);g.name=name;
                    g.transform.position=p;g.transform.localScale=s;
                    var m=new Material(Shader.Find("Universal Render Pipeline/Unlit"));m.SetColor("_BaseColor",color);materials.Add(m);g.GetComponent<Renderer>().sharedMaterial=m;
                };
                System.Action<string,Vector3> label=(text,p)=> {
                    var g=new GameObject(text);SceneManager.MoveGameObjectToScene(g,scene);g.transform.position=p;g.transform.rotation=Quaternion.Euler(35,0,0);
                    var t=g.AddComponent<TextMesh>();t.text=text;t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=60;t.characterSize=.045f;t.color=Color.white;g.GetComponent<MeshRenderer>().sharedMaterial=t.font.material;
                };
                for(int x=0;x<5;x++)for(int z=0;z<4;z++)box("One tile",new Vector3(x+.5f,-.025f,z+.5f),new Vector3(1,.05f,1),(x+z)%2==0?new Color(.17f,.22f,.28f):new Color(.22f,.3f,.37f));
                box("+X east",new Vector3(2.5f,.02f,0),new Vector3(5,.04f,.04f),Color.red);
                box("+Z south",new Vector3(0,.02f,2),new Vector3(.04f,.04f,4),Color.blue);
                box("+Y up",new Vector3(0,1.5f,0),new Vector3(.04f,3,.04f),Color.green);
                // Standalone rulers; these are not source terrain or building meshes.
                box("8 source units",new Vector3(1.5f,.25f,1.5f),new Vector3(1,.5f,1),new Color(.85f,.6f,.25f));
                box("16 source units",new Vector3(3.5f,.5f,1.5f),new Vector3(1,1,1),new Color(.3f,.8f,.65f));
                label("+X EAST",new Vector3(4.5f,.1f,-.5f));label("+Z SOUTH",new Vector3(-1,.1f,4.2f));label("+Y UP",new Vector3(-.5f,3.3f,0));
                label("8 source = 0.5 Unity",new Vector3(.6f,.7f,1));label("16 source = 1 Unity",new Vector3(2.8f,1.2f,1));
                label("Independent unit reference / no game assets\n1 square = 1 tile = 16 source units\nNot a visual-fidelity or building-pivot approval",new Vector3(-.5f,.1f,-2));
                var cg=new GameObject("Reference camera");SceneManager.MoveGameObjectToScene(cg,scene);var c=cg.AddComponent<Camera>();c.scene=scene;
                c.transform.position=new Vector3(2.5f,8,-11);c.transform.LookAt(new Vector3(2,1,1));c.orthographic=true;c.orthographicSize=4.8f;c.aspect=1.6f;c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.025f,.04f,.06f);c.targetTexture=rt;
                c.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();
                Directory.CreateDirectory(PilotImporter.EvidencePath);var path=Path.Combine(PilotImporter.EvidencePath,"independent-unit-reference.png");File.WriteAllBytes(path,image.EncodeToPNG());return path;
            }
            finally
            {
                RenderTexture.active=prior;EditorSceneManager.ClosePreviewScene(scene);
                foreach(var m in materials)Object.DestroyImmediate(m);Object.DestroyImmediate(image);Object.DestroyImmediate(rt);
            }
        }
    }
}
