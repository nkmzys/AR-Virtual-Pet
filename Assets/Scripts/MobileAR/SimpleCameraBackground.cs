using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class SimpleCameraBackground : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private bool preferRearCamera = true;
    [SerializeField] private int requestedWidth = 1280;
    [SerializeField] private int requestedHeight = 720;
    [SerializeField] private int requestedFps = 30;

    private WebCamTexture webcamTexture;
    private Renderer targetRenderer;
    private Coroutine startRoutine;

    private void Awake()
    {
        targetRenderer = GetComponent<Renderer>();
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
    }

    private void OnEnable()
    {
        startRoutine = StartCoroutine(StartCameraWhenReady());
    }

    private void OnDisable()
    {
        if (startRoutine != null)
        {
            StopCoroutine(startRoutine);
            startRoutine = null;
        }

        StopCamera();
    }

    private void Update()
    {
        FitToCamera();
        ApplyVideoOrientation();
    }

    private IEnumerator StartCameraWhenReady()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
        {
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Camera);
            float timeout = Time.realtimeSinceStartup + 8f;
            while (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera) &&
                   Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }
        }
#endif

        yield return null;
        StartCamera();
    }

    private void StartCamera()
    {
        if (webcamTexture != null)
        {
            return;
        }

        string deviceName = FindCameraDeviceName();
        webcamTexture = string.IsNullOrEmpty(deviceName)
            ? new WebCamTexture(requestedWidth, requestedHeight, requestedFps)
            : new WebCamTexture(deviceName, requestedWidth, requestedHeight, requestedFps);

        targetRenderer.material.mainTexture = webcamTexture;
        webcamTexture.Play();
    }

    private void StopCamera()
    {
        if (webcamTexture == null)
        {
            return;
        }

        if (webcamTexture.isPlaying)
        {
            webcamTexture.Stop();
        }

        Destroy(webcamTexture);
        webcamTexture = null;
    }

    private string FindCameraDeviceName()
    {
        WebCamDevice[] devices = WebCamTexture.devices;
        if (devices == null || devices.Length == 0)
        {
            Debug.LogWarning("[SimpleCameraBackground] No WebCamTexture devices found.");
            return null;
        }

        foreach (WebCamDevice device in devices)
        {
            if (preferRearCamera && !device.isFrontFacing)
            {
                return device.name;
            }

            if (!preferRearCamera && device.isFrontFacing)
            {
                return device.name;
            }
        }

        return devices[0].name;
    }

    private void FitToCamera()
    {
        if (targetCamera == null)
        {
            return;
        }

        float distance = Vector3.Distance(targetCamera.transform.position, transform.position);
        float height = 2f * distance * Mathf.Tan(targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float width = height * targetCamera.aspect;
        transform.localScale = new Vector3(width, height, 1f);
    }

    private void ApplyVideoOrientation()
    {
        if (webcamTexture == null || !webcamTexture.isPlaying)
        {
            return;
        }

        Vector2 scale = Vector2.one;
        if (webcamTexture.videoVerticallyMirrored)
        {
            scale.y = -1f;
        }

        targetRenderer.material.mainTextureScale = scale;
        targetRenderer.material.mainTextureOffset = new Vector2(
            scale.x < 0f ? 1f : 0f,
            scale.y < 0f ? 1f : 0f);

        transform.localRotation = Quaternion.Euler(0f, 180f, -webcamTexture.videoRotationAngle);
    }
}
