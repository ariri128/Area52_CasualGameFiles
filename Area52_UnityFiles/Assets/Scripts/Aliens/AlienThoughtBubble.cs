using UnityEngine;
using UnityEngine.UI;

/*
 * The thought bubble above a selected alien, showing its emotion, with the Sell and Explore buttons underneath it
 * Follows the alien on screen and keeps the bubble AND its buttons inside the safe area
 * Tapping the bubble tells AlienTapper to open the file; Explore tells AlienTapper to send the alien exploring
*/

public class AlienThoughtBubble : MonoBehaviour
{
    [SerializeField] private Camera cam;
    [Tooltip("The bubble itself (has a Button). Its bottom-middle sits above the alien. " +
             "The Sell and Explore buttons go inside it as children, so they follow it.")]
    [SerializeField] private RectTransform bubble;
    [SerializeField] private Image emotionImage;
    [Tooltip("Gap between the top of the alien and the bottom of the bubble, in UI units (leave room for the dots).")]
    [SerializeField] private float gapAboveAlien = 60f;
    [Tooltip("How far right of the alien's middle the bubble sits, in UI units. Negative = left.")]
    [SerializeField] private float offsetRight = 70f;

    [Header("Buttons")]
    [Tooltip("Not working yet - stays greyed out.")]
    [SerializeField] private Button sellButton;
    [SerializeField] private Button exploreButton;

    private SpriteRenderer target;
    private Canvas canvas;

    private void Awake()
    {
        if (cam == null) cam = Camera.main;
        canvas = GetComponentInParent<Canvas>().rootCanvas;
        bubble.pivot = new Vector2(0.5f, 0f);

        Button button = bubble.GetComponent<Button>();
        if (button != null)
            button.onClick.AddListener(() => { if (AlienTapper.main != null) AlienTapper.main.OnBubblePressed(); });

        if (sellButton != null) sellButton.interactable = false; //TODO: selling comes later
        if (exploreButton != null)
            exploreButton.onClick.AddListener(() => { if (AlienTapper.main != null) AlienTapper.main.OnExplorePressed(); });

        Hide();
    }

    public void Show(SpriteRenderer alien, Sprite emotion)
    {
        target = alien;
        SetEmotion(emotion);
        bubble.gameObject.SetActive(true);
        FollowTarget();
    }

    public void SetEmotion(Sprite emotion)
    {
        if (emotionImage == null || emotionImage.sprite == emotion) return;
        emotionImage.sprite = emotion;
        emotionImage.enabled = emotion != null;
    }

    public void Hide()
    {
        target = null;
        bubble.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (!bubble.gameObject.activeSelf) return;
        if (target == null) return; // AlienTapper re-attaches or hides it
        FollowTarget();
    }

    private void FollowTarget()
    {
        if (target == null) return;

        // Top-middle of the alien's sprite, in the world -> screen pixels
        Bounds b = target.bounds;
        Vector3 screen = cam.WorldToScreenPoint(new Vector3(b.center.x, b.max.y, b.center.z));
        if (screen.z < 0f) return;

        float scale = canvas.scaleFactor;
        float x = screen.x + offsetRight * scale;
        float y = screen.y + gapAboveAlien * scale;

        // One box around the bubble and everything inside it (buttons, dots), measured from the bubble's pivot
        Bounds group = RectTransformUtility.CalculateRelativeRectTransformBounds(bubble);
        Vector3 pixelsPerUnit = bubble.lossyScale; // UI units -> screen pixels

        // Keep that whole box inside the safe area
        Rect safe = Screen.safeArea;
        x = Mathf.Clamp(x, safe.xMin - group.min.x * pixelsPerUnit.x, safe.xMax - group.max.x * pixelsPerUnit.x);
        y = Mathf.Clamp(y, safe.yMin - group.min.y * pixelsPerUnit.y, safe.yMax - group.max.y * pixelsPerUnit.y);

        bubble.position = new Vector3(x, y, bubble.position.z);
    }
}