using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;

/*
 * Moving a piece of furniture with the gizmo:
 *   Drag the middle handle - slides it along the floor (never up or down):
 *                            side to side follows the finger, forward/back is gentler (Depth Speed), and it glides there
 *   Drag the ring's handle  - turns it left/right like a dial (in Turn Snap steps)
 *   Yellow outline = fine there; pulsing red = touching another piece or a wall
 *   Tap away (or Done) to finish:
 *     valid spot   -> it stays
 *     invalid spot -> it slides back to where it started
 *   "Must place" mode (new / replacing pieces): it can't be let go until it's valid, and Cancel undoes it
 * Decor sitting on the piece comes along with it
*/

public class FurnitureMover : MonoBehaviour
{
    public static FurnitureMover main { get; private set; }

    [Header("Parts")]
    [SerializeField] private Camera cam;
    [Tooltip("The whole gizmo. Hidden when nothing is moving; its middle is kept over the middle of the piece.")]
    [SerializeField] private RectTransform gizmo;
    [Tooltip("Optional. The ring (with the turn handle on it) - it spins with the piece.")]
    [SerializeField] private RectTransform ring;
    [Tooltip("Optional. Finishes, the same as tapping away.")]
    [SerializeField] private Button doneButton;
    [Tooltip("Optional. Only shown in \"must place\" mode: gives up and undoes adding / replacing.")]
    [SerializeField] private Button cancelButton;

    [Header("Moving")]
    [Tooltip("How far it moves side to side when the finger moves sideways. 1 = stays under the finger.")]
    [SerializeField] private float acrossSpeed = 1f;
    [Tooltip("How far it moves forward/back (deeper into the room) when the finger moves up/down. " +
             "Lower = gentler, so small slips barely move it.")]
    [SerializeField] private float depthSpeed = 0.5f;
    [Tooltip("Seconds it takes to catch up with the finger. 0 = instant, higher = smoother but floatier.")]
    [SerializeField] private float moveSmoothing = 0.08f;

    [Header("Turning")]
    [Tooltip("Turns in steps of this many degrees. 0 = smooth.")]
    [SerializeField] private float turnSnap = 15f;

    [Header("Look")]
    [SerializeField] private Color validColor = new Color(1f, 0.85f, 0.25f);
    [SerializeField] private Color invalidColor = Color.red;
    [Tooltip("The red pulses between Invalid Color and this.")]
    [SerializeField] private Color invalidDimColor = new Color(0.45f, 0f, 0f);
    [Tooltip("Pulses per second.")]
    [SerializeField] private float pulseSpeed = 1.5f;
    [SerializeField] private float snapBackSeconds = 0.15f;

    private FurnitureItem piece;
    private RoomFurnitureDetector room;
    private bool mustPlace;
    private Action<bool> whenEnded; // Gets true if the piece stayed where the player put it

    private Vector3 startPosition;
    private Quaternion startRotation;

    // Decor riding along, and where each one belongs when it's put back down
    private readonly List<Transform> riders = new List<Transform>();
    private readonly List<Transform> riderParents = new List<Transform>();

    // Moving
    private Vector2 lastDragPos;
    private Vector3 moveTarget;   // Where the piece's underside is gliding to
    private Vector3 moveVelocity; // Used by the glide
    private bool gliding;
    private bool draggingMove;

    // Turning
    private float lastAngle, turned;
    private Vector3 turnPivot, turnStartPosition;
    private Quaternion turnStartRotation;

    private bool snappingBack;

    public bool IsMoving => piece != null;
    public FurnitureItem Piece => piece;
    public bool IsValid { get; private set; }

    private void Awake()
    {
        if (main != null && main != this)
        {
            Debug.LogWarning("More than one FurnitureMover in the scene. Destroying the extra one.", this);
            Destroy(this);
            return;
        }
        main = this;
        if (cam == null) cam = Camera.main;

        if (doneButton != null) doneButton.onClick.AddListener(TryFinish);
        if (cancelButton != null) cancelButton.onClick.AddListener(CancelPressed);
        gizmo.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (main == this) main = null;
        RoomCameraController.SetSwipeBlocked(this, false);
    }


    // START / FINISH

    // mustPlace false (the Move button): letting go somewhere invalid slides it back
    // mustPlace true (new / replacing pieces): it can't be let go until it's valid; Cancel ends it with false
    // onEnded gets true if the piece stayed where the player put it
    public void Begin(FurnitureItem item, bool mustPlace, Action<bool> onEnded)
    {
        if (item == null || item.Room == null || IsMoving) return;

        piece = item;
        room = item.Room;
        this.mustPlace = mustPlace;
        whenEnded = onEnded;
        startPosition = item.transform.position;
        startRotation = item.transform.rotation;
        StopGlide();

        // Decor on it rides along: parented to it for now, put back when it's done
        riders.Clear();
        riderParents.Clear();
        foreach (FurnitureItem rider in room.ItemsSittingOn(item))
        {
            riders.Add(rider.transform);
            riderParents.Add(rider.transform.parent);
            rider.transform.SetParent(item.transform, true);
        }

        RoomCameraController.SetSwipeBlocked(this, true); // No sliding between rooms mid-move
        gizmo.gameObject.SetActive(true);
        if (ring != null) ring.localRotation = Quaternion.identity;
        if (cancelButton != null) cancelButton.gameObject.SetActive(mustPlace);

        Recheck();
        FollowPiece();
    }

    // Tapping away / Done: keeps it if the spot is valid; otherwise slides it back (or, if it must be placed, stays red)
    public void TryFinish()
    {
        if (!IsMoving || snappingBack) return;

        CompleteGlide(); // Judged where it was heading, not halfway there
        Recheck();
        if (IsValid) { End(true); return; }
        if (mustPlace) return; // Has to go somewhere valid first - the red pulse is saying so

        StartCoroutine(SnapBackThenEnd());
    }

    // Something else needs it stopped right now (e.g. leaving for the breeding room)
    public void ForceStop()
    {
        if (!IsMoving) return;
        StopAllCoroutines();
        snappingBack = false;

        CompleteGlide();
        Recheck();
        if (IsValid) { End(true); return; }
        piece.transform.SetPositionAndRotation(startPosition, startRotation);
        End(false);
    }

    private void CancelPressed()
    {
        if (!IsMoving || snappingBack) return;
        StopGlide();
        piece.transform.SetPositionAndRotation(startPosition, startRotation);
        End(false); // Whoever started it undoes the adding / replacing
    }

    private IEnumerator SnapBackThenEnd()
    {
        snappingBack = true;
        StopGlide();
        Vector3 fromPosition = piece.transform.position;
        Quaternion fromRotation = piece.transform.rotation;

        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / Mathf.Max(0.0001f, snapBackSeconds))
        {
            float eased = t * t * (3f - 2f * t);
            piece.transform.SetPositionAndRotation(Vector3.Lerp(fromPosition, startPosition, eased),
                                                   Quaternion.Slerp(fromRotation, startRotation, eased));
            yield return null;
        }
        piece.transform.SetPositionAndRotation(startPosition, startRotation);

        snappingBack = false;
        End(false);
    }

    private void End(bool placed)
    {
        FurnitureItem item = piece;
        StopGlide();

        // The decor goes back to belonging to the room, wherever the piece took it
        for (int i = 0; i < riders.Count; i++)
            if (riders[i] != null) riders[i].SetParent(riderParents[i], true);
        riders.Clear();
        riderParents.Clear();

        Action<bool> callback = whenEnded;
        piece = null; // Cleared before the callback, so anything it does sees the mover as finished
        room = null;
        whenEnded = null;

        gizmo.gameObject.SetActive(false);
        RoomCameraController.SetSwipeBlocked(this, false);
        if (item != null) item.ClearHighlight();

        callback?.Invoke(placed);
    }


    // DRAGGING (called by the gizmo's handles)

    public void OnHandleBeginDrag(GizmoHandleKind kind, Vector2 screenPos)
    {
        if (!IsMoving || snappingBack) return;

        if (kind == GizmoHandleKind.Move)
        {
            lastDragPos = screenPos;
            if (!gliding) moveTarget = piece.GetBox().BottomCenter; // Still gliding? Carry on from where it's heading
            gliding = true;
            draggingMove = true;
        }
        else
        {
            CompleteGlide(); // Turns from where it actually is

            FurnitureBox box = piece.GetBox();
            lastAngle = ScreenAngle(screenPos, box.center);
            turned = 0f;
            turnPivot = box.center;
            turnStartPosition = piece.transform.position;
            turnStartRotation = piece.transform.rotation;
        }
    }

    public void OnHandleDrag(GizmoHandleKind kind, Vector2 screenPos)
    {
        if (!IsMoving || snappingBack) return;

        if (kind == GizmoHandleKind.Move)
        {
            Vector2 fingerMoved = screenPos - lastDragPos;
            lastDragPos = screenPos;

            // How many pixels one meter of floor takes up here - sideways is used for both directions,
            // so up/down isn't stretched by the camera looking at the floor at an angle
            Vector3 across = room.AcrossDirection;
            float pixelsPerMeter = PixelsPerMeter(moveTarget, across);
            if (pixelsPerMeter < 1f) return;

            moveTarget += across * (fingerMoved.x / pixelsPerMeter * acrossSpeed)
                        + room.BackDirection * (fingerMoved.y / pixelsPerMeter * depthSpeed); // Finger up = further back
            moveTarget = room.ClampToFloor(moveTarget); // Its middle can't leave the floor
        }
        else
        {
            // Adds up the small changes, so turning more than halfway round keeps working
            float angle = ScreenAngle(screenPos, turnPivot);
            turned += Mathf.DeltaAngle(lastAngle, angle);
            lastAngle = angle;

            // Clockwise on screen = clockwise seen from above
            float yaw = -turned;
            if (turnSnap > 0f) yaw = Mathf.Round(yaw / turnSnap) * turnSnap;

            Quaternion turn = Quaternion.AngleAxis(yaw, Vector3.up);
            piece.transform.SetPositionAndRotation(turnPivot + turn * (turnStartPosition - turnPivot), // Turns around its middle
                                                   turn * turnStartRotation);
            if (ring != null) ring.localRotation = Quaternion.Euler(0f, 0f, -yaw);
            Recheck();
        }
    }

    public void OnHandleEndDrag()
    {
        draggingMove = false; // It keeps gliding until it reaches where it was dragged to
        if (IsMoving) Recheck();
    }


    // EVERY FRAME

    private void Update()
    {
        if (!IsMoving) return;
        if (gliding) Glide();

        if (FurnitureEditor.main == null) return;
        Color color = validColor;
        if (!IsValid)
        {
            float wave = (Mathf.Sin(Time.unscaledTime * pulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f; // 0 -> 1 -> 0
            color = Color.Lerp(invalidDimColor, invalidColor, wave);
        }
        FurnitureEditor.main.ShowOutline(piece, color);
    }

    private void LateUpdate()
    {
        if (IsMoving) FollowPiece();
    }

    // Eases the piece toward where it's been dragged
    private void Glide()
    {
        Vector3 bottom = piece.GetBox().BottomCenter;
        Vector3 next = moveSmoothing > 0f
            ? Vector3.SmoothDamp(bottom, moveTarget, ref moveVelocity, moveSmoothing, Mathf.Infinity, Time.unscaledDeltaTime)
            : moveTarget;

        piece.transform.position += Flat(next - bottom);
        Recheck();

        // Arrived, and the finger's let go: stop gliding
        if (!draggingMove && Flat(moveTarget - next).sqrMagnitude < 0.000001f) StopGlide();
    }

    // Jumps straight to where it was gliding to
    private void CompleteGlide()
    {
        if (!gliding || piece == null) return;
        piece.transform.position += Flat(moveTarget - piece.GetBox().BottomCenter);
        StopGlide();
    }

    private void StopGlide()
    {
        gliding = false;
        draggingMove = false;
        moveVelocity = Vector3.zero;
    }

    // Keeps the gizmo's middle over the middle of the piece
    private void FollowPiece()
    {
        Vector3 screen = cam.WorldToScreenPoint(piece.GetBox().center);
        if (screen.z < 0f) return; // Behind the camera
        gizmo.position = new Vector3(screen.x, screen.y, gizmo.position.z);
    }


    // HELPERS

    private void Recheck()
    {
        IsValid = room != null && room.CheckSpot(piece) == RoomFurnitureDetector.SpotProblem.None;
    }

    // How many screen pixels one meter in "direction" takes up, at "point"
    private float PixelsPerMeter(Vector3 point, Vector3 direction)
    {
        Vector2 a = cam.WorldToScreenPoint(point);
        Vector2 b = cam.WorldToScreenPoint(point + direction);
        return Vector2.Distance(a, b);
    }

    // The finger's angle around "middle" (as seen on screen), in degrees
    private float ScreenAngle(Vector2 screenPos, Vector3 middle)
    {
        Vector2 offset = screenPos - (Vector2)cam.WorldToScreenPoint(middle);
        return Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
    }

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
}