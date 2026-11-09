using UnityEngine;
using UnityEngine.InputSystem;

/*
 * Runs the furniture editing of selecting it and moving, changing, or removing it
 * Outlines the furniture when selected
*/

public class FurnitureEditor : MonoBehaviour
{
    public static FurnitureEditor main { get; private set; }

    [SerializeField] private Camera cam;

    [Tooltip("The Replace / Move / Remove popup.")]
    [SerializeField] private FurnitureActionPopup popup;

    [Tooltip("The Furniture Owned panel.")]
    [SerializeField] private FurnitureOwnedPanel ownedPanel;

    [Header("Tapping")]
    [Tooltip("Layers that can be tapped. Leave on Everything unless something else gets in the way.")]
    [SerializeField] private LayerMask tapLayers = ~0;

    [Tooltip("How far (pixels) a finger can move and still count as a tap.")]
    [SerializeField] private float tapMaxMovement = 15f;

    [Tooltip("How long (seconds) a press can last and still count as a tap.")]
    [SerializeField] private float tapMaxDuration = 0.4f;

    [Header("Highlight")]
    [Tooltip("Material using the Alien Apartments/Outline shader. Draws the outline around the selected piece.")]
    [SerializeField] private Material outlineMaterial;

    [Tooltip("Outline material with Flat ticked. Used for rugs and other flat pieces.")]
    [SerializeField] private Material flatOutlineMaterial;

    [Tooltip("Outline color of the selected piece.")]
    [SerializeField] private Color selectedColor = new Color(1f, 0.85f, 0.25f);

    public FurnitureItem Selected { get; private set; }

    private bool isPressed;
    private bool pressStartedOnUI;
    private Vector2 pressStartPos;
    private float pressStartTime;

    private void Awake()
    {
        if (main != null && main != this)
        {
            Debug.LogWarning("More than one FurnitureEditor in the scene. Destroying the extra one.", this);
            Destroy(this);
            return;
        }
        main = this;
        if (cam == null) cam = Camera.main;
        if (outlineMaterial == null)
            Debug.LogWarning("FurnitureEditor: no Outline Material set, so selected pieces won't show an outline.", this);
    }

    private void Start()
    {
        if (RoomManager.main != null) RoomManager.main.onRoomChanged.AddListener(HandleRoomChanged);
    }

    private void OnDestroy()
    {
        if (RoomManager.main != null) RoomManager.main.onRoomChanged.RemoveListener(HandleRoomChanged);
        RoomCameraController.SetSwipeBlocked(this, false);
        if (main == this) main = null;
    }

    private void Update()
    {
        HandlePointer();
    }

    // The RoomFurnitureDetector of the room the camera is on (null for rooms without furniture)
    public RoomFurnitureDetector CurrentRoom
    {
        get
        {
            if (RoomManager.main == null) return null;
            GameObject roomObject = RoomManager.main.GetRoomObject(RoomManager.main.CurrentRoomIndex);
            return roomObject != null ? roomObject.GetComponent<RoomFurnitureDetector>() : null;
        }
    }


    // INPUT

    private void HandlePointer()
    {
        Pointer pointer = Pointer.current;
        if (pointer == null) return;

        bool pressedNow = pointer.press.isPressed;
        Vector2 pos = pointer.position.ReadValue();

        if (pressedNow && !isPressed)
        {
            isPressed = true;
            pressStartPos = pos;
            pressStartTime = Time.unscaledTime;
            pressStartedOnUI = UIPointer.IsOverScreenUI(pos);
        }
        else if (!pressedNow && isPressed)
        {
            isPressed = false;
            if (pressStartedOnUI) return;

            bool barelyMoved = (pos - pressStartPos).magnitude <= tapMaxMovement;
            bool quick = Time.unscaledTime - pressStartTime <= tapMaxDuration;
            if (barelyMoved && quick) HandleTap(pos);
        }
    }

    private void HandleTap(Vector2 screenPos)
    {
        // While moving, any tap away from the gizmo means "I'm done"
        if (FurnitureMover.main != null && FurnitureMover.main.IsMoving)
        {
            FurnitureMover.main.TryFinish();
            return;
        }

        // Makes sure the colliders are where their pieces are right now
        Physics.SyncTransforms();

        Ray ray = cam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 200f, tapLayers, QueryTriggerInteraction.Ignore))
        {
            FurnitureItem item = hit.collider.GetComponentInParent<FurnitureItem>();

            // Selects only the pieces in the room we're looking at, and only ones set up with a data file
            if (item != null && item.Definition != null && item.Room != null && item.Room == CurrentRoom)
            {
                if (item == Selected) Deselect();
                else Select(item);
                return;
            }
        }

        Deselect(); // If empty space is tapped
    }


    // SELECTING

    public void Select(FurnitureItem item)
    {
        if (Selected != null && Selected != item) Selected.ClearHighlight();
        Selected = item;
        item.ShowHighlight(OutlineMaterialFor(item), selectedColor);
        if (popup != null) popup.Show(item);
    }
    
    // Rugs are too flat for the regular outline, so they get the flat one
    private Material OutlineMaterialFor(FurnitureItem item)
    {
        bool isRug = item.Definition != null && item.Definition.placement == FurniturePlacement.Rug;
        if (isRug && flatOutlineMaterial != null) return flatOutlineMaterial;
        return outlineMaterial;
    }

    // Outlines a piece in any color, with the right outline for its type (used by the mover's red pulse)
    public void ShowOutline(FurnitureItem item, Color color)
    {
        if (item != null) item.ShowHighlight(OutlineMaterialFor(item), color);
    }

    public void Deselect()
    {
        if (FurnitureMover.main != null && FurnitureMover.main.IsMoving) FurnitureMover.main.ForceStop(); // E.g. leaving for the breeding room
        if (Selected != null) Selected.ClearHighlight();
        Selected = null;
        if (popup != null) popup.Hide();
        CloseOwnedPanel();
    }


    // POPUP BUTTONS

    public void OnMovePressed()
    {
        if (Selected == null || Selected.Definition == null || Selected.Definition.IsDecor) return;
        if (FurnitureMover.main == null) { Debug.LogWarning("FurnitureEditor: no FurnitureMover in the scene.", this); return; }

        popup.Hide(); // The gizmo takes over
        FurnitureMover.main.Begin(Selected, false, placed => Deselect()); // Either way, it's done once it's let go
    }

    public void OnRemovePressed()
    {
        if (Selected == null || Selected.Room == null) return;

        FurnitureItem removing = Selected;
        Deselect(); // Hides the popup and outline first
        int count = removing.Room.Remove(removing);

        if (count > 1) Debug.Log($"Removed {removing.Definition.displayName} and the {count - 1} decor piece(s) on it.", this);
    }

    public void OnReplacePressed()
    {
        if (Selected == null || ownedPanel == null) return;

        popup.Hide();
        ownedPanel.OpenForReplace(Selected.Definition);
        RoomCameraController.SetSwipeBlocked(this, true); // No sliding between rooms when the panel is open
    }


    // FURNITURE OWNED PANEL

    // The panel's Replace button
    public void OnPanelConfirmed(FurnitureDefinition definition)
    {
        if (Selected == null || Selected.Room == null)
        {
            CloseOwnedPanel();
            return;
        }

        RoomFurnitureDetector room = Selected.Room;
        RoomFurnitureDetector.ReplaceResult result = room.Replace(Selected, definition, out string reason);
        if (result == null)
        {
            Debug.Log(reason);
            return;
        }

        Selected = null; // The old piece is in storage now
        CloseOwnedPanel();

        if (!result.needsMoving || FurnitureMover.main == null)
        {
            Select(result.newPiece); // Fits - shows the popup on the new piece
            return;
        }

        // Doesn't fit even nudged: the player has to move it somewhere valid. Cancel undoes the whole replace
        FurnitureMover.main.Begin(result.newPiece, true, placed =>
        {
            if (placed)
            {
                Select(result.newPiece);
            }
            else
            {
                room.UndoReplace(result);
                Select(result.oldPiece); // Back to how it was, with the old piece selected
            }
        });
    }

    // The panel's X button: back to the popup on the same piece
    public void OnPanelClosed()
    {
        RoomCameraController.SetSwipeBlocked(this, false);
        if (Selected != null) Select(Selected);
    }

    private void CloseOwnedPanel()
    {
        if (ownedPanel != null && ownedPanel.IsOpen) ownedPanel.Hide();
        RoomCameraController.SetSwipeBlocked(this, false);
    }

    // Swiping to another room drops the selection
    private void HandleRoomChanged(int roomIndex)
    {
        if (Selected != null && Selected.Room != CurrentRoom) Deselect();
    }


    // TESTING (select a piece during Play first)

    [ContextMenu("Testing: Check Selected Spot")]
    private void CheckSelectedForTesting()
    {
        if (Selected == null || Selected.Room == null) { Debug.LogWarning("Tap a piece of furniture during Play first.", this); return; }

        FurnitureBox box = Selected.GetBox();
        RoomFurnitureDetector.SpotProblem problem = Selected.Room.CheckSpot(Selected, box, out FurnitureItem blocker);

        string detail = blocker != null ? $" (touching {blocker.Definition.displayName})" : "";
        Debug.Log($"{Selected.Definition.displayName}: {problem}{detail}", Selected);
        box.DebugDraw(problem == RoomFurnitureDetector.SpotProblem.None ? Color.green : Color.red, 5f);
    }

    [ContextMenu("Testing: Find Free Spot Near Room Middle")]
    private void FindSpotForTesting()
    {
        if (Selected == null || Selected.Room == null) { Debug.LogWarning("Tap a piece of furniture during Play first.", this); return; }

        RoomFurnitureDetector room = Selected.Room;
        Vector3 middle = room.FloorPoint(0f, room.FloorDepth * 0.5f);

        if (room.FindFreeSpot(Selected, Selected.GetBox(), middle, 0f, out FurnitureBox spot))
        {
            Debug.Log($"{Selected.Definition.displayName} would fit {Vector3.Distance(spot.BottomCenter, middle):0.00} m from the middle (cyan box in the Scene view).", Selected);
            spot.DebugDraw(Color.cyan, 5f);
        }
        else
        {
            Debug.Log($"There's nowhere in this room {Selected.Definition.displayName} fits.", Selected);
        }
    }
}