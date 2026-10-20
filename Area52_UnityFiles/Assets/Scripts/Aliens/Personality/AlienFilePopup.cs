using UnityEngine;
using TMPro;
using UnityEngine.UI;

/*
 * An alien's file (the manila folder):
 *   Photo card   - sprite, name and date acquired, Rename button
 *   Purple box   - Alien Compatibility: personality, likes, dislikes
 *   Right sheet  - parents, apartment + Move button, emotion status, description
 * Refreshes whenever the roster changes, so renames and moves show straight away
*/

public class AlienFilePopup : MonoBehaviour
{
    public static AlienFilePopup main { get; private set; }

    [Header("Parts")]
    [Tooltip("The child that's shown and hidden (dark background + folder).")]
    [SerializeField] private GameObject root;
    [SerializeField] private Button closeButton;

    [Header("Photo card")]
    [SerializeField] private Image photo;
    [SerializeField] private TMP_Text caption;
    [SerializeField] private Button renameButton;

    [Header("Purple box")]
    [SerializeField] private TMP_Text compatibilityText;

    [Header("Right sheet")]
    [SerializeField] private TMP_Text parentsText;
    [SerializeField] private TMP_Text apartmentText;
    [SerializeField] private Button moveButton;
    [SerializeField] private Image emotionImage;
    [SerializeField] private TMP_Text descriptionText;

    [Header("Rename box")]
    [SerializeField] private GameObject renameRoot;
    [SerializeField] private TMP_InputField renameInput;
    [SerializeField] private Button renameSaveButton;
    [SerializeField] private Button renameCancelButton;

    [Header("Move")]
    [Tooltip("The room popup in this scene (Room Picker Popup).")]
    [SerializeField] private RoomPickerPopup roomPicker;

    [Header("Text")]
    [Tooltip("{0} = name, {1} = date acquired.")]
    [SerializeField] private string captionFormat = "{0}\n<size=70%>{1}</size>";
    [Tooltip("{0} = personality, {1} = likes, {2} = dislikes.")]
    [SerializeField]
    [TextArea(3, 6)]
    private string compatibilityFormat = "<b>Alien Compatibility</b>\nPersonality: {0}\nLikes: {1}\nDislikes: {2}";
    [SerializeField] private string parentsFormat = "<b>Parents:</b>\n{0}";
    [Tooltip("{0} = room number.")]
    [SerializeField] private string apartmentFormat = "<b>Apartment:</b>\nRoom {0}";
    [SerializeField] private string breedingNote = " (away breeding)";
    [SerializeField]
    [TextArea(3, 6)]
    private string descriptionFormat = "<b>Description:</b>\n_______________\n_______________\n_______________";
    [SerializeField] private string unknownText = "Unknown";

    public OwnedAlien Current { get; private set; }
    public bool IsOpen => root != null && root.activeSelf;

    private bool listening;

    private void Awake()
    {
        main = this;

        closeButton.onClick.AddListener(Close);
        renameButton.onClick.AddListener(OpenRename);
        moveButton.onClick.AddListener(OpenMove);
        renameSaveButton.onClick.AddListener(SaveRename);
        renameCancelButton.onClick.AddListener(CloseRename);
        renameInput.onSubmit.AddListener(_ => SaveRename()); // Enter / the keyboard's Done key

        renameRoot.SetActive(false);
        root.SetActive(false);
    }

    private void OnDestroy()
    {
        if (main == this) main = null;
    }

    // Tries in both OnEnable and Start: the roster may not be set up yet the first time this runs
    private void OnEnable() => StartListening();
    private void Start() => StartListening();

    private void StartListening()
    {
        if (listening || AlienRoster.main == null) return;
        AlienRoster.main.onChanged.AddListener(RefreshIfOpen);
        listening = true;
    }

    private void OnDisable()
    {
        if (listening && AlienRoster.main != null) AlienRoster.main.onChanged.RemoveListener(RefreshIfOpen);
        listening = false;
    }

    public void Open(OwnedAlien alien)
    {
        if (alien == null) return;
        Current = alien;
        renameRoot.SetActive(false);
        root.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        CloseRename();
        if (roomPicker != null && roomPicker.IsOpen) roomPicker.Close();
        root.SetActive(false);
        Current = null;
    }

    private void RefreshIfOpen()
    {
        if (IsOpen) Refresh();
    }

    private void Refresh()
    {
        if (Current == null || Current.species == null) return;
        AlienRoster roster = AlienRoster.main;
        AlienMoods moods = AlienMoods.main;

        // Photo card
        photo.sprite = Current.species.sprite;
        photo.preserveAspect = true;
        caption.text = string.Format(captionFormat, Current.DisplayName, Current.dateAcquired);

        // Purple box
        Personality personality = moods != null ? moods.PersonalityOf(Current) : null;
        compatibilityText.text = personality != null
            ? string.Format(compatibilityFormat, personality.displayName, personality.LikesText, personality.DislikesText)
            : string.Format(compatibilityFormat, unknownText, unknownText, unknownText);

        // Right sheet
        parentsText.text = string.Format(parentsFormat, roster != null ? roster.ParentsText(Current) : unknownText);
        apartmentText.text = string.Format(apartmentFormat, Current.roomIndex + 1) +
                             (Current.inBreedingRoom ? breedingNote : "");

        if (emotionImage != null && moods != null)
        {
            emotionImage.sprite = moods.SpriteFor(moods.MoodOf(Current));
            emotionImage.preserveAspect = true;
        }

        if (descriptionText != null) descriptionText.text = descriptionFormat; //TODO: per-alien descriptions once they're written
    }


    // RENAME

    private void OpenRename()
    {
        if (Current == null) return;
        renameInput.characterLimit = AlienRoster.main != null ? AlienRoster.main.MaxNameLength : 16;
        renameInput.text = Current.DisplayName;
        renameRoot.SetActive(true);
        renameInput.Select();
        renameInput.ActivateInputField(); // Opens the keyboard on a phone
    }

    private void SaveRename()
    {
        if (Current != null && AlienRoster.main != null && renameRoot.activeSelf)
            AlienRoster.main.Rename(Current, renameInput.text); // onChanged refreshes the file
        CloseRename();
    }

    private void CloseRename()
    {
        if (renameRoot != null) renameRoot.SetActive(false);
    }


    // MOVE

    private void OpenMove()
    {
        if (Current == null) return;
        if (roomPicker != null) roomPicker.OpenForMove(Current);
        else Debug.LogWarning("AlienFilePopup: no Room Picker set.", this);
    }
}