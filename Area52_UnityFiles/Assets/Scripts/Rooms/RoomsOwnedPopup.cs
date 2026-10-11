using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

/*
 * "Rooms Owned": opened by the camera button
 * Lists every room as Room 1, Room 2... with how many aliens (and eggs) are in it out of the maximum
 * Tapping a room takes the player there
 * "Buy New Room" opens the room shop
*/

public class RoomsOwnedPopup : MonoBehaviour
{
    public static RoomsOwnedPopup main { get; private set; }

    [Header("Parts")]
    [SerializeField] private GameObject root;
    [SerializeField] private Button closeButton;
    [Tooltip("Where the room boxes are lined up (the grid's Content).")]
    [SerializeField] private Transform slotContainer;
    [SerializeField] private Button slotTemplate;
    [SerializeField] private Button buyNewRoomButton;
    [Tooltip("The Rooms to Buy popup.")]
    [SerializeField] private RoomShopPopup shopPopup;

    [Header("Behaviour")]
    [Tooltip("Tapping a room closes the popup and goes to that room.")]
    [SerializeField] private bool tapToGoToRoom = true;

    [Header("Text")]
    [SerializeField] private string roomNameFormat = "Room {0}";
    [SerializeField] private string countFormat = "{0}/{1}";

    private readonly List<GameObject> slots = new List<GameObject>();

    public bool IsOpen => root != null && root.activeSelf;

    private void Awake()
    {
        main = this;
        closeButton.onClick.AddListener(Close);
        buyNewRoomButton.onClick.AddListener(OpenShop);
        slotTemplate.gameObject.SetActive(false);
        root.SetActive(false);
    }

    private void OnDestroy()
    {
        if (main == this) main = null;
    }

    public void Open()
    {
        root.SetActive(true);
        Rebuild();
    }

    public void Close()
    {
        root.SetActive(false);
    }

    // Also called by the shop after a purchase, so the new room shows up
    public void Rebuild()
    {
        foreach (GameObject slot in slots) Destroy(slot);
        slots.Clear();

        if (RoomManager.main == null) return;
        BreedingManager breeding = BreedingManager.main;

        for (int i = 0; i < RoomManager.main.RoomCount; i++)
        {
            int roomIndex = i;
            Button slot = Instantiate(slotTemplate, slotContainer);
            slot.gameObject.SetActive(true);
            slot.name = $"Room_{i + 1}";
            slot.onClick.AddListener(() => GoToRoom(roomIndex));

            SetText(slot.transform, "Name", string.Format(roomNameFormat, i + 1));
            if (breeding != null)
                SetText(slot.transform, "Count", string.Format(countFormat, breeding.Occupancy(i), breeding.MaxPerRoom));

            slots.Add(slot.gameObject);
        }
    }

    private void GoToRoom(int roomIndex)
    {
        if (!tapToGoToRoom || RoomManager.main == null) return;
        Close();
        RoomManager.main.GoToRoom(roomIndex);
    }

    private void OpenShop()
    {
        if (shopPopup != null) shopPopup.Open();
    }

    private static void SetText(Transform parent, string childName, string text)
    {
        Transform child = parent.Find(childName);
        TMP_Text label = child != null ? child.GetComponent<TMP_Text>() : null;
        if (label != null) label.text = text;
    }
}