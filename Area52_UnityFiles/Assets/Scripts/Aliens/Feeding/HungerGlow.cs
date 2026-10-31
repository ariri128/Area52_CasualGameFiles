using UnityEngine;

/*
 * A soft red glow behind an alien that pulses while it's half full or less, so the player knows to feed it
 * Pulses faster and brighter when it's nearly empty (Angry), fades out once it's fed above half
 * Goes on the alien prefab next to FloatingAlien - it makes its own glow child when the game starts
 * Uses the Glow Sprite if you give it one, otherwise draws a soft circle itself
*/

[RequireComponent(typeof(FloatingAlien))]
public class HungerGlow : MonoBehaviour
{
    [Header("Look")]
    [Tooltip("Optional. A soft white blob/circle. Empty = makes one in code.")]
    [SerializeField] private Sprite glowSprite;
    [Tooltip("The glow's color (its alpha is set by the pulse below).")]
    [SerializeField] private Color glowColor = new Color(1f, 0.15f, 0.15f, 1f);
    [Tooltip("How big the glow is compared to the alien (1.6 = 60% bigger than its sprite).")]
    [SerializeField] private float glowSize = 1.6f;

    [Header("Pulse")]
    [Tooltip("How see-through it gets at the dimmest point of a pulse.")]
    [Range(0f, 1f)] [SerializeField] private float minAlpha = 0.15f;
    [Tooltip("How strong it gets at the brightest point of a pulse.")]
    [Range(0f, 1f)] [SerializeField] private float maxAlpha = 0.6f;
    [Tooltip("Extra brightness when it's nearly empty (Angry).")]
    [Range(0f, 1f)] [SerializeField] private float starvingExtraAlpha = 0.2f;
    [Tooltip("Pulses per second while hungry (Sad).")]
    [SerializeField] private float pulsesPerSecond = 0.8f;
    [Tooltip("Pulses per second while nearly empty (Angry).")]
    [SerializeField] private float starvingPulsesPerSecond = 1.6f;
    [Tooltip("Seconds to fade in when it gets hungry, and out when it's fed.")]
    [SerializeField] private float fadeSeconds = 0.4f;

    private static Sprite generatedGlow; // One soft circle shared by every alien

    private FloatingAlien alien;
    private SpriteRenderer alienRenderer;
    private SpriteRenderer glow;
    private Sprite fittedTo; // The alien sprite the glow was last sized for
    private float strength; // 0 = hidden, 1 = fully on (fades between)
    private float phase;

    private void Awake()
    {
        alien = GetComponent<FloatingAlien>();
        alienRenderer = GetComponent<SpriteRenderer>();

        // The glow is its own child sprite, drawn just behind the alien
        GameObject child = new GameObject("HungerGlow");
        child.transform.SetParent(transform, false);

        glow = child.AddComponent<SpriteRenderer>();
        glow.sprite = glowSprite != null ? glowSprite : GeneratedGlow();
        glow.sharedMaterial = alienRenderer.sharedMaterial;
        glow.sortingLayerID = alienRenderer.sortingLayerID;
        glow.sortingOrder = alienRenderer.sortingOrder - 1; // Behind the alien
        glow.enabled = false;

        // Each alien starts its pulse at a different point so they don't all throb in sync
        phase = Random.Range(0f, Mathf.PI * 2f);
    }

    private void LateUpdate()
    {
        FitToAlien();

        OwnedAlien owner = alien.Owner;
        AlienHunger hunger = AlienHunger.main;
        bool hungry = hunger != null && owner != null && !alien.Flying && hunger.IsHungry(owner);
        bool starving = hungry && hunger.IsStarving(owner);

        // Fade in or out
        float fadeStep = fadeSeconds > 0f ? Time.deltaTime / fadeSeconds : 1f;
        strength = Mathf.MoveTowards(strength, hungry ? 1f : 0f, fadeStep);

        if (strength <= 0f)
        {
            if (glow.enabled) glow.enabled = false;
            return;
        }
        glow.enabled = true;

        // Pulse: a smooth wave between dim and bright
        float speed = starving ? starvingPulsesPerSecond : pulsesPerSecond;
        phase += Time.deltaTime * speed * Mathf.PI * 2f;
        float wave = (Mathf.Sin(phase) + 1f) * 0.5f; // 0 to 1

        float brightest = Mathf.Clamp01(maxAlpha + (starving ? starvingExtraAlpha : 0f));
        Color color = glowColor;
        color.a = Mathf.Lerp(minAlpha, brightest, wave) * strength;
        glow.color = color;
    }

    // Centers and sizes the glow on the alien's sprite (redone if the sprite changes, e.g. after Setup)
    private void FitToAlien()
    {
        if (alienRenderer.sprite == fittedTo) return;
        fittedTo = alienRenderer.sprite;
        if (fittedTo == null || glow.sprite == null) return;

        Bounds alienBounds = fittedTo.bounds;   // In the alien's own space
        Vector2 glowUnits = glow.sprite.bounds.size;
        float size = Mathf.Max(alienBounds.size.x, alienBounds.size.y) * glowSize;

        glow.transform.localPosition = new Vector3(alienBounds.center.x, alienBounds.center.y, 0f);
        glow.transform.localScale = new Vector3(size / glowUnits.x, size / glowUnits.y, 1f);
    }

    // A white circle that's solid in the middle and fades to nothing at the edge
    private static Sprite GeneratedGlow()
    {
        if (generatedGlow != null) return generatedGlow;

        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[size * size];
        Vector2 middle = new Vector2(size * 0.5f, size * 0.5f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), middle) / middle.x; // 0 middle, 1 edge
                float alpha = Mathf.Clamp01(1f - distance);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha * alpha); // Squared = softer edge
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();

        generatedGlow = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return generatedGlow;
    }
}