using UnityEngine;
using UnityEngine.UI;

/*
 * Adds the popup with the Replace/Move/Remove options for each furniture when it gets selected
*/

public class FurnitureActionPopup : MonoBehaviour
{
    [SerializeField] private Camera cam;

    [Tooltip("The speech bubble with the buttons in it.")]
    [SerializeField] private RectTransform bubble;

    [SerializeField] private Button replaceButton;
    [SerializeField] private Button moveButton;
    [SerializeField] private Button removeButton;

    [Tooltip("Gap between the top of the piece and the bottom of the bubble's tail, in UI units.")]
    [SerializeField] private float gapAbovePiece = 20f;

    private FurnitureItem target;
    private Canvas canvas;

    private void Awake()
    {
        if (cam == null) cam = Camera.main;
        canvas = GetComponentInParent<Canvas>().rootCanvas;
        if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            Debug.LogWarning("FurnitureActionPopup expects its canvas to be Screen Space - Overlay.", this);

        // The bottom-middle of the bubble is the point that sits above the piece
        bubble.pivot = new Vector2(0.5f, 0f);

        // The editor is looked up when a button is clicked, so it doesn't matter which wakes up first
        replaceButton.onClick.AddListener(() => { if (FurnitureEditor.main != null) FurnitureEditor.main.OnReplacePressed(); });
        moveButton.onClick.AddListener(() => { if (FurnitureEditor.main != null) FurnitureEditor.main.OnMovePressed(); });
        removeButton.onClick.AddListener(() => { if (FurnitureEditor.main != null) FurnitureEditor.main.OnRemovePressed(); });

        Hide();
    }

    public void Show(FurnitureItem item)
    {
        target = item;
        bubble.gameObject.SetActive(true);
        LayoutRebuilder.ForceRebuildLayoutImmediate(bubble);
        FollowTarget();
    }

    public void Hide()
    {
        target = null;
        bubble.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (!bubble.gameObject.activeSelf) return;
        if (target == null)
        {
            Hide();
            return;
        }
        FollowTarget();
    }

    private void FollowTarget()
    {
        // Top-middle of the piece's collider, in the world
        Bounds b = target.Collider.bounds;
        Vector3 topOfPiece = new Vector3(b.center.x, b.max.y, b.center.z);

        // World -> screen pixels
        Vector3 screen = cam.WorldToScreenPoint(topOfPiece);
        if (screen.z < 0f) return;

        float scale = canvas.scaleFactor;
        Vector2 sizePixels = bubble.rect.size * scale;
        float x = screen.x;
        float y = screen.y + gapAbovePiece * scale;

        // Keeps the whole bubble inside the safe area (away from notches and rounded corners)
        Rect safe = Screen.safeArea;
        x = Mathf.Clamp(x, safe.xMin + sizePixels.x * 0.5f, safe.xMax - sizePixels.x * 0.5f);
        y = Mathf.Clamp(y, safe.yMin, safe.yMax - sizePixels.y);

        bubble.position = new Vector3(x, y, bubble.position.z);
    }
}