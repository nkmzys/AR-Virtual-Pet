using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Experiment1;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class ExperimentTcpVerifier
{
    private const string Request = "Library/ExperimentStep5.request";
    private const string Report = "Library/ExperimentStep5-result.txt";
    private const string SessionKey = "Experiment1.TcpVerification";
    private static InterventionController controller;
    private static NotificationReceiver receiver;
    private static GameObject canvas;
    private static string csvPath;
    private static int phase;
    private static int port;
    private static double startedAt;

    static ExperimentTcpVerifier()
    {
        EditorApplication.delayCall += ProcessRequest;
        EditorApplication.playModeStateChanged += OnPlayMode;
    }

    private static void ProcessRequest()
    {
        if (!File.Exists(Request)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += ProcessRequest;
            return;
        }
        if (SceneManager.GetActiveScene().path != "Assets/Experiment/Scenes/Experiment1Text.unity")
        {
            File.Delete(Request);
            Write("NOT RUN: Open Experiment1Text first.");
            return;
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += ProcessRequest;
            return;
        }
        File.Delete(Request);
        var target = UnityEngine.Object.FindAnyObjectByType<InterventionController>();
        var tcp = target.GetComponent<NotificationReceiver>() ?? Undo.AddComponent<NotificationReceiver>(target.gameObject);
        var router = target.GetComponent<NotificationCommandRouter>() ?? Undo.AddComponent<NotificationCommandRouter>(target.gameObject);
        var fields = new SerializedObject(router);
        fields.FindProperty("receiver").objectReferenceValue = tcp;
        fields.FindProperty("controller").objectReferenceValue = target;
        fields.ApplyModifiedProperties();
        // Preserve existing in-memory scene contents; only add the two components.
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Verify();
    }

    [MenuItem("Tools/Experiment 1/Verify TCP Notification Flow")]
    public static void Verify()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            SceneManager.GetActiveScene().path != "Assets/Experiment/Scenes/Experiment1Text.unity")
        {
            Write("NOT RUN: Stop Play and open Experiment1Text.");
            return;
        }
        SessionState.SetBool(SessionKey, true);
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayMode(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode && !string.IsNullOrEmpty(csvPath))
        {
            try
            {
                using (var file = new FileStream(csvPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                var probe = new TcpListener(IPAddress.Any, port);
                try { probe.Start(); } finally { probe.Stop(); }
                File.AppendAllText(Report, "\nPASS: CSV writer closed and TCP port released after Play stopped.");
            }
            catch (Exception exception) { File.AppendAllText(Report, "\nFAIL: Shutdown: " + exception.Message); }
            csvPath = null;
        }
        if (!SessionState.GetBool(SessionKey, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            phase = 0;
            startedAt = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
        }
        else if (state == PlayModeStateChange.ExitingPlayMode)
        {
            EditorApplication.update -= Tick;
            SessionState.SetBool(SessionKey, false);
        }
    }

    private static void Tick()
    {
        try
        {
            Require(EditorApplication.timeSinceStartup - startedAt < 30, "TCP verification timed out.");
            switch (phase)
            {
                case 0:
                    controller = UnityEngine.Object.FindAnyObjectByType<InterventionController>();
                    receiver = UnityEngine.Object.FindAnyObjectByType<NotificationReceiver>();
                    var logger = UnityEngine.Object.FindAnyObjectByType<ExperimentLogger>();
                    canvas = GameObject.Find("Canvas");
                    Require(controller != null && receiver != null && receiver.IsListening && logger != null && logger.IsLogging,
                        "Receiver, controller or CSV logger unavailable.");
                    csvPath = logger.LogFilePath;
                    port = receiver.Port;
                    Check(InterventionState.Waiting);
                    Require(Send("show_notification\n") == "ERROR INVALID_COMMAND", "Invalid command was not rejected.");
                    Require(Send(new string('A', 257) + "\n") == "ERROR LINE_TOO_LONG", "Oversized line was not rejected.");
                    Require(Send("UNKNOWN_TEST\n") == "QUEUED UNKNOWN_TEST", "Generic command transport failed.");
                    Require(Send("SHOW_", "NOTIFICATION\r\n") == "QUEUED SHOW_NOTIFICATION", "Fragmented command failed.");
                    phase = 1;
                    break;
                case 1:
                    if (controller.State == InterventionState.Waiting) return;
                    Check(InterventionState.Prompt);
                    Require(Send("SHOW_NOTIFICATION\n") == "QUEUED SHOW_NOTIFICATION", "Reconnect failed.");
                    phase = 2;
                    break;
                case 2:
                    if (!ReadCsv().Contains(",notification_ignored_busy")) return;
                    Check(InterventionState.Prompt);
                    Click("PromptPanel/AcceptButton");
                    Check(InterventionState.Exercise);
                    Click("ExercisePanel/CompleteButton");
                    Check(InterventionState.Finish);
                    phase = 3;
                    break;
                case 3:
                    if (controller.State == InterventionState.Finish) return;
                    Check(InterventionState.Waiting);
                    Require(SendUsingPcTool() == "QUEUED SHOW_NOTIFICATION", "PC sender tool failed.");
                    phase = 4;
                    break;
                case 4:
                    if (controller.State == InterventionState.Waiting) return;
                    Check(InterventionState.Prompt);
                    Click("PromptPanel/DeclineButton");
                    Check(InterventionState.Finish);
                    phase = 5;
                    break;
                case 5:
                    if (controller.State == InterventionState.Finish) return;
                    Check(InterventionState.Waiting);
                    var lines = ReadCsv().TrimEnd('\r', '\n').Replace("\r\n", "\n").Split('\n');
                    Require(lines[0] == "timestamp,event", "CSV header invalid.");
                    foreach (string row in lines.Skip(1))
                        Require(DateTimeOffset.TryParseExact(row.Split(',')[0], "O", System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.None, out _), "CSV timestamp invalid.");
                    var events = lines.Skip(1).Select(row => row.Split(',')[1]).ToArray();
                    Require(events.Count(name => name == "notification_received") == 3, "Expected three real TCP receive events.");
                    Require(!events.Contains("notification_simulated"), "TCP test was incorrectly logged as simulated.");
                    Require(events.Count(name => name == "prompt_shown") == 2 && events.Count(name => name == "finish_shown") == 2 &&
                        events.Count(name => name == "waiting_returned") == 2 && events.Count(name => name == "notification_ignored_busy") == 1,
                        "Notification or finish event counts invalid.");
                    string[] expected = { "notification_received", "prompt_shown", "notification_received", "notification_ignored_busy",
                        "accepted", "exercise_shown", "exercise_completed", "finish_shown", "waiting_returned",
                        "notification_received", "prompt_shown", "declined", "finish_shown", "waiting_returned" };
                    int next = 0;
                    foreach (string name in events) if (next < expected.Length && name == expected[next]) next++;
                    Require(next == expected.Length, "TCP intervention event order invalid.");
                    Write("PASS: Real localhost TCP -> Prompt -> accept/complete and decline -> Finish -> Waiting; " +
                        "PowerShell PC sender; receive timestamps and CSV order; busy notification; reconnect; split CRLF line; invalid/oversized commands; " +
                        "unknown command leaves intervention unchanged. CSV: " + csvPath);
                    Stop();
                    break;
            }
        }
        catch (Exception exception) { Write("FAIL: " + exception.Message); Debug.LogException(exception); Stop(); }
    }

    private static string SendUsingPcTool()
    {
        string toolPath = Path.GetFullPath("Tools/Experiment1/Send-Notification.ps1");
        var start = new System.Diagnostics.ProcessStartInfo("powershell.exe",
            "-NoProfile -ExecutionPolicy Bypass -File \"" + toolPath + "\" -TargetAddress 127.0.0.1 -Port " + port)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using (var process = System.Diagnostics.Process.Start(start))
        {
            if (!process.WaitForExit(8000))
            {
                process.Kill();
                throw new Exception("PC sender process timed out.");
            }
            string output = process.StandardOutput.ReadToEnd().Trim();
            string error = process.StandardError.ReadToEnd();
            Require(process.ExitCode == 0, "PC sender failed: " + error);
            return output;
        }
    }
    private static string Send(string first, string second = null)
    {
        using (var client = new TcpClient())
        {
            var connection = client.BeginConnect(IPAddress.Loopback, port, null, null);
            try
            {
                Require(connection.AsyncWaitHandle.WaitOne(3000), "TCP connect timeout.");
                client.EndConnect(connection);
            }
            finally { connection.AsyncWaitHandle.Close(); }
            client.ReceiveTimeout = 3000;
            client.SendTimeout = 3000;
            using (var stream = client.GetStream())
            {
                byte[] bytes = Encoding.ASCII.GetBytes(first);
                stream.Write(bytes, 0, bytes.Length);
                if (second != null)
                {
                    bytes = Encoding.ASCII.GetBytes(second);
                    stream.Write(bytes, 0, bytes.Length);
                }
                using (var reader = new StreamReader(stream)) return reader.ReadLine();
            }
        }
    }

    private static string ReadCsv()
    {
        using (var stream = new FileStream(csvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var reader = new StreamReader(stream)) return reader.ReadToEnd();
    }

    private static void Click(string path) => canvas.transform.Find(path).GetComponent<Button>().onClick.Invoke();
    private static void Check(InterventionState state)
    {
        Require(controller.State == state, "Expected " + state + ", got " + controller.State);
        foreach (InterventionState candidate in Enum.GetValues(typeof(InterventionState)))
            Require(canvas.transform.Find(candidate + "Panel").gameObject.activeInHierarchy == (candidate == state),
                "Panel visibility invalid: " + candidate);
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Stop()
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(SessionKey, false);
        EditorApplication.isPlaying = false;
    }
    private static void Write(string result)
    {
        File.WriteAllText(Report, DateTimeOffset.Now.ToString("O") + "\n" + result);
        Debug.Log("[ExperimentTcpVerifier] " + result);
    }
}
