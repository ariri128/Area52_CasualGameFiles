using UnityEngine;
using System.Collections;

/*
 * An alien in its room: roams around the whole floor, bobbing up and down, and turns to face the way it's going
 * It goes around, behind and in front of standing furniture but never through it (rugs and decor don't get in the way)
 * Swaps between its eyes-open and eyes-closed sprites so it looks animated
 * Knows which owned alien it shows, has a box collider so it can be tapped, and can be paused (stops in place) while selected
 * Can also fly straight up off screen (leaving to explore) and drop back down into its spot (coming home)
*/

[RequireComponent(typeof(SpriteRenderer))]
public class FloatingAlien : MonoBehaviour
{
    [Header("Roaming")]
    [Tooltip("How fast it floats around (meters per second).")]
    [SerializeField] private float speed = 0.4f;

    [Tooltip("Shortest and longest time (seconds) it waits after reaching a spot before heading somewhere else.")]
    [SerializeField] private Vector2 waitSeconds = new Vector2(0.5f, 2.5f);

    [Tooltip("How far it keeps from furniture and walls, on top of half its width (meters).")]
    [SerializeField] private float clearance = 0.05f;

    [Tooltip("How much of the sprite's width counts as its body when keeping it out of furniture. " +
             "Lower it if aliens with wide wings or arms can't fit between pieces.")]
    [SerializeField, Range(0.3f, 1f)] private float bodyWidth = 0.7f;

    [Tooltip("How far (meters) it stays back from the front edge of the floor, so it doesn't get too close to the camera.")]
    [SerializeField] private float frontMargin = 0.3f;

    [Tooltip("Shortest trip (meters) - stops it from just shuffling on the spot.")]
    [SerializeField] private float minTrip = 0.5f;

    [Header("Bob")]
    [Tooltip("How far (meters) it bobs up and down.")]
    [SerializeField] private float bobHeight = 0.06f;

    [Tooltip("Bobs per second.")]
    [SerializeField] private float bobSpeed = 0.8f;

    [Header("Blinking")]
    [Tooltip("Shortest and longest time (seconds) its eyes stay open between blinks.")]
    [SerializeField] private Vector2 secondsBetweenBlinks = new Vector2(5f, 6f);

    [Tooltip("How long (seconds) the eyes-closed sprite shows for each blink.")]
    [SerializeField] private float blinkSeconds = 0.15f;

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

    [Header("Eating")]
    [Tooltip("How much bigger it gets for a moment when a pellet lands (0.12 = 12% bigger).")]
    [SerializeField] private float chompAmount = 0.12f;
    [SerializeField] private float chompSeconds = 0.2f;

    private Coroutine chompRoutine;

    [Tooltip("How fast (meters per second) it starts flying up.")]
    [SerializeField] private float flyUpStartSpeed = 1f;

    [Tooltip("How quickly it speeds up while flying away (meters per second, every second).")]
    [SerializeField] private float flyUpAcceleration = 8f;

    [Tooltip("Seconds it takes to drop back into its spot when it comes home.")]
    [SerializeField] private float flyInSeconds = 1.5f;

    [Tooltip("How big it is (compared to normal) when it comes out of the ship's glass. It grows to full size as it floats to its spot.")]
    [SerializeField] private float dropOffStartScale = 0.4f;

    // The owned alien this one shows (set by AlienRoomDisplay)
    public OwnedAlien Owner { get; private set; }

    // While true it stays exactly where it is (selected by a tap)
    public bool Paused { get; set; }

    // True while it's flying off to explore or dropping back in (can't be tapped)
    public bool Flying { get; private set; }

    public SpriteRenderer SpriteRenderer => spriteRenderer;

    private SpriteRenderer spriteRenderer;
    private BoxCollider tapBox;
    private Vector3 centerLocal; // Its home spot (where it lands when it comes back from exploring)
    private Vector3 normalScale;
    private float bobPhase;

    // Roaming
    private RoomFurnitureDetector room;
    private Vector3 floorPos; // The point on the floor under it (world)
    private float hoverHeight; // How high above the floor it floats
    private Vector3 target;
    private bool hasTarget;
    private float waitUntil;

    // Animation
    private Sprite openSprite, closedSprite;
    private float frameTimer;
    private bool showingClosed;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        centerLocal = transform.localPosition;
        normalScale = transform.localScale;

        // Starts each alien at a different point so they don't all move in sync
        bobPhase = Random.Range(0f, Mathf.PI * 2f);
        frameTimer = Random.Range(0f, secondsBetweenBlinks.y); // First blink comes at a different time for each alien
        waitUntil = Time.time + Random.Range(0f, waitSeconds.y);
    }

    // Called right after it's spawned: which alien it is, its sprites, and a tap box that fits the sprite
    public void Setup(OwnedAlien owner)
    {
        Owner = owner;
        openSprite = owner != null && owner.species != null ? owner.species.sprite : null;
        closedSprite = owner != null && owner.species != null ? owner.species.spriteEyesClosed : null;
        showingClosed = false;
        spriteRenderer.sprite = openSprite;
        FitTapBox();

        // The room it's in (aliens are spawned as children of their room)
        room = GetComponentInParent<RoomFurnitureDetector>();
        if (room != null)
        {
            hoverHeight = transform.position.y - room.FloorY;
            floorPos = OnFloor(transform.position);

            // Spawned inside a piece of furniture? Start at the nearest free spot instead
            if (!IsFree(floorPos) && FindNearestFree(floorPos, out Vector3 free))
            {
                floorPos = free;
                transform.position = floorPos + Vector3.up * hoverHeight;
                centerLocal = transform.localPosition;
            }
        }
    }

    // Where it is on the floor right now, and which room - so the room display can put it back after a refresh
    public Vector3 FloorPosition => floorPos;
    public RoomFurnitureDetector Room => room;

    // Carries on from this floor point instead of its starting spot (used after the room's aliens are respawned)
    public void ContinueFrom(Vector3 floorPoint)
    {
        if (room == null) return;
        floorPos = OnFloor(floorPoint);
        transform.position = floorPos + Vector3.up * hoverHeight;
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
        Animate(); // Keeps blinking even while selected
        if (Paused || Flying) return; // Flying - the fly animations move it instead

        bobPhase += Time.deltaTime * bobSpeed * Mathf.PI * 2f;
        if (room != null) Roam();

        float bob = Mathf.Sin(bobPhase) * bobHeight;
        transform.position = floorPos + Vector3.up * (hoverHeight + bob);
    }


    // ANIMATION

    // Eyes open for 5-6 seconds, a quick blink, then open again
    private void Animate()
    {
        if (closedSprite == null || Flying) return;

        frameTimer -= Time.deltaTime;
        if (frameTimer > 0f) return;

        showingClosed = !showingClosed;
        spriteRenderer.sprite = showingClosed ? closedSprite : openSprite;
        frameTimer = showingClosed ? blinkSeconds : Random.Range(secondsBetweenBlinks.x, secondsBetweenBlinks.y);
    }


    // ROAMING

    private void Roam()
    {
        // Furniture was moved on top of it - head straight out to the nearest free spot
        if (!IsFree(floorPos))
        {
            if (FindNearestFree(floorPos, out Vector3 free)) MoveTowards(free, ignoreFurniture: true);
            hasTarget = false;
            return;
        }

        if (!hasTarget)
        {
            if (Time.time < waitUntil) return;
            hasTarget = PickTarget(out target);
            if (!hasTarget) { waitUntil = Time.time + 1f; return; } // Boxed in - try again in a second
        }

        if (!MoveTowards(target, ignoreFurniture: false))
        {
            hasTarget = false; // Something's in the way now (furniture moved) - pick somewhere else
            return;
        }

        if (FlatDistance(floorPos, target) < 0.001f)
        {
            hasTarget = false;
            waitUntil = Time.time + Random.Range(waitSeconds.x, waitSeconds.y);
        }
    }

    // Steps toward "to". Returns false (and doesn't move) if the step would go into furniture
    private bool MoveTowards(Vector3 to, bool ignoreFurniture)
    {
        Vector3 next = Vector3.MoveTowards(floorPos, to, speed * Time.deltaTime);
        if (!ignoreFurniture && !IsFree(next)) return false;

        Face(next - floorPos);
        floorPos = next;
        return true;
    }

    // A random free spot on the floor it can float to in a straight line
    private bool PickTarget(out Vector3 spot)
    {
        for (int attempt = 0; attempt < 40; attempt++)
        {
            float z = Random.Range(0f, room.FloorDepth);
            float halfWidth = room.HalfWidthAt(z);
            Vector3 candidate = OnFloor(room.FloorPoint(Random.Range(-halfWidth, halfWidth), z));

            if (FlatDistance(candidate, floorPos) < minTrip) continue;
            if (IsFree(candidate) && PathIsClear(floorPos, candidate))
            {
                spot = candidate;
                return true;
            }
        }
        spot = floorPos;
        return false;
    }

    private bool PathIsClear(Vector3 from, Vector3 to)
    {
        int steps = Mathf.CeilToInt(FlatDistance(from, to) / 0.05f);
        for (int i = 1; i <= steps; i++)
            if (!IsFree(Vector3.Lerp(from, to, i / (float)steps))) return false;
        return true;
    }

    // Can it float above this floor point without its body being in a wall or a standing piece of furniture?
    private bool IsFree(Vector3 point)
    {
        if (room == null) return true;
        float radius = BodyRadius;

        // Walls (x = across from the middle, z = back from the front edge)
        Vector3 offset = point - room.FloorPoint(0f, 0f);
        float x = Vector3.Dot(offset, room.AcrossDirection);
        float z = Vector3.Dot(offset, room.BackDirection);
        if (z < frontMargin || z > room.FloorDepth - radius) return false;
        if (Mathf.Abs(x) > room.HalfWidthAt(z) - radius) return false;

        // Standing furniture (rugs and decor don't block it), checked flat along the floor
        foreach (FurnitureItem item in room.Items)
        {
            if (item == null || item.Definition == null || !item.isActiveAndEnabled) continue;
            if (item.Definition.placement != FurniturePlacement.Floor) continue;

            FurnitureBox box = item.GetBox().Inflated(radius);
            if (box.Contains(new Vector3(point.x, box.center.y, point.z))) return false;
        }
        return true;
    }

    // Closest free point on a grid around "from" (false if the whole room is full)
    private bool FindNearestFree(Vector3 from, out Vector3 best)
    {
        best = from;
        float bestDistance = float.MaxValue;
        const float step = 0.1f;

        for (float z = 0f; z <= room.FloorDepth; z += step)
        {
            float halfWidth = room.HalfWidthAt(z);
            for (float x = -halfWidth; x <= halfWidth; x += step)
            {
                Vector3 point = OnFloor(room.FloorPoint(x, z));
                float distance = FlatDistance(point, from);
                if (distance < bestDistance && IsFree(point))
                {
                    bestDistance = distance;
                    best = point;
                }
            }
        }
        return bestDistance < float.MaxValue;
    }

    // Half its body's width in meters, plus the clearance
    private float BodyRadius
    {
        get
        {
            Sprite sprite = openSprite != null ? openSprite : spriteRenderer.sprite;
            float width = sprite != null ? sprite.bounds.size.x * Mathf.Abs(transform.lossyScale.x) : 0.5f;
            return width * bodyWidth * 0.5f + clearance;
        }
    }

    private Vector3 OnFloor(Vector3 world) => new Vector3(world.x, room != null ? room.FloorY : world.y, world.z);

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x, dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    // Turns to face left or right; moving straight toward or away from the camera keeps the way it was facing
    private void Face(Vector3 direction)
    {
        if (!flipWhenTurning || room == null) return;
        float across = Vector3.Dot(direction, room.AcrossDirection);
        if (Mathf.Abs(across) < 0.0001f) return;

        bool movingRight = across > 0f;
        spriteRenderer.flipX = artFacesLeft ? movingRight : !movingRight;
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

    // Starts above its spot and drops into it (the old way, without the ship)
    public void FlyIn()
    {
        StartFlyIn(centerLocal + Vector3.up * flyHeight, 1f);
    }

    // Starts where the ship's glass was, small, and floats to its spot while growing to full size
    public void FlyInFrom(Vector3 worldStart)
    {
        Vector3 startLocal = transform.parent != null ? transform.parent.InverseTransformPoint(worldStart) : worldStart;
        StartFlyIn(startLocal, dropOffStartScale);
    }

    private void StartFlyIn(Vector3 startLocal, float startScale)
    {
        StopAllCoroutines();
        StartCoroutine(FlyInRoutine(startLocal, startScale));
    }

    private IEnumerator FlyInRoutine(Vector3 start, float startScale)
    {
        Flying = true;
        bobPhase = 0f;
        spriteRenderer.sprite = openSprite;

        float t = 0f;
        while (t < 1f)
        {
            t = flyInSeconds > 0f ? t + Time.deltaTime / flyInSeconds : 1f;
            float eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f); // Ease out: fast, then slow
            transform.localPosition = Vector3.LerpUnclamped(start, centerLocal, eased);
            transform.localScale = normalScale * Mathf.Lerp(startScale, 1f, eased);
            yield return null;
        }

        transform.localPosition = centerLocal;
        transform.localScale = normalScale;

        // Carries on roaming from its home spot
        if (room != null) floorPos = OnFloor(transform.position);
        hasTarget = false;
        waitUntil = Time.time + Random.Range(waitSeconds.x, waitSeconds.y);
        Flying = false;
    }

    // EATING

    // A quick bump in size when a pellet lands - skipped while flying, since flying changes the size itself
    public void Chomp()
    {
        if (Flying || !isActiveAndEnabled) return;
        if (chompRoutine != null) StopCoroutine(chompRoutine);
        chompRoutine = StartCoroutine(ChompRoutine());
    }

    private IEnumerator ChompRoutine()
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / chompSeconds)
        {
            transform.localScale = normalScale * (1f + chompAmount * Mathf.Sin(t * Mathf.PI));
            yield return null;
        }
        transform.localScale = normalScale;
        chompRoutine = null;
    }

    // Shows where it's heading and how wide its body counts as, when selected in the Scene view
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || room == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(floorPos, BodyRadius);
        if (hasTarget)
        {
            Gizmos.DrawLine(floorPos, target);
            Gizmos.DrawWireSphere(target, 0.05f);
        }
    }
}

/* ver.1
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

    [Header("Eating")]
    [Tooltip("How much bigger it gets for a moment when a pellet lands (0.12 = 12% bigger).")]
    [SerializeField] private float chompAmount = 0.12f;
    [SerializeField] private float chompSeconds = 0.2f;

    private Coroutine chompRoutine;

    [Tooltip("How fast (meters per second) it starts flying up.")]
    [SerializeField] private float flyUpStartSpeed = 1f;

    [Tooltip("How quickly it speeds up while flying away (meters per second, every second).")]
    [SerializeField] private float flyUpAcceleration = 8f;

    [Tooltip("Seconds it takes to drop back into its spot when it comes home.")]
    [SerializeField] private float flyInSeconds = 1.5f;

    [Tooltip("How big it is (compared to normal) when it comes out of the ship's glass. It grows to full size as it floats to its spot.")]
    [SerializeField] private float dropOffStartScale = 0.4f;

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
    private Vector3 normalScale;
    private float sidePhase;
    private float bobPhase;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        centerLocal = transform.localPosition;
        normalScale = transform.localScale;

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

    // Starts above its spot and drops into it (the old way, without the ship)
    public void FlyIn()
    {
        StartFlyIn(centerLocal + Vector3.up * flyHeight, 1f);
    }

    // Starts where the ship's glass was, small, and floats to its spot while growing to full size
    public void FlyInFrom(Vector3 worldStart)
    {
        Vector3 startLocal = transform.parent != null ? transform.parent.InverseTransformPoint(worldStart) : worldStart;
        StartFlyIn(startLocal, dropOffStartScale);
    }

    private void StartFlyIn(Vector3 startLocal, float startScale)
    {
        StopAllCoroutines();
        StartCoroutine(FlyInRoutine(startLocal, startScale));
    }

    private IEnumerator FlyInRoutine(Vector3 start, float startScale)
    {
        Flying = true;

        // Lands right in the middle of its path, then carries on floating from there without a jump
        sidePhase = 0f;
        bobPhase = 0f;

        float t = 0f;
        while (t < 1f)
        {
            t = flyInSeconds > 0f ? t + Time.deltaTime / flyInSeconds : 1f;
            float eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f); // Ease out: fast, then slow
            transform.localPosition = Vector3.LerpUnclamped(start, centerLocal, eased);
            transform.localScale = normalScale * Mathf.Lerp(startScale, 1f, eased);
            yield return null;
        }

        transform.localPosition = centerLocal;
        transform.localScale = normalScale;
        Flying = false;
    }

    // EATING

    // A quick bump in size when a pellet lands - skipped while flying, since flying changes the size itself
    public void Chomp()
    {
        if (Flying || !isActiveAndEnabled) return;
        if (chompRoutine != null) StopCoroutine(chompRoutine);
        chompRoutine = StartCoroutine(ChompRoutine());
    }

    private IEnumerator ChompRoutine()
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / chompSeconds)
        {
            transform.localScale = normalScale * (1f + chompAmount * Mathf.Sin(t * Mathf.PI));
            yield return null;
        }
        transform.localScale = normalScale;
        chompRoutine = null;
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
*/