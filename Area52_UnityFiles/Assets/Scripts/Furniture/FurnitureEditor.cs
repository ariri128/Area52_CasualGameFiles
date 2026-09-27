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

    public void Deselect()
    {
        if (Selected != null) Selected.ClearHighlight();
        Selected = null;
        if (popup != null) popup.Hide();
        CloseOwnedPanel();
    }


    // POPUP BUTTONS

    public void OnMovePressed()
    {
        if (Selected == null) return;
        Debug.Log($"Move pressed on {Selected.Definition.displayName}");
    }

    public void OnRemovePressed()
    {
        if (Selected == null) return;
        Debug.Log($"Remove pressed on {Selected.Definition.displayName}");
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

        FurnitureItem newPiece = Selected.Room.Replace(Selected, definition, out string reason);
        if (newPiece == null)
        {
            Debug.Log(reason);
            return;
        }

        Selected = null; // Puts the old piece in storage
        CloseOwnedPanel();
        Select(newPiece); // Shows the popup on the new piece
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
}