using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

/*
 * The UI for the "Furniture Owned" panel
 * Includes the title, exit (X) button, a tab per furniture type, a scrolling grid of owned pieces, and a confirm button at the bottom that only shows once a piece is selected
*/

public class FurnitureOwnedPanel : MonoBehaviour
{
    [Tooltip("The part that's shown and hidden (dark background + window).")]
    [SerializeField] private GameObject root;
    [SerializeField] private Button closeButton;

    [Header("Tabs")]
    [Tooltip("Content object of the tabs scroll view (has a Horizontal Layout Group).")]
    [SerializeField] private RectTransform tabContainer;
    [SerializeField] private FurnitureTabUI tabTemplate;

    [Header("Grid")]
    [SerializeField] private ScrollRect gridScroll;
    [Tooltip("Content object of the grid scroll view (has a Grid Layout Group).")]
    [SerializeField] private RectTransform gridContainer;
    [SerializeField] private FurnitureSlotUI slotTemplate;
    [SerializeField] private Sprite nothingSelectedIcon;

    [Header("Confirm")]
    [SerializeField] private Button confirmButton;

    private FurnitureDefinition replacing;
    private FurnitureDefinition selected;
    private readonly List<FurnitureTabUI> tabs = new List<FurnitureTabUI>();
    private readonly List<FurnitureSlotUI> slots = new List<FurnitureSlotUI>();

    public bool IsOpen => root.activeSelf;

    private void Awake()
    {
        closeButton.onClick.AddListener(Close);
        confirmButton.onClick.AddListener(Confirm);

        // The templates are only for copying
        tabTemplate.gameObject.SetActive(false);
        slotTemplate.gameObject.SetActive(false);

        root.SetActive(false);
    }


    // OPEN / CLOSE

    // Opens the panel to pick something to replace a piece of type "current"
    public void OpenForReplace(FurnitureDefinition current)
    {
        replacing = current;
        selected = null;
        root.SetActive(true);

        List<FurnitureCategory> categories = BuildTabs();
        ShowTab(categories.Contains(current.category) ? current.category : categories[0]);
    }

    // If the player clicks the X button to close it
    public void Close()
    {
        Hide();
        if (FurnitureEditor.main != null) FurnitureEditor.main.OnPanelClosed();
    }

    // If it's closed by the editor after a successful furniture replacement
    public void Hide()
    {
        root.SetActive(false);
    }

    private void Confirm()
    {
        if (selected != null && FurnitureEditor.main != null) FurnitureEditor.main.OnPanelConfirmed(selected);
    }


    // BUILDING THE TABS AND GRID

    // Owned pieces that could replace the current one
    private List<FurnitureDefinition> GetChoices()
    {
        List<FurnitureDefinition> choices = new List<FurnitureDefinition>();
        if (FurnitureInventory.main == null) return choices;

        foreach (FurnitureDefinition definition in FurnitureInventory.main.GetOwnedDefinitions())
        {
            if (definition == replacing) continue; // Has swapping for the same thing do nothing
            if (!definition.CanReplace(replacing)) continue; // Defines that decor swaps for decor, floor pieces swap for floor pieces
            choices.Add(definition);
        }
        return choices;
    }

    // Sets one tab per category that has something in it plus the current piece's own category
    private List<FurnitureCategory> BuildTabs()
    {
        foreach (FurnitureTabUI tab in tabs) Destroy(tab.gameObject);
        tabs.Clear();

        List<FurnitureCategory> categories = new List<FurnitureCategory> { replacing.category };
        foreach (FurnitureDefinition definition in GetChoices())
        {
            if (!categories.Contains(definition.category)) categories.Add(definition.category);
        }
        categories.Sort();

        foreach (FurnitureCategory category in categories)
        {
            FurnitureTabUI tab = Instantiate(tabTemplate, tabContainer);
            tab.gameObject.SetActive(true);
            FurnitureCategory thisCategory = category;
            tab.Setup(category, () => ShowTab(thisCategory));
            tabs.Add(tab);
        }
        return categories;
    }

    private void ShowTab(FurnitureCategory category)
    {
        foreach (FurnitureTabUI tab in tabs) tab.SetSelected(tab.Category == category);

        foreach (FurnitureSlotUI slot in slots)
        {
            slot.gameObject.SetActive(false); // Destroy only happens at the end of the frame
            Destroy(slot.gameObject);
        }
        slots.Clear();

        // First box: Nothing Selected
        AddSlot(null, nothingSelectedIcon, "Nothing Selected", null, true);

        foreach (FurnitureDefinition definition in GetChoices())
        {
            if (definition.category != category) continue;

            int available = FurnitureInventory.main.GetAvailableCount(definition);
            string count = available == 0 ? "In use" : (available > 1 ? $"x{available}" : null);
            AddSlot(definition, definition.icon, definition.displayName, count, available > 0);
        }

        Select(null);
        gridScroll.verticalNormalizedPosition = 1f;
    }

    private void AddSlot(FurnitureDefinition definition, Sprite icon, string label, string count, bool available)
    {
        FurnitureSlotUI slot = Instantiate(slotTemplate, gridContainer);
        slot.gameObject.SetActive(true);
        slot.Setup(definition, icon, label, count, available, () => Select(definition));
        slots.Add(slot);
    }

    // Highlights the chosen box - the confirm button only shows when a real piece is chosen
    private void Select(FurnitureDefinition definition)
    {
        selected = definition;
        foreach (FurnitureSlotUI slot in slots) slot.SetSelected(slot.Definition == definition);
        confirmButton.gameObject.SetActive(definition != null);
    }
}