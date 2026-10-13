using UnityEngine;

/*
 * Scales a sprite as big as it can go inside an orthographic camera's view without stretching it
*/

[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class FitSpriteToCamera : MonoBehaviour
{
    [Tooltip("The camera that shows this sprite. Must be Orthographic.")]
    [SerializeField] private Camera cam;

    [Header("Free space kept at each edge (fraction of the screen)")]
    [Range(0f, 0.45f)] [SerializeField] private float left = 0.22f;
    [Range(0f, 0.45f)] [SerializeField] private float right = 0.22f;
    [Range(0f, 0.45f)] [SerializeField] private float top = 0.04f;
    [Range(0f, 0.45f)] [SerializeField] private float bottom = 0.18f;

    private SpriteRenderer spriteRenderer;

    private void OnEnable()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        Fit();
    }

    // Cheap, and it catches rotation, resizing the Game view and margin changes
    private void LateUpdate()
    {
        Fit();
    }

    private void Fit()
    {
        if (cam == null || spriteRenderer == null || spriteRenderer.sprite == null) return;
        if (!cam.orthographic) return;

        // What the camera sees, in world units
        float viewHeight = cam.orthographicSize * 2f;
        float viewWidth = viewHeight * cam.aspect;

        float freeWidth = viewWidth * (1f - left - right);
        float freeHeight = viewHeight * (1f - top - bottom);

        Bounds spriteBounds = spriteRenderer.sprite.bounds; // size at scale 1
        if (spriteBounds.size.x <= 0f || spriteBounds.size.y <= 0f) return;

        // Same scale on both axes - prevents stretching
        float scale = Mathf.Min(freeWidth / spriteBounds.size.x, freeHeight / spriteBounds.size.y);

        // Middle of the free space - shifts if one margin is bigger than the other
        Vector3 camPos = cam.transform.position;
        float centerX = camPos.x + (left - right) * viewWidth * 0.5f;
        float centerY = camPos.y + (bottom - top) * viewHeight * 0.5f;

        // Works whatever the sprite's pivot is this
        Vector3 pivotOffset = spriteBounds.center * scale;
        Vector3 newScale = new Vector3(scale, scale, 1f);
        Vector3 newPosition = new Vector3(centerX - pivotOffset.x, centerY - pivotOffset.y, transform.position.z);

        // Only write when something changed, so the scene isn't marked as edited every frame
        if ((transform.localScale - newScale).sqrMagnitude > 1e-8f) transform.localScale = newScale;
        if ((transform.position - newPosition).sqrMagnitude > 1e-8f) transform.position = newPosition;
    }
}