using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

/*
 * The popup up that shows when the player is deciding which room to put the alien egg in
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

    [Header("Text")]
    [Tooltip("{0} is the room number.")]
    [SerializeField] private string roomNameFormat = "Room {0}";
    [Tooltip("{0} is how many are in the room, {1} is the most it holds.")]
    [SerializeField] private string countFormat = "{0}/{1}";
    [SerializeField] private string fullText = "Full";

    private readonly List<GameObject> slots = new List<GameObject>();
    private int selectedRoom = -1;

    public bool IsOpen => root != null && root.activeSelf;

    private void Awake()
    {
        closeButton.onClick.AddListener(Close);
        confirmButton.onClick.AddListener(Confirm);
        slotTemplate.gameObject.SetActive(false);
        root.SetActive(false);
    }

    private void OnEnable()
    {
        if (BreedingManager.main != null) BreedingManager.main.onChanged.AddListener(RefreshIfOpen);
    }

    private void OnDisable()
    {
        if (BreedingManager.main != null) BreedingManager.main.onChanged.RemoveListener(RefreshIfOpen);
    }

    public void Open()
    {
        selectedRoom = -1;
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

        BreedingManager breeding = BreedingManager.main;
        if (breeding == null) return;

        // A selected room that has filled up since is unselected
        if (selectedRoom >= 0 && breeding.IsRoomFull(selectedRoom)) selectedRoom = -1;

        for (int i = 0; i < breeding.AlienRooms.Count; i++)
        {
            int roomIndex = breeding.AlienRooms[i];
            bool full = breeding.IsRoomFull(roomIndex);

            Button slot = Instantiate(slotTemplate, slotContainer);
            slot.gameObject.SetActive(true);
            slot.name = $"Room_{i + 1}";
            slot.interactable = !full;
            slot.onClick.AddListener(() => Select(roomIndex));

            SetText(slot.transform, "Name", string.Format(roomNameFormat, i + 1));
            SetText(slot.transform, "Count", full
                ? fullText
                : string.Format(countFormat, breeding.Occupancy(roomIndex), breeding.MaxPerRoom));

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
        if (selectedRoom < 0 || BreedingManager.main == null) return;
        if (BreedingManager.main.PlaceEgg(selectedRoom)) Close();
    }

    private static void SetText(Transform parent, string childName, string text)
    {
        Transform child = parent.Find(childName);
        TMP_Text label = child != null ? child.GetComponent<TMP_Text>() : null;
        if (label != null) label.text = text;
    }
}