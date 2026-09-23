using System.Globalization;
using UnityEngine;

public class HealthBehaviorNativePhoneUI : MonoBehaviour
{
    private HealthBehaviorAnimationController healthController;
    private readonly object pendingInputLock = new object();
    private string pendingSteps;
    private string pendingSleepHours;
    private bool hasPendingInput;

#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject activity;
    private AndroidJavaObject nativePanel;
    private AndroidJavaObject stepsInput;
    private AndroidJavaObject sleepHoursInput;
    private AndroidJavaObject applyButton;
    private AndroidJavaObject resultText;
#endif

    public static HealthBehaviorNativePhoneUI Create(HealthBehaviorAnimationController controller)
    {
        GameObject uiObject = new GameObject("Health Behavior Native Phone UI");
        uiObject.SetActive(false);
        HealthBehaviorNativePhoneUI phoneUi = uiObject.AddComponent<HealthBehaviorNativePhoneUI>();
        phoneUi.healthController = controller;
        uiObject.SetActive(true);
        return phoneUi;
    }

    private void Awake()
    {
        TryFindHealthController();

#if UNITY_ANDROID && !UNITY_EDITOR
        CreateNativeControls();
#endif
    }

    private void Update()
    {
        string stepsText;
        string sleepText;

        lock (pendingInputLock)
        {
            if (!hasPendingInput)
            {
                return;
            }

            stepsText = pendingSteps;
            sleepText = pendingSleepHours;
            hasPendingInput = false;
        }

        ApplyInput(stepsText, sleepText);
    }

    private void OnDestroy()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        RemoveNativeControls();
#endif
    }

    private void ApplyInput(string stepsText, string sleepText)
    {
        TryFindHealthController();

        if (healthController == null)
        {
            SetNativeResult("健康状態コントローラーが見つかりません。");
            return;
        }

        if (!TryParseNonNegativeInt(stepsText, out int steps))
        {
            SetNativeResult("歩数には0以上の整数を入力してください。");
            return;
        }

        if (!TryParseNonNegativeFloat(sleepText, out float sleepHours))
        {
            SetNativeResult("睡眠時間には0以上の数値を入力してください。");
            return;
        }

        if (sleepHours > 24f)
        {
            SetNativeResult("睡眠時間は24時間以下で入力してください。");
            return;
        }

        healthController.SetHealthData(steps, sleepHours);
        string result = GetCurrentResult();
        SetNativeResult(result);
        Debug.Log($"[HealthBehaviorNativePhoneUI] {result}");
    }

    private void TryFindHealthController()
    {
        if (healthController != null)
        {
            return;
        }

        healthController = FindAnyObjectByType<HealthBehaviorAnimationController>(FindObjectsInactive.Include);
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

#if UNITY_ANDROID && !UNITY_EDITOR
    private void CreateNativeControls()
    {
        using AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

        int initialSteps = healthController != null ? healthController.Steps : 0;
        float initialSleepHours = healthController != null ? healthController.SleepHours : 0f;

        activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
        {
            try
            {
                BuildNativeControls(initialSteps, initialSleepHours);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
            }
        }));
    }

    private void BuildNativeControls(int initialSteps, float initialSleepHours)
    {
        using AndroidJavaObject window = activity.Call<AndroidJavaObject>("getWindow");
        using AndroidJavaObject decorView = window.Call<AndroidJavaObject>("getDecorView");
        using AndroidJavaObject rootView = decorView.Call<AndroidJavaObject>("getRootView");

        nativePanel = new AndroidJavaObject("android.widget.LinearLayout", activity);
        nativePanel.Call("setOrientation", 1);
        nativePanel.Call("setPadding", Dp(16), Dp(14), Dp(16), Dp(16));
        nativePanel.Call("setBackgroundColor", unchecked((int)0xe61a2029));
        nativePanel.Call("setElevation", (float)Dp(16));

        using AndroidJavaObject title = CreateTextView("健康データ入力", 20f, unchecked((int)0xffffffff));
        nativePanel.Call("addView", title, CreateLinearLayoutParams(-1, Dp(44)));

        stepsInput = CreateEditText("歩数 例: 8000", initialSteps.ToString(CultureInfo.InvariantCulture), false);
        nativePanel.Call("addView", stepsInput, CreateLinearLayoutParams(-1, Dp(56)));

        sleepHoursInput = CreateEditText("睡眠時間 例: 7.5", initialSleepHours.ToString("0.##", CultureInfo.InvariantCulture), true);
        nativePanel.Call("addView", sleepHoursInput, CreateLinearLayoutParams(-1, Dp(56)));

        applyButton = new AndroidJavaObject("android.widget.Button", activity);
        applyButton.Call("setText", "反映");
        applyButton.Call("setTextSize", 18f);
        applyButton.Call("setAllCaps", false);
        applyButton.Call("setOnClickListener", new NativeApplyClickListener(this));
        nativePanel.Call("addView", applyButton, CreateLinearLayoutParams(-1, Dp(56)));

        resultText = CreateTextView(GetCurrentResult(), 15f, unchecked((int)0xffffffff));
        resultText.Call("setPadding", 0, Dp(8), 0, 0);
        nativePanel.Call("addView", resultText, CreateLinearLayoutParams(-1, Dp(82)));

        int gravityTopStart = 0x00000030 | 0x00000003;
        using AndroidJavaObject panelParams = new AndroidJavaObject("android.widget.FrameLayout$LayoutParams", Dp(340), -2);
        panelParams.Set("gravity", gravityTopStart);
        panelParams.Set("topMargin", Dp(24));
        panelParams.Set("leftMargin", Dp(16));
        rootView.Call("addView", nativePanel, panelParams);
        nativePanel.Call("bringToFront");
    }

    private AndroidJavaObject CreateEditText(string hint, string initialValue, bool allowDecimal)
    {
        AndroidJavaObject input = new AndroidJavaObject("android.widget.EditText", activity);
        input.Call("setHint", hint);
        input.Call("setText", initialValue);
        input.Call("setTextSize", 18f);
        input.Call("setSingleLine", true);
        input.Call("setTextColor", unchecked((int)0xff111111));
        input.Call("setHintTextColor", unchecked((int)0xff666666));
        input.Call("setBackgroundColor", unchecked((int)0xffffffff));

        const int typeClassNumber = 0x00000002;
        const int typeNumberFlagDecimal = 0x00002000;
        int inputType = allowDecimal ? typeClassNumber | typeNumberFlagDecimal : typeClassNumber;
        input.Call("setInputType", inputType);
        return input;
    }

    private AndroidJavaObject CreateTextView(string value, float textSize, int textColor)
    {
        AndroidJavaObject text = new AndroidJavaObject("android.widget.TextView", activity);
        text.Call("setText", value);
        text.Call("setTextSize", textSize);
        text.Call("setTextColor", textColor);
        return text;
    }

    private AndroidJavaObject CreateLinearLayoutParams(int width, int height)
    {
        AndroidJavaObject layoutParams = new AndroidJavaObject("android.widget.LinearLayout$LayoutParams", width, height);
        layoutParams.Call("setMargins", 0, Dp(4), 0, Dp(4));
        return layoutParams;
    }

    private int Dp(int value)
    {
        using AndroidJavaObject resources = activity.Call<AndroidJavaObject>("getResources");
        using AndroidJavaObject metrics = resources.Call<AndroidJavaObject>("getDisplayMetrics");
        float density = metrics.Get<float>("density");
        return Mathf.RoundToInt(value * density);
    }

    private string GetCurrentResult()
    {
        TryFindHealthController();

        if (healthController == null)
        {
            return "健康状態コントローラーが見つかりません。";
        }

        return
            $"歩数: {healthController.StepRankSymbol}\n" +
            $"睡眠時間: {healthController.SleepRankSymbol}\n" +
            $"再生中: {healthController.CurrentBehaviorName}";
    }

    private void QueueNativeInput()
    {
        string stepsText = ReadJavaText(stepsInput);
        string sleepText = ReadJavaText(sleepHoursInput);

        lock (pendingInputLock)
        {
            pendingSteps = stepsText;
            pendingSleepHours = sleepText;
            hasPendingInput = true;
        }

        HideKeyboard();
    }

    private static string ReadJavaText(AndroidJavaObject input)
    {
        if (input == null)
        {
            return string.Empty;
        }

        using AndroidJavaObject editable = input.Call<AndroidJavaObject>("getText");
        return editable?.Call<string>("toString") ?? string.Empty;
    }

    private void HideKeyboard()
    {
        using AndroidJavaObject inputMethodManager = activity.Call<AndroidJavaObject>("getSystemService", "input_method");
        using AndroidJavaObject windowToken = applyButton.Call<AndroidJavaObject>("getWindowToken");
        inputMethodManager?.Call<bool>("hideSoftInputFromWindow", windowToken, 0);
        applyButton.Call("clearFocus");
    }

    private void SetNativeResult(string message)
    {
        if (activity == null || resultText == null)
        {
            return;
        }

        activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
        {
            resultText?.Call("setText", message);
        }));
    }

    private void RemoveNativeControls()
    {
        if (activity == null || nativePanel == null)
        {
            return;
        }

        activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
        {
            using AndroidJavaObject parent = nativePanel.Call<AndroidJavaObject>("getParent");
            parent?.Call("removeView", nativePanel);
            stepsInput = null;
            sleepHoursInput = null;
            applyButton = null;
            resultText = null;
            nativePanel = null;
        }));
    }

    private sealed class NativeApplyClickListener : AndroidJavaProxy
    {
        private readonly HealthBehaviorNativePhoneUI owner;

        public NativeApplyClickListener(HealthBehaviorNativePhoneUI owner)
            : base("android.view.View$OnClickListener")
        {
            this.owner = owner;
        }

        public void onClick(AndroidJavaObject view)
        {
            owner.QueueNativeInput();
        }
    }
#else
    private string GetCurrentResult()
    {
        TryFindHealthController();

        if (healthController == null)
        {
            return "健康状態コントローラーが見つかりません。";
        }

        return
            $"歩数: {healthController.StepRankSymbol}\n" +
            $"睡眠時間: {healthController.SleepRankSymbol}\n" +
            $"再生中: {healthController.CurrentBehaviorName}";
    }

    private void SetNativeResult(string message)
    {
        Debug.Log($"[HealthBehaviorNativePhoneUI] {message}");
    }
#endif
}
