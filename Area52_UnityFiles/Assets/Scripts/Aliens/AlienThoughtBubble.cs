using UnityEngine;
using UnityEngine.UI;

/*
 * The thought bubble above a selected alien, showing its emotion, with the Sell, Explore, and Feed buttons underneath it
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

    [Header("Feeding")]
    [Tooltip("Always clickable - uses one pellet each press. Needs a Mask component so the fill takes its shape.")]
    [SerializeField] private Button feedButton;
    [Tooltip("Plain white Image inside the Feed button, stretched to fill it, Raycast Target off. Its width shows how full the alien is.")]
    [SerializeField] private Image hungerFill;

    [Tooltip("Fill colors. Keep alpha low so the button shows through.")]
    [SerializeField] private Color fullColor = new Color(0.35f, 0.85f, 0.35f, 0.45f); // green
    [SerializeField] private Color hungryColor = new Color(1f, 0.7f, 0.2f, 0.45f); // orange - Sad
    [SerializeField] private Color starvingColor = new Color(0.95f, 0.25f, 0.25f, 0.5f); // red - Angry

    [Tooltip("How much the whole button takes on the hunger color (0 = its normal color, 1 = fully that color).")]
    [Range(0f, 1f)] [SerializeField] private float buttonTint = 0.35f;
    [Tooltip("How fast the bar slides to its new amount (full bars per second).")]
    [SerializeField] private float fillSpeed = 1.5f;

    private Image feedBackground; // The Feed button's own image
    private Color feedBaseColor;
    private float shownFullness = 1f; // What the bar shows right now
    private float targetFullness = 1f; // What it's sliding towards

    // Where the pellet flies from
    public RectTransform FeedButtonRect => feedButton != null ? (RectTransform)feedButton.transform : null;

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

        if (feedButton != null)
        {
            feedButton.onClick.AddListener(() => { if (AlienTapper.main != null) AlienTapper.main.OnFeedPressed(); });
            feedBackground = feedButton.GetComponent<Image>();
            if (feedBackground != null) feedBaseColor = feedBackground.color;
        }

        if (hungerFill != null)
        {
            // Zero offsets, so moving its right anchor sets its width exactly
            hungerFill.rectTransform.offsetMin = Vector2.zero;
            hungerFill.rectTransform.offsetMax = Vector2.zero;
            hungerFill.raycastTarget = false;
        }

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

    // Greys out Explore when this alien can't go (an alien from its room is already out)
    public void SetExploreAllowed(bool allowed)
    {
        if (exploreButton != null) exploreButton.interactable = allowed;
    }

    // 0 = empty, 1 = full. Instant = jump straight there (when the bubble first opens) instead of sliding
    public void SetHunger(float fullness, bool hungry, bool starving, bool instant = false)
    {
        targetFullness = Mathf.Clamp01(fullness);
        if (instant) shownFullness = targetFullness;

        Color color = starving ? starvingColor : hungry ? hungryColor : fullColor;
        if (hungerFill != null) hungerFill.color = color;

        // The whole button leans towards the same color (solid, so it doesn't go see-through)
        if (feedBackground != null)
        {
            Color solid = new Color(color.r, color.g, color.b, feedBaseColor.a);
            feedBackground.color = Color.Lerp(feedBaseColor, solid, buttonTint);
        }

        ApplyFill();
    }

    private void AnimateHunger()
    {
        if (Mathf.Approximately(shownFullness, targetFullness)) return;
        shownFullness = Mathf.MoveTowards(shownFullness, targetFullness, fillSpeed * Time.unscaledDeltaTime);
        ApplyFill();
    }

    // Stretches the fill from the left edge to the fullness point; the Mask rounds it to the button's shape
    private void ApplyFill()
    {
        if (hungerFill == null) return;
        RectTransform fill = hungerFill.rectTransform;
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(shownFullness, 1f);
    }

    public void Hide()
    {
        target = null;
        bubble.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (!bubble.gameObject.activeSelf) return;
        AnimateHunger();
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