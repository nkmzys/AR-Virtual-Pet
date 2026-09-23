using System.Collections;
using UnityEngine;

public class HapticController : MonoBehaviour
{
    [SerializeField] private bool vibrateOnStart = true;
    [SerializeField, Min(0f)] private float startDelaySeconds = 1f;

    private IEnumerator Start()
    {
        if (!vibrateOnStart)
        {
            yield break;
        }

        if (startDelaySeconds > 0f)
        {
            yield return new WaitForSeconds(startDelaySeconds);
        }

        VibrateOnce();
    }

    [ContextMenu("Vibrate Once")]
    public void VibrateOnce()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
          Handheld.Vibrate();
#else

#endif
    }
}