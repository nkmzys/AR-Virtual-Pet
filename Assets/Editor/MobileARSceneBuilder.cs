using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MobileARSceneBuilder
{
    private const string SimpleScenePath = "Assets/MobileAR/Scenes/SimpleCameraHealthBehaviorScene.unity";
    private const string DogPrefabPath = "Assets/Dog_Pomeranian/Base/Pomeranian.prefab";
    private const string ControllerSearchRoot = "Assets/Dog_Pomeranian/Animator";

    [MenuItem("Tools/Mobile AR/Create Simple Camera Health Scene")]
    public static void CreateSimpleCameraHealthScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "SimpleCameraHealthBehaviorScene";

        CreateMainCamera();
        SimpleCameraBackgroundUI cameraBackground = CreateCameraBackgroundCanvas();
        CreateLighting();

        GameObject dogRoot = CreateDogRoot();
        dogRoot.transform.SetPositionAndRotation(new Vector3(0f, -0.55f, 2.2f), Quaternion.Euler(0f, 180f, 0f));
        dogRoot.transform.localScale = new Vector3(0.55f, 0.55f, 0.55f);
        dogRoot.SetActive(false);

        HealthBehaviorAnimationController healthController = ConfigureHealthController(dogRoot);
        TMP_Text statusText = CreateHealthCanvas(healthController);
        CreateMarkerDetector(cameraBackground, dogRoot, statusText);
        EnsureEventSystem();

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.82f, 0.82f, 0.82f, 1f);

        EditorSceneManager.SaveScene(scene, SimpleScenePath);
        AddSceneToBuildSettings(SimpleScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log($"Created simple camera health behavior scene: {SimpleScenePath}");
    }

    [MenuItem("Tools/Mobile AR/Create Mobile AR Health Scene")]
    public static void CreateMobileARHealthScene()
    {
        CreateSimpleCameraHealthScene();
    }

    private static Camera CreateMainCamera()
    {
        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 0f, -3f), Quaternion.identity);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.Depth;
        camera.backgroundColor = Color.clear;
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = 100f;
        camera.fieldOfView = 55f;
        cameraObject.AddComponent<AudioListener>();
        return camera;
    }

    private static SimpleCameraBackgroundUI CreateCameraBackgroundCanvas()
    {
        GameObject canvasObject = new GameObject("Phone Camera Background Canvas", typeof(RectTransform));
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = Camera.main;
        canvas.planeDistance = 12f;
        canvas.sortingOrder = -100;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject backgroundObject = CreateUiObject("Phone Camera Background", canvasObject.transform);
        RectTransform rect = backgroundObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        RawImage image = backgroundObject.AddComponent<RawImage>();
        image.color = Color.white;
        return backgroundObject.AddComponent<SimpleCameraBackgroundUI>();
    }

    private static void CreateLighting()
    {
        GameObject lightObject = new GameObject("Directional Light");
        lightObject.transform.rotation = Quaternion.Euler(45f, 30f, 0f);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.15f;
    }

    private static GameObject CreateDogRoot()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DogPrefabPath);
        GameObject dogRoot;

        if (prefab != null)
        {
            dogRoot = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            dogRoot.name = "Simple Camera Dog";
        }
        else
        {
            dogRoot = new GameObject("Simple Camera Dog");
            Debug.LogWarning($"Dog prefab was not found: {DogPrefabPath}");
        }

        return dogRoot;
    }

    private static HealthBehaviorAnimationController ConfigureHealthController(GameObject dogRoot)
    {
        HealthBehaviorAnimationController controller = dogRoot.GetComponent<HealthBehaviorAnimationController>();
        if (controller == null)
        {
            controller = dogRoot.AddComponent<HealthBehaviorAnimationController>();
        }

        Animator animator = dogRoot.GetComponentInChildren<Animator>(true);
        if (animator == null)
        {
            animator = dogRoot.AddComponent<Animator>();
        }

        animator.applyRootMotion = false;
        animator.updateMode = AnimatorUpdateMode.Normal;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        SerializedObject serializedController = new SerializedObject(controller);
        SetObjectReference(serializedController, "targetAnimator", animator);
        SetObjectReference(serializedController, "walkRequestController", FindAnimatorController("my_pome_walk_request"));
        SetObjectReference(serializedController, "sleepyController", FindAnimatorController("my_pome_sleepy"));
        SetObjectReference(serializedController, "idleController", FindAnimatorController("my_pome_idle"));
        SetObjectReference(serializedController, "happyController", FindAnimatorController("my_pome_happy"));
        SetObjectReference(serializedController, "veryHappyController", FindAnimatorController("my_pome_very_happy"));
        SetObjectReference(serializedController, "transitionIdleClip",
            AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Dog_Pomeranian/Ani/077_Idle_Loop.fbx"));
        SetInt(serializedController, "steps", 10000);
        SetFloat(serializedController, "sleepHours", 8f);
        serializedController.ApplyModifiedPropertiesWithoutUndo();
        return controller;
    }

    private static RuntimeAnimatorController FindAnimatorController(string assetName)
    {
        string[] guids = AssetDatabase.FindAssets($"{assetName} t:AnimatorController", new[] { ControllerSearchRoot });
        if (guids.Length == 0)
        {
            Debug.LogWarning($"AnimatorController was not found: {assetName}");
            return null;
        }

        return AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    private static TMP_Text CreateHealthCanvas(HealthBehaviorAnimationController healthController)
    {
        TMP_FontAsset fontAsset = GetTmpFontAsset();

        GameObject canvasObject = new GameObject("Simple Health UI", typeof(RectTransform));
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject panel = CreateUiObject("Input Panel", canvasObject.transform);
        SetBottomRect(panel.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1080f, 620f));
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.04f, 0.07f, 0.08f, 0.88f);

        TMP_Text title = CreateText(panel.transform, "Title", "Health Input", 42, TextAlignmentOptions.Center, fontAsset);
        SetTopLeftRect(title.rectTransform, new Vector2(36f, -28f), new Vector2(1008f, 64f));

        TMP_Text stepsLabel = CreateText(panel.transform, "Steps Label", "Steps", 34, TextAlignmentOptions.Left, fontAsset);
        SetTopLeftRect(stepsLabel.rectTransform, new Vector2(50f, -120f), new Vector2(210f, 78f));
        TMP_InputField stepsInput = CreateInputField(panel.transform, "Steps Input", "8000", fontAsset, TMP_InputField.ContentType.IntegerNumber, 38);
        SetTopLeftRect(stepsInput.GetComponent<RectTransform>(), new Vector2(290f, -118f), new Vector2(720f, 86f));

        TMP_Text sleepLabel = CreateText(panel.transform, "Sleep Label", "Sleep", 34, TextAlignmentOptions.Left, fontAsset);
        SetTopLeftRect(sleepLabel.rectTransform, new Vector2(50f, -230f), new Vector2(210f, 78f));
        TMP_InputField sleepInput = CreateInputField(panel.transform, "Sleep Input", "7.5", fontAsset, TMP_InputField.ContentType.DecimalNumber, 38);
        SetTopLeftRect(sleepInput.GetComponent<RectTransform>(), new Vector2(290f, -228f), new Vector2(720f, 86f));

        Button applyButton = CreateButton(panel.transform, "Apply Button", "Apply", fontAsset, 40);
        SetTopLeftRect(applyButton.GetComponent<RectTransform>(), new Vector2(50f, -352f), new Vector2(360f, 108f));

        TMP_Text resultText = CreateText(panel.transform, "Result Text", string.Empty, 34, TextAlignmentOptions.Left, fontAsset);
        SetTopLeftRect(resultText.rectTransform, new Vector2(450f, -340f), new Vector2(570f, 170f));

        TMP_Text statusText = CreateText(panel.transform, "Status Text", "Show dog marker to camera.", 26, TextAlignmentOptions.Left, fontAsset);
        SetTopLeftRect(statusText.rectTransform, new Vector2(50f, -520f), new Vector2(980f, 64f));

        GameObject collapsedPanel = CreateUiObject("Collapsed Panel", canvasObject.transform);
        SetBottomRightRect(collapsedPanel.GetComponent<RectTransform>(), new Vector2(-28f, 28f), new Vector2(500f, 190f));
        Image collapsedImage = collapsedPanel.AddComponent<Image>();
        collapsedImage.color = new Color(0.04f, 0.07f, 0.08f, 0.72f);

        TMP_Text collapsedResult = CreateText(collapsedPanel.transform, "Collapsed Result", string.Empty, 24, TextAlignmentOptions.Left, fontAsset);
        SetTopLeftRect(collapsedResult.rectTransform, new Vector2(18f, -16f), new Vector2(300f, 150f));
        Button editButton = CreateButton(collapsedPanel.transform, "Edit Button", "Edit", fontAsset, 30);
        SetTopLeftRect(editButton.GetComponent<RectTransform>(), new Vector2(340f, -52f), new Vector2(130f, 86f));
        collapsedPanel.SetActive(false);

        MobileHealthInputUI inputUi = canvasObject.AddComponent<MobileHealthInputUI>();
        SerializedObject serializedUi = new SerializedObject(inputUi);
        SetObjectReference(serializedUi, "healthController", healthController);
        SetObjectReference(serializedUi, "inputPanel", panel);
        SetObjectReference(serializedUi, "collapsedPanel", collapsedPanel);
        SetObjectReference(serializedUi, "stepsInput", stepsInput);
        SetObjectReference(serializedUi, "sleepHoursInput", sleepInput);
        SetObjectReference(serializedUi, "applyButton", applyButton);
        SetObjectReference(serializedUi, "editButton", editButton);
        SetObjectReference(serializedUi, "resultText", resultText);
        SetObjectReference(serializedUi, "collapsedResultText", collapsedResult);
        SetBool(serializedUi, "hideInputPanelAfterApply", true);
        serializedUi.ApplyModifiedPropertiesWithoutUndo();
        return statusText;
    }

    private static void CreateMarkerDetector(SimpleCameraBackgroundUI cameraBackground, GameObject dogRoot, TMP_Text statusText)
    {
        GameObject detectorObject = new GameObject("Simple Marker Dog Detector");
        SimpleMarkerDogDetector detector = detectorObject.AddComponent<SimpleMarkerDogDetector>();
        SerializedObject serializedDetector = new SerializedObject(detector);
        SetObjectReference(serializedDetector, "cameraBackground", cameraBackground);
        SetObjectReference(serializedDetector, "dogRoot", dogRoot);
        SetObjectReference(serializedDetector, "dogTransform", dogRoot != null ? dogRoot.transform : null);
        SetObjectReference(serializedDetector, "sceneCamera", Camera.main);
        SetObjectReference(serializedDetector, "statusText", statusText);
        SetVector3(serializedDetector, "markerDogScale", new Vector3(2.2f, 2.2f, 2.2f));
        SetBool(serializedDetector, "hideDogUntilMarkerFound", true);
        SetFloat(serializedDetector, "requiredConfidence", 0.50f);
        SetInt(serializedDetector, "missingFramesBeforeHide", 2);
        SetInt(serializedDetector, "minMarkerSize", 72);
        SetInt(serializedDetector, "maxMarkerSize", 520);
        SetInt(serializedDetector, "sizeStep", 28);
        SetInt(serializedDetector, "gridStep", 28);
        SetInt(serializedDetector, "consecutiveDetectionsBeforeShow", 1);
        SetFloat(serializedDetector, "guideDetectionMargin", 0.12f);
        SetFloat(serializedDetector, "guideScreenWidth", 0.74f);
        SetFloat(serializedDetector, "minimumFinderCornerScore", 0.36f);
        SetFloat(serializedDetector, "minimumCenterWhiteRatio", 0.12f);
        SetFloat(serializedDetector, "minimumCenterBlackRatio", 0.04f);
        SetFloat(serializedDetector, "maximumCenterBlackRatio", 0.72f);
        serializedDetector.ApplyModifiedPropertiesWithoutUndo();
    }

    private static TMP_InputField CreateInputField(Transform parent, string name, string placeholder, TMP_FontAsset fontAsset, TMP_InputField.ContentType contentType, int fontSize)
    {
        GameObject inputObject = CreateUiObject(name, parent);
        Image background = inputObject.AddComponent<Image>();
        background.color = Color.white;

        GameObject textArea = CreateUiObject("Text Area", inputObject.transform);
        RectTransform textAreaRect = textArea.GetComponent<RectTransform>();
        textAreaRect.anchorMin = Vector2.zero;
        textAreaRect.anchorMax = Vector2.one;
        textAreaRect.offsetMin = new Vector2(22f, 8f);
        textAreaRect.offsetMax = new Vector2(-22f, -8f);
        textArea.AddComponent<RectMask2D>();

        TMP_Text inputText = CreateText(textArea.transform, "Text", string.Empty, fontSize, TextAlignmentOptions.MidlineLeft, fontAsset);
        Stretch(inputText.rectTransform);
        inputText.color = Color.black;

        TMP_Text placeholderText = CreateText(textArea.transform, "Placeholder", placeholder, fontSize, TextAlignmentOptions.MidlineLeft, fontAsset);
        Stretch(placeholderText.rectTransform);
        placeholderText.color = new Color(0f, 0f, 0f, 0.45f);

        TMP_InputField inputField = inputObject.AddComponent<TMP_InputField>();
        inputField.textViewport = textAreaRect;
        inputField.textComponent = inputText;
        inputField.placeholder = placeholderText;
        inputField.targetGraphic = background;
        inputField.lineType = TMP_InputField.LineType.SingleLine;
        inputField.contentType = contentType;
        return inputField;
    }

    private static Button CreateButton(Transform parent, string name, string label, TMP_FontAsset fontAsset, int fontSize)
    {
        GameObject buttonObject = CreateUiObject(name, parent);
        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.1f, 0.44f, 0.9f, 1f);
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;

        TMP_Text labelText = CreateText(buttonObject.transform, "Text", label, fontSize, TextAlignmentOptions.Center, fontAsset);
        Stretch(labelText.rectTransform);
        return button;
    }

    private static TMP_Text CreateText(Transform parent, string name, string value, int fontSize, TextAlignmentOptions alignment, TMP_FontAsset fontAsset)
    {
        GameObject textObject = CreateUiObject(name, parent);
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.enableWordWrapping = true;
        if (fontAsset != null)
        {
            text.font = fontAsset;
        }

        return text;
    }

    private static TMP_FontAsset GetTmpFontAsset()
    {
        TMP_FontAsset fontAsset = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (fontAsset != null)
        {
            return fontAsset;
        }

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return font != null ? TMP_FontAsset.CreateFontAsset(font) : null;
    }

    private static void EnsureEventSystem()
    {
        EventSystem eventSystem = Object.FindAnyObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            return;
        }

        if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
        {
            eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
        }
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static void SetTopLeftRect(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }

    private static void SetBottomRect(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }

    private static void SetBottomRightRect(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
    {
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void AddSceneToBuildSettings(string scenePath)
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        EditorBuildSettingsScene[] nextScenes = new EditorBuildSettingsScene[scenes.Length + 1];
        nextScenes[0] = new EditorBuildSettingsScene(scenePath, true);
        int writeIndex = 1;

        foreach (EditorBuildSettingsScene buildScene in scenes)
        {
            if (buildScene.path == scenePath)
            {
                continue;
            }

            buildScene.enabled = false;
            nextScenes[writeIndex] = buildScene;
            writeIndex++;
        }

        if (writeIndex != nextScenes.Length)
        {
            System.Array.Resize(ref nextScenes, writeIndex);
        }

        EditorBuildSettings.scenes = nextScenes;
    }

    private static void SetObjectReference(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }

    private static void SetInt(SerializedObject serializedObject, string propertyName, int value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.intValue = value;
        }
    }

    private static void SetFloat(SerializedObject serializedObject, string propertyName, float value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.floatValue = value;
        }
    }

    private static void SetVector3(SerializedObject serializedObject, string propertyName, Vector3 value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.vector3Value = value;
        }
    }

    private static void SetBool(SerializedObject serializedObject, string propertyName, bool value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.boolValue = value;
        }
    }
}
