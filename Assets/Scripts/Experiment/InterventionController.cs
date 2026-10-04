using System;
using UnityEngine;

namespace Experiment1
{
    public enum InterventionState { Waiting, Prompt, Exercise, Finish }

    public sealed class InterventionController : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float finishDisplaySeconds = 3f;
        public InterventionState State { get; private set; } = InterventionState.Waiting;
        public event Action<InterventionState> StateChanged;
        public event Action<string, DateTimeOffset> ExperimentEvent;
        private double finishDeadline;
        private bool waitingWasShown;

        [ContextMenu("Show Notification (Local Preview)")]
        public void ShowNotification()
        {
            if (!Application.isPlaying) return;
            Record("notification_simulated");
            TryShowPrompt();
        }

        // The future receiver passes its receive time, rather than a later UI time.
        public void ReceiveNotification(DateTimeOffset receivedAt)
        {
            if (!Application.isPlaying) return;
            ExperimentEvent?.Invoke("notification_received", receivedAt);
            TryShowPrompt();
        }

        private void TryShowPrompt()
        {
            if (State == InterventionState.Waiting) ChangeState(InterventionState.Prompt);
            else Record("notification_ignored_busy");
        }

        public void Accept()
        {
            if (State != InterventionState.Prompt) return;
            Record("accepted");
            ChangeState(InterventionState.Exercise);
        }

        public void Decline()
        {
            if (State != InterventionState.Prompt) return;
            Record("declined");
            ChangeState(InterventionState.Finish);
        }

        public void Complete()
        {
            if (State != InterventionState.Exercise) return;
            Record("exercise_completed");
            ChangeState(InterventionState.Finish);
        }

        // Called after the UI has applied panel visibility (not a display refresh timestamp).
        public void ReportStateShown(InterventionState shown)
        {
            if (shown != State) return;
            switch (shown)
            {
                case InterventionState.Waiting:
                    Record(waitingWasShown ? "waiting_returned" : "waiting_shown");
                    waitingWasShown = true;
                    break;
                case InterventionState.Prompt: Record("prompt_shown"); break;
                case InterventionState.Exercise: Record("exercise_shown"); break;
                case InterventionState.Finish: Record("finish_shown"); break;
            }
        }

        private void Record(string name) => ExperimentEvent?.Invoke(name, DateTimeOffset.Now);

        private void Update()
        {
            if (State == InterventionState.Finish && Time.realtimeSinceStartupAsDouble >= finishDeadline)
                ChangeState(InterventionState.Waiting);
        }

        private void ChangeState(InterventionState next)
        {
            State = next;
            if (next == InterventionState.Finish)
                finishDeadline = Time.realtimeSinceStartupAsDouble + finishDisplaySeconds;
            StateChanged?.Invoke(next);
        }
    }
}
