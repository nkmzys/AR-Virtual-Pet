using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

public class HealthBehaviorInputUI : MonoBehaviour
{
    [Header("Health Behavior")]
    [SerializeField] private HealthBehaviorAnimationController healthController;

    [Header("Input UI")]
    [SerializeField] private InputField stepsInput;
    [SerializeField] private InputField sleepHoursInput;
    [SerializeField] private Button applyButton;
    [SerializeField] private Text resultText;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeUi()
    {
        if (FindAnyObjectByType<MobileHealthInputUI>(FindObjectsInactive.Include) != null)
        {
            return;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        if (FindAnyObjectByType<HealthBehaviorNativePhoneUI>(FindObjectsInactive.Include) != null)
        {
            return;
        }

        HealthBehaviorAnimationController androidController =
            FindAnyObjectByType<HealthBehaviorAnimationController>(FindObjectsInactive.Include);
        HealthBehaviorNativePhoneUI.Create(androidController);
        return;
#else
        if (FindAnyObjectByType<HealthBehaviorInputUI>(FindObjectsInactive.Include) != null)
        {
            return;
        }

        HealthBehaviorAnimationController controller =
            FindAnyObjectByType<HealthBehaviorAnimationController>(FindObjectsInactive.Include);
        if (controller != null)
        {
            HealthBehaviorRuntimeUIFactory.Create(controller);
        }
#endif
    }

    public void Configure(
        HealthBehaviorAnimationController controller,
        InputField steps,
        InputField sleep,
        Button apply,
        Text result)
    {
        healthController = controller;
        stepsInput = steps;
        sleepHoursInput = sleep;
        applyButton = apply;
        resultText = result;
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

        PopulateInputsFromController();
        RefreshResult();
    }

    private void OnDestroy()
    {
        if (applyButton != null)
        {
            applyButton.onClick.RemoveListener(ApplyInput);
        }
    }

    public void ApplyInput()
    {
        if (healthController == null)
        {
            SetResult("健康状態コントローラーが設定されていません。");
            return;
        }

        if (!TryParseNonNegativeInt(stepsInput?.text, out int steps))
        {
            SetResult("歩数には0以上の整数を入力してください。");
            return;
        }

        if (!TryParseNonNegativeFloat(sleepHoursInput?.text, out float sleepHours))
        {
            SetResult("睡眠時間には0以上の数値を入力してください。");
            return;
        }

        if (sleepHours > 24f)
        {
            SetResult("睡眠時間は24時間以下で入力してください。");
            return;
        }

        healthController.SetHealthData(steps, sleepHours);
        RefreshResult();
    }

    private void PopulateInputsFromController()
    {
        if (healthController == null)
        {
            return;
        }

        if (stepsInput != null)
        {
            stepsInput.text = healthController.Steps.ToString(CultureInfo.InvariantCulture);
        }

        if (sleepHoursInput != null)
        {
            sleepHoursInput.text = healthController.SleepHours.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }

    private void RefreshResult()
    {
        if (healthController == null)
        {
            SetResult("健康状態コントローラーが設定されていません。");
            return;
        }

        SetResult(
            $"歩数: {healthController.StepRankSymbol}\n" +
            $"睡眠時間: {healthController.SleepRankSymbol}\n" +
            $"再生中: {healthController.CurrentBehaviorName}");
    }

    private void SetResult(string message)
    {
        if (resultText != null)
        {
            resultText.text = message;
        }

        Debug.Log($"[HealthBehaviorInputUI] {message}");
    }

    private static bool TryParseNonNegativeInt(string text, out int value)
    {
        return int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
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
