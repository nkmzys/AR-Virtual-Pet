using System;
using UnityEngine;

namespace Experiment1
{
    public sealed class NotificationCommandRouter : MonoBehaviour
    {
        [SerializeField] private NotificationReceiver receiver;
        [SerializeField] private InterventionController controller;

        private void OnEnable()
        {
            if (receiver == null) receiver = GetComponent<NotificationReceiver>();
            if (controller == null) controller = GetComponent<InterventionController>();
            if (receiver == null || controller == null)
            {
                Debug.LogError("[NotificationCommandRouter] Receiver or controller missing.", this);
                return;
            }
            receiver.CommandReceived += HandleCommand;
        }

        private void OnDisable()
        {
            if (receiver != null) receiver.CommandReceived -= HandleCommand;
        }

        private void HandleCommand(string command, DateTimeOffset receivedAt)
        {
            switch (command)
            {
                case "SHOW_NOTIFICATION": controller.ReceiveNotification(receivedAt); break;
                // Add condition-specific handlers here or subscribe to CommandReceived in an adapter.
                case "SHOW_DOG":
                case "PLAY_DOG_HAPPY":
                case "HIDE_ALL":
                    Debug.LogWarning("[NotificationCommandRouter] Not implemented: " + command, this);
                    break;
                default:
                    Debug.LogWarning("[NotificationCommandRouter] Unknown command: " + command, this);
                    break;
            }
        }
    }
}
