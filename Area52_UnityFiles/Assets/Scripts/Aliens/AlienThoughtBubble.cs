using UnityEngine;
using UnityEngine.UI;

/*
 * The thought bubble above a selected alien, showing its emotion
 * Follows the alien on screen and stays inside the safe area - tapping it tells AlienTapper
*/

public class AlienThoughtBubble : MonoBehaviour
{
    [SerializeField] private Camera cam;
    [Tooltip("The bubble itself (has a Button). Its bottom-middle sits above the alien.")]
    [SerializeField] private RectTransform bubble;
    [SerializeField] private Image emotionImage;
    [Tooltip("Gap between the top of the alien and the bottom of the bubble, in UI units (leave room for the dots).")]
    [SerializeField] private float gapAboveAlien = 60f;
    [Tooltip("How far right of the alien's middle the bubble sits, in UI units. Negative = left.")]
    [SerializeField] private float offsetRight = 70f;

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

        // UI units -> pixels, then keep the whole bubble inside the safe area
        float scale = canvas.scaleFactor;
        Vector2 sizePixels = bubble.rect.size * scale;
        float x = screen.x + offsetRight * scale;
        float y = screen.y + gapAboveAlien * scale;

        Rect safe = Screen.safeArea;
        x = Mathf.Clamp(x, safe.xMin + sizePixels.x * 0.5f, safe.xMax - sizePixels.x * 0.5f);
        y = Mathf.Clamp(y, safe.yMin, safe.yMax - sizePixels.y);

        bubble.position = new Vector3(x, y, bubble.position.z);
    }
}