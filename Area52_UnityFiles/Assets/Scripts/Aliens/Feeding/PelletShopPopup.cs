using UnityEngine;
using UnityEngine.UI;
using TMPro;

/*
 * The pellet shop the pellet bag opens (looks like the wallet popup):
 *   top - how many pellets the player has
 *   middle - the 5 options; ones the player can't afford are greyed out
 *   bottom - Buy, greyed out until an option is picked
 * Pick an option, press Buy: spends the coins and adds the pellets. The option stays picked so you can buy it again
*/

public class PelletShopPopup : MonoBehaviour
{
    public static PelletShopPopup main { get; private set; }

    [Header("Parts")]
    [Tooltip("The child that's shown and hidden (dark background + window).")]
    [SerializeField] private GameObject root;
    [SerializeField] private Button closeButton;
    [Tooltip("Shows how many pellets the player has.")]
    [SerializeField] private TMP_Text pelletsText;
    [Tooltip("Optional. Shows how many coins the player has, so they can see why options are greyed out.")]
    [SerializeField] private TMP_Text coinsText;
    [SerializeField] private Button buyButton;
    [Tooltip("The 5 options. Leave empty to use every PelletShopOption inside this popup.")]
    [SerializeField] private PelletShopOption[] options;

    private PelletShopOption selected;
    private bool listening;

    public bool IsOpen => root != null && root.activeSelf;

    private void Awake()
    {
        main = this;
        if (options == null || options.Length == 0) options = GetComponentsInChildren<PelletShopOption>(true);

        closeButton.onClick.AddListener(Close);
        buyButton.onClick.AddListener(Buy);
        root.SetActive(false);
    }

    private void OnDestroy()
    {
        if (main == this) main = null;
    }

    // Tries in both OnEnable and Start: the banks may not be set up yet the first time this runs
    private void OnEnable() => StartListening();
    private void Start() => StartListening();

    private void StartListening()
    {
        if (listening || CoinBank.main == null || PelletBank.main == null) return;
        CoinBank.main.onChanged.AddListener(Refresh);
        PelletBank.main.onChanged.AddListener(Refresh);
        listening = true;
    }

    private void OnDisable()
    {
        if (!listening) return;
        if (CoinBank.main != null) CoinBank.main.onChanged.RemoveListener(Refresh);
        if (PelletBank.main != null) PelletBank.main.onChanged.RemoveListener(Refresh);
        listening = false;
    }

    public void Open()
    {
        selected = null;
        root.SetActive(true);
        foreach (PelletShopOption option in options)
            if (option != null) option.Setup(this);
        Refresh();
    }

    public void Close()
    {
        selected = null;
        root.SetActive(false);
    }

    // Called by an option when it's tapped. Tapping the picked one again un-picks it
    public void Select(PelletShopOption option)
    {
        selected = selected == option ? null : option;
        Refresh();
    }

    // Also runs whenever coins or pellets change while it's open
    private void Refresh()
    {
        if (!IsOpen || PelletBank.main == null) return;

        pelletsText.text = PelletBank.main.Pellets.ToString("N0");
        if (coinsText != null && CoinBank.main != null) coinsText.text = CoinBank.main.Wallet.ToString("N0");

        // Can't keep something picked that's no longer affordable (e.g. after buying it)
        if (selected != null && !PelletBank.main.CanAfford(selected.PackIndex)) selected = null;

        foreach (PelletShopOption option in options)
        {
            if (option == null) continue;
            option.Refresh(PelletBank.main.CanAfford(option.PackIndex), option == selected);
        }

        buyButton.interactable = selected != null;
    }

    private void Buy()
    {
        if (selected == null || PelletBank.main == null) return;
        PelletBank.main.TryBuy(selected.PackIndex); // The banks fire onChanged, which refreshes everything
    }
}