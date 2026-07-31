using UnityEngine;

namespace SheepCircle
{
    /// <summary>
    /// Adjusts the RectTransform anchors of a UI panel to fit within Screen.safeArea.
    /// Prevents UI elements from being blocked by notches, camera cutouts, or home gesture bars on mobile devices.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [ExecuteAlways]
    public class SafeAreaFitter : MonoBehaviour
    {
        RectTransform rectTransform;
        Rect lastSafeArea = new Rect(0, 0, 0, 0);
        Vector2Int lastScreenSize = Vector2Int.zero;

        void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            ApplySafeArea();
        }

        void LateUpdate()
        {
            if (Screen.safeArea != lastSafeArea || Screen.width != lastScreenSize.x || Screen.height != lastScreenSize.y)
            {
                ApplySafeArea();
            }
        }

        public void ApplySafeArea()
        {
            if (rectTransform == null) return;

            Rect safeArea = Screen.safeArea;
            lastSafeArea = safeArea;
            lastScreenSize = new Vector2Int(Screen.width, Screen.height);

            if (Screen.width <= 0 || Screen.height <= 0) return;

            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;

            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
        }
    }
}
