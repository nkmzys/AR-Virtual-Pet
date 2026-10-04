using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class ExperimentAndroidBuild
{
    public static void Build()
    {
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "jp.arpet.experiment1.text");
        PlayerSettings.productName = "20261004_Experiment1_Text";
        PlayerSettings.companyName = "ARPetResearch";
        // Separate app identity preserves existing pet apps and their data.
        string output = Path.GetFullPath("Builds/Android/20261004_Experiment1_Text/20261004_Experiment1_Text.apk");
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Experiment/Scenes/Experiment1Text.unity" },
            locationPathName = output,
            target = BuildTarget.Android,
            options = BuildOptions.Development
        });
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(output), "build-result.txt"), report.summary.result + "\n" + output + "\nErrors: " + report.summary.totalErrors);
        if (report.summary.result != BuildResult.Succeeded) throw new Exception("Android build failed.");
        Debug.Log("[ExperimentAndroidBuild] APK: " + output);
    }
}
