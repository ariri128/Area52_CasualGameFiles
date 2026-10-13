using UnityEngine;
using TMPro;
using UnityEngine.UI;

/*
 * The UI wallet popup the chest opens
*/

public class CoinPopup : MonoBehaviour
{
    public static CoinPopup main { get; private set; }

    [Header("Parts")]
    [Tooltip("The child that's shown and hidden (dark background + window).")]
    [SerializeField] private GameObject root;
    [SerializeField] private Button closeButton;
    [Tooltip("Shows how many coins the player owns.")]
    [SerializeField] private TMP_Text walletText;
    [SerializeField] private Button collectButton;

    private int roomIndex;

    public bool IsOpen => root != null && root.activeSelf;

    private void Awake()
    {
        main = this;
        closeButton.onClick.AddListener(Close);
        collectButton.onClick.AddListener(Collect);
        root.SetActive(false);
    }

    private void OnDestroy()
    {
        if (main == this) main = null;
    }

    private bool listening;

    // Tries in both OnEnable and Start: when the game starts, CoinBank may not be set up yet the first time this runs
    private void OnEnable() => StartListening();
    private void Start() => StartListening();

    private void StartListening()
    {
        if (listening || CoinBank.main == null) return;
        CoinBank.main.onChanged.AddListener(Refresh);
        listening = true;
    }

    private void OnDisable()
    {
        if (listening && CoinBank.main != null) CoinBank.main.onChanged.RemoveListener(Refresh);
        listening = false;
    }

    public void Open(int forRoom)
    {
        roomIndex = forRoom;
        root.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        root.SetActive(false);
    }

    // Also runs on every drop while open, so the button lights up the moment coins arrive
    private void Refresh()
    {
        if (CoinBank.main == null) return;
        walletText.text = CoinBank.main.Wallet.ToString("N0"); // 5000 -> 5,000
        collectButton.interactable = CoinBank.main.ChestAmount(roomIndex) > 0;
    }

    private void Collect()
    {
        if (CoinBank.main != null) CoinBank.main.Collect(roomIndex);
    }
}