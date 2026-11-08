using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

/*
 * Artifacts already in a room are greyed out but can still be picked, to take them out
 * Select one, then:
 *   Unplaced -> Place in Room puts it straight into the room the player is looking at ("Room Full" if there's no space)
 *   Placed   -> the button says Remove and takes it out of its room
 * Sell only works on unplaced artifacts
*/

public class ArtifactCollectionPopup : MonoBehaviour
{
    public static ArtifactCollectionPopup main { get; private set; }

    [Header("Parts")]
    [Tooltip("The child that's shown and hidden (dark background + window).")]
    [SerializeField] private GameObject root;
    [SerializeField] private Button closeButton;
    [Tooltip("Where the artifact boxes are lined up (the scroll view's Content).")]
    [SerializeField] private RectTransform slotContainer;
    [Tooltip("One artifact box. Copied once per artifact. Needs children named Picture, Name, Room, SelectedBorder and NewBadge.")]
    [SerializeField] private Button slotTemplate;
    [SerializeField] private Button placeButton;
    [Tooltip("The text on the place button.")]
    [SerializeField] private TMP_Text placeLabel;
    [SerializeField] private Button sellButton;
    [Tooltip("Optional. Scrolled to the top (or to the selected artifact) each time the popup opens. " +
             "Also used to tell which boxes the player can actually see.")]
    [SerializeField] private ScrollRect scroll;
    [Tooltip("The room popup in this scene (Room Picker Popup).")]
    [SerializeField] private RoomPickerPopup roomPicker;

    [Tooltip("The \"Selling for ___ coins\" popup.")]
    [SerializeField] private ArtifactSellPopup sellPopup;
    [SerializeField] private Button buyButton;
    [Tooltip("The Common / Uncommon / Rare popup.")]
    [SerializeField] private TierShopPopup shopPopup;
    [Tooltip("The artifact reveal animation (ArtifactReveal). Bought artifacts fly into their box with it.")]
    [SerializeField] private ArtifactReveal reveal;

    [Header("Look")]
    [Tooltip("How see-through a box is when its artifact is already in a room.")]
    [SerializeField, Range(0f, 1f)] private float placedAlpha = 0.45f;

    [Header("Text")]
    [SerializeField] private string placeText = "Place in Room";
    // [SerializeField] private string changeText = "Change Rooms"; // No longer used - kept as a fail safe
    [SerializeField] private string removeText = "Remove";
    [SerializeField] private string shopTitle = "Buy an Artifact";
    [SerializeField] private string unplacedText = "Unplaced";
    [Tooltip("{0} is the room number.")]
    [SerializeField] private string roomFormat = "Room {0}";

    private readonly List<GameObject> slots = new List<GameObject>();
    private readonly List<OwnedArtifact> slotArtifacts = new List<OwnedArtifact>(); // Which artifact each box shows
    private readonly HashSet<OwnedArtifact> showingNew = new HashSet<OwnedArtifact>(); // Boxes with a NEW tag this visit
    private OwnedArtifact selected;
    private bool listening;

    public bool IsOpen => root != null && root.activeSelf;

    private void Awake()
    {
        main = this;
        closeButton.onClick.AddListener(Close);
        placeButton.onClick.AddListener(PlaceOrRemove); // Used to be OpenRoomPicker
        sellButton.onClick.AddListener(OpenSell);
        buyButton.onClick.AddListener(OpenShop);
        slotTemplate.gameObject.SetActive(false);
        root.SetActive(false);
    }

    private void OnDestroy()
    {
        if (main == this) main = null;
    }

    // Tries in both OnEnable and Start: the inventory may not be set up yet the first time this runs
    private void OnEnable() => StartListening();
    private void Start() => StartListening();

    private void StartListening()
    {
        if (listening || ArtifactInventory.main == null) return;
        ArtifactInventory.main.onChanged.AddListener(RefreshIfOpen); // New finds and placing
        listening = true;
    }

    private void OnDisable()
    {
        if (listening && ArtifactInventory.main != null) ArtifactInventory.main.onChanged.RemoveListener(RefreshIfOpen);
        listening = false;
    }


    // OPEN / CLOSE

    // Pass an artifact to open with it already selected (e.g. from a room's artifact circle)
    public void Open(OwnedArtifact select = null)
    {
        selected = select;
        showingNew.Clear(); // A fresh visit: only artifacts that are still new get the tag
        root.SetActive(true);
        Rebuild();

        if (scroll == null) return;
        GameObject selectedSlot = SlotFor(select);
        if (selectedSlot != null) ScrollTo((RectTransform)selectedSlot.transform);
        else scroll.verticalNormalizedPosition = 1f; // Back to the top
    }

    public void Close()
    {
        if (roomPicker != null && roomPicker.IsOpen) roomPicker.Close();
        if (sellPopup != null && sellPopup.IsOpen) sellPopup.Close();
        if (shopPopup != null && shopPopup.IsOpen) shopPopup.Close();
        root.SetActive(false);
        selected = null;
        showingNew.Clear();
    }

    private void RefreshIfOpen()
    {
        if (IsOpen) Rebuild();
    }


    // THE GRID

    private void Rebuild()
    {
        foreach (GameObject slot in slots)
        {
            slot.SetActive(false); // Destroy only happens at the end of the frame - this takes it out of the layout now
            Destroy(slot);
        }
        slots.Clear();
        slotArtifacts.Clear();

        ArtifactInventory inventory = ArtifactInventory.main;
        if (inventory == null) return;

        bool selectedStillOwned = false;

        foreach (OwnedArtifact artifact in inventory.All)
        {
            if (artifact == null || artifact.definition == null) continue;
            OwnedArtifact thisArtifact = artifact;
            if (artifact == selected) selectedStillOwned = true;

            // Still unseen = gets the tag, and keeps it until the page closes (even once it's been seen)
            if (artifact.isNew) showingNew.Add(artifact);

            Button slot = Instantiate(slotTemplate, slotContainer);
            slot.gameObject.SetActive(true);
            slot.name = $"Artifact_{artifact.DisplayName}";
            slot.onClick.AddListener(() => Select(thisArtifact));

            Transform picture = slot.transform.Find("Picture");
            Image pictureImage = picture != null ? picture.GetComponent<Image>() : null;
            if (pictureImage != null)
            {
                pictureImage.sprite = artifact.definition.sprite;
                pictureImage.enabled = artifact.definition.sprite != null;
                pictureImage.preserveAspect = true;
            }

            SetText(slot.transform, "Name", artifact.DisplayName);
            SetText(slot.transform, "Room", artifact.IsPlaced ? string.Format(roomFormat, artifact.roomIndex + 1) : unplacedText);

            // Greyed out when it's already in a room - still clickable so it can be moved
            CanvasGroup group = slot.GetComponent<CanvasGroup>();
            if (group == null) group = slot.gameObject.AddComponent<CanvasGroup>();
            group.alpha = artifact.IsPlaced ? placedAlpha : 1f;

            Transform border = slot.transform.Find("SelectedBorder");
            if (border != null) border.gameObject.SetActive(artifact == selected);

            Transform newBadge = slot.transform.Find("NewBadge");
            if (newBadge != null) newBadge.gameObject.SetActive(showingNew.Contains(artifact));

            slots.Add(slot.gameObject);
            slotArtifacts.Add(artifact);
        }

        if (!selectedStillOwned) selected = null;
        RefreshButtons();

        // Put the boxes in their real spots now, so the "can the player see it?" check isn't fooled this frame
        LayoutRebuilder.ForceRebuildLayoutImmediate(slotContainer);
    }

    // Place in Room: greyed out until something's picked, and says Remove when the pick is already in a room
    // Sell: only for unplaced artifacts - placed ones have to be removed first
    private void RefreshButtons()
    {
        bool hasPick = selected != null;
        bool pickIsPlaced = hasPick && selected.IsPlaced;

        placeButton.interactable = hasPick;
        if (placeLabel != null) placeLabel.text = pickIsPlaced ? removeText : placeText;

        sellButton.interactable = hasPick && !pickIsPlaced;
    }

    private void Select(OwnedArtifact artifact)
    {
        selected = artifact;
        Rebuild();
    }

    private GameObject SlotFor(OwnedArtifact artifact)
    {
        int index = artifact != null ? slotArtifacts.IndexOf(artifact) : -1;
        return index >= 0 ? slots[index] : null;
    }

    // Scrolls so this box is in the middle of the view (or as close as the scroll can go)
    private void ScrollTo(RectTransform target)
    {
        RectTransform content = scroll.content;
        RectTransform viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
        LayoutRebuilder.ForceRebuildLayoutImmediate(content); // The new boxes haven't been laid out yet

        float contentHeight = content.rect.height;
        float viewHeight = viewport.rect.height;
        if (contentHeight <= viewHeight) { scroll.verticalNormalizedPosition = 1f; return; }

        // How far down from the top of the content the box's middle is
        float targetY = content.InverseTransformPoint(target.TransformPoint(target.rect.center)).y;
        float fromTop = content.rect.yMax - targetY;

        float scrollAmount = Mathf.Clamp01((fromTop - viewHeight * 0.5f) / (contentHeight - viewHeight));
        scroll.verticalNormalizedPosition = 1f - scrollAmount; // 1 = top, 0 = bottom
    }


    // SEEN

    // Every frame the page is open: any NEW box the player can see counts as seen
    private void LateUpdate()
    {
        if (!IsOpen || ArtifactInventory.main == null) return;

        for (int i = 0; i < slots.Count; i++)
        {
            OwnedArtifact artifact = slotArtifacts[i];
            if (artifact == null || !artifact.isNew) continue;
            if (IsOnScreen((RectTransform)slots[i].transform)) ArtifactInventory.main.MarkSeen(artifact);
        }
    }

    // True when the middle of the box is inside the scroll view's visible area
    private bool IsOnScreen(RectTransform slot)
    {
        if (scroll == null) return true; // No scroll view = everything is always visible
        RectTransform viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;

        Vector3 middle = slot.TransformPoint(slot.rect.center);
        Vector3 inViewport = viewport.InverseTransformPoint(middle);
        return viewport.rect.Contains(inViewport);
    }


    // PLACING

    // Placed -> takes it out of its room
    // Unplaced -> puts it in the room the player is looking at
    private void PlaceOrRemove()
    {
        ArtifactInventory inventory = ArtifactInventory.main;
        if (selected == null || inventory == null) return;

        if (selected.IsPlaced)
        {
            inventory.Unplace(selected); // onChanged rebuilds the grid: the box lights back up, the button says Place in Room
            return;
        }

        if (RoomManager.main == null) return;
        int room = RoomManager.main.CurrentRoomIndex;

        if (inventory.IsRoomFull(room))
        {
            ToastPopup toast = ToastPopup.Find(ToastKind.RoomFull);
            if (toast != null) toast.Show();
            else Debug.LogWarning("Room is full, but there's no Room Full ToastPopup in the scene.", this);
            return;
        }

        inventory.PlaceInRoom(selected, room); // Fills the next empty circle; the box greys out and the button says Remove
    }

    private void OpenRoomPicker()
    {
        if (selected == null) return;
        if (roomPicker != null) roomPicker.OpenForArtifact(selected);
        else Debug.LogWarning("ArtifactCollectionPopup: no Room Picker set.", this);
    }

    // BUYING

    // The shop's options, in order
    private static readonly ArtifactRarity[] ShopRarities = { ArtifactRarity.Common, ArtifactRarity.Uncommon, ArtifactRarity.Rare };

    private void OpenShop()
    {
        if (shopPopup == null) { Debug.LogWarning("ArtifactCollectionPopup: no Shop Popup set.", this); return; }
        if (PriceList.main == null || ArtifactInventory.main == null) { Debug.LogWarning("ArtifactCollectionPopup: needs a PriceList and an ArtifactInventory to buy.", this); return; }

        ArtifactCatalog catalog = ArtifactInventory.main.Catalog;
        ShopTier[] tiers = new ShopTier[ShopRarities.Length];
        for (int i = 0; i < ShopRarities.Length; i++)
        {
            ArtifactRarity rarity = ShopRarities[i];
            tiers[i] = new ShopTier(rarity.ToString(), PriceList.main.ArtifactBuyPrice(rarity),
                                    catalog != null && catalog.HasAny(rarity));
        }

        shopPopup.Open(shopTitle, tiers, index => Buy(ShopRarities[index]));
    }

    // Called by the shop's Purchase button - true = bought (the shop closes)
    private bool Buy(ArtifactRarity rarity)
    {
        OwnedArtifact bought = ArtifactInventory.main.Buy(rarity);
        if (bought == null) return false;

        if (reveal != null && reveal.isActiveAndEnabled)
        {
            Close(); // The reveal opens the page again once it's done pulsing
            reveal.ShowPurchase(bought, () =>
            {
                Open(bought); // Selected, scrolled to, with its NEW tag
                GameObject slot = SlotFor(bought);
                return slot != null ? (RectTransform)slot.transform : null;
            });
        }
        else
        {
            Open(bought); // No animation set up - just show it selected
        }
        return true;
    }

    private static void SetText(Transform parent, string childName, string text)
    {
        Transform child = parent.Find(childName);
        TMP_Text label = child != null ? child.GetComponent<TMP_Text>() : null;
        if (label != null) label.text = text;
    }

    private void OpenSell()
    {
        if (selected == null || selected.IsPlaced) return; // Placed ones have to be removed first
        if (sellPopup != null) sellPopup.Open(selected);
        else Debug.LogWarning("ArtifactCollectionPopup: no Sell Popup set.", this);
    }

    // TESTING

    [ContextMenu("Testing: Open")]
    private void OpenForTesting() => Open();
}