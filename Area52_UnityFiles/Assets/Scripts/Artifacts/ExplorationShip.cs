using UnityEngine;
using System;
using System.Collections;

/*
 * The blue spaceship that takes an alien exploring and brings it back
 *   PickUp  - flies down into the room, the alien disappears and shows up in the glass, then the ship flies off
 *   DropOff - flies down with the alien in the glass, lets it out into the room, then flies off
 * ExplorationManager spawns it inside the room and tells it what to do - it deletes itself once it's gone
*/

public class ExplorationShip : MonoBehaviour
{
    [Header("Parts")]
    [Tooltip("The alien's picture in the glass: a child Sprite Renderer placed where the glass is.")]
    [SerializeField] private SpriteRenderer passenger;

    [Tooltip("Biggest the alien can be in the glass (width, height), in the ship's own units. " +
             "Shown as a yellow box when the ship is selected. Bigger sprites are shrunk to fit.")]
    [SerializeField] private Vector2 passengerMaxSize = new Vector2(3f, 3f);

    [Header("Flying")]
    [Tooltip("How far above its stop it starts and finishes (meters). Big enough to be fully off screen.")]
    [SerializeField] private float flyHeight = 8f;

    [Tooltip("Seconds it takes to come down to its stop.")]
    [SerializeField] private float arriveSeconds = 1.5f;

    [Tooltip("Seconds it waits at its stop before and after taking or dropping off the alien.")]
    [SerializeField] private float pauseSeconds = 0.5f;

    [Tooltip("How fast (meters per second) it starts flying away.")]
    [SerializeField] private float leaveStartSpeed = 1f;

    [Tooltip("How quickly it speeds up while flying away (meters per second, every second).")]
    [SerializeField] private float leaveAcceleration = 10f;

    private Vector3 passengerSpot;

    private void Awake()
    {
        if (passenger != null) passengerSpot = passenger.transform.localPosition;
        SetPassenger(null);
        ScreenBlocker.SetBlocked(this, true); // Nothing else works while the ship is here
    }

    private void OnDestroy()
    {
        ScreenBlocker.SetBlocked(this, false);
    }

    // Comes down, takes the alien (its sprite in the room is deleted), then leaves
    // onLeaving runs as it starts flying off with the alien
    public void PickUp(Vector3 stop, FloatingAlien alienInRoom, Sprite alienSprite, Action onLeaving)
    {
        StartCoroutine(PickUpRoutine(stop, alienInRoom, alienSprite, onLeaving));
    }

    // Comes down with the alien, lets it out, then leaves
    // onDroppedOff gets where the glass was (so the alien can come out from there); onGone runs once it's off screen
    public void DropOff(Vector3 stop, Sprite alienSprite, Action<Vector3> onDroppedOff, Action onGone)
    {
        StartCoroutine(DropOffRoutine(stop, alienSprite, onDroppedOff, onGone));
    }

    private IEnumerator PickUpRoutine(Vector3 stop, FloatingAlien alienInRoom, Sprite alienSprite, Action onLeaving)
    {
        SetPassenger(null);
        yield return Arrive(stop);
        yield return new WaitForSeconds(pauseSeconds);

        // The alien leaves the room and appears in the glass
        if (alienInRoom != null) Destroy(alienInRoom.gameObject);
        SetPassenger(alienSprite);
        yield return new WaitForSeconds(pauseSeconds);

        onLeaving?.Invoke();
        yield return Leave(stop);
        Destroy(gameObject);
    }

    private IEnumerator DropOffRoutine(Vector3 stop, Sprite alienSprite, Action<Vector3> onDroppedOff, Action onGone)
    {
        SetPassenger(alienSprite);
        yield return Arrive(stop);
        yield return new WaitForSeconds(pauseSeconds);

        // The alien leaves the glass and goes back into the room
        Vector3 glass = passenger != null ? transform.TransformPoint(passengerSpot) : transform.position; // Middle of the glass
        SetPassenger(null);
        onDroppedOff?.Invoke(glass);
        yield return new WaitForSeconds(pauseSeconds);

        yield return Leave(stop);
        onGone?.Invoke();
        Destroy(gameObject);
    }

    // From above the room down to its stop - fast at first, then slowing to a gentle stop
    private IEnumerator Arrive(Vector3 stop)
    {
        Vector3 start = stop + Vector3.up * flyHeight;
        float t = 0f;

        while (t < 1f)
        {
            t = arriveSeconds > 0f ? t + Time.deltaTime / arriveSeconds : 1f;
            float eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
            transform.localPosition = Vector3.LerpUnclamped(start, stop, eased);
            yield return null;
        }
        transform.localPosition = stop;
    }

    // Straight up, speeding up, until it's off screen
    private IEnumerator Leave(Vector3 stop)
    {
        Vector3 pos = transform.localPosition;
        float topY = stop.y + flyHeight;
        float speed = leaveStartSpeed;

        while (pos.y < topY)
        {
            speed += leaveAcceleration * Time.deltaTime;
            pos.y += speed * Time.deltaTime;
            transform.localPosition = pos;
            yield return null;
        }
    }

    // Shows (or hides, with null) the alien in the glass, shrunk to fit the glass
    private void SetPassenger(Sprite sprite)
    {
        if (passenger == null) return;

        passenger.sprite = sprite;
        passenger.enabled = sprite != null;
        if (sprite == null) return;

        Vector2 size = sprite.bounds.size;
        float fit = Mathf.Min(passengerMaxSize.x / size.x, passengerMaxSize.y / size.y);
        passenger.transform.localScale = Vector3.one * fit;

        // Centers the alien in the box, wherever its sprite's pivot is
        passenger.transform.localPosition = passengerSpot - sprite.bounds.center * fit;
    }

    // The yellow box shows how big the alien can be in the glass
    private void OnDrawGizmosSelected()
    {
        if (passenger == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.matrix = transform.localToWorldMatrix;
        Vector3 center = Application.isPlaying ? passengerSpot : passenger.transform.localPosition;
        Gizmos.DrawWireCube(passenger.transform.localPosition, new Vector3(passengerMaxSize.x, passengerMaxSize.y, 0f));
    }
}

/* ver.1
public class ExplorationShip : MonoBehaviour
{
    [Header("Parts")]
    [Tooltip("The alien's picture in the glass: a child Sprite Renderer placed where the glass is.")]
    [SerializeField] private SpriteRenderer passenger;

    [Tooltip("Biggest the alien can be in the glass (width, height), in the ship's own units. " +
             "Shown as a yellow box when the ship is selected. Bigger sprites are shrunk to fit.")]
    [SerializeField] private Vector2 passengerMaxSize = new Vector2(3f, 3f);

    [Header("Flying")]
    [Tooltip("How far above its stop it starts and finishes (meters). Big enough to be fully off screen.")]
    [SerializeField] private float flyHeight = 8f;

    [Tooltip("Seconds it takes to come down to its stop.")]
    [SerializeField] private float arriveSeconds = 1.5f;

    [Tooltip("Seconds it waits at its stop before and after taking or dropping off the alien.")]
    [SerializeField] private float pauseSeconds = 0.5f;

    [Tooltip("How fast (meters per second) it starts flying away.")]
    [SerializeField] private float leaveStartSpeed = 1f;

    [Tooltip("How quickly it speeds up while flying away (meters per second, every second).")]
    [SerializeField] private float leaveAcceleration = 10f;

    private Vector3 passengerSpot;

    private void Awake()
    {
        if (passenger != null) passengerSpot = passenger.transform.localPosition;
        SetPassenger(null);
    }

    // Comes down, takes the alien (its sprite in the room is deleted), then leaves
    // onLeaving runs as it starts flying off with the alien
    public void PickUp(Vector3 stop, FloatingAlien alienInRoom, Sprite alienSprite, Action onLeaving)
    {
        StartCoroutine(PickUpRoutine(stop, alienInRoom, alienSprite, onLeaving));
    }

    // Comes down with the alien, lets it out, then leaves
    // onDroppedOff gets where the glass was (so the alien can come out from there); onGone runs once it's off screen
    public void DropOff(Vector3 stop, Sprite alienSprite, Action<Vector3> onDroppedOff, Action onGone)
    {
        StartCoroutine(DropOffRoutine(stop, alienSprite, onDroppedOff, onGone));
    }

    private IEnumerator PickUpRoutine(Vector3 stop, FloatingAlien alienInRoom, Sprite alienSprite, Action onLeaving)
    {
        SetPassenger(null);
        yield return Arrive(stop);
        yield return new WaitForSeconds(pauseSeconds);

        // The alien leaves the room and appears in the glass
        if (alienInRoom != null) Destroy(alienInRoom.gameObject);
        SetPassenger(alienSprite);
        yield return new WaitForSeconds(pauseSeconds);

        onLeaving?.Invoke();
        yield return Leave(stop);
        Destroy(gameObject);
    }

    private IEnumerator DropOffRoutine(Vector3 stop, Sprite alienSprite, Action<Vector3> onDroppedOff, Action onGone)
    {
        SetPassenger(alienSprite);
        yield return Arrive(stop);
        yield return new WaitForSeconds(pauseSeconds);

        // The alien leaves the glass and goes back into the room
        Vector3 glass = passenger != null ? transform.TransformPoint(passengerSpot) : transform.position; // Middle of the glass
        SetPassenger(null);
        onDroppedOff?.Invoke(glass);
        yield return new WaitForSeconds(pauseSeconds);

        yield return Leave(stop);
        onGone?.Invoke();
        Destroy(gameObject);
    }

    // From above the room down to its stop - fast at first, then slowing to a gentle stop
    private IEnumerator Arrive(Vector3 stop)
    {
        Vector3 start = stop + Vector3.up * flyHeight;
        float t = 0f;

        while (t < 1f)
        {
            t = arriveSeconds > 0f ? t + Time.deltaTime / arriveSeconds : 1f;
            float eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
            transform.localPosition = Vector3.LerpUnclamped(start, stop, eased);
            yield return null;
        }
        transform.localPosition = stop;
    }

    // Straight up, speeding up, until it's off screen
    private IEnumerator Leave(Vector3 stop)
    {
        Vector3 pos = transform.localPosition;
        float topY = stop.y + flyHeight;
        float speed = leaveStartSpeed;

        while (pos.y < topY)
        {
            speed += leaveAcceleration * Time.deltaTime;
            pos.y += speed * Time.deltaTime;
            transform.localPosition = pos;
            yield return null;
        }
    }

    // Shows (or hides, with null) the alien in the glass, shrunk to fit the glass
    private void SetPassenger(Sprite sprite)
    {
        if (passenger == null) return;

        passenger.sprite = sprite;
        passenger.enabled = sprite != null;
        if (sprite == null) return;

        Vector2 size = sprite.bounds.size;
        float fit = Mathf.Min(passengerMaxSize.x / size.x, passengerMaxSize.y / size.y);
        passenger.transform.localScale = Vector3.one * fit;

        // Centers the alien in the box, wherever its sprite's pivot is
        passenger.transform.localPosition = passengerSpot - sprite.bounds.center * fit;
    }

    // The yellow box shows how big the alien can be in the glass
    private void OnDrawGizmosSelected()
    {
        if (passenger == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.matrix = transform.localToWorldMatrix;
        Vector3 center = Application.isPlaying ? passengerSpot : passenger.transform.localPosition;
        Gizmos.DrawWireCube(passenger.transform.localPosition, new Vector3(passengerMaxSize.x, passengerMaxSize.y, 0f));
    }
}
*/