using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SimpleMarkerDogDetector : MonoBehaviour
{
    [Header("Camera")]
    [SerializeField] private SimpleCameraBackgroundUI cameraBackground;

    [Header("Dog")]
    [SerializeField] private GameObject dogRoot;
    [SerializeField] private Transform dogTransform;
    [SerializeField] private Camera sceneCamera;
    [SerializeField] private Vector3 baseDogPosition = new Vector3(0f, -0.55f, 2.2f);
    [SerializeField] private Vector3 markerDogScale = new Vector3(2.2f, 2.2f, 2.2f);
    [Tooltip("Offset from the detected placement in world units. Positive Y raises the dog. Can be adjusted live in Play mode.")]
    [SerializeField] private Vector3 dogPositionOffset = Vector3.zero;
    [Tooltip("Positive values tilt the whole dog forward toward the viewer. Does not change the animation bones.")]
    [SerializeField, Range(-60f, 60f)] private float forwardTiltDegrees;
    [SerializeField] private bool hideDogUntilMarkerFound = true;

#if UNITY_EDITOR
    [Header("Editor Preview (Play Mode Only)")]
    [Tooltip("Show the animated dog without a marker in the Game view. Ignored in APK builds.")]
    [SerializeField] private bool previewWithoutMarker;
    private bool previewActive;
#endif

    [Header("Detection")]
    [SerializeField, Range(0.05f, 1f)] private float scanInterval = 0.18f;
    [SerializeField, Range(0f, 1f)] private float requiredConfidence = 0.50f;
    [SerializeField] private int missingFramesBeforeHide = 5;
    [SerializeField] private int minMarkerSize = 72;
    [SerializeField] private int maxMarkerSize = 520;
    [SerializeField] private int sizeStep = 28;
    [SerializeField] private int gridStep = 28;
    [SerializeField] private int consecutiveDetectionsBeforeShow = 1;
    [SerializeField, Range(0.1f, 1f)] private float guideDetectionMargin = 0.12f;
    [SerializeField, Range(0f, 1f)] private float minimumFinderCornerScore = 0.36f;
    [SerializeField, Range(0f, 1f)] private float minimumCenterWhiteRatio = 0.12f;
    [SerializeField, Range(0f, 1f)] private float minimumCenterBlackRatio = 0.04f;
    [SerializeField, Range(0f, 1f)] private float maximumCenterBlackRatio = 0.72f;

    [Header("Status")]
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private bool showScanningGuide = true;
    [SerializeField] private bool showDebugStatus = true;
    [SerializeField, Range(0.25f, 0.9f)] private float guideScreenWidth = 0.74f;
    [SerializeField] private Color guideColor = new Color(0.1f, 0.75f, 1f, 0.92f);
    [SerializeField] private Color detectedGuideColor = new Color(0.1f, 1f, 0.35f, 0.95f);

    private float nextScanTime;
    private int missingFrames;
    private int consecutiveDetections;
    private Color32[] pixels;
    private int width;
    private int height;
    private float bestConfidence;
    private bool markerVisible;
    private bool hasFoundMarker;
    private bool hasLockedDogTransform;
    private string cameraStatus = "Camera: not ready";
    private TMP_Text runtimeDebugText;
    private Image[] guideCorners;
    private GameObject guideRoot;
    private RectTransform guideRect;
    private Transform facingPivot;
    private Vector3 lockedMarkerPosition;
    private Transform nose;
    private Transform tail;
    private bool facingCalibrated;
    private Quaternion calibratedFacing = Quaternion.identity;
    private Vector3 tiltAxis = Vector3.right;
    private const bool KeepDogVisibleAfterFirstMarkerFound = true;

    private void Awake()
    {
        NormalizeDetectionSettings();

        if (sceneCamera == null)
        {
            sceneCamera = Camera.main;
        }

        if (dogTransform == null && dogRoot != null)
        {
            dogTransform = dogRoot.transform;
        }

        if (dogRoot != null && hideDogUntilMarkerFound)
        {
            dogRoot.SetActive(false);
        }

        if (dogTransform != null)
        {
            // Placement belongs outside the hierarchy controlled by the Animator.
            facingPivot = new GameObject("Mobile Dog Facing Pivot").transform;
            facingPivot.SetParent(dogTransform.parent, false);
            facingPivot.SetPositionAndRotation(dogTransform.position, Quaternion.identity);
            dogTransform.SetParent(facingPivot, true);
            foreach (Transform bone in dogTransform.GetComponentsInChildren<Transform>(true))
            {
                if (bone.name == "Bone_Nose") nose = bone;
                if (bone.name == "Bone_tail_00") tail = bone;
            }
        }

        SetStatus("Show dog marker to camera.");
        EnsureRuntimeOverlay();
    }

    private void NormalizeDetectionSettings()
    {
        requiredConfidence = Mathf.Min(requiredConfidence, 0.50f);
        minMarkerSize = Mathf.Min(minMarkerSize, 72);
        maxMarkerSize = Mathf.Max(maxMarkerSize, 520);
        sizeStep = Mathf.Min(sizeStep, 28);
        gridStep = Mathf.Min(gridStep, 28);
        missingFramesBeforeHide = Mathf.Clamp(missingFramesBeforeHide, 1, 2);
        consecutiveDetectionsBeforeShow = 1;
        minimumFinderCornerScore = Mathf.Min(minimumFinderCornerScore, 0.36f);
        minimumCenterWhiteRatio = Mathf.Min(minimumCenterWhiteRatio, 0.12f);
        minimumCenterBlackRatio = Mathf.Min(minimumCenterBlackRatio, 0.04f);
        maximumCenterBlackRatio = Mathf.Max(maximumCenterBlackRatio, 0.72f);
        if (markerDogScale.x <= 0f || markerDogScale.y <= 0f || markerDogScale.z <= 0f)
        {
            markerDogScale = new Vector3(2.2f, 2.2f, 2.2f);
        }
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (previewWithoutMarker)
        {
            if (!previewActive)
            {
                // Use the same placement and animation path as a centered marker.
                width = height = 2;
                ApplyMarkerResult(new DetectionResult(1, 1, 1, 1f));
                hasLockedDogTransform = true;
                previewActive = true;
                SetMarkerVisible(true, "Editor preview: adjust scale and forward tilt in Inspector.");
            }
            return;
        }
        if (previewActive)
        {
            previewActive = false;
            hasFoundMarker = false;
            hasLockedDogTransform = false;
            facingCalibrated = false;
            consecutiveDetections = 0;
            SetMarkerVisible(false, "Show dog marker to camera.");
        }
#endif
        if (Time.unscaledTime < nextScanTime)
        {
            UpdateGuideLayout();
            return;
        }

        UpdateGuideLayout();
        nextScanTime = Time.unscaledTime + scanInterval;
        ScanCameraFrame();
    }

    private void LateUpdate()
    {
        if (hasLockedDogTransform && facingPivot != null)
        {
            facingPivot.position = lockedMarkerPosition + dogPositionOffset;
        }
        // Sample the actual animated pose once, after the first Animator evaluation.
        // Later turns in the animation remain intact; no bones are overwritten.
        if (!facingCalibrated && hasLockedDogTransform && facingPivot != null &&
            dogTransform.gameObject.activeInHierarchy && sceneCamera != null && nose != null && tail != null)
        {
            Vector3 forward = Vector3.ProjectOnPlane(nose.position - tail.position, Vector3.up);
            Vector3 toCamera = Vector3.ProjectOnPlane(sceneCamera.transform.position - dogTransform.position, Vector3.up);
            if (forward.sqrMagnitude > 0.000001f && toCamera.sqrMagnitude > 0.000001f)
            {
                float yaw = Vector3.SignedAngle(forward, toCamera, Vector3.up);
                facingPivot.Rotate(Vector3.up, yaw, Space.World);
                calibratedFacing = facingPivot.rotation;
                tiltAxis = Vector3.Cross(Vector3.up, toCamera.normalized).normalized;
                facingCalibrated = true;
                Debug.Log($"[MobileDogFacing v2] Corrected animated facing by {yaw:0.0} degrees.");
            }
        }
        if (facingCalibrated && facingPivot != null)
        {
            // Absolute offset: live Inspector edits must not accumulate each frame.
            facingPivot.rotation = Quaternion.AngleAxis(forwardTiltDegrees, tiltAxis) * calibratedFacing;
            dogTransform.localScale = markerDogScale;
        }
    }

    private void ScanCameraFrame()
    {
        if (cameraBackground == null || !cameraBackground.IsCameraReady)
        {
            cameraStatus = "Camera: not ready";
            SetMarkerVisible(false, "Camera not ready.");
            return;
        }

        WebCamTexture texture = cameraBackground.CurrentTexture;
        cameraStatus = $"Camera: ready {texture.width}x{texture.height}";
        width = texture.width;
        height = texture.height;
        pixels = texture.GetPixels32();
        bestConfidence = 0f;

        DetectionResult best = FindBestMarkerCandidate();
        bool validMarker = best.Confidence >= requiredConfidence && LooksLikeDogMarker(best);
        if (validMarker)
        {
            missingFrames = 0;
            consecutiveDetections++;

            if (consecutiveDetections >= consecutiveDetectionsBeforeShow)
            {
                hasFoundMarker = true;
                if (!hasLockedDogTransform)
                {
                    ApplyMarkerResult(best);
                    hasLockedDogTransform = true;
                }
                SetMarkerVisible(true, $"Marker detected ({best.Confidence:0.00}).");
            }
            else
            {
                SetMarkerVisible(false, $"Marker locking... {consecutiveDetections}/{consecutiveDetectionsBeforeShow} ({best.Confidence:0.00}).");
            }

            return;
        }

        consecutiveDetections = 0;

        if (KeepDogVisibleAfterFirstMarkerFound && hasFoundMarker)
        {
            SetMarkerVisible(true, $"Marker locked. Pet stays visible ({bestConfidence:0.00}).");
            return;
        }

        missingFrames++;
        if (missingFrames >= missingFramesBeforeHide)
        {
            string hint = best.Confidence >= requiredConfidence
                ? $"Hold dog_pome_marker steady ({bestConfidence:0.00})."
                : $"Show dog marker to camera ({bestConfidence:0.00}).";
            SetMarkerVisible(false, hint);
        }
    }

    private bool IsCandidateInsideGuide(DetectionResult result)
    {
        if (width <= 0 || height <= 0)
        {
            return false;
        }

        float guideHalf = guideScreenWidth * 0.5f;
        float margin = guideDetectionMargin;
        float normalizedX = Mathf.Abs((result.CenterX / (float)width) - 0.5f);
        float normalizedY = Mathf.Abs((result.CenterY / (float)height) - 0.5f);
        float normalizedSize = result.Size / (float)Mathf.Min(width, height);
        bool centerInsideGuide = normalizedX <= guideHalf + margin && normalizedY <= guideHalf + margin;
        bool plausibleSize = normalizedSize >= 0.18f && normalizedSize <= guideScreenWidth + margin;
        return centerInsideGuide && plausibleSize && LooksLikeDogMarker(result);
    }

    private bool LooksLikeDogMarker(DetectionResult result)
    {
        int left = result.CenterX - result.Size / 2;
        int top = result.CenterY - result.Size / 2;

        if (ScoreOuterBlackBorder(left, top, result.Size) < 0.54f)
        {
            return false;
        }

        float minFinderScore = Mathf.Min(
            Mathf.Min(
                ScoreFinder(left, top, result.Size, 0.16f, 0.16f),
                ScoreFinder(left, top, result.Size, 0.84f, 0.16f)),
            Mathf.Min(
                ScoreFinder(left, top, result.Size, 0.16f, 0.84f),
                ScoreFinder(left, top, result.Size, 0.84f, 0.84f)));
        if (minFinderScore < minimumFinderCornerScore)
        {
            return false;
        }

        SampleSquareRatios(
            left + Mathf.RoundToInt(result.Size * 0.30f),
            top + Mathf.RoundToInt(result.Size * 0.30f),
            Mathf.RoundToInt(result.Size * 0.40f),
            out float whiteRatio,
            out float blackRatio);

        return whiteRatio >= minimumCenterWhiteRatio ||
            (blackRatio >= minimumCenterBlackRatio && blackRatio <= maximumCenterBlackRatio);
    }

    private void SampleSquareRatios(int left, int top, int size, out float whiteRatio, out float blackRatio)
    {
        int samples = 0;
        int white = 0;
        int black = 0;
        int stride = Mathf.Max(4, size / 14);

        for (int y = top; y < top + size; y += stride)
        {
            for (int x = left; x < left + size; x += stride)
            {
                Color32 color = GetPixel(x, y);
                samples++;
                if (IsWhite(color))
                {
                    white++;
                }
                else if (IsBlack(color))
                {
                    black++;
                }
            }
        }

        whiteRatio = samples > 0 ? white / (float)samples : 0f;
        blackRatio = samples > 0 ? black / (float)samples : 0f;
    }

    private DetectionResult FindBestMarkerCandidate()
    {
        DetectionResult best = default;
        int largestSize = Mathf.Min(maxMarkerSize, Mathf.Min(width, height) - 8);
        int smallestSize = Mathf.Min(minMarkerSize, largestSize);

        for (int size = largestSize; size >= smallestSize; size -= sizeStep)
        {
            int halfStep = Mathf.Max(24, gridStep);
            int startX = size / 2;
            int endX = width - size / 2;
            int startY = size / 2;
            int endY = height - size / 2;

            for (int y = startY; y <= endY; y += halfStep)
            {
                for (int x = startX; x <= endX; x += halfStep)
                {
                    float confidence = ScoreCandidate(x - size / 2, y - size / 2, size);
                    if (confidence > best.Confidence)
                    {
                        best = new DetectionResult(x, y, size, confidence);
                        bestConfidence = confidence;
                    }
                }
            }

            if (best.Confidence >= requiredConfidence + 0.12f)
            {
                break;
            }
        }

        return best;
    }

    private float ScoreCandidate(int left, int top, int size)
    {
        float edgeScore = ScoreOuterBlackBorder(left, top, size);
        if (edgeScore < 0.5f)
        {
            return edgeScore * 0.45f;
        }

        float cornerScore = 0f;
        cornerScore += ScoreFinder(left, top, size, 0.16f, 0.16f);
        cornerScore += ScoreFinder(left, top, size, 0.84f, 0.16f);
        cornerScore += ScoreFinder(left, top, size, 0.16f, 0.84f);
        cornerScore += ScoreFinder(left, top, size, 0.84f, 0.84f);
        cornerScore *= 0.25f;

        float centerScore = ScoreCenterContrast(left, top, size);
        return edgeScore * 0.42f + cornerScore * 0.46f + centerScore * 0.12f;
    }

    private float ScoreOuterBlackBorder(int left, int top, int size)
    {
        int samples = 0;
        int black = 0;
        int thickness = Mathf.Max(6, size / 12);
        int stride = Mathf.Max(4, size / 18);

        for (int y = top; y < top + size; y += stride)
        {
            for (int x = left; x < left + size; x += stride)
            {
                bool inBorder = x < left + thickness || x >= left + size - thickness ||
                    y < top + thickness || y >= top + size - thickness;
                if (!inBorder)
                {
                    continue;
                }

                samples++;
                if (IsBlack(GetPixel(x, y)))
                {
                    black++;
                }
            }
        }

        return samples > 0 ? (float)black / samples : 0f;
    }

    private float ScoreFinder(int left, int top, int size, float nx, float ny)
    {
        int cx = left + Mathf.RoundToInt(size * nx);
        int cy = top + Mathf.RoundToInt(size * ny);
        int outer = Mathf.Max(14, size / 10);
        int inner = Mathf.Max(7, outer / 2);

        float whiteRing = SampleRegionRatio(cx, cy, outer, inner, true);
        float blackCenter = SampleRegionRatio(cx, cy, inner, 0, false);
        return whiteRing * 0.55f + blackCenter * 0.45f;
    }

    private float ScoreCenterContrast(int left, int top, int size)
    {
        int cx = left + size / 2;
        int cy = top + size / 2;
        int radius = Mathf.Max(18, size / 5);
        int stride = Mathf.Max(4, radius / 5);
        int samples = 0;
        int bright = 0;
        int dark = 0;

        for (int y = cy - radius; y <= cy + radius; y += stride)
        {
            for (int x = cx - radius; x <= cx + radius; x += stride)
            {
                float luma = Luma(GetPixel(x, y));
                samples++;
                if (luma > 0.68f)
                {
                    bright++;
                }
                else if (luma < 0.25f)
                {
                    dark++;
                }
            }
        }

        if (samples == 0)
        {
            return 0f;
        }

        float brightRatio = (float)bright / samples;
        float darkRatio = (float)dark / samples;
        return Mathf.Clamp01((brightRatio + darkRatio) * 1.2f);
    }

    private float SampleRegionRatio(int cx, int cy, int outerRadius, int innerRadius, bool wantWhite)
    {
        int stride = Mathf.Max(3, outerRadius / 5);
        int samples = 0;
        int matches = 0;
        int outerSqr = outerRadius * outerRadius;
        int innerSqr = innerRadius * innerRadius;

        for (int y = cy - outerRadius; y <= cy + outerRadius; y += stride)
        {
            for (int x = cx - outerRadius; x <= cx + outerRadius; x += stride)
            {
                int dx = x - cx;
                int dy = y - cy;
                int d = dx * dx + dy * dy;
                if (d > outerSqr || d < innerSqr)
                {
                    continue;
                }

                Color32 c = GetPixel(x, y);
                samples++;
                if (wantWhite ? IsWhite(c) : IsBlack(c))
                {
                    matches++;
                }
            }
        }

        return samples > 0 ? (float)matches / samples : 0f;
    }

    private void ApplyMarkerResult(DetectionResult result)
    {
        if (dogTransform == null)
        {
            return;
        }

        float normalizedX = (result.CenterX / Mathf.Max(1f, width)) - 0.5f;
        float normalizedY = (result.CenterY / Mathf.Max(1f, height)) - 0.5f;
        float x = normalizedX * 1.2f;
        float y = -normalizedY * 1.6f - 0.25f;
        lockedMarkerPosition = new Vector3(x, y, baseDogPosition.z);
        Vector3 position = lockedMarkerPosition + dogPositionOffset;
        if (facingPivot != null)
        {
            facingPivot.SetPositionAndRotation(position, Quaternion.identity);
            dogTransform.localPosition = Vector3.zero;
            dogTransform.localRotation = Quaternion.identity;
            facingCalibrated = false;
        }
        else
        {
            dogTransform.position = position;
        }
        dogTransform.localScale = markerDogScale;
    }

    private void SetMarkerVisible(bool visible, string status)
    {
        markerVisible = visible;

        if (dogRoot != null && dogRoot.activeSelf != visible)
        {
            dogRoot.SetActive(visible);
        }

        SetStatus(status);
        SetGuideVisible(!visible);
    }

    private void SetStatus(string message)
    {
        string status = BuildStatusMessage(message);

        if (statusText != null)
        {
            statusText.text = status;
        }

        if (runtimeDebugText != null)
        {
            runtimeDebugText.text = status;
        }

        UpdateGuideColor();
    }

    private string BuildStatusMessage(string markerMessage)
    {
        if (!showDebugStatus)
        {
            return markerMessage;
        }

        string markerState = markerVisible ? "Marker: detected" : "Marker: not detected";
        return $"{cameraStatus}\nBest marker confidence: {bestConfidence:0.00}\n{markerState}\n{markerMessage}";
    }

    private void EnsureRuntimeOverlay()
    {
        if (!showScanningGuide && !showDebugStatus)
        {
            return;
        }

        Canvas canvas = FindOverlayCanvas();
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("Marker Guide Overlay", typeof(RectTransform));
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        if (showScanningGuide)
        {
            CreateGuide(canvas.transform);
        }

        if (showDebugStatus && runtimeDebugText == null)
        {
            TMP_FontAsset fontAsset = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            GameObject textObject = new GameObject("Marker Debug Status", typeof(RectTransform));
            textObject.transform.SetParent(canvas.transform, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(28f, -28f);
            rect.sizeDelta = new Vector2(760f, 190f);

            runtimeDebugText = textObject.AddComponent<TextMeshProUGUI>();
            runtimeDebugText.fontSize = 30f;
            runtimeDebugText.alignment = TextAlignmentOptions.TopLeft;
            runtimeDebugText.color = Color.white;
            runtimeDebugText.enableWordWrapping = true;
            if (fontAsset != null)
            {
                runtimeDebugText.font = fontAsset;
            }
        }
    }

    private Canvas FindOverlayCanvas()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Canvas canvas in canvases)
        {
            if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay && canvas.sortingOrder >= 100)
            {
                return canvas;
            }
        }

        return null;
    }

    private void CreateGuide(Transform parent)
    {
        if (guideCorners != null && guideCorners.Length > 0)
        {
            return;
        }

        guideRoot = new GameObject("Marker Four Corner Guide", typeof(RectTransform));
        guideRoot.transform.SetParent(parent, false);
        guideRect = guideRoot.GetComponent<RectTransform>();
        guideRect.anchorMin = new Vector2(0.5f, 0.5f);
        guideRect.anchorMax = new Vector2(0.5f, 0.5f);
        guideRect.pivot = new Vector2(0.5f, 0.5f);
        guideRect.anchoredPosition = Vector2.zero;
        UpdateGuideLayout();

        guideCorners = new Image[8];
        CreateCornerLine(guideRoot.transform, 0, "Top Left H", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(150f, 10f));
        CreateCornerLine(guideRoot.transform, 1, "Top Left V", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, 150f));
        CreateCornerLine(guideRoot.transform, 2, "Top Right H", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(150f, 10f));
        CreateCornerLine(guideRoot.transform, 3, "Top Right V", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(10f, 150f));
        CreateCornerLine(guideRoot.transform, 4, "Bottom Left H", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(150f, 10f));
        CreateCornerLine(guideRoot.transform, 5, "Bottom Left V", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(10f, 150f));
        CreateCornerLine(guideRoot.transform, 6, "Bottom Right H", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(150f, 10f));
        CreateCornerLine(guideRoot.transform, 7, "Bottom Right V", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(10f, 150f));
        UpdateGuideColor();
    }

    private void UpdateGuideLayout()
    {
        if (guideRect == null)
        {
            return;
        }

        RectTransform parent = guideRect.parent as RectTransform;
        if (parent == null)
        {
            return;
        }

        float availableWidth = Mathf.Max(1f, parent.rect.width);
        float availableHeight = Mathf.Max(1f, parent.rect.height);
        float guideSize = Mathf.Min(availableWidth * guideScreenWidth, availableHeight * 0.46f);
        guideRect.sizeDelta = new Vector2(guideSize, guideSize);
    }

    private void SetGuideVisible(bool visible)
    {
        if (guideRoot != null && guideRoot.activeSelf != visible)
        {
            guideRoot.SetActive(visible);
        }
    }

    private void CreateCornerLine(Transform parent, int index, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size)
    {
        GameObject lineObject = new GameObject(name, typeof(RectTransform));
        lineObject.transform.SetParent(parent, false);
        RectTransform rect = lineObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMin;
        rect.pivot = pivot;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;

        Image image = lineObject.AddComponent<Image>();
        image.color = guideColor;
        guideCorners[index] = image;
    }

    private void UpdateGuideColor()
    {
        if (guideCorners == null)
        {
            return;
        }

        Color color = markerVisible ? detectedGuideColor : guideColor;
        foreach (Image image in guideCorners)
        {
            if (image != null)
            {
                image.color = color;
            }
        }
    }

    private Color32 GetPixel(int x, int y)
    {
        x = Mathf.Clamp(x, 0, width - 1);
        y = Mathf.Clamp(y, 0, height - 1);
        return pixels[y * width + x];
    }

    private static bool IsBlack(Color32 c)
    {
        return Luma(c) < 0.24f;
    }

    private static bool IsWhite(Color32 c)
    {
        return Luma(c) > 0.72f;
    }

    private static float Luma(Color32 c)
    {
        return (0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b) / 255f;
    }

    private readonly struct DetectionResult
    {
        public DetectionResult(int centerX, int centerY, int size, float confidence)
        {
            CenterX = centerX;
            CenterY = centerY;
            Size = size;
            Confidence = confidence;
        }

        public int CenterX { get; }
        public int CenterY { get; }
        public int Size { get; }
        public float Confidence { get; }
    }
}
