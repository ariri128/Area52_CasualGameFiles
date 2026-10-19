using UnityEngine;
using TMPro;
using UnityEngine.UI;

/*
 * The UI for the chest under the settings button
 * Shows how full the current room's chest is (empty, 1/3, 2/3, full) and the number of coins in it underneath ("FULL" at the cap)
*/

[RequireComponent(typeof(Button))]
public class ChestButton : MonoBehaviour
{
    [SerializeField] private Image chestImage;

    [Tooltip("Empty, 1/3, 2/3, full - in that order.")]
    [SerializeField] private Sprite[] fillSprites = new Sprite[4];

    [Tooltip("How full the chest must be to show the 1/3, 2/3 and full sprites (0 to 1). " +
             "Set the first to 0.001 if you'd like the 1/3 sprite as soon as any coins drop.")]
    [SerializeField] private float[] thresholds = { 1f / 3f, 2f / 3f, 1f };

    [Header("Amount")]
    [Tooltip("Text under the chest showing the coins in this room's chest.")]
    [SerializeField] private TMP_Text amountText;
    [Tooltip("Shown instead of the number once the chest reaches its cap.")]
    [SerializeField] private string fullText = "FULL";

    private void Awake()
    {
        if (chestImage == null) chestImage = GetComponent<Image>();
        GetComponent<Button>().onClick.AddListener(OpenPopup);
    }

    private bool listening;

    // Tries in both OnEnable and Start: when the game starts, CoinBank may not be set up yet the first time this runs
    private void OnEnable() => StartListening();
    private void Start() => StartListening();

    private void StartListening()
    {
        if (!listening && CoinBank.main != null && RoomManager.main != null)
        {
            CoinBank.main.onChanged.AddListener(Refresh);
            RoomManager.main.onRoomChanged.AddListener(OnRoomChanged);
            listening = true;
        }
        Refresh();
    }

    private void OnDisable()
    {
        if (!listening) return;
        if (CoinBank.main != null) CoinBank.main.onChanged.RemoveListener(Refresh);
        if (RoomManager.main != null) RoomManager.main.onRoomChanged.RemoveListener(OnRoomChanged);
        listening = false;
    }

    private void OnRoomChanged(int roomIndex) => Refresh();

    private int CurrentRoom => RoomManager.main != null ? RoomManager.main.CurrentRoomIndex : 0;

    private void Refresh()
    {
        if (CoinBank.main == null) return;

        int amount = CoinBank.main.ChestAmount(CurrentRoom);

        // The number under the chest
        if (amountText != null)
            amountText.text = amount >= CoinBank.main.ChestCapacity ? fullText : amount.ToString("N0"); // 1250 -> 1,250

        // The chest picture
        if (chestImage == null || fillSprites.Length == 0) return;

        float fill = CoinBank.main.ChestFill(CurrentRoom);

        // Count how many marks the chest has reached: 0 = empty sprite, 3 = full sprite
        int level = 0;
        foreach (float mark in thresholds)
            if (fill >= mark - 0.0001f) level++;

        level = Mathf.Clamp(level, 0, fillSprites.Length - 1);
        if (fillSprites[level] != null) chestImage.sprite = fillSprites[level];
    }

    private void OpenPopup()
    {
        if (CoinPopup.main != null) CoinPopup.main.Open(CurrentRoom);
        else Debug.LogWarning("ChestButton: no CoinPopup in the scene.", this);
    }
}