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
        RefreshResult();

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

        SetResult(
            $"Steps: {healthController.StepRankSymbol}\n" +
            $"Sleep: {healthController.SleepRankSymbol}\n" +
            $"Playing: {healthController.CurrentBehaviorName}");
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
