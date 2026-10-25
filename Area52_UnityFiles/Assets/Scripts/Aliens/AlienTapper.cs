using UnityEngine;
using UnityEngine.InputSystem;

/*
 * Tapping an alien in the room you're looking at: it stops and a thought bubble shows its emotion
 * Tapping it again or anywhere else deselects it and it floats again
 * Tapping the bubble opens that alien's file
*/

public class AlienTapper : MonoBehaviour
{
    public static AlienTapper main { get; private set; }

    [SerializeField] private Camera cam;
    [SerializeField] private AlienThoughtBubble bubble;

    [Header("Tapping")]
    [SerializeField] private LayerMask tapLayers = ~0;
    [Tooltip("How far (pixels) a finger can move and still count as a tap.")]
    [SerializeField] private float tapMaxMovement = 15f;
    [Tooltip("How long (seconds) a press can last and still count as a tap.")]
    [SerializeField] private float tapMaxDuration = 0.4f;

    public OwnedAlien Selected { get; private set; }

    private FloatingAlien selectedVisual;
    private bool isPressed;
    private bool pressStartedOnUI;
    private Vector2 pressStartPos;
    private float pressStartTime;

    private void Awake()
    {
        if (main != null && main != this)
        {
            Debug.LogWarning("More than one AlienTapper in the scene. Destroying the extra one.", this);
            Destroy(this);
            return;
        }
        main = this;
        if (cam == null) cam = Camera.main;
    }

    private void Start()
    {
        if (RoomManager.main != null) RoomManager.main.onRoomChanged.AddListener(HandleRoomChanged);
    }

    // SceneFlow switches this off in the breeding room, which also drops the selection
    private void OnDisable()
    {
        Deselect();
        isPressed = false;
    }

    private void OnDestroy()
    {
        if (RoomManager.main != null) RoomManager.main.onRoomChanged.RemoveListener(HandleRoomChanged);
        if (main == this) main = null;
    }

    private void Update()
    {
        HandlePointer();
        KeepSelectionAlive();
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
            pressStartedOnUI = UIPointer.IsOverScreenUI(pos); // The bubble and buttons handle their own taps
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
        Physics.SyncTransforms(); // The aliens move every frame
        Ray ray = cam.ScreenPointToRay(screenPos);

        if (Physics.Raycast(ray, out RaycastHit hit, 200f, tapLayers, QueryTriggerInteraction.Ignore))
        {
            FloatingAlien alien = hit.collider.GetComponentInParent<FloatingAlien>();
            if (alien != null && !alien.Flying && IsInCurrentRoom(alien.Owner))
            {
                if (alien.Owner == Selected) Deselect();
                else Select(alien);
                return;
            }
        }

        Deselect(); // Tapped something else
    }

    private static bool IsInCurrentRoom(OwnedAlien alien)
    {
        return alien != null && !alien.IsAway && RoomManager.main != null
               && alien.roomIndex == RoomManager.main.CurrentRoomIndex;
    }


    // SELECTING

    private void Select(FloatingAlien alien)
    {
        if (selectedVisual != null) selectedVisual.Paused = false;

        Selected = alien.Owner;
        selectedVisual = alien;
        selectedVisual.Paused = true;

        if (bubble != null) bubble.Show(alien.SpriteRenderer, CurrentEmotionSprite());
    }

    public void Deselect()
    {
        if (selectedVisual != null) selectedVisual.Paused = false;
        Selected = null;
        selectedVisual = null;
        if (bubble != null) bubble.Hide();
    }

    // Rooms respawn their aliens when the roster changes - find the new copy and keep it paused
    private void KeepSelectionAlive()
    {
        if (Selected == null) return;

        if (selectedVisual == null)
        {
            FloatingAlien again = AlienRoomDisplay.main != null ? AlienRoomDisplay.main.Find(Selected) : null;
            if (again == null || !IsInCurrentRoom(Selected)) { Deselect(); return; }
            Select(again);
        }

        // The mood can change while it's selected (someone moves in or leaves)
        if (bubble != null) bubble.SetEmotion(CurrentEmotionSprite());
    }

    private Sprite CurrentEmotionSprite()
    {
        if (AlienMoods.main == null || Selected == null) return null;
        return AlienMoods.main.SpriteFor(AlienMoods.main.MoodOf(Selected));
    }

    private void HandleRoomChanged(int roomIndex)
    {
        if (Selected != null && Selected.roomIndex != roomIndex) Deselect();
    }


    // BUBBLE

    public void OnBubblePressed()
    {
        if (Selected == null) return;
        if (AlienFilePopup.main != null) AlienFilePopup.main.Open(Selected);
        else Debug.LogWarning("AlienTapper: no AlienFilePopup in the scene.", this);
    }

    // Sends the selected alien out to look for an artifact
    public void OnExplorePressed()
    {
        if (Selected == null) return;
        if (ExplorationManager.main == null)
        {
            Debug.LogWarning("AlienTapper: no ExplorationManager in the scene.", this);
            return;
        }

        OwnedAlien alien = Selected;
        Deselect(); // Hides the bubble and lets go of the alien before it flies off
        ExplorationManager.main.Send(alien);
    }
}