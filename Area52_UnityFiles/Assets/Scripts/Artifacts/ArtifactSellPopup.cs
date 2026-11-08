using UnityEngine;
using TMPro;
using UnityEngine.UI;

/*
 * "Selling for ___ coins" - opened by the Sell button on the Artifacts page
 *   Cancel  - closes it, back to the Artifacts page
 *   Confirm - the artifact is gone, the coins go in the wallet, and the Sold message plays (coins + cha-ching)
 * The price comes from the artifact's rarity, set on the Price List
 * Only unplaced artifacts can be sold (the Artifacts page greys Sell out for placed ones)
*/

public class ArtifactSellPopup : MonoBehaviour
{
    [Header("Parts")]
    [Tooltip("The child that's shown and hidden (dark background + window).")]
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    [Tooltip("Optional. The artifact's picture.")]
    [SerializeField] private Image picture;
    [Tooltip("Optional. The artifact's name.")]
    [SerializeField] private TMP_Text nameText;

    [Header("Text")]
    [Tooltip("{0} = how many coins, {1} = the artifact's name.")]
    [SerializeField] private string messageFormat = "Selling for {0} coins";

    private OwnedArtifact selling;
    private int price;

    public bool IsOpen => root != null && root.activeSelf;

    private void Awake()
    {
        confirmButton.onClick.AddListener(Confirm);
        cancelButton.onClick.AddListener(Close);
        root.SetActive(false);
    }

    public void Open(OwnedArtifact artifact)
    {
        if (artifact == null || artifact.definition == null || artifact.IsPlaced) return;

        if (PriceList.main == null)
        {
            Debug.LogWarning("ArtifactSellPopup: no PriceList in the scene, so there's no price to sell for.", this);
            return;
        }

        selling = artifact;
        price = PriceList.main.ArtifactSellPrice(artifact.definition.rarity); // Set now, so the number shown is the number paid

        if (messageText != null) messageText.text = string.Format(messageFormat, price.ToString("N0"), artifact.DisplayName);
        if (nameText != null) nameText.text = artifact.DisplayName;
        if (picture != null)
        {
            picture.sprite = artifact.definition.sprite;
            picture.enabled = artifact.definition.sprite != null;
            picture.preserveAspect = true;
        }

        transform.SetAsLastSibling(); // In front of the Artifacts page
        root.SetActive(true);
    }

    public void Close()
    {
        root.SetActive(false);
        selling = null;
    }

    private void Confirm()
    {
        if (selling == null || ArtifactInventory.main == null || CoinBank.main == null) { Close(); return; }

        int earned = price;
        if (!ArtifactInventory.main.Remove(selling)) { Close(); return; } // Got placed somehow - nothing is sold

        CoinBank.main.Earn(earned);
        Close();

        // Plays over the Artifacts page, which is still open underneath
        ToastPopup toast = ToastPopup.Find(ToastKind.Sold);
        if (toast != null) toast.ShowSold(earned);
        else AudioManager.PlayPurchase(); // No toast set up - at least the sound plays
    }
}