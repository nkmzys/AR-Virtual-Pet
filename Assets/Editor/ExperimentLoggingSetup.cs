using System.IO;
using Experiment1;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class ExperimentLoggingSetup
{
    private const string RequestPath = "Library/ExperimentStep4.request";
    static ExperimentLoggingSetup() { EditorApplication.delayCall += CheckRequest; }

    private static void CheckRequest()
    {
        if (!File.Exists(RequestPath)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += CheckRequest;
            return;
        }
        File.Delete(RequestPath);
        if (EditorApplication.isPlaying && !SceneManager.GetActiveScene().isDirty &&
            SceneManager.GetActiveScene().path == "Assets/Experiment/Scenes/Experiment1Text.unity")
        {
            EditorApplication.playModeStateChanged += InstallAfterStop;
            EditorApplication.isPlaying = false;
            return;
        }
        InstallAndVerify();
    }

    private static void InstallAfterStop(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredEditMode) return;
        EditorApplication.playModeStateChanged -= InstallAfterStop;
        EditorApplication.delayCall += InstallAndVerify;
    }

    [MenuItem("Tools/Experiment 1/Add CSV Logger and Verify")]
    public static void InstallAndVerify()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty ||
            scene.path != "Assets/Experiment/Scenes/Experiment1Text.unity")
        {
            File.WriteAllText("Library/ExperimentStep4-result.txt",
                "NOT RUN: playing=" + EditorApplication.isPlayingOrWillChangePlaymode + ", dirty=" + scene.isDirty + ", scene=" + scene.path + ". No scene was modified.");
            Debug.LogWarning("[ExperimentLoggingSetup] Stop Play mode and open the saved Experiment1Text scene.");
            return;
        }
        var controller = Object.FindAnyObjectByType<InterventionController>();
        if (controller == null) throw new System.InvalidOperationException("InterventionController missing.");
        var logger = controller.GetComponent<ExperimentLogger>();
        if (logger == null) logger = Undo.AddComponent<ExperimentLogger>(controller.gameObject);
        var settings = new SerializedObject(logger);
        settings.FindProperty("controller").objectReferenceValue = controller;
        settings.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        ExperimentFlowVerifier.Verify();
    }
}
