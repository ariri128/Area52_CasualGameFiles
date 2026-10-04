using UnityEngine;

/*
 * Shrinks a full-screen UI rectangle to the phone's safe area, so anything placed inside it stays clear of notches, camera cutouts and rounded corners
*/

[RequireComponent(typeof(RectTransform))]
public class SafeAreaFitter : MonoBehaviour
{
    private RectTransform rectTransform;
    private Rect lastSafeArea;
    private Vector2Int lastScreenSize;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        Apply();
    }

    private void Update()
    {
        // Cheap check every frame - only re-fits when something actually changed
        if (Screen.safeArea != lastSafeArea || Screen.width != lastScreenSize.x || Screen.height != lastScreenSize.y)
            Apply();
    }

    private void Apply()
    {
        Rect safe = Screen.safeArea;
        lastSafeArea = safe;
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);

        if (Screen.width <= 0 || Screen.height <= 0) return;

        // Safe area in pixels -> anchors from 0 to 1 across the screen
        Vector2 min = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
        Vector2 max = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);

        rectTransform.anchorMin = min;
        rectTransform.anchorMax = max;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }
}