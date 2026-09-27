using UnityEngine;
using TMPro;
using UnityEngine.Events;
using UnityEngine.UI;

/*
 * ONe tab along the top of the Furniture Owned panel that separates the furniture pieces by furniture type (Couches, Lapms, Shelves, etc.)
*/

public class FurnitureTabUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image background;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Color normalColor = new Color(0.82f, 0.76f, 0.64f);
    [SerializeField] private Color selectedColor = new Color(1f, 0.96f, 0.88f);

    public FurnitureCategory Category { get; private set; }

    public void Setup(FurnitureCategory category, UnityAction onClick)
    {
        Category = category;
        label.text = category.ToString();
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(onClick);
        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        background.color = selected ? selectedColor : normalColor;
        label.fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
    }
}