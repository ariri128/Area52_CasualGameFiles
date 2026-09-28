using UnityEngine;

/*
 * Art-prototype alien: flaots back and forth from left to right across the room, bobbing up and down a little, and turns to face the way it's going
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

    private SpriteRenderer spriteRenderer;
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

    private void Update()
    {
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