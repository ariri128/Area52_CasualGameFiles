using UnityEngine;
using System.Collections.Generic;

/*
 * Detects where the room's floor is and which furniture pieces are in the room
 * Is also used to keep pieces inside the walls and to stop a room going from going over its limit of furniture pieces
*/

public class RoomFurnitureDetector : MonoBehaviour
{
    // Lists every room currently spawned - used later to count what the player has placed
    public static readonly List<RoomFurnitureDetector> All = new List<RoomFurnitureDetector>();

    // Empties the list when Play starts
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        All.Clear();
    }

    [Header("Floor (measured from the room's origin: front-center edge of the floor)")]
    [Tooltip("Inside width of the room at the FRONT edge of the floor, wall to wall.")]
    [SerializeField] private float floorWidth = 6.5f;

    [Tooltip("Inside width of the room at the BACK wall. Same as Floor Width for a square room, " +
             "smaller if the side walls angle in toward the back.")]
    [SerializeField] private float backWidth = 6.5f;

    [Tooltip("Inside depth of the room, from the front edge of the floor to the back wall.")]
    [SerializeField] private float floorDepth = 2.6f;

    [Tooltip("Height of the floor's top above the room's origin. Usually 0.")]
    [SerializeField] private float floorHeight = 0f;

    [Tooltip("The room shell mesh (sm_RoomBase_low), used by Measure Floor From Room Base.")]
    [SerializeField] private Renderer measureFrom;

    [Tooltip("Wall thickness taken off when measuring from the room base.")]
    [SerializeField] private float wallThickness = 0.1f;

    [Header("Rules")]
    [Tooltip("Most standing furniture pieces allowed in this room. Rugs and decor don't count.")]
    [SerializeField] private int maxFloorItems = 6;

    [Tooltip("Smallest gap (meters) kept between pieces, and between pieces and the walls, so they never touch.")]
    [SerializeField] private float gap = 0.02f;

    [Tooltip("Print how many pieces this room found when it spawns.")]
    [SerializeField] private bool logSummary = true;

    private readonly List<FurnitureItem> items = new List<FurnitureItem>();

    public IReadOnlyList<FurnitureItem> Items => items;
    public int MaxFloorItems => maxFloorItems;
    public float Gap => gap;
    public float FloorWidth => floorWidth;
    public float BackWidth => backWidth;
    public float FloorDepth => floorDepth;

    // World height of the top of the floor
    public float FloorY => transform.position.y + floorHeight;

    // Counts how many standing pieces are in the room - rugs and decor aren't count
    public int FloorItemCount
    {
        get
        {
            int count = 0;
            foreach (FurnitureItem item in items)
            {
                if (item != null && item.Definition != null && item.Definition.CountsTowardLimit) count++;
            }
            return count;
        }
    }

    public bool IsFull => FloorItemCount >= maxFloorItems;

    // The room lies flat, so only its turn around Y matters
    private Quaternion FlatRotation => Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

    // World position of a point on the floor: x across (0 = middle), z back from the front edge
    public Vector3 FloorPoint(float x, float z)
    {
        return transform.position + FlatRotation * new Vector3(x, floorHeight, z);
    }

    // Half the floor's width at "z" meters back from the front edge
    public float HalfWidthAt(float z)
    {
        float t = floorDepth > 0f ? Mathf.Clamp(z / floorDepth, 0f, 1f) : 0f;
        return Mathf.Lerp(floorWidth, backWidth, t) * 0.5f;
    }


    // SETUP

    private void Awake()
    {
        CollectStartingPieces();
    }

    private void OnEnable()
    {
        if (!All.Contains(this)) All.Add(this);
    }

    private void OnDisable()
    {
        All.Remove(this);
    }

    // Finds every furniture piece placed inside this room prefab
    private void CollectStartingPieces()
    {
        items.Clear();
        foreach (FurnitureItem item in GetComponentsInChildren<FurnitureItem>(true))
        {
            if (item.Definition == null)
            {
                Debug.LogWarning($"{name}: {item.name} has a FurnitureItem but no Definition, so it'll be ignored.", item);
                continue;
            }
            Register(item);
        }

        if (logSummary)
            Debug.Log($"{name}: found {items.Count} furniture pieces ({FloorItemCount}/{maxFloorItems} standing).", this);

        if (FloorItemCount > maxFloorItems)
            Debug.LogWarning($"{name} starts with {FloorItemCount} standing pieces, more than its limit of {maxFloorItems}. " +
                             "It'll work, but the player can't add more until some are removed.", this);
    }


    // ADDING/REMOVING/REPLACING MECHANIC

    private void Register(FurnitureItem item)
    {
        if (!items.Contains(item)) items.Add(item);
        item.Room = this;
    }

    private void Unregister(FurnitureItem item)
    {
        items.Remove(item);
        if (item.Room == this) item.Room = null;
    }

    public FurnitureItem Replace(FurnitureItem old, FurnitureDefinition definition, out string reason)
    {
        reason = null;
        FurnitureInventory inventory = FurnitureInventory.main;

        if (inventory == null) { reason = "There's no FurnitureInventory in the scene."; return null; }
        if (old == null || old.Room != this) { reason = "That piece isn't in this room."; return null; }
        if (definition == null || !definition.CanReplace(old.Definition)) { reason = "That can't go in this spot."; return null; }

        FurnitureItem piece = inventory.Take(definition);
        if (piece == null) { reason = $"No {definition.displayName} available."; return null; }

        // Measures the old piece before anything moves
        FurnitureBox oldBox = old.GetBox();
        GetFlatExtents(oldBox, out Vector3 oldMin, out Vector3 oldMax);

        // Brings the new piece into this room, facing the way its prefab / its last room had it
        piece.transform.SetParent(transform, true);

        FurnitureBox newBox = piece.GetBox();
        GetFlatExtents(newBox, out Vector3 newMin, out Vector3 newMax);

        Vector3 back = FlatRotation * Vector3.forward;
        float oldBackDistance = Vector3.Dot(oldMax, back);
        float newHalfDepth = (Vector3.Dot(newMax, back) - Vector3.Dot(newMin, back)) * 0.5f;

        // Calculates where the new piece's underside should end up
        Vector3 target = oldBox.BottomCenter;
        target += back * ((oldBackDistance - newHalfDepth) - Vector3.Dot(target, back));

        piece.MoveBoxTo(newBox.Moved(target - newBox.BottomCenter));

        // Swaps them over
        Unregister(old);
        inventory.Store(old);
        Register(piece);
        inventory.NotifyChanged();
        return piece;
    }

    private void GetFlatExtents(FurnitureBox box, out Vector3 min, out Vector3 max)
    {
        Vector3[] corners = new Vector3[8];
        box.GetCorners(corners);
        Quaternion toRoom = Quaternion.Inverse(FlatRotation);

        Vector3 localMin = Vector3.one * float.MaxValue;
        Vector3 localMax = Vector3.one * float.MinValue;
        foreach (Vector3 corner in corners)
        {
            Vector3 local = toRoom * corner;
            localMin = Vector3.Min(localMin, local);
            localMax = Vector3.Max(localMax, local);
        }
        min = FlatRotation * localMin;
        max = FlatRotation * localMax;
    }


    // EDITOR

    [ContextMenu("Measure Floor From Room Base")]
    private void MeasureFloorFromRoomBase()
    {
        if (measureFrom == null)
        {
            Debug.LogWarning($"{name}: drag the room shell (sm_RoomBase_low) into Measure From first.", this);
            return;
        }

        // Renderer bounds are in world space, so this works however the model is rotated or scaled
        Bounds b = measureFrom.bounds;
        floorWidth = b.size.x - wallThickness * 2f;
        backWidth = floorWidth;
        floorDepth = (b.max.z - transform.position.z) - wallThickness;
        Debug.Log($"{name}: floor measured at {floorWidth:0.00} wide x {floorDepth:0.00} deep. " +
                  "If the side walls angle in, lower Back Width until the outline's back corners meet them.", this);
    }

    // Draws the floor (bright outline) and the area pieces must stay inside (faint outline)
    private void OnDrawGizmos()
    {
        Matrix4x4 old = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(transform.position, FlatRotation, Vector3.one);

        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.9f);
        DrawFloorOutline(0f);
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.35f);
        DrawFloorOutline(gap);

        Gizmos.matrix = old;
    }

    // Four sides of the floor, pulled in by "inset" from every wall
    private void DrawFloorOutline(float inset)
    {
        float front = inset;
        float back = floorDepth - inset;
        float frontHalf = HalfWidthAt(front) - inset;
        float backHalf = HalfWidthAt(back) - inset;
        float y = floorHeight;

        Vector3 frontLeft = new Vector3(-frontHalf, y, front);
        Vector3 frontRight = new Vector3(frontHalf, y, front);
        Vector3 backLeft = new Vector3(-backHalf, y, back);
        Vector3 backRight = new Vector3(backHalf, y, back);

        Gizmos.DrawLine(frontLeft, frontRight);
        Gizmos.DrawLine(frontRight, backRight);
        Gizmos.DrawLine(backRight, backLeft);
        Gizmos.DrawLine(backLeft, frontLeft);
    }
}