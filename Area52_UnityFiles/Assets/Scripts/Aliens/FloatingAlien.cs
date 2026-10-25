using UnityEngine;
using System.Collections;

/*
 * Art-prototype alien: flaots back and forth from left to right across the room, bobbing up and down a little, and turns to face the way it's going
 * Knows which owned alien it shows, has a box collider so it can be tapped, and can be paused (stops in place) while selected
 * Can also fly straight up off screen (leaving to explore) and drop back down into its spot (coming home)
*/

[RequireComponent(typeof(SpriteRenderer))]
public class FloatingAlien : MonoBehaviour
{
    [Header("Side to side")]
    [Tooltip("How far (meters) it floats to each side of where you placed it.")]
    [SerializeField] private float travelDistance = 2f;

    [Tooltip("Roughly how fast it floats across (meters per second).")]
    [SerializeField] private float speed = 0.4f;

    [Header("Bob")]
    [Tooltip("How far (meters) it bobs up and down.")]
    [SerializeField] private float bobHeight = 0.06f;

    [Tooltip("Bobs per second.")]
    [SerializeField] private float bobSpeed = 0.8f;

    [Header("Facing")]
    [Tooltip("Flip the sprite so it faces the way it's moving.")]
    [SerializeField] private bool flipWhenTurning = true;

    [Tooltip("Tick this if it faces backwards while moving.")]
    [SerializeField] private bool artFacesLeft = false;

    [Header("Tapping")]
    [Tooltip("How thick (meters) the tap box is front to back.")]
    [SerializeField] private float tapBoxDepth = 0.2f;

    [Header("Exploring")]
    [Tooltip("How far (meters) it flies up when it leaves, and drops from when it comes back. Big enough to be fully off screen.")]
    [SerializeField] private float flyHeight = 8f;

    [Tooltip("How fast (meters per second) it starts flying up.")]
    [SerializeField] private float flyUpStartSpeed = 1f;

    [Tooltip("How quickly it speeds up while flying away (meters per second, every second).")]
    [SerializeField] private float flyUpAcceleration = 8f;

    [Tooltip("Seconds it takes to drop back into its spot when it comes home.")]
    [SerializeField] private float flyInSeconds = 1.5f;

    // The owned alien this one shows (set by AlienRoomDisplay)
    public OwnedAlien Owner { get; private set; }

    // While true it stays exactly where it is (selected by a tap)
    public bool Paused { get; set; }

    // True while it's flying off to explore or dropping back in (can't be tapped)
    public bool Flying { get; private set; }

    public SpriteRenderer SpriteRenderer => spriteRenderer;

    private SpriteRenderer spriteRenderer;
    private BoxCollider tapBox;
    private Vector3 centerLocal;
    private float sidePhase;
    private float bobPhase;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        centerLocal = transform.localPosition;

        // Starts each alien at a different point so two rooms don't move in sync
        sidePhase = Random.Range(0f, Mathf.PI * 2f);
        bobPhase = Random.Range(0f, Mathf.PI * 2f);
    }

    // Called right after it's spawned: which alien it is, its sprite, and a tap box that fits the sprite
    public void Setup(OwnedAlien owner)
    {
        Owner = owner;
        spriteRenderer.sprite = owner != null && owner.species != null ? owner.species.sprite : null;
        FitTapBox();
    }

    private void FitTapBox()
    {
        if (tapBox == null) tapBox = GetComponent<BoxCollider>();
        if (tapBox == null) tapBox = gameObject.AddComponent<BoxCollider>();
        if (spriteRenderer.sprite == null) return;

        // Sprite bounds are in the object's own space, so this fits whatever the pivot and scale are
        Bounds b = spriteRenderer.sprite.bounds;
        tapBox.center = b.center;
        tapBox.size = new Vector3(b.size.x, b.size.y, tapBoxDepth);
    }

    private void Update()
    {
        if (Paused || Flying) return; // Flying - the fly animations move it instead

        // Sine wave: full speed in the middle, slows and turns around at the ends
        if (travelDistance > 0f) sidePhase += Time.deltaTime * speed / travelDistance;
        bobPhase += Time.deltaTime * bobSpeed * Mathf.PI * 2f;

        float x = Mathf.Sin(sidePhase) * travelDistance;
        float y = Mathf.Sin(bobPhase) * bobHeight;
        transform.localPosition = centerLocal + new Vector3(x, y, 0f);

        if (flipWhenTurning)
        {
            bool movingRight = Mathf.Cos(sidePhase) >= 0f;
            spriteRenderer.flipX = artFacesLeft ? movingRight : !movingRight;
        }
    }


    // EXPLORING

    // Flies straight up, speeding up, then deletes itself once it's off screen
    // (only used on an alien AlienRoomDisplay has let go of, so nothing else is keeping track of this object)
    public void FlyAway()
    {
        if (Flying) return;
        Paused = false;
        StartCoroutine(FlyAwayRoutine());
    }

    private IEnumerator FlyAwayRoutine()
    {
        Flying = true;

        Vector3 pos = transform.localPosition;
        float topY = pos.y + flyHeight;
        float upSpeed = flyUpStartSpeed;

        while (pos.y < topY)
        {
            upSpeed += flyUpAcceleration * Time.deltaTime;
            pos.y += upSpeed * Time.deltaTime;
            transform.localPosition = pos;
            yield return null;
        }

        Destroy(gameObject);
    }

    // Starts above its spot and drops into it, fast at first then landing gently
    public void FlyIn()
    {
        StopAllCoroutines();
        StartCoroutine(FlyInRoutine());
    }

    private IEnumerator FlyInRoutine()
    {
        Flying = true;

        // Lands right in the middle of its path, then carries on floating from there without a jump
        sidePhase = 0f;
        bobPhase = 0f;

        Vector3 start = centerLocal + Vector3.up * flyHeight;
        float t = 0f;

        while (t < 1f)
        {
            t = flyInSeconds > 0f ? t + Time.deltaTime / flyInSeconds : 1f;
            float eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f); // Ease out: fast, then slow
            transform.localPosition = Vector3.LerpUnclamped(start, centerLocal, eased);
            yield return null;
        }

        transform.localPosition = centerLocal;
        Flying = false;
    }

    // Shows the path in the Scene view when the alien is selected
    private void OnDrawGizmosSelected()
    {
        Vector3 center = Application.isPlaying && transform.parent != null
            ? transform.parent.TransformPoint(centerLocal)
            : transform.position;
        Vector3 side = (transform.parent != null ? transform.parent.right : Vector3.right) * travelDistance;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(center - side, center + side);
        Gizmos.DrawWireSphere(center - side, 0.05f);
        Gizmos.DrawWireSphere(center + side, 0.05f);
    }
}