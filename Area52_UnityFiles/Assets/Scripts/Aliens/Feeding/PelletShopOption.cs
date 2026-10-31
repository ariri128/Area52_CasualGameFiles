using UnityEngine;
using UnityEngine.UI;
using TMPro;

/*
 * One option in the pellet shop (e.g. one bag = 15 pellets for 30 coins). Its pellet pictures are laid out by hand
 * Tapping it picks it in PelletShopPopup (tapping it again un-picks it)
 * Greys out when the player doesn't have enough coins
*/

[RequireComponent(typeof(Button))]
public class PelletShopOption : MonoBehaviour
{
    [Tooltip("Which pack in PelletBank's Packs list this is: 0 = 1 pellet, 1 = 5 pellets, 2 = 1 bag, 3 = 2 bags, 4 = 3 bags.")]
    [SerializeField] private int packIndex;

    [Tooltip("Optional. Filled in with how many pellets, e.g. \"x15\".")]
    [SerializeField] private TMP_Text amountText;
    [Tooltip("Optional. Filled in with the cost in coins, e.g. \"30\".")]
    [SerializeField] private TMP_Text costText;
    [Tooltip("Shown while this option is picked (e.g. a green border like the artifact slots).")]
    [SerializeField] private GameObject selectedBorder;
    [Tooltip("How see-through the whole option goes when the player can't afford it.")]
    [Range(0f, 1f)] [SerializeField] private float greyedAlpha = 0.4f;

    public int PackIndex => packIndex;

    private Button button;
    private CanvasGroup group;
    private PelletShopPopup shop;

    private void Awake()
    {
        button = GetComponent<Button>();

        // Greys out the pictures and texts too, not just the button's own image
        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();

        button.onClick.AddListener(() => { if (shop != null) shop.Select(this); });
        if (selectedBorder != null) selectedBorder.SetActive(false);
    }

    // Fills in the texts from PelletBank, so the prices only live in one place
    public void Setup(PelletShopPopup owner)
    {
        shop = owner;

        PelletPack pack = PelletBank.main != null ? PelletBank.main.GetPack(packIndex) : null;
        if (pack == null) return;
        if (amountText != null) amountText.text = pack.pellets == 1 ? "1 Pellet" : $"{pack.pellets} Pellets";
        if (costText != null) costText.text = pack.cost.ToString("N0");
    }

    public void Refresh(bool affordable, bool selected)
    {
        button.interactable = affordable;
        group.alpha = affordable ? 1f : greyedAlpha;
        if (selectedBorder != null) selectedBorder.SetActive(selected);
    }
}