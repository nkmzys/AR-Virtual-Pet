using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MobileHealthInputUI : MonoBehaviour
{
    [Header("Health Behavior")]
    [SerializeField] private HealthBehaviorAnimationController healthController;

    [Header("Panels")]
    [SerializeField] private GameObject inputPanel;
    [SerializeField] private GameObject collapsedPanel;

    [Header("TextMeshPro UI")]
    [SerializeField] private TMP_InputField stepsInput;
    [SerializeField] private TMP_InputField sleepHoursInput;
    [SerializeField] private Button applyButton;
    [SerializeField] private Button editButton;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private TMP_Text collapsedResultText;

    [Header("Behavior")]
    [SerializeField] private bool hideInputPanelAfterApply = true;

    [Header("Legacy UI Fallback")]
    [SerializeField] private InputField legacyStepsInput;
    [SerializeField] private InputField legacySleepHoursInput;
    [SerializeField] private Text legacyResultText;

    private string latestResult;
    private GameObject appliedNotice;
    private Font noticeFont;
    private float noticeHideTime;
    private bool followPlayback;
    private bool lastTransitioning;
    private string lastBehavior;

    private void Update()
    {
        if (appliedNotice != null && appliedNotice.activeSelf && Time.unscaledTime >= noticeHideTime)
            appliedNotice.SetActive(false);
        if (followPlayback && healthController != null &&
            (lastTransitioning != healthController.IsTransitioning || lastBehavior != healthController.CurrentBehaviorName))
            RefreshResult();
    }

    private void Awake()
    {
        if (healthController == null)
        {
            healthController = FindAnyObjectByType<HealthBehaviorAnimationController>(FindObjectsInactive.Include);
        }

        if (applyButton != null)
        {
            applyButton.onClick.AddListener(ApplyInput);
        }

        if (editButton != null)
        {
            editButton.onClick.AddListener(ShowInputPanel);
        }

        PopulateInputsFromController();
        RefreshResult();
        SetInputPanelVisible(true);
    }

    private void OnDestroy()
    {
        if (appliedNotice != null) Destroy(appliedNotice);
        if (noticeFont != null) Destroy(noticeFont);
        if (applyButton != null)
        {
            applyButton.onClick.RemoveListener(ApplyInput);
        }

        if (editButton != null)
        {
            editButton.onClick.RemoveListener(ShowInputPanel);
        }
    }

    public void Configure(
        HealthBehaviorAnimationController controller,
        TMP_InputField steps,
        TMP_InputField sleep,
        Button apply,
        TMP_Text result)
    {
        healthController = controller;
        stepsInput = steps;
        sleepHoursInput = sleep;
        applyButton = apply;
        resultText = result;
    }

    public void ApplyInput()
    {
        followPlayback = false;
        if (healthController == null)
        {
            SetResult("Health controller is not assigned.");
            return;
        }

        if (!TryParseNonNegativeInt(GetStepsText(), out int steps))
        {
            SetResult("Steps must be a non-negative integer.");
            return;
        }

        if (!TryParseNonNegativeFloat(GetSleepText(), out float sleepHours))
        {
            SetResult("Sleep must be a non-negative number.");
            return;
        }

        if (sleepHours > 24f)
        {
            SetResult("Sleep must be 24 hours or less.");
            return;
        }

        healthController.SetHealthData(steps, sleepHours);
        followPlayback = true;
        RefreshResult();
        ShowAppliedNotice();

        if (hideInputPanelAfterApply)
        {
            SetInputPanelVisible(false);
        }
    }

    public void ShowInputPanel()
    {
        SetInputPanelVisible(true);
    }

    private void SetInputPanelVisible(bool visible)
    {
        if (inputPanel != null)
        {
            inputPanel.SetActive(visible);
        }

        if (collapsedPanel != null)
        {
            collapsedPanel.SetActive(!visible);
        }
    }

    private void PopulateInputsFromController()
    {
        if (healthController == null)
        {
            return;
        }

        SetStepsText(healthController.Steps.ToString(CultureInfo.InvariantCulture));
        SetSleepText(healthController.SleepHours.ToString("0.##", CultureInfo.InvariantCulture));
    }

    private void RefreshResult()
    {
        if (healthController == null)
        {
            SetResult("Health controller is not assigned.");
            return;
        }

        lastTransitioning = healthController.IsTransitioning;
        lastBehavior = healthController.CurrentBehaviorName;
        SetResult(
            $"Steps: {healthController.StepRankSymbol}\n" +
            $"Sleep: {healthController.SleepRankSymbol}\n" +
            (lastTransitioning ? "Changing behavior..." : $"Playing: {lastBehavior}"));
    }

    private void ShowAppliedNotice()
    {
        if (appliedNotice == null)
        {
            // OS font fallback supports Japanese, unlike the scene's LiberationSans SDF.
            noticeFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Noto Sans CJK JP", "Noto Sans JP", "Yu Gothic", "Meiryo", "sans-serif" }, 32);
            appliedNotice = new GameObject("Health Input Applied Notice", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            Canvas canvas = appliedNotice.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 400;
            CanvasScaler scaler = appliedNotice.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            GameObject panel = new GameObject("Notice", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(appliedNotice.transform, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.8f);
            rect.sizeDelta = new Vector2(600f, 76f);
            Image background = panel.GetComponent<Image>();
            background.color = new Color(0.04f, 0.07f, 0.08f, 0.85f);
            background.raycastTarget = false;
            GameObject label = new GameObject("Message", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(panel.transform, false);
            RectTransform labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            Text text = label.GetComponent<Text>();
            text.font = noticeFont != null ? noticeFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 32;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            text.text = "入力を反映しました";
        }
        appliedNotice.SetActive(true);
        noticeHideTime = Time.unscaledTime + 2f;
    }

    private string GetStepsText()
    {
        if (stepsInput != null)
        {
            return stepsInput.text;
        }

        return legacyStepsInput != null ? legacyStepsInput.text : string.Empty;
    }

    private string GetSleepText()
    {
        if (sleepHoursInput != null)
        {
            return sleepHoursInput.text;
        }

        return legacySleepHoursInput != null ? legacySleepHoursInput.text : string.Empty;
    }

    private void SetStepsText(string value)
    {
        if (stepsInput != null)
        {
            stepsInput.text = value;
        }

        if (legacyStepsInput != null)
        {
            legacyStepsInput.text = value;
        }
    }

    private void SetSleepText(string value)
    {
        if (sleepHoursInput != null)
        {
            sleepHoursInput.text = value;
        }

        if (legacySleepHoursInput != null)
        {
            legacySleepHoursInput.text = value;
        }
    }

    private void SetResult(string message)
    {
        latestResult = message;

        if (resultText != null)
        {
            resultText.text = message;
        }

        if (collapsedResultText != null)
        {
            collapsedResultText.text = latestResult;
        }

        if (legacyResultText != null)
        {
            legacyResultText.text = message;
        }

        Debug.Log($"[MobileHealthInputUI] {message}");
    }

    private static bool TryParseNonNegativeInt(string text, out int value)
    {
        return int.TryParse(
                text?.Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value)
            && value >= 0;
    }

    private static bool TryParseNonNegativeFloat(string text, out float value)
    {
        string normalized = text?.Trim().Replace(',', '.') ?? string.Empty;
        return float.TryParse(
                normalized,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value)
            && float.IsFinite(value)
            && value >= 0f;
    }
}
