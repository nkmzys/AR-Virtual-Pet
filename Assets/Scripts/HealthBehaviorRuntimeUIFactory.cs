using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

public static class HealthBehaviorRuntimeUIFactory
{
    public static HealthBehaviorInputUI Create(HealthBehaviorAnimationController healthController)
    {
        EnsureEventSystem();

        Font font = CreateRuntimeFont();
        GameObject canvasObject = CreateUiObject("Health Behavior UI", null);
        canvasObject.SetActive(false);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject panel = CreateUiObject("Input Panel", canvasObject.transform);
        SetTopLeftRect(panel.GetComponent<RectTransform>(), new Vector2(32f, -32f), new Vector2(720f, 500f));
        panel.AddComponent<Image>().color = new Color(0.05f, 0.08f, 0.12f, 0.9f);

        CreateText("Title", panel.transform, font, "健康データ入力", 40, TextAnchor.MiddleCenter, new Vector2(30f, -24f), new Vector2(660f, 64f));
        CreateText("Steps Label", panel.transform, font, "歩数", 32, TextAnchor.MiddleLeft, new Vector2(40f, -112f), new Vector2(180f, 64f));
        InputField stepsInput = CreateInputField("Steps Input", panel.transform, font, "例: 8000", InputField.ContentType.IntegerNumber, new Vector2(220f, -112f), new Vector2(440f, 64f));

        CreateText("Sleep Label", panel.transform, font, "睡眠時間", 32, TextAnchor.MiddleLeft, new Vector2(40f, -200f), new Vector2(180f, 64f));
        InputField sleepInput = CreateInputField("Sleep Input", panel.transform, font, "例: 7.5", InputField.ContentType.DecimalNumber, new Vector2(220f, -200f), new Vector2(440f, 64f));

        Button applyButton = CreateButton("Apply Button", panel.transform, font, "反映", new Vector2(40f, -288f), new Vector2(620f, 72f));
        Text resultText = CreateText("Result Text", panel.transform, font, string.Empty, 25, TextAnchor.MiddleLeft, new Vector2(40f, -384f), new Vector2(640f, 84f));
        resultText.horizontalOverflow = HorizontalWrapMode.Wrap;
        resultText.verticalOverflow = VerticalWrapMode.Overflow;

        HealthBehaviorInputUI inputUi = canvasObject.AddComponent<HealthBehaviorInputUI>();
        inputUi.Configure(healthController, stepsInput, sleepInput, applyButton, resultText);
        canvasObject.SetActive(true);
        return inputUi;
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
#else
        eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
    }

    private static Font CreateRuntimeFont()
    {
        Font font = Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic UI", "Meiryo", "Noto Sans CJK JP", "sans-serif" }, 32);
        return font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static Text CreateText(string name, Transform parent, Font font, string value, int fontSize, TextAnchor alignment, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject textObject = CreateUiObject(name, parent);
        SetTopLeftRect(textObject.GetComponent<RectTransform>(), anchoredPosition, size);
        Text text = textObject.AddComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        return text;
    }

    private static InputField CreateInputField(string name, Transform parent, Font font, string placeholderValue, InputField.ContentType contentType, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject inputObject = CreateUiObject(name, parent);
        SetTopLeftRect(inputObject.GetComponent<RectTransform>(), anchoredPosition, size);
        Image background = inputObject.AddComponent<Image>();
        background.color = Color.white;

        Text inputText = CreateText("Text", inputObject.transform, font, string.Empty, 30, TextAnchor.MiddleLeft, new Vector2(16f, -4f), new Vector2(size.x - 32f, size.y - 8f));
        inputText.color = Color.black;

        Text placeholder = CreateText("Placeholder", inputObject.transform, font, placeholderValue, 30, TextAnchor.MiddleLeft, new Vector2(16f, -4f), new Vector2(size.x - 32f, size.y - 8f));
        placeholder.color = new Color(0.35f, 0.35f, 0.35f, 0.7f);
        placeholder.fontStyle = FontStyle.Italic;

        InputField inputField = inputObject.AddComponent<InputField>();
        inputField.textComponent = inputText;
        inputField.placeholder = placeholder;
        inputField.targetGraphic = background;
        inputField.contentType = contentType;
        inputField.lineType = InputField.LineType.SingleLine;
        return inputField;
    }

    private static Button CreateButton(string name, Transform parent, Font font, string label, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject buttonObject = CreateUiObject(name, parent);
        SetTopLeftRect(buttonObject.GetComponent<RectTransform>(), anchoredPosition, size);
        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.1f, 0.55f, 0.95f, 1f);
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;

        Text labelText = CreateText("Text", buttonObject.transform, font, label, 34, TextAnchor.MiddleCenter, Vector2.zero, size);
        RectTransform labelRect = labelText.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.anchoredPosition = Vector2.zero;
        labelRect.sizeDelta = Vector2.zero;
        return button;
    }

    private static void SetTopLeftRect(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }
}
