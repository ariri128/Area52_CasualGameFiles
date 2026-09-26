using UnityEngine;
using UnityEngine.InputSystem;

/*
 * Handles swiping between rooms - while the player drags, the camera follows the giner one-to-one
 * On release, it decides whether it's been dragged far enough to change the room, and then smoothly slides to that room's position
 * Should work with mouse in engine and touch on device
 * Only the camera's X moves for this - it's Y, Z, rotation, and FOV is set in the editor
*/

[RequireComponent(typeof(Camera))]
public class RoomCameraController : MonoBehaviour
{
    [Header("Swipe")]
    [Tooltip("How far (as a fraction of room spacing) you have to drag before letting go switches rooms.")]
    [SerializeField, Range(0.05f, 0.5f)] private float switchThreshold = 0.2f;

    [Tooltip("Swipe speed (in screen widths per second) that counts as a flick and switches rooms even on a short drag.")]
    [SerializeField] private float flickSpeed = 1.5f;

    [Tooltip("Pixels the finger must move before it counts as a drag and not a tap.")]
    [SerializeField] private float dragDeadZone = 15f;

    [Tooltip("How much the camera follows when dragging past the first or last room (0 = not at all, 1 = no resistance).")]
    [SerializeField, Range(0f, 1f)] private float edgeResistance = 0.3f;

    [Header("Slide")]
    [Tooltip("Roughly how long the slide into a room takes, in seconds.")]
    [SerializeField] private float slideTime = 0.25f;

    private Camera cam;

    private float targetX;
    private float slideVelocity;

    private bool isPressed;
    private bool isDragging;
    private Vector2 pressStartPos;
    private float dragStartCamX;
    private float lastPointerX;
    private float pointerVelocityX;

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void Start()
    {
        if (RoomManager.main == null)
        {
            Debug.LogError("RoomCameraController needs a RoomManager in the scene.", this);
            enabled = false;
            return;
        }

        RoomManager.main.onRoomChanged.AddListener(HandleRoomChanged);

        targetX = RoomManager.main.GetRoomPosition(RoomManager.main.CurrentRoomIndex).x;
        SetCameraX(targetX);
    }

    private void OnDestroy()
    {
        if (RoomManager.main != null) RoomManager.main.onRoomChanged.RemoveListener(HandleRoomChanged);
    }

    private void Update()
    {
        HandleSwipeInput();
        UpdateSlide();
    }


    // INPUT

    private void HandleSwipeInput()
    {
        Pointer pointer = Pointer.current;
        if (pointer == null) return;

        bool pressedNow = pointer.press.isPressed;
        Vector2 pointerPos = pointer.position.ReadValue();

        if (pressedNow && !isPressed) BeginPress(pointerPos);
        else if (pressedNow && isPressed) ContinuePress(pointerPos);
        else if (!pressedNow && isPressed) EndPress();
    }

    private void BeginPress(Vector2 pointerPos)
    {
        isPressed = true;
        isDragging = false;
        pressStartPos = pointerPos;
        lastPointerX = pointerPos.x;
        pointerVelocityX = 0f;
        dragStartCamX = transform.position.x; // Starts from where the camera is, even mid-slide
    }

    private void ContinuePress(Vector2 pointerPos)
    {
        TrackPointerVelocity(pointerPos.x);

        float dragPixels = pointerPos.x - pressStartPos.x;
        if (!isDragging && Mathf.Abs(dragPixels) > dragDeadZone)
        {
            isDragging = true;
            slideVelocity = 0f;
        }

        if (isDragging)
        {
            // Finger moves right -> camera moves left (toward the previous room)
            float x = dragStartCamX - dragPixels * WorldUnitsPerPixel();
            SetCameraX(ApplyEdgeResistance(x));
        }
    }

    private void EndPress()
    {
        isPressed = false;
        if (!isDragging) return; // Recognizes if it was a tap instead of a swipe
        isDragging = false;

        float dragWorld = (lastPointerX - pressStartPos.x) * WorldUnitsPerPixel();
        float flick = pointerVelocityX / Screen.width;

        int direction = 0;
        if (Mathf.Abs(flick) >= flickSpeed)
            direction = flick > 0f ? -1 : 1;
        else if (Mathf.Abs(dragWorld) >= RoomManager.main.RoomSpacing * switchThreshold)
            direction = dragWorld > 0f ? -1 : 1;

        // Direction 0 still calls GoToRoom so the camera slides back into place
        RoomManager.main.GoToRoom(RoomManager.main.CurrentRoomIndex + direction);
    }

    private void TrackPointerVelocity(float pointerX)
    {
        float dt = Time.unscaledDeltaTime;
        if (dt > 0f)
        {
            float frameVelocity = (pointerX - lastPointerX) / dt;
            pointerVelocityX = Mathf.Lerp(pointerVelocityX, frameVelocity, 0.5f);
        }
        lastPointerX = pointerX;
    }


    // MOVEMENT

    private void HandleRoomChanged(int roomIndex)
    {
        targetX = RoomManager.main.GetRoomPosition(roomIndex).x;
    }

    private void UpdateSlide()
    {
        if (isDragging) return;
        float x = Mathf.SmoothDamp(transform.position.x, targetX, ref slideVelocity, slideTime);
        SetCameraX(x);
    }

    private float ApplyEdgeResistance(float x)
    {
        float minX = RoomManager.main.GetRoomPosition(0).x;
        float maxX = RoomManager.main.GetRoomPosition(Mathf.Max(0, RoomManager.main.RoomCount - 1)).x;

        if (x < minX) return minX - (minX - x) * edgeResistance;
        if (x > maxX) return maxX + (x - maxX) * edgeResistance;
        return x;
    }

    // Calculates many world units one screen pixel covers at the front of the rooms, so that the room under your finger moves exactly with it
    private float WorldUnitsPerPixel()
    {
        const float samplePixels = 100f;
        Plane frontPlane = new Plane(Vector3.back, RoomManager.main.transform.position);

        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Ray rayA = cam.ScreenPointToRay(screenCenter);
        Ray rayB = cam.ScreenPointToRay(screenCenter + Vector2.right * samplePixels);

        if (frontPlane.Raycast(rayA, out float hitA) && frontPlane.Raycast(rayB, out float hitB))
        {
            return Vector3.Distance(rayA.GetPoint(hitA), rayB.GetPoint(hitB)) / samplePixels;
        }

        return 0.01f; // Fallback, shouldn't happen with a camera in front of the rooms
    }

    private void SetCameraX(float x)
    {
        Vector3 pos = transform.position;
        pos.x = x;
        transform.position = pos;
    }
}