using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class SimpleCameraBackgroundUI : MonoBehaviour
{
    [SerializeField] private bool preferRearCamera = true;
    [SerializeField] private int requestedWidth = 1280;
    [SerializeField] private int requestedHeight = 720;
    [SerializeField] private int requestedFps = 30;
    [SerializeField] private bool renderBehindSceneObjects = true;
    [SerializeField] private float backgroundPlaneDistance = 12f;

    private RawImage rawImage;
    private RectTransform rectTransform;
    private Canvas parentCanvas;
    private WebCamTexture webcamTexture;
    private Coroutine startRoutine;

    public WebCamTexture CurrentTexture => webcamTexture;
    public bool IsCameraReady => webcamTexture != null && webcamTexture.isPlaying && webcamTexture.width > 16 && webcamTexture.height > 16;

    private void Awake()
    {
        rawImage = GetComponent<RawImage>();
        rectTransform = GetComponent<RectTransform>();
        parentCanvas = GetComponentInParent<Canvas>();
        rawImage.raycastTarget = false;
        ConfigureBackgroundCanvas();
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
        ConfigureBackgroundCanvas();
        UpdateImageLayout();
    }

    private void ConfigureBackgroundCanvas()
    {
        if (!renderBehindSceneObjects || parentCanvas == null)
        {
            return;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return;
        }

        if (parentCanvas.renderMode != RenderMode.ScreenSpaceCamera)
        {
            parentCanvas.renderMode = RenderMode.ScreenSpaceCamera;
        }

        parentCanvas.worldCamera = mainCamera;
        parentCanvas.planeDistance = backgroundPlaneDistance;
        parentCanvas.sortingOrder = -100;
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

        rawImage.texture = webcamTexture;
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
            Debug.LogWarning("[SimpleCameraBackgroundUI] No WebCamTexture devices found.");
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

    private void UpdateImageLayout()
    {
        if (!IsCameraReady)
        {
            return;
        }

        RectTransform parent = rectTransform.parent as RectTransform;
        if (parent == null)
        {
            return;
        }

        Rect parentRect = parent.rect;
        float parentWidth = parentRect.width;
        float parentHeight = parentRect.height;
        if (parentWidth <= 0f || parentHeight <= 0f)
        {
            return;
        }

        int rotation = webcamTexture.videoRotationAngle;
        bool rotatedSideways = rotation == 90 || rotation == 270;
        float videoWidth = webcamTexture.width;
        float videoHeight = webcamTexture.height;

        float displayedVideoWidth = rotatedSideways ? videoHeight : videoWidth;
        float displayedVideoHeight = rotatedSideways ? videoWidth : videoHeight;
        float coverScale = Mathf.Max(parentWidth / displayedVideoWidth, parentHeight / displayedVideoHeight);

        float rectWidth = videoWidth * coverScale;
        float rectHeight = videoHeight * coverScale;
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = new Vector2(rectWidth, rectHeight);
        rectTransform.localEulerAngles = new Vector3(0f, 0f, -rotation);

        Rect uv = new Rect(0f, 0f, 1f, 1f);
        if (webcamTexture.videoVerticallyMirrored)
        {
            uv = new Rect(0f, 1f, 1f, -1f);
        }

        rawImage.uvRect = uv;
    }
}
