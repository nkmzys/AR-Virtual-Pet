using TMPro;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class TrackedImageDogPlacer : MonoBehaviour
{
    [Header("AR")]
    [SerializeField] private ARTrackedImageManager trackedImageManager;
    [SerializeField] private string targetImageName = "dog_pome_marker";

    [Header("Dog")]
    [SerializeField] private GameObject dogRoot;
    [SerializeField] private GameObject dogPrefab;
    [SerializeField] private bool instantiatePrefabIfDogRootMissing = true;
    [SerializeField] private Vector3 placedDogScale = new Vector3(0.33f, 0.33f, 0.33f);
    [SerializeField] private Vector3 localPositionOffset = Vector3.zero;
    [SerializeField] private Vector3 localRotationOffsetEuler = Vector3.zero;
    [SerializeField] private bool keepDogUprightFacingCamera = true;
    [SerializeField] private bool placeOnlyOnce = false;
    [SerializeField] private bool hideDogUntilMarkerFound = true;
    [SerializeField] private bool hideDogWhenTrackingLost = false;

    [Header("Status UI")]
    [SerializeField] private TMP_Text statusText;

    private bool hasPlacedDog;
    private ARTrackedImage currentTrackedImage;

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

        EnsureDogInstance();

        if (hideDogUntilMarkerFound && dogRoot != null)
        {
            dogRoot.SetActive(false);
        }

        SetStatus("マーカーをカメラに映してください。");
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
        foreach (ARTrackedImage image in args.added)
        {
            HandleTrackedImage(image);
        }

        foreach (ARTrackedImage image in args.updated)
        {
            HandleTrackedImage(image);
        }

        foreach (ARTrackedImage image in args.removed)
        {
            if (image == currentTrackedImage)
            {
                HandleTrackingLost();
            }
        }
    }

    private void HandleTrackedImage(ARTrackedImage image)
    {
        if (!IsTargetImage(image))
        {
            return;
        }

        currentTrackedImage = image;

        if (image.trackingState == TrackingState.Tracking)
        {
            PlaceDog(image.transform);
            return;
        }

        if (hideDogWhenTrackingLost)
        {
            HandleTrackingLost();
        }
        else if (hasPlacedDog)
        {
            SetStatus("マーカーを見失いました。犬は最後の位置に固定しています。");
        }
    }

    private bool IsTargetImage(ARTrackedImage image)
    {
        if (image == null)
        {
            return false;
        }

        return string.IsNullOrEmpty(targetImageName) ||
            image.referenceImage.name == targetImageName;
    }

    private void PlaceDog(Transform markerTransform)
    {
        if (placeOnlyOnce && hasPlacedDog)
        {
            return;
        }

        EnsureDogInstance();
        if (dogRoot == null)
        {
            SetStatus("犬のPrefabまたはDog Rootが設定されていません。");
            return;
        }

        Vector3 targetPosition = markerTransform.TransformPoint(localPositionOffset);
        Quaternion targetRotation = markerTransform.rotation * Quaternion.Euler(localRotationOffsetEuler);

        Camera camera = Camera.main;
        if (keepDogUprightFacingCamera && camera != null)
        {
            Vector3 directionToCamera = camera.transform.position - targetPosition;
            directionToCamera.y = 0f;
            if (directionToCamera.sqrMagnitude > 0.001f)
            {
                targetRotation = Quaternion.LookRotation(directionToCamera.normalized, Vector3.up) *
                    Quaternion.Euler(localRotationOffsetEuler);
            }
        }

        dogRoot.transform.SetParent(null, true);
        dogRoot.transform.SetPositionAndRotation(targetPosition, targetRotation);
        dogRoot.transform.localScale = placedDogScale;
        dogRoot.SetActive(true);
        hasPlacedDog = true;
        SetStatus("マーカー認識中");
    }

    private void HandleTrackingLost()
    {
        currentTrackedImage = null;

        if (hideDogWhenTrackingLost && dogRoot != null)
        {
            dogRoot.SetActive(false);
            SetStatus("マーカーを見失いました。もう一度マーカーを映してください。");
            return;
        }

        SetStatus("マーカーを見失いました。犬は最後の位置に固定しています。");
    }

    private void EnsureDogInstance()
    {
        if (dogRoot != null || !instantiatePrefabIfDogRootMissing || dogPrefab == null)
        {
            return;
        }

        dogRoot = Instantiate(dogPrefab);
        dogRoot.name = "Mobile AR Dog";
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }

        Debug.Log($"[TrackedImageDogPlacer] {message}");
    }
}
