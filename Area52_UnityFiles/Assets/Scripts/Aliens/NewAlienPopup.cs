using UnityEngine;
using System.Collections;
using TMPro;
using UnityEngine.UI;

/*
 * Shows an alien the player just bought: a big copy of its box grows in the middle of the screen, with Place in Room under it
 *   Place in Room - opens the Room Picker; once it's placed, the player is taken to that room (the picker does that)
 *   Closing the Room Picker without placing it brings this back, so a bought alien can't be lost
 * There's no close button on purpose: the alien has been paid for, so it has to go somewhere
*/

public class NewAlienPopup : MonoBehaviour
{
    [Header("Parts")]
    [Tooltip("The child that's shown and hidden (dark background + the panel).")]
    [SerializeField] private GameObject root;
    [Tooltip("Everything that grows in: the big alien box and the Place in Room button.")]
    [SerializeField] private RectTransform panel;
    [Tooltip("The alien's picture in the big box.")]
    [SerializeField] private Image picture;
    [Tooltip("Optional. The alien's name in the big box.")]
    [SerializeField] private TMP_Text nameText;
    [Tooltip("Optional. Shows its rarity.")]
    [SerializeField] private TMP_Text rarityText;
    [SerializeField] private Button placeButton;
    [Tooltip("The Room Picker in this scene.")]
    [SerializeField] private RoomPickerPopup roomPicker;

    [Header("Text")]
    [Tooltip("{0} = Common / Rare / Ultra Rare.")]
    [SerializeField] private string rarityFormat = "{0}";

    [Header("Timing (seconds)")]
    [SerializeField] private float growSeconds = 0.35f;

    private AlienSpecies pending; // Bought but not placed yet

    public bool IsOpen => root != null && root.activeSelf;

    private void Awake()
    {
        placeButton.onClick.AddListener(PlaceInRoom);
        root.SetActive(false);
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        if (panel != null) panel.localScale = Vector3.one; // Never left stuck half-grown
    }

    // Called after paying
    public void Open(AlienSpecies species)
    {
        if (species == null) return;
        pending = species;
        Show();
    }

    private void Show()
    {
        if (AlienTapper.main != null) AlienTapper.main.Deselect(); // No thought bubble over the top

        picture.sprite = pending.sprite;
        picture.preserveAspect = true;
        if (nameText != null) nameText.text = pending.displayName;
        if (rarityText != null) rarityText.text = string.Format(rarityFormat, AlienSpecies.RarityName(pending.OverallRarity));

        transform.SetAsLastSibling(); // In front of everything
        root.SetActive(true);

        StopAllCoroutines();
        StartCoroutine(Grow());
    }

    private void PlaceInRoom()
    {
        if (pending == null) return;
        if (roomPicker == null) { Debug.LogWarning("NewAlienPopup: no Room Picker set.", this); return; }

        root.SetActive(false); // Out of the way while picking - comes back if the picker is closed
        roomPicker.OpenForNewAlien(pending, OnPlaced, OnCancelled);
    }

    // It's in a room now (the picker has already added it and moved the camera)
    private void OnPlaced(OwnedAlien alien)
    {
        pending = null;
        root.SetActive(false);
    }

    // The picker was closed without placing it
    private void OnCancelled()
    {
        if (pending != null) Show();
    }


    // THE ANIMATION

    private IEnumerator Grow()
    {
        placeButton.interactable = false;
        panel.localScale = Vector3.zero;

        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / growSeconds)
        {
            panel.localScale = Vector3.one * EaseOutBack(t);
            yield return null;
        }
        panel.localScale = Vector3.one;
        placeButton.interactable = true;
    }

    // Goes a bit past 1 then settles back - the "pop"
    private static float EaseOutBack(float t)
    {
        const float c = 1.7f;
        float u = Mathf.Clamp01(t) - 1f;
        return 1f + (c + 1f) * u * u * u + c * u * u;
    }
}