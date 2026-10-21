using NUnit.Framework;
using System.Linq;
using UnityEngine;
using PokemonRoguelite.NewBarkPilot.Editor;

namespace PokemonRoguelite.NewBarkPilot.Tests
{
    public sealed class PilotSourceCameraTests
    {
        [Test] public void SourceCameraProfileHasPinnedRomAndIrIdentity()
        {var p=PilotSourceCamera.Load();Assert.AreEqual(PilotIr.RomHash,(string)p["sourceRomSha256"]);Assert.AreEqual(0,(int)p["cameraSelector"]);Assert.AreEqual(2731713,(int)p["distanceRawFx32"]);}
        [Test] public void EyeOffsetRetainsFixedPointRounding()
        {var p=PilotSourceCamera.Load();var v=PilotSourceCamera.Offset(p);Assert.AreEqual(2049452,v.y*65536);Assert.AreEqual(1803357,v.z*65536);Assert.AreEqual(0,v.x);}
        [Test] public void QuantizedTrigFovIsNotNominalAngleDoubling()
        {var p=PilotSourceCamera.Load();double exact=(double)p["fullVerticalFovDegreesDerived"];Assert.That(exact,Is.EqualTo(16.16919938033632).Within(1e-10));Assert.That(System.Math.Abs(exact-2*1473*360.0/65536),Is.GreaterThan(.01));}
        [Test] public void ExplicitViewPreservesEastRightSouthDownAndUpUp()
        {var p=PilotSourceCamera.Load();var t=new Vector3(14.5f,1,17.5f);var c=PilotSourceCamera.Pixel(p,t,t);Assert.That(c.x,Is.EqualTo(128).Within(.001));Assert.That(c.y,Is.EqualTo(96).Within(.001));Assert.Greater(PilotSourceCamera.Pixel(p,t,t+Vector3.right).x,c.x);Assert.Greater(PilotSourceCamera.Pixel(p,t,t+Vector3.forward).y,c.y);Assert.Less(PilotSourceCamera.Pixel(p,t,t+Vector3.up).y,c.y);}
        [Test] public void ExplicitPerspectiveHasPinnedAspectAndClipping()
        {var p=PilotSourceCamera.Load();var m=PilotSourceCamera.Projection(p);Assert.That(m[1,1]/m[0,0],Is.EqualTo(5461f/4096).Within(1e-6));foreach(float z in new[]{9.375f,75f}){var v=m*new Vector4(0,0,-z,1);Assert.That(v.z/v.w,Is.EqualTo(z==9.375f?-1:1).Within(1e-5));}}
        [Test] public void SourceCameraPreviewDoesNotMutateSourceProfile()
        {var p=PilotSourceCamera.Load();var raw=PilotIr.Compact(p);var t=new Vector3(-4,2,13);PilotSourceCamera.Pixel(p,t,new Vector3(7,1,18));Assert.AreEqual(raw,PilotIr.Compact(p));Assert.IsFalse((bool)PilotSourceCamera.Load()["status"].ToString().Contains("accepted"));}
        [Test] public void DisposablePreviewRendersAllFiveWarpProjectionsWithoutChangingActiveScene()
        {var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();var before=PilotImporter.ManifestHash(PilotImporter.Manifest(scene));var result=PilotSourceCamera.Capture("regression-preview",new Vector3(14.5f,1,17.5f));Assert.AreEqual(5,result["projectionPoints"].Count());Assert.IsFalse((bool)result["accepted"]);Assert.AreEqual(before,PilotImporter.ManifestHash(PilotImporter.Manifest(scene)));Assert.IsTrue(System.IO.File.Exists((string)result["image"]));}
    }
}
