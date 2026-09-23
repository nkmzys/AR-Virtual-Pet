using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class DogMarkerPlacement : MonoBehaviour
{
    [SerializeField] private ARTrackedImageManager trackedImageManager;
    [SerializeField] private GameObject dogRoot;
    [SerializeField] private bool hideDogUntilMarkerFound = true;
    [SerializeField] private bool placeOnlyOnce = true;
    [SerializeField] private Vector3 localPositionOffset = Vector3.zero;
    [SerializeField] private Vector3 localRotationOffsetEuler = Vector3.zero;
    [SerializeField] private Vector3 placedDogScale = new Vector3(0.33f, 0.33f, 0.33f);
    [SerializeField] private bool keepDogUprightFacingCamera = true;
    [SerializeField] private bool preserveDogVisualLocalTransform = true;
    [SerializeField] private bool centerDogVisualLocalPosition = true;
    [SerializeField] private bool showScanningGuide = true;
    [SerializeField] private Color scanningGuideColor = Color.white;
    [SerializeField, Range(0.2f, 0.9f)] private float scanningGuideScreenSize = 0.55f;
    [SerializeField] private float scanningGuideDistance = 1.2f;
    [SerializeField] private float scanningGuideLineWidth = 0.01f;

    private bool hasPlacedDog;
    private Transform guideRoot;
    private LineRenderer[] guideLines;
    private Material guideMaterial;

    private void Reset()
    {
        trackedImageManager = GetComponent<ARTrackedImageManager>();
    }

    private void Awake()
    {
        if (trackedImageManager == null)
        {
            trackedImageManager = GetComponent<ARTrackedImageManager>();
        }

        if (hideDogUntilMarkerFound && dogRoot != null)
        {
            dogRoot.SetActive(false);
        }

        if (centerDogVisualLocalPosition && dogRoot != null && dogRoot.transform.childCount > 0)
        {
            dogRoot.transform.GetChild(0).localPosition = Vector3.zero;
        }

        if (showScanningGuide)
        {
            CreateScanningGuide();
        }
    }

    private void OnEnable()
    {
        if (trackedImageManager != null)
        {
            trackedImageManager.trackedImagesChanged += OnTrackedImagesChanged;
        }
    }

    private void OnDisable()
    {
        if (trackedImageManager != null)
        {
            trackedImageManager.trackedImagesChanged -= OnTrackedImagesChanged;
        }
    }

    private void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs args)
    {
        Debug.Log($"[DogMarkerPlacement] trackedImagesChanged added:{args.added.Count} updated:{args.updated.Count} removed:{args.removed.Count}");

        foreach (var image in args.added)
        {
            TryPlaceDog(image);
        }

        foreach (var image in args.updated)
        {
            TryPlaceDog(image);
        }
    }

    private void LateUpdate()
    {
        if (!showScanningGuide || hasPlacedDog || guideRoot == null)
        {
            return;
        }

        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        guideRoot.gameObject.SetActive(true);
        guideRoot.SetPositionAndRotation(
            camera.transform.position + camera.transform.forward * scanningGuideDistance,
            camera.transform.rotation);

        float squareSize = 2f * scanningGuideDistance * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * scanningGuideScreenSize;
        float cornerLength = squareSize * 0.2f;

        Vector3 topLeft = new Vector3(-squareSize * 0.5f, squareSize * 0.5f, 0f);
        Vector3 topRight = new Vector3(squareSize * 0.5f, squareSize * 0.5f, 0f);
        Vector3 bottomLeft = new Vector3(-squareSize * 0.5f, -squareSize * 0.5f, 0f);
        Vector3 bottomRight = new Vector3(squareSize * 0.5f, -squareSize * 0.5f, 0f);

        SetCorner(0, topLeft, Vector3.right, Vector3.down, cornerLength);
        SetCorner(2, topRight, Vector3.left, Vector3.down, cornerLength);
        SetCorner(4, bottomLeft, Vector3.right, Vector3.up, cornerLength);
        SetCorner(6, bottomRight, Vector3.left, Vector3.up, cornerLength);
    }

    private void CreateScanningGuide()
    {
        guideMaterial = new Material(Shader.Find("Sprites/Default"));
        guideMaterial.color = scanningGuideColor;

        guideRoot = new GameObject("Dog Marker Scanning Guide").transform;
        guideLines = new LineRenderer[8];

        for (int i = 0; i < guideLines.Length; i++)
        {
            var lineObject = new GameObject($"Guide Line {i}");
            lineObject.transform.SetParent(guideRoot, false);

            var line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 2;
            line.startWidth = scanningGuideLineWidth;
            line.endWidth = scanningGuideLineWidth;
            line.material = guideMaterial;
            line.startColor = scanningGuideColor;
            line.endColor = scanningGuideColor;
            line.numCapVertices = 2;
            guideLines[i] = line;
        }
    }

    private void SetCorner(int lineIndex, Vector3 corner, Vector3 horizontalDirection, Vector3 verticalDirection, float length)
    {
        guideLines[lineIndex].SetPosition(0, corner);
        guideLines[lineIndex].SetPosition(1, corner + horizontalDirection * length);
        guideLines[lineIndex + 1].SetPosition(0, corner);
        guideLines[lineIndex + 1].SetPosition(1, corner + verticalDirection * length);
    }

    private void TryPlaceDog(ARTrackedImage image)
    {
        Debug.Log($"[DogMarkerPlacement] image:{image.referenceImage.name} state:{image.trackingState} position:{image.transform.position}");

        if (dogRoot == null || image.trackingState != TrackingState.Tracking)
        {
            return;
        }

        if (placeOnlyOnce && hasPlacedDog)
        {
            return;
        }

        Transform imageTransform = image.transform;
        Debug.Log($"[DogMarkerPlacement] Placing dog on marker '{image.referenceImage.name}' at {imageTransform.position}");
        if (guideRoot != null)
        {
            guideRoot.gameObject.SetActive(false);
        }

        Vector3 targetPosition = imageTransform.TransformPoint(localPositionOffset);
        Quaternion targetRotation = imageTransform.rotation * Quaternion.Euler(localRotationOffsetEuler);

        Camera camera = Camera.main;
        if (keepDogUprightFacingCamera && camera != null)
        {
            Vector3 directionToCamera = camera.transform.position - targetPosition;
            directionToCamera.y = 0f;
            if (directionToCamera.sqrMagnitude > 0.001f)
            {
                targetRotation = Quaternion.LookRotation(directionToCamera.normalized, Vector3.up)
                    * Quaternion.Euler(localRotationOffsetEuler);
            }
        }

        dogRoot.transform.SetParent(null, true);
        dogRoot.transform.SetPositionAndRotation(targetPosition, targetRotation);
        dogRoot.transform.localScale = placedDogScale;
        dogRoot.SetActive(true);
        hasPlacedDog = true;
    }
}
