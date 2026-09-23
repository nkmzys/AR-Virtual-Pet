using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class HealthBehaviorAnimationController : MonoBehaviour
{
    [Header("Animator")]
    [SerializeField] private Animator targetAnimator;

    [Header("Temporary Health Input")]
    [SerializeField, Min(0)] private int steps;
    [SerializeField, Min(0f)] private float sleepHours;

    [Header("Behavior Animator Controllers")]
    [SerializeField] private RuntimeAnimatorController walkRequestController;
    [SerializeField] private RuntimeAnimatorController sleepyController;
    [SerializeField] private RuntimeAnimatorController idleController;
    [SerializeField] private RuntimeAnimatorController happyController;
    [SerializeField] private RuntimeAnimatorController veryHappyController;

    [Header("Debug")]
    [SerializeField] private HealthRank stepRank;
    [SerializeField] private HealthRank sleepRank;
    [SerializeField] private string currentBehaviorName;

    private int lastSteps = -1;
    private float lastSleepHours = -1f;
    private RuntimeAnimatorController lastAppliedController;

    private enum HealthRank
    {
        Bad,
        Okay,
        Good,
    }

    public int Steps => steps;
    public float SleepHours => sleepHours;
    public string StepRankSymbol => ToSymbol(stepRank);
    public string SleepRankSymbol => ToSymbol(sleepRank);
    public string CurrentBehaviorName => currentBehaviorName;

    private void Reset()
    {
        targetAnimator = GetComponent<Animator>();
#if UNITY_EDITOR
        AutoAssignControllersInEditor();
#endif
    }

    private void Awake()
    {
        if (targetAnimator == null)
        {
            targetAnimator = GetComponent<Animator>();
        }

        ApplyBehavior();
    }

    private void Update()
    {
        if (steps == lastSteps && Mathf.Approximately(sleepHours, lastSleepHours))
        {
            return;
        }

        ApplyBehavior();
    }

    private void OnValidate()
    {
        steps = Mathf.Max(0, steps);
        sleepHours = Mathf.Max(0f, sleepHours);

        if (Application.isPlaying)
        {
            ApplyBehavior();
        }
        else
        {
            UpdateRanksAndController();
#if UNITY_EDITOR
            AutoAssignControllersInEditor();
#endif
        }
    }

    private void ApplyBehavior()
    {
        RuntimeAnimatorController selectedController = UpdateRanksAndController();

        lastSteps = steps;
        lastSleepHours = sleepHours;

        if (targetAnimator == null || selectedController == null)
        {
            return;
        }

        if (lastAppliedController == selectedController && targetAnimator.runtimeAnimatorController == selectedController)
        {
            return;
        }

        targetAnimator.runtimeAnimatorController = selectedController;
        targetAnimator.Rebind();
        targetAnimator.Update(0f);
        lastAppliedController = selectedController;
    }

    public void SetHealthData(int newSteps, float newSleepHours)
    {
        steps = Mathf.Max(0, newSteps);
        sleepHours = Mathf.Max(0f, newSleepHours);
        ApplyBehavior();
    }

    private RuntimeAnimatorController UpdateRanksAndController()
    {
        stepRank = EvaluateSteps(steps);
        sleepRank = EvaluateSleep(sleepHours);
        return SelectController(stepRank, sleepRank);
    }

    private HealthRank EvaluateSteps(int value)
    {
        if (value < 6000)
        {
            return HealthRank.Bad;
        }

        if (value <= 8000)
        {
            return HealthRank.Okay;
        }

        return HealthRank.Good;
    }

    private HealthRank EvaluateSleep(float value)
    {
        if (value < 5.25f)
        {
            return HealthRank.Bad;
        }

        if (value < 7f)
        {
            return HealthRank.Okay;
        }

        return HealthRank.Good;
    }

    private RuntimeAnimatorController SelectController(HealthRank step, HealthRank sleep)
    {
        if (step == HealthRank.Bad)
        {
            currentBehaviorName = "my_pome_walk_request";
            return walkRequestController;
        }

        if (sleep == HealthRank.Bad)
        {
            currentBehaviorName = "my_pome_sleepy";
            return sleepyController;
        }

        if (step == HealthRank.Good && sleep == HealthRank.Good)
        {
            currentBehaviorName = "my_pome_very_happy";
            return veryHappyController;
        }

        if (step == HealthRank.Good || sleep == HealthRank.Good)
        {
            currentBehaviorName = "my_pome_happy";
            return happyController;
        }

        currentBehaviorName = "my_pome_idle";
        return idleController;
    }

    private static string ToSymbol(HealthRank rank)
    {
        return rank switch
        {
            HealthRank.Bad => "×",
            HealthRank.Okay => "△",
            HealthRank.Good => "○",
            _ => "-",
        };
    }

#if UNITY_EDITOR
    private void AutoAssignControllersInEditor()
    {
        walkRequestController ??= FindControllerAsset("my_pome_walk_request");
        sleepyController ??= FindControllerAsset("my_pome_sleepy");
        idleController ??= FindControllerAsset("my_pome_idle");
        happyController ??= FindControllerAsset("my_pome_happy");
        veryHappyController ??= FindControllerAsset("my_pome_very_happy");
    }

    private static RuntimeAnimatorController FindControllerAsset(string controllerName)
    {
        string[] guids = AssetDatabase.FindAssets($"{controllerName} t:AnimatorController", new[] { "Assets/Dog_Pomeranian/Animator" });
        if (guids.Length == 0)
        {
            return null;
        }

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(path);
    }
#endif
}
