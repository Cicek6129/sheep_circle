using UnityEngine;

namespace SheepCircle
{
    /// <summary>
    /// Adjusts the camera's orthographic size dynamically for Portrait (dikey) mobile screens.
    /// Ensures that the roundabout ring and animals are never clipped horizontally or vertically,
    /// regardless of the device's aspect ratio (9:16, 9:20, iPad 3:4, etc.).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [ExecuteAlways]
    public class MobileCameraScaler : MonoBehaviour
    {
        [Header("Target World Dimensions (Portrait)")]
        [Tooltip("Minimum horizontal world units that must be visible across the screen width.")]
        public float targetWorldWidth = 13.0f;

        [Tooltip("Minimum vertical world units that must be visible across the screen height.")]
        public float targetWorldHeight = 16.0f;

        Camera cam;
        float lastAspect = -1f;

        void Awake()
        {
            cam = GetComponent<Camera>();
            UpdateCameraSize();
        }

        void LateUpdate()
        {
            if (cam == null) return;

            // Only recalculate if screen aspect ratio changed (e.g. resolution change or orientation rotation)
            if (!Mathf.Approximately(cam.aspect, lastAspect))
            {
                UpdateCameraSize();
            }
        }

        public void UpdateCameraSize()
        {
            if (cam == null || !cam.orthographic) return;

            lastAspect = cam.aspect;

            float neededForWidth = (targetWorldWidth * 0.5f) / Mathf.Max(0.01f, cam.aspect);
            float neededForHeight = targetWorldHeight * 0.5f;

            cam.orthographicSize = Mathf.Max(neededForWidth, neededForHeight);
        }
    }
}
