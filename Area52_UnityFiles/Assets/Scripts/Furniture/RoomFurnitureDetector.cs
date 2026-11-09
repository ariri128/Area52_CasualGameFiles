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

    [Tooltip("How far (meters) a decor piece's underside can be from a surface and still count as sitting on it.")]
    [SerializeField] private float sittingTolerance = 0.05f;

    [Tooltip("How far apart (meters) the spots tried by the free-spot search are. Smaller = finds tighter gaps, but slower.")]
    [SerializeField] private float searchStep = 0.1f;

    [Tooltip("When a replacement doesn't fit where the old piece was, how far (meters) it can be nudged to fit " +
             "before the player has to move it themselves.")]
    [SerializeField] private float replaceNudgeDistance = 0.4f;

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

    // The room's directions along the floor: across (left to right) and back (away from the front edge)
    public Vector3 AcrossDirection => FlatRotation * Vector3.right;
    public Vector3 BackDirection => FlatRotation * Vector3.forward;

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

    // What a replace did, so it can be undone if the new piece can't be placed
    public class ReplaceResult
    {
        public FurnitureItem newPiece;
        public FurnitureItem oldPiece;
        public readonly List<FurnitureItem> storedDecor = new List<FurnitureItem>(); // Decor that was on the old piece
        public bool needsMoving; // True = it doesn't fit, even nudged - the player has to move it
    }

    // Swaps "old" for a piece of "definition": same spot, facing the same way
    // Returns null (and changes nothing) if it can't be done - reason says why
    public ReplaceResult Replace(FurnitureItem old, FurnitureDefinition definition, out string reason)
    {
        reason = null;
        FurnitureInventory inventory = FurnitureInventory.main;

        if (inventory == null) { reason = "There's no FurnitureInventory in the scene."; return null; }
        if (old == null || old.Room != this) { reason = "That piece isn't in this room."; return null; }
        if (definition == null || !definition.CanReplace(old.Definition)) { reason = "That can't go in this spot."; return null; }

        FurnitureItem piece = inventory.Take(definition);
        if (piece == null) { reason = $"No {definition.displayName} available."; return null; }

        ReplaceResult result = new ReplaceResult { newPiece = piece, oldPiece = old };

        // Measured before anything moves (this is wherever the old piece is NOW - including if the player moved / turned it)
        FurnitureBox oldBox = old.GetBox();
        List<FurnitureItem> decorOnOld = ItemsSittingOn(old);

        // Into this room, then turned (around straight up only) to face the same way as the old piece, plus any Facing Offset
        // Its own tilt (e.g. -90 on X from the model import) is never touched
        piece.transform.SetParent(transform, true);
        float turnBy = Mathf.DeltaAngle(piece.FacingYaw, old.FacingYaw) + definition.facingOffset - old.Definition.facingOffset;
        piece.transform.rotation = Quaternion.AngleAxis(turnBy, Vector3.up) * piece.transform.rotation;

        // Same spot: its underside centered where the old one's was
        FurnitureBox newBox = piece.GetBox();
        piece.MoveBoxTo(newBox.Moved(oldBox.BottomCenter - newBox.BottomCenter));

        // The swap: the old piece (and decor that was on it) go into storage
        foreach (FurnitureItem decor in decorOnOld)
        {
            Unregister(decor);
            inventory.Store(decor);
            result.storedDecor.Add(decor);
        }
        Unregister(old);
        inventory.Store(old);
        Register(piece);

        // Doesn't fit? Try a small nudge first (e.g. a deeper couch away from the wall)
        if (CheckSpot(piece) != SpotProblem.None)
        {
            if (FindFreeSpot(piece, piece.GetBox(), oldBox.BottomCenter, replaceNudgeDistance, out FurnitureBox spot))
                piece.MoveBoxTo(spot);
            else
                result.needsMoving = true; // The player will have to move it
        }

        inventory.NotifyChanged();
        return result;
    }

    // Puts everything back the way it was before the replace - the new piece goes back into storage
    public void UndoReplace(ReplaceResult result)
    {
        FurnitureInventory inventory = FurnitureInventory.main;
        if (result == null || inventory == null) return;

        if (result.newPiece != null)
        {
            Unregister(result.newPiece);
            inventory.Store(result.newPiece);
        }

        BringBack(result.oldPiece);
        foreach (FurnitureItem decor in result.storedDecor) BringBack(decor);

        inventory.NotifyChanged();
    }

    // Storage kept the piece exactly where it was, so putting it back in the room puts it back in its old spot
    private void BringBack(FurnitureItem item)
    {
        if (item == null || FurnitureInventory.main == null || !FurnitureInventory.main.TakeBack(item)) return;
        item.transform.SetParent(transform, true);
        Register(item);
    }

    // Takes a piece out of the room and into storage - it shows as available in Furniture Owned again
    // Decor sitting on it goes too, so nothing is left floating. Returns how many pieces were put away in total
    public int Remove(FurnitureItem item)
    {
        FurnitureInventory inventory = FurnitureInventory.main;
        if (inventory == null || item == null || item.Room != this) return 0;

        List<FurnitureItem> goingAway = ItemsSittingOn(item); // Found BEFORE anything moves
        goingAway.Add(item);

        foreach (FurnitureItem piece in goingAway)
        {
            Unregister(piece);
            inventory.Store(piece);
        }

        inventory.NotifyChanged(); // Furniture Owned updates its counts
        return goingAway.Count;
    }

    // Decor resting on this piece: on its top, or on one of its shelves
    // (the middle of the decor's underside is inside the piece's box)
    public List<FurnitureItem> ItemsSittingOn(FurnitureItem host)
    {
        List<FurnitureItem> result = new List<FurnitureItem>();
        if (host == null || host.Definition == null || host.Definition.IsDecor) return result; // Nothing sits on decor

        FurnitureBox hostBox = host.GetBox().Inflated(sittingTolerance); // A little bigger, so things resting right on top count

        foreach (FurnitureItem other in items)
        {
            if (other == null || other == host || other.Definition == null || !other.Definition.IsDecor) continue;
            if (hostBox.Contains(other.GetBox().BottomCenter)) result.Add(other);
        }
        return result;
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


    // PLACEMENT RULES

    public enum SpotProblem { None, OutsideRoom, Overlapping }

    // Would "piece" fit if its box were at "box"? The piece itself never gets in its own way
    //   Floor pieces - inside the walls, not touching other floor pieces (rugs are fine to stand on)
    //   Rugs         - inside the walls, not touching other rugs (floor pieces can stand on them)
    //   Decor        - always fine here (it sits on other furniture)
    // blockedBy = the piece it's touching (null if it isn't touching anything)
    public SpotProblem CheckSpot(FurnitureItem piece, FurnitureBox box, out FurnitureItem blockedBy)
    {
        blockedBy = null;
        if (piece == null || piece.Definition == null || piece.Definition.IsDecor) return SpotProblem.None;

        if (!IsInsideWalls(box)) return SpotProblem.OutsideRoom;

        FurnitureBox padded = box.Inflated(gap); // Closer than the gap counts as touching
        foreach (FurnitureItem other in items)
        {
            if (other == null || other == piece || other.Definition == null) continue;
            if (!GetsInTheWay(piece.Definition, other.Definition)) continue;

            if (FurnitureBox.Intersects(padded, other.GetBox()))
            {
                blockedBy = other;
                return SpotProblem.Overlapping;
            }
        }
        return SpotProblem.None;
    }

    // Is the piece fine where it is right now?
    public SpotProblem CheckSpot(FurnitureItem piece) => CheckSpot(piece, piece.GetBox(), out _);

    // Floor pieces only bump into floor pieces, rugs only into rugs, and decor never gets in the way
    private static bool GetsInTheWay(FurnitureDefinition moving, FurnitureDefinition other)
    {
        if (other.IsDecor) return false;
        bool movingIsRug = moving.placement == FurniturePlacement.Rug;
        bool otherIsRug = other.placement == FurniturePlacement.Rug;
        return movingIsRug == otherIsRug;
    }

    // Every corner of the box is on the floor, at least Gap away from every wall
    public bool IsInsideWalls(FurnitureBox box)
    {
        Vector3[] corners = new Vector3[8];
        box.GetCorners(corners);
        Quaternion toRoom = Quaternion.Inverse(FlatRotation);

        foreach (Vector3 corner in corners)
        {
            // x = across the room (0 = middle), z = back from the front edge
            Vector3 local = toRoom * (corner - transform.position);
            if (local.z < gap || local.z > floorDepth - gap) return false;
            if (Mathf.Abs(local.x) > HalfWidthAt(local.z) - gap) return false; // Follows angled side walls too
        }
        return true;
    }

    // The closest point to "world" that's on this room's floor (its height is left as it is)
    // Used while dragging, so the middle of a piece can't be dragged out of the room
    public Vector3 ClampToFloor(Vector3 world)
    {
        Quaternion toRoom = Quaternion.Inverse(FlatRotation);
        Vector3 local = toRoom * (world - transform.position);

        local.z = Mathf.Clamp(local.z, 0f, floorDepth);
        float halfWidth = HalfWidthAt(local.z);
        local.x = Mathf.Clamp(local.x, -halfWidth, halfWidth);

        Vector3 result = transform.position + FlatRotation * local;
        result.y = world.y;
        return result;
    }

    // The closest spot to "near" where the piece fits, keeping the turn of "start" and standing on the floor
    // maxDistance: how far from "near" to look (0 or less = anywhere in the room). False if nowhere fits
    public bool FindFreeSpot(FurnitureItem piece, FurnitureBox start, Vector3 near, float maxDistance, out FurnitureBox spot)
    {
        spot = start;

        // Every point on a grid across the floor, plus "near" itself
        List<Vector3> points = new List<Vector3> { new Vector3(near.x, FloorY, near.z) };
        float step = Mathf.Max(0.02f, searchStep);
        for (float z = 0f; z <= floorDepth; z += step)
        {
            float halfWidth = HalfWidthAt(z);
            for (float x = -halfWidth; x <= halfWidth; x += step)
                points.Add(FloorPoint(x, z));
        }

        // Closest first (measured flat along the floor)
        points.Sort((a, b) => FlatDistance(a, near).CompareTo(FlatDistance(b, near)));

        foreach (Vector3 point in points)
        {
            if (maxDistance > 0f && FlatDistance(point, near) > maxDistance) break; // The rest are even further

            FurnitureBox candidate = start.Moved(point - start.BottomCenter); // Its underside on that point
            if (CheckSpot(piece, candidate, out _) == SpotProblem.None)
            {
                spot = candidate;
                return true;
            }
        }
        return false;
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x, dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
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