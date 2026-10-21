using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using PokemonRoguelite.NewBarkPilot.Editor;

namespace PokemonRoguelite.NewBarkPilot.Tests
{
    public static class PilotTestRunner
    {
        static TestRunnerApi api;
        public static string Run()
        {
            Directory.CreateDirectory(PilotImporter.EvidencePath);
            File.WriteAllText(Path.Combine(PilotImporter.EvidencePath,"test-results.json"),"{\"status\":\"running\"}");
            api=ScriptableObject.CreateInstance<TestRunnerApi>();api.RegisterCallbacks(new Results());
            return api.Execute(new ExecutionSettings(new Filter {testMode=TestMode.EditMode,assemblyNames=new[]{"PokemonRoguelite.NewBarkPilot.Editor.Tests"}}));
        }
        sealed class Results:ICallbacks
        {
            readonly JArray tests=new JArray();
            public void RunStarted(ITestAdaptor testsToRun) {}
            public void TestStarted(ITestAdaptor test) {}
            public void TestFinished(ITestResultAdaptor result)
            {
                if(!result.HasChildren) tests.Add(new JObject { ["name"]=result.FullName,["result"]=result.ResultState,["durationSeconds"]=result.Duration,["message"]=result.Message,["stackTrace"]=result.StackTrace });
            }
            public void RunFinished(ITestResultAdaptor result)
            {
                TestRunnerApi.SaveResultToFile(result,Path.Combine(PilotImporter.EvidencePath,"editmode-tests.xml"));
                File.WriteAllText(Path.Combine(PilotImporter.EvidencePath,"test-results.json"),new JObject { ["status"]="completed",["result"]=result.ResultState,["passed"]=result.PassCount,["failed"]=result.FailCount,["skipped"]=result.SkipCount,["tests"]=tests }.ToString());
            }
        }
    }
}
