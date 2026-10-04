using Experiment1;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public static class ExperimentSceneBuilder
{
    private const string ScenePath = "Assets/Experiment/Scenes/Experiment1Text.unity";

    [MenuItem("Tools/Experiment 1/Create Independent Scene")]
    public static void CreateScene()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";
        var camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.04f, 0.06f, 0.09f);
        cameraObject.transform.position = new Vector3(0, 0, -10);
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        var system = new GameObject("ExperimentSystem");
        var controller = system.AddComponent<InterventionController>();
        system.AddComponent<ExperimentLogger>();
        system.AddComponent<NotificationReceiver>();
        system.AddComponent<NotificationCommandRouter>();
        var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        var waiting = Panel("WaitingPanel", canvasObject.transform);
        Label(waiting.transform, "通知を待っています。", 100);
        var prompt = Panel("PromptPanel", canvasObject.transform);
        Label(prompt.transform, "座っている時間が長くなっています。\nよければ、立って体を伸ばしてみませんか？", 140);
        var accept = MakeButton("AcceptButton", prompt.transform, "実施する", -70);
        var decline = MakeButton("DeclineButton", prompt.transform, "今回は行わない", -230);
        var exercise = Panel("ExercisePanel", canvasObject.transform);
        Label(exercise.transform, "立って体を伸ばしてください。\n終わったら『完了』を押してください。", 140);
        var complete = MakeButton("CompleteButton", exercise.transform, "完了", -70);
        var finish = Panel("FinishPanel", canvasObject.transform);
        Label(finish.transform, "PCの作業に戻ってください。", 100);
        var ui = canvasObject.AddComponent<InterventionUIController>();
        var serialized = new SerializedObject(ui);
        Assign(serialized, "controller", controller);
        Assign(serialized, "waitingPanel", waiting);
        Assign(serialized, "promptPanel", prompt);
        Assign(serialized, "exercisePanel", exercise);
        Assign(serialized, "finishPanel", finish);
        Assign(serialized, "acceptButton", accept);
        Assign(serialized, "declineButton", decline);
        Assign(serialized, "completeButton", complete);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        prompt.SetActive(false);
        exercise.SetActive(false);
        finish.SetActive(false);
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("Created independent scene: " + ScenePath);
    }

    private static void Assign(SerializedObject target, string name, Object value)
        => target.FindProperty(name).objectReferenceValue = value;

    private static GameObject Panel(string name, Transform parent)
    {
        var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        var rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0.04f, 0.06f, 0.09f);
        return panel;
    }

    private static Text Label(Transform parent, string message, float y)
    {
        var obj = new GameObject("MessageText", typeof(RectTransform), typeof(Text));
        obj.transform.SetParent(parent, false);
        var rect = obj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(900, 400);
        rect.anchoredPosition = new Vector2(0, y);
        var label = obj.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.text = message;
        label.fontSize = 42;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleCenter;
        label.raycastTarget = false;
        return label;
    }

    private static Button MakeButton(string name, Transform parent, string caption, float y)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        obj.transform.SetParent(parent, false);
        var rect = obj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(850, 120);
        rect.anchoredPosition = new Vector2(0, y);
        var image = obj.GetComponent<Image>();
        image.color = new Color(0.12f, 0.35f, 0.6f);
        var button = obj.GetComponent<Button>();
        button.targetGraphic = image;
        var label = Label(obj.transform, caption, 0);
        label.name = "Label";
        label.rectTransform.sizeDelta = rect.sizeDelta;
        return button;
    }
}
