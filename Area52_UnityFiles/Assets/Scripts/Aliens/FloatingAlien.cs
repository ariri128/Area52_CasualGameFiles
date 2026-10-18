using UnityEngine;

/*
 * Art-prototype alien: flaots back and forth from left to right across the room, bobbing up and down a little, and turns to face the way it's going
 * Knows which owned alien it shows, has a box collider so it can be tapped, and can be paused (stops in place) while selected
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

    // The owned alien this one shows (set by AlienRoomDisplay)
    public OwnedAlien Owner { get; private set; }

    // While true it stays exactly where it is (selected by a tap)
    public bool Paused { get; set; }

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
        if (Paused) return;

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