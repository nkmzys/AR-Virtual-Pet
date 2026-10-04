using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Experiment1
{
    // Subscribe before the Canvas first displays Waiting. No AR or UI dependencies.
    [DefaultExecutionOrder(-100)]
    public sealed class ExperimentLogger : MonoBehaviour
    {
        [SerializeField] private InterventionController controller;
        public string LogFilePath { get; private set; }
        public bool IsLogging => writer != null;
        private StreamWriter writer;

        private void OnEnable()
        {
            if (controller == null) controller = GetComponent<InterventionController>();
            if (controller == null)
            {
                Debug.LogError("[ExperimentLogger] Missing InterventionController; CSV logging is unavailable.", this);
                return;
            }
            try
            {
                string directory = Path.Combine(Application.persistentDataPath, "Experiment1", "Logs");
                Directory.CreateDirectory(directory);
                string fileName = "experiment1_" + DateTimeOffset.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture)
                    + "_" + Guid.NewGuid().ToString("N") + ".csv";
                LogFilePath = Path.Combine(directory, fileName);
                writer = new StreamWriter(new FileStream(LogFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read),
                    new UTF8Encoding(false));
                writer.AutoFlush = true;
                writer.WriteLine("timestamp,event");
                controller.ExperimentEvent += WriteEvent;
                Debug.Log("[ExperimentLogger] CSV: " + LogFilePath, this);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                CloseWriter();
                Debug.LogError("[ExperimentLogger] Cannot create CSV: " + exception.Message, this);
            }
        }

        private void WriteEvent(string name, DateTimeOffset timestamp)
        {
            if (writer == null) return;
            try
            {
                writer.WriteLine(timestamp.ToString("O", CultureInfo.InvariantCulture) + "," + EscapeCsv(name));
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                CloseWriter();
                Debug.LogError("[ExperimentLogger] CSV write failed; further logging stopped: " + exception.Message, this);
            }
        }

        private static string EscapeCsv(string value)
        {
            if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0) return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private void OnDisable()
        {
            if (controller != null) controller.ExperimentEvent -= WriteEvent;
            CloseWriter();
        }

        private void CloseWriter()
        {
            var closing = writer;
            writer = null;
            if (closing == null) return;
            try { closing.Dispose(); }
            catch (IOException exception) { Debug.LogError("[ExperimentLogger] CSV close failed: " + exception.Message, this); }
        }
    }
}
