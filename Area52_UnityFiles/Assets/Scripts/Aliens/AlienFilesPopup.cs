using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

/*
 * "Alien Files": opened by the folder button
 * One box per alien the player owns (duplicates get their own box) with its picture and name
 * Select one, then Open File shows its file, or Sell asks "Are you sure?" in its room
 * Sell is greyed out for aliens away breeding or exploring
 * Buy opens the Common / Rare / Ultra Rare shop; the bought alien is shown big, then placed with the Room Picker
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
    [Tooltip("The \"Are you sure you want to sell this alien?\" popup. Leave empty to use the one in the scene.")]
    [SerializeField] private AlienSellPopup sellPopup;

    [Header("Buying")]
    [SerializeField] private Button buyButton;
    [Tooltip("The Common / Rare / Ultra Rare popup (the same one the Artifacts page uses).")]
    [SerializeField] private TierShopPopup shopPopup;
    [Tooltip("Every alien that can exist - bought aliens are picked from here.")]
    [SerializeField] private AlienCatalog catalog;
    [Tooltip("Shows the bought alien big, with Place in Room.")]
    [SerializeField] private NewAlienPopup newAlienPopup;
    [SerializeField] private string shopTitle = "Buy an Alien";

    private readonly List<GameObject> slots = new List<GameObject>();
    private OwnedAlien selected;
    private bool listening;

    public bool IsOpen => root != null && root.activeSelf;

    private void Awake()
    {
        main = this;
        closeButton.onClick.AddListener(Close);
        openFileButton.onClick.AddListener(OpenFile);
        sellButton.onClick.AddListener(Sell);
        if (buyButton != null) buyButton.onClick.AddListener(OpenShop);
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

    // Pass an alien to open with it already selected (e.g. coming back from "No")
    public void Open(OwnedAlien select = null)
    {
        selected = select;
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
        sellButton.interactable = AlienSellPopup.CanSell(selected); // False for nothing selected, or away breeding / exploring
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

    private void Sell()
    {
        if (!AlienSellPopup.CanSell(selected)) return;

        AlienSellPopup popup = sellPopup != null ? sellPopup : AlienSellPopup.main;
        if (popup == null) { Debug.LogWarning("AlienFilesPopup: no AlienSellPopup in the scene.", this); return; }

        OwnedAlien alien = selected;
        Close(); // Out of the way, so the room shows
        popup.Open(alien);
    }


    // BUYING

    // The shop's options, in order
    private static readonly TraitRarity[] ShopRarities = { TraitRarity.Common, TraitRarity.Rare, TraitRarity.UltraRare };

    private void OpenShop()
    {
        if (shopPopup == null || catalog == null || newAlienPopup == null)
        {
            Debug.LogWarning("AlienFilesPopup: fill in Shop Popup, Catalog and New Alien Popup to buy aliens.", this);
            return;
        }
        if (PriceList.main == null) { Debug.LogWarning("AlienFilesPopup: no PriceList in the scene.", this); return; }

        ShopTier[] tiers = new ShopTier[ShopRarities.Length];
        for (int i = 0; i < ShopRarities.Length; i++)
        {
            TraitRarity rarity = ShopRarities[i];
            tiers[i] = new ShopTier(AlienSpecies.RarityName(rarity), PriceList.main.AlienBuyPrice(rarity), catalog.HasAny(rarity));
        }

        shopPopup.Open(shopTitle, tiers, index => Buy(ShopRarities[index]));
    }

    // Called by the shop's Purchase button - true = bought (the shop closes)
    private bool Buy(TraitRarity rarity)
    {
        if (CoinBank.main == null) return false;

        AlienSpecies pick = catalog.PickRandom(rarity); // Picked before paying, so nothing is spent on nothing
        if (pick == null) return false;
        if (!CoinBank.main.TrySpend(PriceList.main.AlienBuyPrice(rarity))) return false; // Plays the cha-ching

        Close(); // Out of the way - the new alien gets the whole screen
        newAlienPopup.Open(pick);
        return true;
    }
}