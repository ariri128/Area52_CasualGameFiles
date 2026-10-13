using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

/*
 * "Radio Tunes": opened by the radio button
 * One row per track:
 *   - Play / pause button: previews the track, even if not owned
 *      - The background music pauses while a preview plays - only one preview at a time
 *   - The track's name, and "Owned" or its cost with a coin
 *   - Owned rows are tinted, locked rows are grey
 * Tap a row to select it
 * Confirm makes the selected (owned) track the background music
*/

public class RadioPopup : MonoBehaviour
{
    public static RadioPopup main { get; private set; }

    [Header("Parts")]
    [SerializeField] private GameObject root;
    [SerializeField] private Button closeButton;
    [Tooltip("Where the rows are lined up (has a Vertical Layout Group).")]
    [SerializeField] private Transform rowContainer;
    [SerializeField] private Button rowTemplate;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button purchaseButton;

    [Header("Look")]
    [SerializeField] private Sprite playSprite;
    [SerializeField] private Sprite pauseSprite;
    [Tooltip("Row color for tracks the player owns.")]
    [SerializeField] private Color ownedColor = new Color(0.78f, 0.86f, 1f);
    [Tooltip("Row color for tracks still locked.")]
    [SerializeField] private Color lockedColor = new Color(0.7f, 0.7f, 0.7f);
    [SerializeField] private string ownedText = "Owned";

    private readonly List<GameObject> rows = new List<GameObject>();
    private MusicShop.Track selected;
    private MusicShop.Track previewing;
    private bool listening;

    public bool IsOpen => root != null && root.activeSelf;

    private void Awake()
    {
        main = this;
        closeButton.onClick.AddListener(Close);
        confirmButton.onClick.AddListener(Confirm);
        purchaseButton.onClick.AddListener(Purchase);
        rowTemplate.gameObject.SetActive(false);
        root.SetActive(false);
    }

    private void OnDestroy()
    {
        if (main == this) main = null;
    }

    // Triea in both OnEnable and Start: when the game starts, the shops may not be set up yet the first time this runs
    private void OnEnable() => StartListening();
    private void Start() => StartListening();

    private void StartListening()
    {
        if (listening || MusicShop.main == null || CoinBank.main == null) return;
        MusicShop.main.onChanged.AddListener(RefreshIfOpen);
        CoinBank.main.onChanged.AddListener(RefreshIfOpen);
        listening = true;
    }

    private void OnDisable()
    {
        if (!listening) return;
        if (MusicShop.main != null) MusicShop.main.onChanged.RemoveListener(RefreshIfOpen);
        if (CoinBank.main != null) CoinBank.main.onChanged.RemoveListener(RefreshIfOpen);
        listening = false;
    }

    public void Open()
    {
        if (MusicShop.main == null) return;
        selected = MusicShop.main.CurrentTrack;
        root.SetActive(true);
        Rebuild();
    }

    public void Close()
    {
        StopPreview(); // Never leave a preview playing behind a closed popup
        root.SetActive(false);
    }

    private void RefreshIfOpen()
    {
        if (IsOpen) Rebuild();
    }

    private void Rebuild()
    {
        foreach (GameObject row in rows) Destroy(row);
        rows.Clear();

        MusicShop shop = MusicShop.main;
        if (shop == null) return;

        foreach (MusicShop.Track track in shop.Tracks)
        {
            MusicShop.Track thisTrack = track;
            bool isOwned = shop.IsOwned(track);

            Button row = Instantiate(rowTemplate, rowContainer);
            row.gameObject.SetActive(true);
            row.name = $"Track_{track.displayName}";
            row.onClick.AddListener(() => Select(thisTrack));

            Image rowImage = row.GetComponent<Image>();
            if (rowImage != null) rowImage.color = isOwned ? ownedColor : lockedColor;

            SetText(row.transform, "Name", track.displayName);
            SetText(row.transform, "Status", isOwned ? ownedText : track.cost.ToString("N0"));

            Transform coin = row.transform.Find("CoinIcon");
            if (coin != null) coin.gameObject.SetActive(!isOwned);

            Transform border = row.transform.Find("SelectedBorder");
            if (border != null) border.gameObject.SetActive(track == selected);

            Transform play = row.transform.Find("PlayButton");
            if (play != null)
            {
                Button playButton = play.GetComponent<Button>();
                Image playImage = play.GetComponent<Image>();
                if (playImage != null) playImage.sprite = track == previewing ? pauseSprite : playSprite;
                if (playButton != null) playButton.onClick.AddListener(() => TogglePreview(thisTrack));
            }

            rows.Add(row.gameObject);
        }

        bool selectedOwned = shop.IsOwned(selected);
        confirmButton.interactable = selected != null && selectedOwned && selected != shop.CurrentTrack;
        purchaseButton.interactable = selected != null && !selectedOwned && shop.CanAfford(selected);
    }

    private void Select(MusicShop.Track track)
    {
        selected = track;
        Rebuild();
    }

    private void TogglePreview(MusicShop.Track track)
    {
        if (AudioManager.main == null || track.clip == null) return;

        if (previewing == track)
        {
            StopPreview(); // Pause button: back to the background music
        }
        else
        {
            previewing = track; // Play button: starts this one (stops any other preview)
            AudioManager.main.PlayPreview(track.clip);
        }
        Rebuild();
    }

    private void StopPreview()
    {
        if (previewing == null) return;
        previewing = null;
        if (AudioManager.main != null) AudioManager.main.StopPreview();
    }

    private void Confirm()
    {
        if (MusicShop.main == null || selected == null) return;
        StopPreview();
        MusicShop.main.Use(selected);
        Rebuild();
    }

    private void Purchase()
    {
        if (MusicShop.main == null || selected == null) return;
        MusicShop.main.Buy(selected); // onChanged rebuilds the rows: the track turns "Owned"
    }

    private static void SetText(Transform parent, string childName, string text)
    {
        Transform child = parent.Find(childName);
        TMP_Text label = child != null ? child.GetComponent<TMP_Text>() : null;
        if (label != null) label.text = text;
    }
}