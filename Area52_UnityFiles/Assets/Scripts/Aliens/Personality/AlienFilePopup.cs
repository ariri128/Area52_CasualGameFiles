using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine.UI;

/*
 * An alien's file (the manila folder):
 *   Photo card   - sprite, name and date acquired, Rename button
 *   Purple box   - Alien Compatibility: personality, likes, dislikes
 *   Right sheet  - 3 pages, turned with the bottom-right corner (and Prev in the bottom-left):
 *                    1. parents, apartment + Move button, emotion status, description
 *                    2. rarity, status (Breeding / Exploring / At Home), sell price range
 *                    3. collected artifacts
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

    [Header("Pages")]
    [Tooltip("The right sheet's pages, in order. Each is a whole sheet of paper (its own paper image + its text), " +
             "all in the same spot under the same parent.")]
    [SerializeField] private GameObject[] pages = new GameObject[3];
    [Tooltip("Bottom-right corner. Hidden on the last page.")]
    [SerializeField] private Button turnPageButton;
    [Tooltip("Bottom-left corner. Hidden on the first page.")]
    [SerializeField] private Button prevButton;
    [SerializeField] private float turnSeconds = 0.45f;
    [Tooltip("How dark the page gets as it turns away (1 = no change).")]
    [SerializeField, Range(0f, 1f)] private float turnShade = 0.7f;
    [Tooltip("Optional. Plays on every page turn.")]
    [SerializeField] private AudioClip pageTurnSound;
    [SerializeField, Range(0f, 1f)] private float pageTurnVolume = 1f;

    [Header("Page 2")]
    [SerializeField] private TMP_Text rarityText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text priceText;

    [Header("Page 3")]
    [SerializeField] private TMP_Text artifactsText;

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

    [Tooltip("{0} = Common / Rare / Ultra Rare.")]
    [SerializeField] private string rarityFormat = "<b>Rarity:</b>\n{0}";
    [Tooltip("{0} = Breeding / Exploring / At Home.")]
    [SerializeField] private string statusFormat = "<b>Status:</b>\n{0}";
    [Tooltip("{0} = lowest price, {1} = highest price.")]
    [SerializeField] private string priceFormat = "<b>Price (based on Rarity):</b>\nSold for around {0} - {1} coins";
    [SerializeField] private string statusBreeding = "Breeding";
    [SerializeField] private string statusExploring = "Exploring";
    [SerializeField] private string statusHome = "At Home";
    [Tooltip("{0} = the list (empty if it hasn't found anything yet).")]
    [SerializeField] private string artifactsFormat = "<b>Collected Artifacts:</b>\n{0}";
    [Tooltip("One line of the list. {0} = artifact name.")]
    [SerializeField] private string artifactLineFormat = "- {0}";
    [Tooltip("Added after the name when it found more than one. {0} = how many.")]
    [SerializeField] private string artifactCountFormat = " x{0}";

    public OwnedAlien Current { get; private set; }
    public bool IsOpen => root != null && root.activeSelf;

    private bool listening;

    private int currentPage;
    private Coroutine turning;
    private GameObject flap; // The copy of a page that's mid-turn

    private void Awake()
    {
        main = this;

        closeButton.onClick.AddListener(Close);
        renameButton.onClick.AddListener(OpenRename);
        moveButton.onClick.AddListener(OpenMove);
        renameSaveButton.onClick.AddListener(SaveRename);
        renameCancelButton.onClick.AddListener(CloseRename);
        renameInput.onSubmit.AddListener(_ => SaveRename()); // Enter / the keyboard's Done key

        if (turnPageButton != null) turnPageButton.onClick.AddListener(NextPage);
        if (prevButton != null) prevButton.onClick.AddListener(PrevPage);

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
        StopTurning();
        ShowPage(0); // Always opens on the first page
        Refresh();
    }

    public void Close()
    {
        StopTurning();
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

        // Pages 2 and 3 (filled in even while hidden, so they're ready when the page turns)
        RefreshDetailsPage();
        RefreshArtifactsPage();
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

    // PAGE 2 + 3

    private void RefreshDetailsPage()
    {
        TraitRarity rarity = Current.species.OverallRarity;

        if (rarityText != null) rarityText.text = string.Format(rarityFormat, AlienSpecies.RarityName(rarity));

        if (statusText != null)
        {
            string status;
            if (Current.inBreedingRoom) status = statusBreeding;
            else if (Current.isExploring) status = statusExploring;
            else status = statusHome;
            statusText.text = string.Format(statusFormat, status);
        }

        if (priceText != null)
        {
            string low = unknownText, high = unknownText;
            if (PriceList.main != null)
            {
                PriceList.PriceRange range = PriceList.main.AlienSellRange(rarity);
                low = range.min.ToString("N0");
                high = range.max.ToString("N0");
            }
            priceText.text = string.Format(priceFormat, low, high);
        }
    }

    // Each artifact once, in the order first found, with "x2" etc. for repeats
    private void RefreshArtifactsPage()
    {
        if (artifactsText == null) return;

        List<ArtifactDefinition> order = new List<ArtifactDefinition>();
        Dictionary<ArtifactDefinition, int> counts = new Dictionary<ArtifactDefinition, int>();
        if (Current.collectedArtifacts != null)
        {
            foreach (ArtifactDefinition artifact in Current.collectedArtifacts)
            {
                if (artifact == null) continue;
                if (!counts.ContainsKey(artifact)) { counts[artifact] = 0; order.Add(artifact); }
                counts[artifact]++;
            }
        }

        StringBuilder list = new StringBuilder();
        foreach (ArtifactDefinition artifact in order)
        {
            if (list.Length > 0) list.Append('\n');
            list.Append(string.Format(artifactLineFormat, artifact.displayName));
            if (counts[artifact] > 1) list.Append(string.Format(artifactCountFormat, counts[artifact]));
        }

        artifactsText.text = string.Format(artifactsFormat, list.ToString()); // Nothing found yet = just the heading
    }


    // TURNING PAGES

    private void ShowPage(int index)
    {
        if (pages == null || pages.Length == 0) return;
        currentPage = Mathf.Clamp(index, 0, pages.Length - 1);
        for (int i = 0; i < pages.Length; i++)
            if (pages[i] != null) pages[i].SetActive(i == currentPage);
        RefreshPageButtons();
    }

    private void RefreshPageButtons()
    {
        bool idle = turning == null;
        if (turnPageButton != null) turnPageButton.gameObject.SetActive(idle && currentPage < pages.Length - 1);
        if (prevButton != null) prevButton.gameObject.SetActive(idle && currentPage > 0);
    }

    private void NextPage()
    {
        if (turning != null || currentPage >= pages.Length - 1) return;
        CloseRename();
        turning = StartCoroutine(TurnPage(currentPage + 1, true));
    }

    private void PrevPage()
    {
        if (turning != null || currentPage <= 0) return;
        CloseRename();
        turning = StartCoroutine(TurnPage(currentPage - 1, false));
    }

    // Forward: a copy of the current page swings away to the left (the spine), uncovering the next page
    // Back:    a copy of the previous page swings back in from the left, over the current one
    private IEnumerator TurnPage(int target, bool forward)
    {
        RefreshPageButtons(); // Hides both buttons while turning (turning is already set)
        AudioManager.PlaySound(pageTurnSound, pageTurnVolume);

        GameObject source = forward ? pages[currentPage] : pages[target];
        flap = Instantiate(source, source.transform.parent);
        flap.name = "PageTurning";
        flap.SetActive(true);
        flap.transform.SetSiblingIndex(source.transform.parent.childCount - 1); // On top of every page

        // Just for show - taps go through it
        CanvasGroup flapGroup = flap.GetComponent<CanvasGroup>();
        if (flapGroup == null) flapGroup = flap.AddComponent<CanvasGroup>();
        flapGroup.blocksRaycasts = false;
        flapGroup.interactable = false;

        RectTransform flapRect = (RectTransform)flap.transform;
        SetPivotKeepPosition(flapRect, new Vector2(0f, 0.5f)); // Turns around its left edge, like a book's spine

        // Every picture and text on the page, so it can darken as it turns
        Graphic[] graphics = flap.GetComponentsInChildren<Graphic>(true);
        Color[] colors = new Color[graphics.Length];
        for (int i = 0; i < graphics.Length; i++) colors[i] = graphics[i].color;

        if (forward) ShowOnly(target); // The next page is already underneath, waiting to be uncovered

        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / turnSeconds)
        {
            float turned = forward ? t : 1f - t; // 0 = flat, 1 = edge-on
            float eased = turned * turned * (3f - 2f * turned);

            Vector3 scale = flapRect.localScale;
            scale.x = Mathf.Cos(eased * Mathf.PI * 0.5f); // Squashing toward the spine looks like turning in 3D
            flapRect.localScale = scale;

            float shade = Mathf.Lerp(1f, turnShade, eased);
            for (int i = 0; i < graphics.Length; i++)
            {
                Color c = colors[i];
                graphics[i].color = new Color(c.r * shade, c.g * shade, c.b * shade, c.a);
            }
            yield return null;
        }

        Destroy(flap);
        flap = null;

        if (!forward) ShowOnly(target); // The page that swung back in is now the real one
        currentPage = target;
        turning = null;
        RefreshPageButtons();
    }

    // Shows one page without changing currentPage (used mid-turn)
    private void ShowOnly(int index)
    {
        for (int i = 0; i < pages.Length; i++)
            if (pages[i] != null) pages[i].SetActive(i == index);
    }

    private void StopTurning()
    {
        if (turning != null) StopCoroutine(turning);
        turning = null;
        if (flap != null) Destroy(flap);
        flap = null;
    }

    // Changing a pivot normally makes the object jump - this moves it back so it stays put
    // Works for any anchors (stretched or not): it measures the corner on screen before and after, and undoes the difference
    private static void SetPivotKeepPosition(RectTransform rect, Vector2 pivot)
    {
        Vector3[] before = new Vector3[4];
        rect.GetWorldCorners(before);

        rect.pivot = pivot;

        Vector3[] after = new Vector3[4];
        rect.GetWorldCorners(after);
        rect.position += before[0] - after[0]; // Corner 0 = bottom-left, back where it was
    }
}