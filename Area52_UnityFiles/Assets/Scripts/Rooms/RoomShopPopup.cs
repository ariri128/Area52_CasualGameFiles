using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

/*
 * "Rooms to Buy": opened from Rooms Owned
 * Shows every room theme from RoomShop with its picture, name and cost
 * Themes the player can't afford are greyed out
 * Tap one to select it, then Purchase Room buys it
 * The room appears at the end of the swipe row, and Rooms Owned lists it
*/

public class RoomShopPopup : MonoBehaviour
{
    [Header("Parts")]
    [SerializeField] private GameObject root;
    [SerializeField] private Button closeButton;
    [SerializeField] private Transform slotContainer;
    [SerializeField] private Button slotTemplate;
    [SerializeField] private Button purchaseButton;
    [Tooltip("Rooms Owned, refreshed after a purchase.")]
    [SerializeField] private RoomsOwnedPopup roomsOwnedPopup;

    private readonly List<GameObject> slots = new List<GameObject>();
    private RoomShop.Offer selected;
    private bool listening;

    public bool IsOpen => root != null && root.activeSelf;

    private void Awake()
    {
        closeButton.onClick.AddListener(Close);
        purchaseButton.onClick.AddListener(Purchase);
        slotTemplate.gameObject.SetActive(false);
        root.SetActive(false);
    }

    // Prices grey out or light up as coins change
    private void OnEnable() => StartListening();
    private void Start() => StartListening();

    private void StartListening()
    {
        if (listening || CoinBank.main == null) return;
        CoinBank.main.onChanged.AddListener(RefreshIfOpen);
        listening = true;
    }

    private void OnDisable()
    {
        if (listening && CoinBank.main != null) CoinBank.main.onChanged.RemoveListener(RefreshIfOpen);
        listening = false;
    }

    public void Open()
    {
        selected = null;
        root.SetActive(true);
        Rebuild();
    }

    public void Close()
    {
        root.SetActive(false);
    }

    private void RefreshIfOpen()
    {
        if (IsOpen) Rebuild();
    }

    private void Rebuild()
    {
        foreach (GameObject slot in slots) Destroy(slot);
        slots.Clear();

        if (RoomShop.main == null) return;

        // A selection the player can no longer afford is dropped
        if (selected != null && !RoomShop.main.CanAfford(selected)) selected = null;

        foreach (RoomShop.Offer offer in RoomShop.main.Offers)
        {
            RoomShop.Offer thisOffer = offer;
            Button slot = Instantiate(slotTemplate, slotContainer);
            slot.gameObject.SetActive(true);
            slot.name = $"Offer_{offer.displayName}";
            slot.interactable = RoomShop.main.CanAfford(offer); // Greyed out if too expensive
            slot.onClick.AddListener(() => Select(thisOffer));

            Transform preview = slot.transform.Find("Preview");
            Image previewImage = preview != null ? preview.GetComponent<Image>() : null;
            if (previewImage != null)
            {
                previewImage.sprite = offer.preview;
                previewImage.enabled = offer.preview != null;
            }

            SetText(slot.transform, "Name", offer.displayName);
            SetText(slot.transform, "Cost", offer.cost.ToString("N0"));

            Transform border = slot.transform.Find("SelectedBorder");
            if (border != null) border.gameObject.SetActive(offer == selected);

            slots.Add(slot.gameObject);
        }

        purchaseButton.interactable = selected != null;
    }

    private void Select(RoomShop.Offer offer)
    {
        selected = offer;
        Rebuild();
    }

    private void Purchase()
    {
        if (selected == null || RoomShop.main == null) return;

        if (RoomShop.main.Buy(selected) >= 0)
        {
            Close();
            if (roomsOwnedPopup != null && roomsOwnedPopup.IsOpen) roomsOwnedPopup.Rebuild();
        }
    }

    private static void SetText(Transform parent, string childName, string text)
    {
        Transform child = parent.Find(childName);
        TMP_Text label = child != null ? child.GetComponent<TMP_Text>() : null;
        if (label != null) label.text = text;
    }
}