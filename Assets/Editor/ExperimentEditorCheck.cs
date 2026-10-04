using System;
using System.IO;
using System.Reflection;
using Experiment1;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Temporary interaction observation is kept in Library; never modifies a saved scene.
[InitializeOnLoad]
public static class ExperimentEditorCheck
{
    private const string CommandFile = "Library/ExperimentStep6.command";
    private const string ActiveKey = "Experiment1.EditorInteractionCheck";
    private static string previousState;
    private static string pendingCapture;
    private static int pendingFrame;
    private static double nextSnapshot;

    [Serializable]
    private class Snapshot
    {
        public bool playing;
        public bool paused;
        public string scene;
        public string state;
        public string csv;
        public int screenWidth;
        public int screenHeight;
        public Rect gameViewPosition;
        public Rect renderRect;
        public string renderRectMethod;
        public Vector2 acceptScreen;
        public Vector2 declineScreen;
        public Vector2 completeScreen;
    }

    static ExperimentEditorCheck()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(ActiveKey, false))
            {
                SaveSnapshot();
                SessionState.SetBool(ActiveKey, false);
            }
        };
    }

    private static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (File.Exists(CommandFile))
        {
            string command = File.ReadAllText(CommandFile).Trim();
            File.Delete(CommandFile);
            if (SceneManager.GetActiveScene().path != "Assets/Experiment/Scenes/Experiment1Text.unity")
            {
                File.WriteAllText("Library/ExperimentStep6-status.json", "{\"error\":\"Open Experiment1Text first\"}");
                return;
            }
            if (command == "START")
            {
                SessionState.SetBool(ActiveKey, true);
                previousState = null;
                var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
                if (type != null) EditorWindow.GetWindow(type).Focus();
                EditorApplication.isPlaying = true;
            }
            else if (command == "STOP") EditorApplication.isPlaying = false;
        }
        if (!SessionState.GetBool(ActiveKey, false)) return;
        if (EditorApplication.timeSinceStartup >= nextSnapshot)
        {
            SaveSnapshot();
            nextSnapshot = EditorApplication.timeSinceStartup + 0.25;
        }
        if (!EditorApplication.isPlaying) return;
        var controller = UnityEngine.Object.FindAnyObjectByType<InterventionController>();
        if (controller == null) return;
        string current = controller.State.ToString();
        if (current != previousState)
        {
            previousState = current;
            pendingCapture = current;
            pendingFrame = Time.frameCount;
        }
        if (pendingCapture != null && Time.frameCount > pendingFrame + 1)
        {
            ScreenCapture.CaptureScreenshot("Library/ExperimentStep6-" + pendingCapture + ".png");
            pendingCapture = null;
        }
    }

    private static void SaveSnapshot()
    {
        var controller = UnityEngine.Object.FindAnyObjectByType<InterventionController>();
        var logger = UnityEngine.Object.FindAnyObjectByType<ExperimentLogger>();
        var result = new Snapshot
        {
            playing = EditorApplication.isPlaying,
            paused = EditorApplication.isPaused,
            scene = SceneManager.GetActiveScene().path,
            state = controller == null ? "Unavailable" : controller.State.ToString(),
            csv = logger == null ? "" : logger.LogFilePath,
            screenWidth = Screen.width,
            screenHeight = Screen.height
        };
        var gameType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        if (gameType != null)
        {
            var windows = Resources.FindObjectsOfTypeAll(gameType);
            if (windows.Length > 0)
            {
                var window = (EditorWindow)windows[0];
                result.gameViewPosition = window.position;
                var method = gameType.GetMethod("GetMainGameViewRenderRect", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (method != null && method.GetParameters().Length == 0)
                {
                    result.renderRect = (Rect)method.Invoke(null, null);
                    result.renderRectMethod = method.Name;
                }
            }
        }
        var canvas = GameObject.Find("Canvas");
        if (canvas != null)
        {
            result.acceptScreen = Position(canvas, "PromptPanel/AcceptButton");
            result.declineScreen = Position(canvas, "PromptPanel/DeclineButton");
            result.completeScreen = Position(canvas, "ExercisePanel/CompleteButton");
        }
        File.WriteAllText("Library/ExperimentStep6-status.json", JsonUtility.ToJson(result, true));
    }

    private static Vector2 Position(GameObject canvas, string path)
    {
        var rect = canvas.transform.Find(path) as RectTransform;
        return rect == null ? Vector2.zero : RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
    }
}
