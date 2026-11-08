using UnityEngine;
using System;
using TMPro;
using UnityEngine.UI;

/*
 * The "buy a random one" popup, shared by the Artifacts page and (later) the Alien Files:
 *   up to 3 options (e.g. Common 100 / Uncommon 200 / Rare 300), each greyed out if the player can't afford it
 *   Purchase - greyed out until an option is picked; asks whoever opened the popup to do the buying
 *   Cancel   - closes it, back to the page underneath
 * It doesn't spend anything itself: the page that opened it does, so that page decides what the coins buy
*/

public struct ShopTier
{
    public string name;
    public int price;
    public bool inStock; // False = there's nothing to give for this option (e.g. no Rare artifacts made yet)

    public ShopTier(string name, int price, bool inStock = true)
    {
        this.name = name;
        this.price = price;
        this.inStock = inStock;
    }
}

public class TierShopPopup : MonoBehaviour
{
    [Header("Parts")]
    [Tooltip("The child that's shown and hidden (dark background + window).")]
    [SerializeField] private GameObject root;
    [Tooltip("Optional. The popup's title text.")]
    [SerializeField] private TMP_Text titleText;
    [Tooltip("One button per option, top to bottom. Each needs children named Name, Price and SelectedBorder.")]
    [SerializeField] private Button[] optionButtons = new Button[3];
    [SerializeField] private Button purchaseButton;
    [SerializeField] private Button cancelButton;

    [Header("Look")]
    [Tooltip("How see-through an option is when the player can't buy it.")]
    [SerializeField, Range(0f, 1f)] private float unavailableAlpha = 0.45f;

    private ShopTier[] tiers = new ShopTier[0];
    private Func<int, bool> onPurchase; // Gets the picked option's number; returns true if it was bought
    private int selected = -1;
    private bool listening;

    public bool IsOpen => root != null && root.activeSelf;

    private void Awake()
    {
        for (int i = 0; i < optionButtons.Length; i++)
        {
            if (optionButtons[i] == null) continue;
            int index = i;
            optionButtons[i].onClick.AddListener(() => Select(index));
        }

        purchaseButton.onClick.AddListener(Purchase);
        cancelButton.onClick.AddListener(Close);
        root.SetActive(false);
    }

    // Options grey out or light up as coins change (e.g. a chest is collected)
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

    public void Open(string title, ShopTier[] options, Func<int, bool> purchase)
    {
        tiers = options ?? new ShopTier[0];
        onPurchase = purchase;
        selected = -1;

        if (titleText != null) titleText.text = title;
        transform.SetAsLastSibling(); // In front of the page that opened it
        root.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        root.SetActive(false);
        onPurchase = null;
        selected = -1;
    }

    private void RefreshIfOpen()
    {
        if (IsOpen) Refresh();
    }


    // OPTIONS

    private bool CanBuy(int index)
    {
        if (index < 0 || index >= tiers.Length) return false;
        return tiers[index].inStock && CoinBank.main != null && CoinBank.main.CanAfford(tiers[index].price);
    }

    private void Refresh()
    {
        if (selected >= 0 && !CanBuy(selected)) selected = -1; // Can't afford it anymore

        for (int i = 0; i < optionButtons.Length; i++)
        {
            Button button = optionButtons[i];
            if (button == null) continue;

            bool used = i < tiers.Length;
            button.gameObject.SetActive(used); // Fewer options than buttons = the extras are hidden
            if (!used) continue;

            SetText(button.transform, "Name", tiers[i].name);
            SetText(button.transform, "Price", tiers[i].price.ToString("N0"));

            bool canBuy = CanBuy(i);
            button.interactable = canBuy;

            CanvasGroup group = button.GetComponent<CanvasGroup>();
            if (group == null) group = button.gameObject.AddComponent<CanvasGroup>();
            group.alpha = canBuy ? 1f : unavailableAlpha;

            Transform border = button.transform.Find("SelectedBorder");
            if (border != null) border.gameObject.SetActive(i == selected);
        }

        purchaseButton.interactable = selected >= 0;
    }

    private void Select(int index)
    {
        if (!CanBuy(index)) return;
        selected = index;
        Refresh();
    }

    private void Purchase()
    {
        if (!CanBuy(selected) || onPurchase == null) return;

        Func<int, bool> purchase = onPurchase;
        if (purchase(selected)) Close();
        else Refresh(); // Didn't go through - stay open
    }

    private static void SetText(Transform parent, string childName, string text)
    {
        Transform child = parent.Find(childName);
        TMP_Text label = child != null ? child.GetComponent<TMP_Text>() : null;
        if (label != null) label.text = text;
    }
}