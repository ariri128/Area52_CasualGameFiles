using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

/*
 * The "Furniture Catalog": looks just like Furniture Owned, but lists every piece of furniture there is, with its price
 * Opened by Buy Furniture on the Furniture Owned panel
 *   Pieces the player can't afford are greyed out; "Owned x2" shows on pieces they already have
 *   Purchase - greyed out until a piece is picked; buys it and goes back to Furniture Owned with it selected (if it fits)
 *   Cancel (and the X) - back to Furniture Owned
*/

public class FurnitureCatalogPanel : MonoBehaviour
{
    [Tooltip("The part that's shown and hidden (dark background + window).")]
    [SerializeField] private GameObject root;
    [Tooltip("Optional. Works the same as Cancel.")]
    [SerializeField] private Button closeButton;

    [Header("Tabs")]
    [SerializeField] private RectTransform tabContainer;
    [SerializeField] private FurnitureTabUI tabTemplate;

    [Header("Grid")]
    [SerializeField] private ScrollRect gridScroll;
    [SerializeField] private RectTransform gridContainer;
    [SerializeField] private FurnitureSlotUI slotTemplate;

    [Header("Buttons")]
    [SerializeField] private Button purchaseButton;
    [SerializeField] private Button cancelButton;

    [Header("Data")]
    [Tooltip("Every piece of furniture there is.")]
    [SerializeField] private FurnitureCatalog catalog;
    [Tooltip("The Furniture Owned panel - where Cancel and Purchase go back to.")]
    [SerializeField] private FurnitureOwnedPanel ownedPanel;

    [Header("Text")]
    [Tooltip("Shown on pieces the player already owns. {0} = how many.")]
    [SerializeField] private string ownedFormat = "Owned x{0}";

    private FurnitureDefinition selected;
    private FurnitureCategory currentCategory;
    private readonly List<FurnitureTabUI> tabs = new List<FurnitureTabUI>();
    private readonly List<FurnitureSlotUI> slots = new List<FurnitureSlotUI>();
    private bool listening;

    public bool IsOpen => root.activeSelf;

    private void Awake()
    {
        purchaseButton.onClick.AddListener(Purchase);
        cancelButton.onClick.AddListener(Cancel);
        if (closeButton != null) closeButton.onClick.AddListener(Cancel);

        tabTemplate.gameObject.SetActive(false);
        slotTemplate.gameObject.SetActive(false);
        root.SetActive(false);
    }

    // Pieces grey out or light up as coins change
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


    // OPEN / CLOSE

    // Opens on the given tab if there's anything in it, otherwise the first tab
    public void Open(FurnitureCategory startTab)
    {
        if (catalog == null) { Debug.LogWarning("FurnitureCatalogPanel: no Catalog set.", this); return; }

        selected = null;
        root.SetActive(true);

        List<FurnitureCategory> categories = BuildTabs();
        if (categories.Count == 0) { Debug.LogWarning("The Furniture Catalog is empty - fill it in first.", catalog); return; }

        ShowTab(categories.Contains(startTab) ? startTab : categories[0], true);
    }

    public void Hide()
    {
        root.SetActive(false);
        selected = null;
    }

    private void Cancel()
    {
        Hide();
        if (ownedPanel != null) ownedPanel.ReturnFromCatalog(null);
    }

    private void Purchase()
    {
        if (selected == null || FurnitureInventory.main == null) return;

        FurnitureDefinition bought = selected;
        if (!FurnitureInventory.main.Buy(bought)) return; // Not enough coins - nothing happens

        Hide();
        if (ownedPanel != null) ownedPanel.ReturnFromCatalog(bought);
    }

    private void RefreshIfOpen()
    {
        if (IsOpen) ShowTab(currentCategory, false);
    }


    // TABS AND GRID

    // One tab per category that has at least one piece
    private List<FurnitureCategory> BuildTabs()
    {
        foreach (FurnitureTabUI tab in tabs) Destroy(tab.gameObject);
        tabs.Clear();

        List<FurnitureCategory> categories = new List<FurnitureCategory>();
        foreach (FurnitureDefinition definition in catalog.furniture)
            if (definition != null && !categories.Contains(definition.category)) categories.Add(definition.category);
        categories.Sort();

        foreach (FurnitureCategory category in categories)
        {
            FurnitureTabUI tab = Instantiate(tabTemplate, tabContainer);
            tab.gameObject.SetActive(true);
            FurnitureCategory thisCategory = category;
            tab.Setup(category, () => ShowTab(thisCategory, true));
            tabs.Add(tab);
        }
        return categories;
    }

    // scrollToTop: true when switching tabs, false when just refreshing prices
    private void ShowTab(FurnitureCategory category, bool scrollToTop)
    {
        currentCategory = category;
        foreach (FurnitureTabUI tab in tabs) tab.SetSelected(tab.Category == category);

        foreach (FurnitureSlotUI slot in slots)
        {
            slot.gameObject.SetActive(false); // Destroy only happens at the end of the frame
            Destroy(slot.gameObject);
        }
        slots.Clear();

        FurnitureInventory inventory = FurnitureInventory.main;
        bool keepSelection = false;

        foreach (FurnitureDefinition definition in catalog.furniture)
        {
            if (definition == null || definition.category != category) continue;

            bool affordable = CoinBank.main != null && CoinBank.main.CanAfford(definition.price);
            int owned = inventory != null ? inventory.GetOwnedCount(definition) : 0;
            string count = owned > 0 ? string.Format(ownedFormat, owned) : null;

            FurnitureSlotUI slot = Instantiate(slotTemplate, gridContainer);
            slot.gameObject.SetActive(true);
            FurnitureDefinition thisDefinition = definition;
            slot.Setup(definition, definition.icon, definition.displayName, count, affordable, () => Select(thisDefinition));
            slot.SetPrice(definition.price);
            slots.Add(slot);

            if (definition == selected && affordable) keepSelection = true;
        }

        Select(keepSelection ? selected : null); // Dropped if it's on another tab or can't be afforded anymore
        if (scrollToTop) gridScroll.verticalNormalizedPosition = 1f;
    }

    private void Select(FurnitureDefinition definition)
    {
        selected = definition;
        foreach (FurnitureSlotUI slot in slots) slot.SetSelected(slot.Definition == definition);
        purchaseButton.interactable = definition != null;
    }
}