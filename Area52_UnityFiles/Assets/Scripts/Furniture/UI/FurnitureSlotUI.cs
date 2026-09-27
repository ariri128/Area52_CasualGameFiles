using UnityEngine;
using TMPro;
using UnityEngine.Events;
using UnityEngine.UI;

/*
 * One box in the Furniture Owned panel grid
 * Includes icon, name, how many are free, and a highilghted border when selected
 * The first box in every tab is the "Nothing Selected" box
*/

public class FurnitureSlotUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text countText;
    [SerializeField] private GameObject selectedBorder;

    [Tooltip("How see-through a slot is when every copy of it is already in a room.")]
    [SerializeField, Range(0f, 1f)] private float unavailableAlpha = 0.4f;

    private CanvasGroup canvasGroup;

    // Sets the piece this slot stands for
    public FurnitureDefinition Definition { get; private set; }

    public void Setup(FurnitureDefinition definition, Sprite icon, string label, string count, bool available, UnityAction onClick)
    {
        Definition = definition;

        iconImage.sprite = icon;
        iconImage.enabled = icon != null;
        iconImage.preserveAspect = true;

        nameText.text = label;

        countText.text = count;
        countText.gameObject.SetActive(!string.IsNullOrEmpty(count));

        // A CanvasGroup fades everything in the slot at once
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = available ? 1f : unavailableAlpha;

        button.interactable = available;
        button.onClick.RemoveAllListeners();
        if (onClick != null) button.onClick.AddListener(onClick);

        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        selectedBorder.SetActive(selected);
    }
}