using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

/*
 * The "which room?" popup, used three ways:
 *   Open()                     - where to put the egg (breeding room)
 *   OpenForMove(alien)         - where to move an alien to (alien file)
 *   OpenForArtifact(artifact)  - where to place an artifact (artifacts page)
 * Full rooms are greyed out; when moving, the alien's / artifact's own room shows "Here" and can't be picked
 * Aliens and eggs count against the room's alien limit; artifacts have their own limit
*/

public class RoomPickerPopup : MonoBehaviour
{
    [Header("Parts")]
    [Tooltip("The child that's shown and hidden (dark background + window).")]
    [SerializeField] private GameObject root;
    [SerializeField] private Button closeButton;
    [Tooltip("Where the room boxes are lined up (the grid's Content).")]
    [SerializeField] private Transform slotContainer;
    [Tooltip("One room box. Copied once per room.")]
    [SerializeField] private Button slotTemplate;
    [SerializeField] private Button confirmButton;
    [Tooltip("Optional. Only shown when placing an artifact, and only clickable when it's already in a room.")]
    [SerializeField] private Button removeButton;
    [Tooltip("Optional. The popup's title text.")]
    [SerializeField] private TMP_Text titleText;
    [Tooltip("Optional. The text on the confirm button.")]
    [SerializeField] private TMP_Text confirmLabel;

    [Header("Text")]
    [Tooltip("{0} is the room number.")]
    [SerializeField] private string roomNameFormat = "Room {0}";
    [Tooltip("{0} is how many are in the room, {1} is the most it holds.")]
    [SerializeField] private string countFormat = "{0}/{1}";
    [SerializeField] private string fullText = "Full";
    [SerializeField] private string eggTitle = "Put egg in which room?";
    [SerializeField] private string eggConfirm = "Put egg here";
    [Tooltip("{0} is the alien's name.")]
    [SerializeField] private string moveTitle = "Move {0} to which room?";
    [SerializeField] private string moveConfirm = "Move Here";
    [SerializeField] private string currentRoomText = "Here";

    [Header("Text - artifacts")]
    [Tooltip("{0} is the artifact's name. Used when it's Unplaced.")]
    [SerializeField] private string artifactPlaceTitle = "Place {0} in which room?";
    [SerializeField] private string artifactPlaceConfirm = "Place Here";
    [Tooltip("{0} is the artifact's name. Used when it's already in a room.")]
    [SerializeField] private string artifactMoveTitle = "Move {0} to which room?";
    [SerializeField] private string artifactMoveConfirm = "Move Here";

    private readonly List<GameObject> slots = new List<GameObject>();
    private int selectedRoom = -1;
    private OwnedAlien moving; // null = placing an egg (or an artifact)
    private OwnedArtifact placingArtifact; // Null unless placing an artifact
    private bool listening;

    public bool IsOpen => root != null && root.activeSelf;

    private void Awake()
    {
        closeButton.onClick.AddListener(Close);
        confirmButton.onClick.AddListener(Confirm);
        if (removeButton != null) removeButton.onClick.AddListener(RemoveArtifact);
        slotTemplate.gameObject.SetActive(false);
        root.SetActive(false);
    }

    // Tries in both OnEnable and Start: the managers may not be set up yet the first time this runs
    private void OnEnable() => StartListening();
    private void Start() => StartListening();

    private void StartListening()
    {
        if (listening || BreedingManager.main == null || AlienRoster.main == null) return;
        BreedingManager.main.onChanged.AddListener(RefreshIfOpen);
        AlienRoster.main.onChanged.AddListener(RefreshIfOpen); // Moves and hatches change the counts
        if (ArtifactInventory.main != null) ArtifactInventory.main.onChanged.AddListener(RefreshIfOpen);
        listening = true;
    }

    private void OnDisable()
    {
        if (!listening) return;
        if (BreedingManager.main != null) BreedingManager.main.onChanged.RemoveListener(RefreshIfOpen);
        if (AlienRoster.main != null) AlienRoster.main.onChanged.RemoveListener(RefreshIfOpen);
        if (ArtifactInventory.main != null) ArtifactInventory.main.onChanged.RemoveListener(RefreshIfOpen);
        listening = false;
    }

    // Egg mode
    public void Open()
    {
        moving = null;
        placingArtifact = null;
        OpenPopup();
    }

    // Move mode
    public void OpenForMove(OwnedAlien alien)
    {
        if (alien == null) return;
        moving = alien;
        placingArtifact = null;
        OpenPopup();
    }

    // Artifact mode
    public void OpenForArtifact(OwnedArtifact artifact)
    {
        if (artifact == null) return;
        moving = null;
        placingArtifact = artifact;
        OpenPopup();
    }

    private void OpenPopup()
    {
        selectedRoom = -1;
        root.SetActive(true);
        Rebuild();
    }

    public void Close()
    {
        root.SetActive(false);
        moving = null;
        placingArtifact = null;
    }

    private void RefreshIfOpen()
    {
        if (IsOpen) Rebuild();
    }

    // Can this room be picked right now?
    private bool CanPick(int roomIndex)
    {
        if (placingArtifact != null) return ArtifactInventory.main != null && ArtifactInventory.main.CanPlace(placingArtifact, roomIndex);
        if (moving != null) return AlienRoster.main != null && AlienRoster.main.CanMoveTo(moving, roomIndex);
        return BreedingManager.main != null && !BreedingManager.main.IsRoomFull(roomIndex);
    }

    // The popup's title and button text for the current mode
    private void RefreshTexts()
    {
        string title, confirm;
        if (placingArtifact != null)
        {
            title = string.Format(placingArtifact.IsPlaced ? artifactMoveTitle : artifactPlaceTitle, placingArtifact.DisplayName);
            confirm = placingArtifact.IsPlaced ? artifactMoveConfirm : artifactPlaceConfirm;
        }
        else if (moving != null)
        {
            title = string.Format(moveTitle, moving.DisplayName);
            confirm = moveConfirm;
        }
        else
        {
            title = eggTitle;
            confirm = eggConfirm;
        }

        if (titleText != null) titleText.text = title;
        if (confirmLabel != null) confirmLabel.text = confirm;
    }

    private void Rebuild()
    {
        foreach (GameObject slot in slots) Destroy(slot);
        slots.Clear();

        BreedingManager breeding = BreedingManager.main;
        if (breeding == null) return;
        ArtifactInventory artifacts = ArtifactInventory.main;

        RefreshTexts();

        // A selected room that can't be picked anymore is unselected
        if (selectedRoom >= 0 && !CanPick(selectedRoom)) selectedRoom = -1;

        for (int i = 0; i < breeding.AlienRooms.Count; i++)
        {
            int roomIndex = breeding.AlienRooms[i];

            // Artifacts have their own count and limit
            bool forArtifact = placingArtifact != null && artifacts != null;
            bool isCurrent = forArtifact ? placingArtifact.roomIndex == roomIndex
                                         : moving != null && moving.roomIndex == roomIndex;
            bool full = forArtifact ? artifacts.IsRoomFull(roomIndex) : breeding.IsRoomFull(roomIndex);
            int countHere = forArtifact ? artifacts.CountInRoom(roomIndex) : breeding.Occupancy(roomIndex);
            int maxHere = forArtifact ? artifacts.MaxPerRoom : breeding.MaxPerRoom;

            Button slot = Instantiate(slotTemplate, slotContainer);
            slot.gameObject.SetActive(true);
            slot.name = $"Room_{i + 1}";
            slot.interactable = CanPick(roomIndex);
            slot.onClick.AddListener(() => Select(roomIndex));

            SetText(slot.transform, "Name", string.Format(roomNameFormat, i + 1));

            string count;
            if (isCurrent) count = currentRoomText;
            else if (full) count = fullText;
            else count = string.Format(countFormat, countHere, maxHere);
            SetText(slot.transform, "Count", count);

            Transform border = slot.transform.Find("SelectedBorder");
            if (border != null) border.gameObject.SetActive(roomIndex == selectedRoom);

            slots.Add(slot.gameObject);
        }

        confirmButton.interactable = selectedRoom >= 0;

        // Remove only exists for artifacts, and only works if it's in a room
        if (removeButton != null)
        {
            removeButton.gameObject.SetActive(placingArtifact != null);
            removeButton.interactable = placingArtifact != null && placingArtifact.IsPlaced;
        }
    }

    private void Select(int roomIndex)
    {
        selectedRoom = roomIndex;
        Rebuild();
    }

    private void Confirm()
    {
        if (selectedRoom < 0) return;

        if (placingArtifact != null)
        {
            if (ArtifactInventory.main != null && ArtifactInventory.main.PlaceInRoom(placingArtifact, selectedRoom)) Close();
        }
        else if (moving != null)
        {
            if (AlienRoster.main != null && AlienRoster.main.MoveTo(moving, selectedRoom)) Close();
        }
        else if (BreedingManager.main != null && BreedingManager.main.PlaceEgg(selectedRoom))
        {
            Close();
        }
    }

    // Takes the artifact out of its room (back to Unplaced)
    private void RemoveArtifact()
    {
        if (placingArtifact == null || ArtifactInventory.main == null) return;
        if (ArtifactInventory.main.Unplace(placingArtifact)) Close();
    }

    private static void SetText(Transform parent, string childName, string text)
    {
        Transform child = parent.Find(childName);
        TMP_Text label = child != null ? child.GetComponent<TMP_Text>() : null;
        if (label != null) label.text = text;
    }
}

/* ver.1
public class RoomPickerPopup : MonoBehaviour
{
    [Header("Parts")]
    [Tooltip("The child that's shown and hidden (dark background + window).")]
    [SerializeField] private GameObject root;
    [SerializeField] private Button closeButton;
    [Tooltip("Where the room boxes are lined up (the grid's Content).")]
    [SerializeField] private Transform slotContainer;
    [Tooltip("One room box. Copied once per room.")]
    [SerializeField] private Button slotTemplate;
    [SerializeField] private Button confirmButton;
    [Tooltip("Optional. The popup's title text.")]
    [SerializeField] private TMP_Text titleText;
    [Tooltip("Optional. The text on the confirm button.")]
    [SerializeField] private TMP_Text confirmLabel;

    [Header("Text")]
    [Tooltip("{0} is the room number.")]
    [SerializeField] private string roomNameFormat = "Room {0}";
    [Tooltip("{0} is how many are in the room, {1} is the most it holds.")]
    [SerializeField] private string countFormat = "{0}/{1}";
    [SerializeField] private string fullText = "Full";
    [SerializeField] private string eggTitle = "Put egg in which room?";
    [SerializeField] private string eggConfirm = "Put egg here";
    [Tooltip("{0} is the alien's name.")]
    [SerializeField] private string moveTitle = "Move {0} to which room?";
    [SerializeField] private string moveConfirm = "Move Here";
    [SerializeField] private string currentRoomText = "Here";

    [Header("Text - artifacts")]
    [Tooltip("{0} is the artifact's name. Used when it's Unplaced.")]
    [SerializeField] private string artifactPlaceTitle = "Place {0} in which room?";
    [SerializeField] private string artifactPlaceConfirm = "Place Here";
    [Tooltip("{0} is the artifact's name. Used when it's already in a room.")]
    [SerializeField] private string artifactMoveTitle = "Move {0} to which room?";
    [SerializeField] private string artifactMoveConfirm = "Move Here";

    private readonly List<GameObject> slots = new List<GameObject>();
    private int selectedRoom = -1;
    private OwnedAlien moving; // null = placing an egg (or an artifact)
    private OwnedArtifact placingArtifact; // Null unless placing an artifact
    private bool listening;

    public bool IsOpen => root != null && root.activeSelf;

    private void Awake()
    {
        closeButton.onClick.AddListener(Close);
        confirmButton.onClick.AddListener(Confirm);
        slotTemplate.gameObject.SetActive(false);
        root.SetActive(false);
    }

    // Tries in both OnEnable and Start: the managers may not be set up yet the first time this runs
    private void OnEnable() => StartListening();
    private void Start() => StartListening();

    private void StartListening()
    {
        if (listening || BreedingManager.main == null || AlienRoster.main == null) return;
        BreedingManager.main.onChanged.AddListener(RefreshIfOpen);
        AlienRoster.main.onChanged.AddListener(RefreshIfOpen); // Moves and hatches change the counts
        if (ArtifactInventory.main != null) ArtifactInventory.main.onChanged.AddListener(RefreshIfOpen);
        listening = true;
    }

    private void OnDisable()
    {
        if (!listening) return;
        if (BreedingManager.main != null) BreedingManager.main.onChanged.RemoveListener(RefreshIfOpen);
        if (AlienRoster.main != null) AlienRoster.main.onChanged.RemoveListener(RefreshIfOpen);
        if (ArtifactInventory.main != null) ArtifactInventory.main.onChanged.RemoveListener(RefreshIfOpen);
        listening = false;
    }

    // Egg mode
    public void Open()
    {
        moving = null;
        placingArtifact = null;
        OpenPopup();
    }

    // Move mode
    public void OpenForMove(OwnedAlien alien)
    {
        if (alien == null) return;
        moving = alien;
        placingArtifact = null;
        OpenPopup();
    }

    // Artifact mode
    public void OpenForArtifact(OwnedArtifact artifact)
    {
        if (artifact == null) return;
        moving = null;
        placingArtifact = artifact;
        OpenPopup();
    }

    private void OpenPopup()
    {
        selectedRoom = -1;
        root.SetActive(true);
        Rebuild();
    }

    public void Close()
    {
        root.SetActive(false);
        moving = null;
        placingArtifact = null;
    }

    private void RefreshIfOpen()
    {
        if (IsOpen) Rebuild();
    }

    // Can this room be picked right now?
    private bool CanPick(int roomIndex)
    {
        if (placingArtifact != null) return ArtifactInventory.main != null && ArtifactInventory.main.CanPlace(placingArtifact, roomIndex);
        if (moving != null) return AlienRoster.main != null && AlienRoster.main.CanMoveTo(moving, roomIndex);
        return BreedingManager.main != null && !BreedingManager.main.IsRoomFull(roomIndex);
    }

    // The popup's title and button text for the current mode
    private void RefreshTexts()
    {
        string title, confirm;
        if (placingArtifact != null)
        {
            title = string.Format(placingArtifact.IsPlaced ? artifactMoveTitle : artifactPlaceTitle, placingArtifact.DisplayName);
            confirm = placingArtifact.IsPlaced ? artifactMoveConfirm : artifactPlaceConfirm;
        }
        else if (moving != null)
        {
            title = string.Format(moveTitle, moving.DisplayName);
            confirm = moveConfirm;
        }
        else
        {
            title = eggTitle;
            confirm = eggConfirm;
        }

        if (titleText != null) titleText.text = title;
        if (confirmLabel != null) confirmLabel.text = confirm;
    }

    private void Rebuild()
    {
        foreach (GameObject slot in slots) Destroy(slot);
        slots.Clear();

        BreedingManager breeding = BreedingManager.main;
        if (breeding == null) return;
        ArtifactInventory artifacts = ArtifactInventory.main;

        RefreshTexts();

        // A selected room that can't be picked anymore is unselected
        if (selectedRoom >= 0 && !CanPick(selectedRoom)) selectedRoom = -1;

        for (int i = 0; i < breeding.AlienRooms.Count; i++)
        {
            int roomIndex = breeding.AlienRooms[i];

            // Artifacts have their own count and limit
            bool forArtifact = placingArtifact != null && artifacts != null;
            bool isCurrent = forArtifact ? placingArtifact.roomIndex == roomIndex
                                         : moving != null && moving.roomIndex == roomIndex;
            bool full = forArtifact ? artifacts.IsRoomFull(roomIndex) : breeding.IsRoomFull(roomIndex);
            int countHere = forArtifact ? artifacts.CountInRoom(roomIndex) : breeding.Occupancy(roomIndex);
            int maxHere = forArtifact ? artifacts.MaxPerRoom : breeding.MaxPerRoom;

            Button slot = Instantiate(slotTemplate, slotContainer);
            slot.gameObject.SetActive(true);
            slot.name = $"Room_{i + 1}";
            slot.interactable = CanPick(roomIndex);
            slot.onClick.AddListener(() => Select(roomIndex));

            SetText(slot.transform, "Name", string.Format(roomNameFormat, i + 1));

            string count;
            if (isCurrent) count = currentRoomText;
            else if (full) count = fullText;
            else count = string.Format(countFormat, countHere, maxHere);
            SetText(slot.transform, "Count", count);

            Transform border = slot.transform.Find("SelectedBorder");
            if (border != null) border.gameObject.SetActive(roomIndex == selectedRoom);

            slots.Add(slot.gameObject);
        }

        confirmButton.gameObject.SetActive(selectedRoom >= 0);
    }

    private void Select(int roomIndex)
    {
        selectedRoom = roomIndex;
        Rebuild();
    }

    private void Confirm()
    {
        if (selectedRoom < 0) return;

        if (placingArtifact != null)
        {
            if (ArtifactInventory.main != null && ArtifactInventory.main.PlaceInRoom(placingArtifact, selectedRoom)) Close();
        }
        else if (moving != null)
        {
            if (AlienRoster.main != null && AlienRoster.main.MoveTo(moving, selectedRoom)) Close();
        }
        else if (BreedingManager.main != null && BreedingManager.main.PlaceEgg(selectedRoom))
        {
            Close();
        }
    }

    private static void SetText(Transform parent, string childName, string text)
    {
        Transform child = parent.Find(childName);
        TMP_Text label = child != null ? child.GetComponent<TMP_Text>() : null;
        if (label != null) label.text = text;
    }
}
*/