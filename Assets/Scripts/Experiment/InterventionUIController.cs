using UnityEngine;
using UnityEngine.UI;

namespace Experiment1
{
    public sealed class InterventionUIController : MonoBehaviour
    {
        [SerializeField] private InterventionController controller;
        [SerializeField] private GameObject waitingPanel;
        [SerializeField] private GameObject promptPanel;
        [SerializeField] private GameObject exercisePanel;
        [SerializeField] private GameObject finishPanel;
        [SerializeField] private Button acceptButton;
        [SerializeField] private Button declineButton;
        [SerializeField] private Button completeButton;
        private Font runtimeFont;

        private void Awake()
        {
            // Japanese glyphs are provided by the OS font in this initial preview.
            runtimeFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Yu Gothic", "Meiryo", "Noto Sans CJK JP", "sans-serif" }, 32);
            if (runtimeFont != null)
                foreach (Text label in GetComponentsInChildren<Text>(true)) label.font = runtimeFont;
        }

        private void OnEnable()
        {
            if (controller == null) { Debug.LogError("InterventionController is missing.", this); return; }
            controller.StateChanged += ShowState;
            acceptButton.onClick.AddListener(controller.Accept);
            declineButton.onClick.AddListener(controller.Decline);
            completeButton.onClick.AddListener(controller.Complete);
            ShowState(controller.State);
        }

        private void OnDisable()
        {
            if (controller == null) return;
            controller.StateChanged -= ShowState;
            acceptButton.onClick.RemoveListener(controller.Accept);
            declineButton.onClick.RemoveListener(controller.Decline);
            completeButton.onClick.RemoveListener(controller.Complete);
        }

        private void OnDestroy()
        {
            if (runtimeFont != null) Destroy(runtimeFont);
        }

        private void ShowState(InterventionState state)
        {
            waitingPanel.SetActive(state == InterventionState.Waiting);
            promptPanel.SetActive(state == InterventionState.Prompt);
            exercisePanel.SetActive(state == InterventionState.Exercise);
            finishPanel.SetActive(state == InterventionState.Finish);
            controller.ReportStateShown(state);
        }
    }
}
