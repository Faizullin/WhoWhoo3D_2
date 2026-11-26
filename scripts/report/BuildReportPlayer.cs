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
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/_Project/Scenes/Demo4x4.unity" },
            locationPathName = System.Environment.GetEnvironmentVariable("WHOWHOO_PLAYER"),
            target = BuildTarget.StandaloneOSX
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new System.InvalidOperationException("Unity could not build the report player.");
    }
}
