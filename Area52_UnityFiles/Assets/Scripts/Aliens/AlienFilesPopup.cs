using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

/*
 * "Alien Files": opened by the folder button
 * One box per alien the player owns (duplicates get their own box) with its picture and name
 * Select one, then Open File shows its file; Sell isn't available yet
*/

public class AlienFilesPopup : MonoBehaviour
{
    public static AlienFilesPopup main { get; private set; }

    [Header("Parts")]
    [Tooltip("The child that's shown and hidden (dark background + window).")]
    [SerializeField] private GameObject root;
    [SerializeField] private Button closeButton;
    [Tooltip("Where the alien boxes are lined up (the scroll view's Content).")]
    [SerializeField] private Transform slotContainer;
    [Tooltip("One alien box. Copied once per alien. Needs children named Picture, Name and SelectedBorder.")]
    [SerializeField] private Button slotTemplate;
    [SerializeField] private Button openFileButton;
    [SerializeField] private Button sellButton;
    [Tooltip("Optional. Scrolled back to the top each time the popup opens.")]
    [SerializeField] private ScrollRect scroll;
    [Tooltip("The alien file popup. Leave empty to use the one in the scene.")]
    [SerializeField] private AlienFilePopup filePopup;

    private readonly List<GameObject> slots = new List<GameObject>();
    private OwnedAlien selected;
    private bool listening;

    public bool IsOpen => root != null && root.activeSelf;

    private void Awake()
    {
        main = this;
        closeButton.onClick.AddListener(Close);
        openFileButton.onClick.AddListener(OpenFile);
        sellButton.interactable = false; //TODO: selling comes later
        slotTemplate.gameObject.SetActive(false);
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
        AlienRoster.main.onChanged.AddListener(RefreshIfOpen); // Renames, hatches, moves
        listening = true;
    }

    private void OnDisable()
    {
        if (listening && AlienRoster.main != null) AlienRoster.main.onChanged.RemoveListener(RefreshIfOpen);
        listening = false;
    }

    public void Open()
    {
        selected = null;
        root.SetActive(true);
        Rebuild();
        if (scroll != null) scroll.verticalNormalizedPosition = 1f; // Back to the top
    }

    public void Close()
    {
        root.SetActive(false);
        selected = null;
    }

    private void RefreshIfOpen()
    {
        if (IsOpen) Rebuild();
    }

    private void Rebuild()
    {
        foreach (GameObject slot in slots) Destroy(slot);
        slots.Clear();

        AlienRoster roster = AlienRoster.main;
        if (roster == null) return;

        bool selectedStillOwned = false;

        foreach (OwnedAlien alien in roster.All)
        {
            if (alien == null || alien.species == null) continue;
            OwnedAlien thisAlien = alien;
            if (alien == selected) selectedStillOwned = true;

            Button slot = Instantiate(slotTemplate, slotContainer);
            slot.gameObject.SetActive(true);
            slot.name = $"Alien_{alien.DisplayName}";
            slot.onClick.AddListener(() => Select(thisAlien));

            Transform picture = slot.transform.Find("Picture");
            Image pictureImage = picture != null ? picture.GetComponent<Image>() : null;
            if (pictureImage != null)
            {
                pictureImage.sprite = alien.species.sprite;
                pictureImage.preserveAspect = true;
            }

            Transform nameChild = slot.transform.Find("Name");
            TMP_Text nameText = nameChild != null ? nameChild.GetComponent<TMP_Text>() : null;
            if (nameText != null) nameText.text = alien.DisplayName;

            Transform border = slot.transform.Find("SelectedBorder");
            if (border != null) border.gameObject.SetActive(alien == selected);

            slots.Add(slot.gameObject);
        }

        if (!selectedStillOwned) selected = null;
        openFileButton.interactable = selected != null;
    }

    private void Select(OwnedAlien alien)
    {
        selected = alien;
        Rebuild();
    }

    private void OpenFile()
    {
        if (selected == null) return;
        AlienFilePopup file = filePopup != null ? filePopup : AlienFilePopup.main;
        if (file != null) file.Open(selected); // Draws on top; closing it comes back here
        else Debug.LogWarning("AlienFilesPopup: no AlienFilePopup in the scene.", this);
    }
}