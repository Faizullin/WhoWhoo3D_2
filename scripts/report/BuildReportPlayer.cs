using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildReportPlayer
{
    public static void Build()
    {
        // These settings belong to the disposable capture copy.
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.runInBackground = true;
        PlayerSettings.productName = "WhoWhoo3D Report";
        string backend = System.Environment.GetEnvironmentVariable("WHOWHOO_LAN_BACKEND");
        if (backend != null)
        {
            var target = UnityEditor.Build.NamedBuildTarget.Standalone;
            PlayerSettings.SetScriptingBackend(target, backend == "il2cpp" ? ScriptingImplementation.IL2CPP : ScriptingImplementation.Mono2x);
            if (backend == "il2cpp")
                PlayerSettings.SetIl2CppCodeGeneration(target, UnityEditor.Build.Il2CppCodeGeneration.OptimizeSize);
        }
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/_Project/Scenes/Demo4x4.unity" },
            locationPathName = System.Environment.GetEnvironmentVariable("WHOWHOO_PLAYER"),
            target = backend == null ? BuildTarget.StandaloneOSX : BuildTarget.StandaloneLinux64
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new System.InvalidOperationException("Unity could not build the report player.");
    }
}
