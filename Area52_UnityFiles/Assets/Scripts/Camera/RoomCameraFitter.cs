using UnityEngine;

/*
 * Positions the camera so a room's front opening lines up with the edges of the screen on any device
 * The FOV stays a set position in inspector, this script works out how far back the camera should be based on each room
 * The camera in scene should be on (Z) and should center it on the opening (Y)
*/

[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class RoomCameraFitter : MonoBehaviour
{
    [Tooltip("The Rooms object (with RoomManager). Its position is room 0's front-center floor edge.")]
    [SerializeField] private Transform roomOrigin;

    [Tooltip("Inside width of the room (inside of left wall to inside of right wall).")]
    [SerializeField] private float openingWidth = 8f;

    [Tooltip("Inside height of the room (floor to ceiling).")]
    [SerializeField] private float openingHeight = 4.5f;

    [Tooltip("Inside depth of the room (front edge of the floor to the back wall).")]
    [SerializeField] private float roomDepth = 4.5f;

    [Tooltip("Keeps the top of the view low enough that the ceiling never shows, even at the back wall.")]
    [SerializeField] private bool hideCeiling = true;

    [Tooltip("Zooms in slightly so thin gaps at the screen edges never show. 1 = exact fit.")]
    [SerializeField, Range(1f, 1.1f)] private float overscan = 1.01f;

    private Camera cam;
    private float lastAspect = -1f;
    private float lastFov = -1f;

    private void OnEnable()
    {
        cam = GetComponent<Camera>();
        FitCamera();
    }

    // Changing a value in the Inspector forces a refit on the next frame
    private void OnValidate()
    {
        lastAspect = -1f;
    }

    private void LateUpdate()
    {
        if (cam == null) cam = GetComponent<Camera>();

        bool screenChanged = !Mathf.Approximately(cam.aspect, lastAspect);
        bool fovChanged = !Mathf.Approximately(cam.fieldOfView, lastFov);
        if (screenChanged || fovChanged) FitCamera();
    }

    public void FitCamera()
    {
        if (cam == null || roomOrigin == null) return;
        if (cam.orthographic)
        {
            Debug.LogWarning("RoomCameraFitter expects a Perspective camera.", this);
            return;
        }

        lastAspect = cam.aspect;
        lastFov = cam.fieldOfView;

        float tanHalfFov = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);

        float maxHeightFromWidth = openingWidth / cam.aspect;
        float maxHeightFromCeiling = hideCeiling ? openingHeight - roomDepth * tanHalfFov : openingHeight;

        if (maxHeightFromCeiling <= 0f)
        {
            Debug.LogWarning("RoomCameraFitter: Field of View is too high to hide the ceiling in a room this deep. Lower the FOV.", this);
            return;
        }

        float visibleHeight = Mathf.Min(maxHeightFromWidth, maxHeightFromCeiling) / overscan;
        float distance = visibleHeight / (2f * tanHalfFov);

        Vector3 pos = transform.position;
        pos.y = roomOrigin.position.y + visibleHeight * 0.5f;
        pos.z = roomOrigin.position.z - distance;
        transform.position = pos;
    }
}