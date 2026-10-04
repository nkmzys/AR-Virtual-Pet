using System;
using System.IO;
using Experiment1;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Editor-only smoke check using the actual scene components and button listeners.
[InitializeOnLoad]
public static class ExperimentFlowVerifier
{
    private const string ScenePath = "Assets/Experiment/Scenes/Experiment1Text.unity";
    private const string RequestPath = "Library/ExperimentStep3.request";
    private const string ReportPath = "Library/ExperimentStep4-result.txt";
    private const string SessionKey = "Experiment1.FlowVerification";
    private static InterventionController controller;
    private static GameObject canvas;
    private static string csvPath;
    private static int phase;
    private static double startedAt;
    private static double finishStartedAt;

    static ExperimentFlowVerifier()
    {
        EditorApplication.delayCall += ProcessRequest;
        EditorApplication.playModeStateChanged += OnPlayMode;
    }

    private static void ProcessRequest()
    {
        if (!File.Exists(RequestPath)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += ProcessRequest;
            return;
        }
        File.Delete(RequestPath);
        Verify();
    }

    [MenuItem("Tools/Experiment 1/Verify State Flow in Play Mode")]
    public static void Verify()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            WriteReport("NOT RUN: Stop Play mode before starting verification.");
            return;
        }
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            WriteReport("NOT RUN: Open Experiment1Text first. No scene was modified.");
            return;
        }
        SessionState.SetBool(SessionKey, true);
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayMode(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredEditMode && !string.IsNullOrEmpty(csvPath))
        {
            try
            {
                using (var stream = new FileStream(csvPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                File.AppendAllText(ReportPath, "\nPASS: CSV readable and writer closed after leaving Play mode.");
            }
            catch (Exception exception) { File.AppendAllText(ReportPath, "\nFAIL: CSV close check: " + exception.Message); }
            csvPath = null;
        }
        if (!SessionState.GetBool(SessionKey, false)) return;
        if (change == PlayModeStateChange.EnteredPlayMode)
        {
            phase = 0;
            startedAt = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
        }
        else if (change == PlayModeStateChange.ExitingPlayMode)
        {
            EditorApplication.update -= Tick;
            SessionState.SetBool(SessionKey, false);
        }
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - startedAt > 25)
                throw new Exception("Timed out waiting for Finish to return to Waiting.");
            switch (phase)
            {
                case 0:
                    controller = UnityEngine.Object.FindAnyObjectByType<InterventionController>();
                    canvas = GameObject.Find("Canvas");
                    Require(controller != null && canvas != null, "Scene components are missing.");
                    var logger = UnityEngine.Object.FindAnyObjectByType<ExperimentLogger>();
                    Require(logger != null && logger.IsLogging, "CSV logger is unavailable.");
                    csvPath = logger.LogFilePath;
                    Require(File.Exists(csvPath), "CSV file was not created.");
                    Require(canvas.GetComponent<Canvas>().renderMode == RenderMode.ScreenSpaceOverlay,
                        "Canvas must use Screen Space Overlay.");
                    Check(InterventionState.Waiting);
                    controller.Accept(); controller.Decline(); controller.Complete();
                    Check(InterventionState.Waiting);
                    controller.ShowNotification();
                    Check(InterventionState.Prompt);
                    controller.Complete();
                    Check(InterventionState.Prompt);
                    Click("PromptPanel/AcceptButton");
                    Check(InterventionState.Exercise);
                    controller.ShowNotification(); controller.Decline();
                    Check(InterventionState.Exercise);
                    Click("ExercisePanel/CompleteButton");
                    Check(InterventionState.Finish);
                    controller.ShowNotification();
                    Check(InterventionState.Finish);
                    finishStartedAt = EditorApplication.timeSinceStartup;
                    phase = 1;
                    break;
                case 1:
                    if (controller.State == InterventionState.Finish) return;
                    Require(EditorApplication.timeSinceStartup - finishStartedAt >= 2.8,
                        "Finish was hidden too soon.");
                    Check(InterventionState.Waiting);
                    controller.ShowNotification();
                    Check(InterventionState.Prompt);
                    Click("PromptPanel/DeclineButton");
                    Check(InterventionState.Finish);
                    finishStartedAt = EditorApplication.timeSinceStartup;
                    phase = 2;
                    break;
                case 2:
                    if (controller.State == InterventionState.Finish) return;
                    Require(EditorApplication.timeSinceStartup - finishStartedAt >= 2.8,
                        "Decline finish was hidden too soon.");
                    Check(InterventionState.Waiting);
                    controller.ShowNotification();
                    Check(InterventionState.Prompt);
                    // Capture a frame after UI layout has updated.
                    phase = 3;
                    break;
                case 3:
                    ScreenCapture.CaptureScreenshot("Library/ExperimentStep3-prompt.png");
                    phase = 4;
                    break;
                case 4:
                    VerifyCsv();
                    WriteReport("PASS: CSV schema, timestamps, initial Waiting, both event sequences and busy notification logs; CSV: " + csvPath + "\nPASS: Play mode: Waiting -> Prompt -> Accept -> Exercise -> Complete -> Finish -> Waiting; " +
                        "Prompt -> Decline -> Finish -> Waiting; repeated prompt; state guards; exactly one visible panel; " +
                        "actual Button.onClick listeners. Pointer clicks and device rendering need manual verification.");
                    Stop();
                    break;
            }
        }
        catch (Exception exception)
        {
            WriteReport("FAIL: " + exception.Message);
            Debug.LogException(exception);
            Stop();
        }
    }

    private static void VerifyCsv()
    {
        string text;
        using (var stream = new FileStream(csvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var reader = new StreamReader(stream)) text = reader.ReadToEnd();
        var rows = text.TrimEnd('\r', '\n').Replace("\r\n", "\n").Split('\n');
        Require(rows.Length > 1 && rows[0] == "timestamp,event", "Invalid CSV header or empty log.");
        var names = new System.Collections.Generic.List<string>();
        for (int i = 1; i < rows.Length; i++)
        {
            var columns = rows[i].Split(',');
            Require(columns.Length == 2, "Invalid CSV row: " + rows[i]);
            Require(DateTimeOffset.TryParseExact(columns[0], "O", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out _), "Invalid timestamp: " + columns[0]);
            names.Add(columns[1]);
        }
        Require(names[0] == "waiting_shown", "Initial Waiting was not logged.");
        Require(!names.Contains("notification_received"), "Local preview must not claim a PC notification.");
        Require(names.Contains("notification_ignored_busy"), "Busy notifications were not logged.");
        string[] expected = { "notification_simulated", "prompt_shown", "accepted", "exercise_shown",
            "exercise_completed", "finish_shown", "waiting_returned", "notification_simulated", "prompt_shown",
            "declined", "finish_shown", "waiting_returned", "notification_simulated", "prompt_shown" };
        int next = 0;
        foreach (string name in names)
            if (next < expected.Length && name == expected[next]) next++;
        if (next != expected.Length) throw new Exception("CSV event sequence is incomplete at " + expected[next]);
    }
    private static void Click(string path)
    {
        var button = canvas.transform.Find(path).GetComponent<Button>();
        Require(button != null && button.gameObject.activeInHierarchy && button.interactable,
            "Button unavailable: " + path);
        button.onClick.Invoke();
    }

    private static void Check(InterventionState expected)
    {
        Require(controller.State == expected, "Expected " + expected + ", got " + controller.State);
        foreach (InterventionState state in Enum.GetValues(typeof(InterventionState)))
        {
            var panel = canvas.transform.Find(state + "Panel");
            Require(panel != null && panel.gameObject.activeInHierarchy == (state == expected),
                "Incorrect panel visibility: " + state);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void Stop()
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(SessionKey, false);
        EditorApplication.isPlaying = false;
    }

    private static void WriteReport(string result)
    {
        File.WriteAllText(ReportPath, DateTimeOffset.Now.ToString("O") + "\n" + result);
        Debug.Log("[ExperimentFlowVerifier] " + result);
    }
}
