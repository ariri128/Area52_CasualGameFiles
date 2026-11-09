using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

/*
 * The UI for the "Furniture Owned" panel, used two ways:
 *   OpenForReplace(current) - pieces that could replace the tapped one; the button says Replace
 *   OpenForAdd()            - floor pieces and rugs to add to the room; the button says Add to Room
 *                             (decor isn't listed: it has to sit on something, so it only goes in by replacing decor)
 * Includes the title, exit (X) button, a tab per furniture type, a scrolling grid of owned pieces,
 * the Replace / Add to Room button (greyed out until a piece is selected) and Buy Furniture (opens the Furniture Catalog)
 * The catalog counts as part of this panel to the Furniture Editor: hiding this hides it too
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
    [Tooltip("Optional. The text on the confirm button - says Replace or Add to Room.")]
    [SerializeField] private TMP_Text confirmLabel;
    [SerializeField] private string replaceText = "Replace";
    [SerializeField] private string addText = "Add to Room";

    [Header("Buying")]
    [SerializeField] private Button buyButton;
    [SerializeField] private FurnitureCatalogPanel catalogPanel;

    private bool adding;                    // True = Add mode, false = Replace mode
    private FurnitureDefinition replacing;  // The tapped piece's type (Replace mode only)
    private FurnitureDefinition selected;
    private FurnitureCategory currentCategory;
    private readonly List<FurnitureTabUI> tabs = new List<FurnitureTabUI>();
    private readonly List<FurnitureSlotUI> slots = new List<FurnitureSlotUI>();

    public bool IsOpen => root.activeSelf || (catalogPanel != null && catalogPanel.IsOpen);

    private void Awake()
    {
        closeButton.onClick.AddListener(Close);
        confirmButton.onClick.AddListener(Confirm);
        confirmButton.gameObject.SetActive(true); // Always there - greyed out until something's selected
        if (buyButton != null) buyButton.onClick.AddListener(OpenCatalog);

        // The templates are only for copying
        tabTemplate.gameObject.SetActive(false);
        slotTemplate.gameObject.SetActive(false);

        root.SetActive(false);
    }


    // OPEN / CLOSE

    // Opens the panel to pick something to replace a piece of type "current"
    public void OpenForReplace(FurnitureDefinition current)
    {
        adding = false;
        replacing = current;
        Open(current.category);
    }

    // Opens the panel to pick a floor piece or rug to add to the room
    public void OpenForAdd()
    {
        adding = true;
        replacing = null;
        Open(currentCategory);
    }

    private void Open(FurnitureCategory startTab)
    {
        selected = null;
        if (confirmLabel != null) confirmLabel.text = adding ? addText : replaceText;
        root.SetActive(true);

        List<FurnitureCategory> categories = BuildTabs();
        ShowTab(categories.Contains(startTab) ? startTab : categories[0]);
    }

    // If the player clicks the X button to close it
    public void Close()
    {
        Hide();
        if (FurnitureEditor.main != null) FurnitureEditor.main.OnPanelClosed();
    }

    // If it's closed by the editor after a successful replace / add
    public void Hide()
    {
        root.SetActive(false);
        if (catalogPanel != null && catalogPanel.IsOpen) catalogPanel.Hide();
    }

    private void Confirm()
    {
        if (selected == null || FurnitureEditor.main == null) return;
        if (adding) FurnitureEditor.main.OnPanelAddConfirmed(selected);
        else FurnitureEditor.main.OnPanelConfirmed(selected);
    }


    // BUILDING THE TABS AND GRID

    // Replace mode: owned pieces that could replace the tapped one
    // Add mode: owned floor pieces and rugs
    private List<FurnitureDefinition> GetChoices()
    {
        List<FurnitureDefinition> choices = new List<FurnitureDefinition>();
        if (FurnitureInventory.main == null) return choices;

        foreach (FurnitureDefinition definition in FurnitureInventory.main.GetOwnedDefinitions())
        {
            if (adding)
            {
                if (definition.IsDecor) continue; // Decor needs something to sit on
            }
            else
            {
                if (definition == replacing) continue; // Has swapping for the same thing do nothing
                if (!definition.CanReplace(replacing)) continue; // Decor swaps for decor, floor pieces swap for floor pieces
            }
            choices.Add(definition);
        }
        return choices;
    }

    // One tab per category that has something in it (plus the tapped piece's own category, when replacing)
    private List<FurnitureCategory> BuildTabs()
    {
        foreach (FurnitureTabUI tab in tabs) Destroy(tab.gameObject);
        tabs.Clear();

        List<FurnitureCategory> categories = new List<FurnitureCategory>();
        if (!adding && replacing != null) categories.Add(replacing.category);
        foreach (FurnitureDefinition definition in GetChoices())
        {
            if (!categories.Contains(definition.category)) categories.Add(definition.category);
        }
        if (categories.Count == 0) categories.Add(FurnitureCategory.Couches); // Nothing owned to add - still needs one tab
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
        currentCategory = category;
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

    // Highlights the chosen box - the confirm button only works once a real piece is chosen
    private void Select(FurnitureDefinition definition)
    {
        selected = definition;
        foreach (FurnitureSlotUI slot in slots) slot.SetSelected(slot.Definition == definition);
        confirmButton.interactable = definition != null;
    }


    // THE FURNITURE CATALOG

    private void OpenCatalog()
    {
        if (catalogPanel == null) { Debug.LogWarning("FurnitureOwnedPanel: no Catalog Panel set.", this); return; }
        root.SetActive(false); // Just this panel - not Hide(), which would close the catalog too
        catalogPanel.Open(currentCategory);
    }

    // Back from the catalog: bought = what was just bought, or null for Cancel
    public void ReturnFromCatalog(FurnitureDefinition bought)
    {
        root.SetActive(true);
        List<FurnitureCategory> categories = BuildTabs(); // Rebuilt - the new piece may need a new tab

        // Only selected if it fits this mode (can replace the tapped piece / can be added)
        bool canPick = bought != null && GetChoices().Contains(bought)
                       && FurnitureInventory.main != null && FurnitureInventory.main.GetAvailableCount(bought) > 0;

        FurnitureCategory tab = canPick ? bought.category
                              : (categories.Contains(currentCategory) ? currentCategory : categories[0]);
        ShowTab(tab);
        if (canPick) Select(bought);
    }
}