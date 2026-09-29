using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
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

    [Header("Behavior Transition")]
    [SerializeField] private AnimationClip transitionIdleClip;
    [SerializeField, Min(0.05f)] private float blendSeconds = 0.35f;
    [SerializeField, Min(0f)] private float idlePauseSeconds = 0.5f;
    [SerializeField, Min(0f)] private float maximumBoundaryWait = 2f;

    private enum TransitionPhase { Stable, Waiting, ToIdle, Idle, ToBehavior }
    private TransitionPhase phase;
    private PlayableGraph graph;
    private AnimationMixerPlayable mixer;
    private AnimatorControllerPlayable playing;
    private AnimationClipPlayable idlePlayable;
    private RuntimeAnimatorController requestedController;
    private string requestedBehaviorName;
    private float phaseTime;
    private float boundary;
    private int boundaryState;
    public bool IsTransitioning => phase != TransitionPhase.Stable;

    [Header("Debug")]
    [SerializeField] private HealthRank stepRank;
    [SerializeField] private HealthRank sleepRank;
    [SerializeField] private string currentBehaviorName;

    private int lastSteps = -1;
    private float lastSleepHours = -1f;
    private RuntimeAnimatorController lastAppliedController;
    private bool hasWarnedAboutMissingAnimator;
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
        if (targetAnimator == null)
        {
            targetAnimator = GetComponentInChildren<Animator>(true);
        }

#if UNITY_EDITOR
        AutoAssignControllersInEditor();
#endif
    }

    private void Awake()
    {
        EnsureAnimator();
        ApplyBehavior();
    }

    private void OnEnable()
    {
        ApplyBehavior();
    }

    private void Update()
    {
        if (steps != lastSteps || !Mathf.Approximately(sleepHours, lastSleepHours))
            ApplyBehavior();
        UpdateTransition();
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

    public void SetHealthData(int newSteps, float newSleepHours)
    {
        steps = Mathf.Max(0, newSteps);
        sleepHours = Mathf.Max(0f, newSleepHours);
        ApplyBehavior();
    }

    private void ApplyBehavior()
    {
        string previousName = currentBehaviorName;
        requestedController = UpdateRanksAndController();
        requestedBehaviorName = currentBehaviorName;
        currentBehaviorName = previousName;
        lastSteps = steps;
        lastSleepHours = sleepHours;
        if (!isActiveAndEnabled || requestedController == null) return;

        EnsureAnimator();
        if (!graph.IsValid())
        {
            targetAnimator.enabled = true;
            targetAnimator.speed = 1f;
            targetAnimator.updateMode = AnimatorUpdateMode.Normal;
            targetAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            targetAnimator.applyRootMotion = false;
            // The graph owns playback; preserve each controller's internal state machine.
            targetAnimator.runtimeAnimatorController = null;
            graph = PlayableGraph.Create("Health behavior transitions");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            mixer = AnimationMixerPlayable.Create(graph, 2);
            var output = AnimationPlayableOutput.Create(graph, "Dog", targetAnimator);
            output.SetSourcePlayable(mixer);
            ConnectBehavior();
            if (transitionIdleClip != null)
            {
                idlePlayable = AnimationClipPlayable.Create(graph, transitionIdleClip);
                idlePlayable.SetApplyFootIK(false);
                graph.Connect(idlePlayable, 0, mixer, 1);
            }
            graph.Play();
            graph.Evaluate(0f);
        }
        RequestTransition();
    }

    private void ConnectBehavior()
    {
        if (playing.IsValid())
        {
            graph.Disconnect(mixer, 0);
            graph.DestroyPlayable(playing);
        }
        playing = AnimatorControllerPlayable.Create(graph, requestedController);
        graph.Connect(playing, 0, mixer, 0);
        mixer.SetInputWeight(0, 1f);
        mixer.SetInputWeight(1, 0f);
        lastAppliedController = requestedController;
        currentBehaviorName = requestedBehaviorName;
    }

    private void RequestTransition()
    {
        if (phase != TransitionPhase.Stable || requestedController == lastAppliedController) return;
        var state = playing.GetCurrentAnimatorStateInfo(0);
        boundary = Mathf.Floor(state.normalizedTime) + 1f;
        boundaryState = state.fullPathHash;
        SetPhase(TransitionPhase.Waiting);
    }

    private void SetPhase(TransitionPhase next)
    {
        phase = next;
        phaseTime = 0f;
    }

    private void UpdateTransition()
    {
        if (!graph.IsValid()) return;
        phaseTime += Time.deltaTime;
        float blend = Mathf.Clamp01(phaseTime / Mathf.Max(0.05f, blendSeconds));
        switch (phase)
        {
            case TransitionPhase.Stable:
                RequestTransition();
                break;
            case TransitionPhase.Waiting:
                // A repeated Apply always uses the latest input; same behavior does not restart.
                if (requestedController == lastAppliedController)
                {
                    SetPhase(TransitionPhase.Stable);
                    break;
                }
                var state = playing.GetCurrentAnimatorStateInfo(0);
                bool atBoundary = state.fullPathHash != boundaryState || state.normalizedTime >= boundary ||
                    (!state.loop && state.normalizedTime >= 1f) || playing.IsInTransition(0);
                if (atBoundary || phaseTime >= maximumBoundaryWait)
                {
                    if (!idlePlayable.IsValid())
                    {
                        // Older scenes without an assigned bridge clip retain functional switching.
                        ConnectBehavior();
                        SetPhase(TransitionPhase.Stable);
                        break;
                    }
                    idlePlayable.SetTime(0);
                    SetPhase(TransitionPhase.ToIdle);
                }
                break;
            case TransitionPhase.ToIdle:
                mixer.SetInputWeight(0, 1f - blend);
                mixer.SetInputWeight(1, blend);
                if (blend >= 1f) SetPhase(TransitionPhase.Idle);
                break;
            case TransitionPhase.Idle:
                if (phaseTime >= idlePauseSeconds)
                {
                    ConnectBehavior();
                    mixer.SetInputWeight(0, 0f);
                    mixer.SetInputWeight(1, 1f);
                    SetPhase(TransitionPhase.ToBehavior);
                }
                break;
            case TransitionPhase.ToBehavior:
                mixer.SetInputWeight(0, blend);
                mixer.SetInputWeight(1, 1f - blend);
                if (blend >= 1f) SetPhase(TransitionPhase.Stable);
                break;
        }
    }

    private void OnDisable()
    {
        if (graph.IsValid()) graph.Destroy();
        phase = TransitionPhase.Stable;
        lastAppliedController = null;
    }

    private void EnsureAnimator()
    {
        if (targetAnimator == null)
        {
            targetAnimator = GetComponent<Animator>();
        }

        if (targetAnimator == null)
        {
            targetAnimator = GetComponentInChildren<Animator>(true);
        }

        if (targetAnimator == null)
        {
            targetAnimator = gameObject.AddComponent<Animator>();
            Debug.Log("[HealthBehaviorAnimationController] Added missing Animator to health behavior dog root.");
        }

        if (targetAnimator == null && !hasWarnedAboutMissingAnimator)
        {
            hasWarnedAboutMissingAnimator = true;
            Debug.LogWarning("[HealthBehaviorAnimationController] Animator was not found, so health behavior animation cannot play.");
        }
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
        string[] guids = AssetDatabase.FindAssets(
            $"{controllerName} t:AnimatorController",
            new[] { "Assets/Dog_Pomeranian/Animator" });
        if (guids.Length == 0)
        {
            return null;
        }

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(path);
    }
#endif
}

