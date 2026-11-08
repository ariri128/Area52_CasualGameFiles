using UnityEngine;
using System.Collections;
using TMPro;
using UnityEngine.UI;

/*
 * "Are you sure you want to sell this alien?" - opened by Sell in the Alien Files
 *   Takes the player to the alien's room, then the panel (a big copy of the alien's box, the question, Yes / No) grows in the middle
 *   No  - back to the Alien Files, with the alien still selected
 *   Yes - the popup closes, the spaceship takes the alien away for good, and once the ship is off screen:
 *         "Sold for ___ coins!" + coins + cha-ching, and the coins go in the wallet
 * The price is a random number in the alien's rarity range (Price List), picked when Yes is pressed
 * Aliens away breeding or exploring can't be sold - they're not in their room
*/

public class AlienSellPopup : MonoBehaviour
{
    public static AlienSellPopup main { get; private set; }

    [Header("Parts")]
    [Tooltip("The child that's shown and hidden (a see-through background so the room shows behind it, + the panel).")]
    [SerializeField] private GameObject root;
    [Tooltip("Everything that grows in: the big alien box, the question, and the buttons.")]
    [SerializeField] private RectTransform panel;
    [Tooltip("The alien's picture in the big box.")]
    [SerializeField] private Image picture;
    [Tooltip("Optional. The alien's name in the big box.")]
    [SerializeField] private TMP_Text nameText;
    [Tooltip("Optional. The question.")]
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;
    [Tooltip("The Alien Files. Leave empty to use the one in the scene.")]
    [SerializeField] private AlienFilesPopup filesPopup;

    [Header("Text")]
    [SerializeField] private string message = "Are you sure you want to sell this alien?";

    [Header("Timing (seconds)")]
    [Tooltip("Wait for the camera to reach the alien's room before the panel grows.")]
    [SerializeField] private float waitForCamera = 0.35f;
    [SerializeField] private float growSeconds = 0.3f;

    private OwnedAlien selling;
    private Coroutine opening;

    public bool IsOpen => root != null && root.activeSelf;

    private void Awake()
    {
        main = this;
        yesButton.onClick.AddListener(Yes);
        noButton.onClick.AddListener(No);
        root.SetActive(false);
    }

    private void OnDestroy()
    {
        if (main == this) main = null;
    }

    // Hidden partway (e.g. off to the breeding room): nothing is sold
    private void OnDisable()
    {
        StopAllCoroutines();
        opening = null;
        if (root != null) root.SetActive(false);
        selling = null;
    }

    // Can this alien be sold right now? (The Alien Files greys Sell out when it can't)
    public static bool CanSell(OwnedAlien alien)
    {
        return alien != null && !alien.IsAway && ExplorationManager.main != null && ExplorationManager.main.CanSendAway(alien);
    }

    public void Open(OwnedAlien alien)
    {
        if (!CanSell(alien)) return;
        selling = alien;

        if (AlienTapper.main != null) AlienTapper.main.Deselect(); // No thought bubble over the top
        if (RoomManager.main != null) RoomManager.main.GoToRoom(alien.roomIndex);

        picture.sprite = alien.species.sprite;
        picture.preserveAspect = true;
        if (nameText != null) nameText.text = alien.DisplayName;
        if (messageText != null) messageText.text = message;

        transform.SetAsLastSibling(); // In front of everything
        root.SetActive(true);

        if (opening != null) StopCoroutine(opening);
        opening = StartCoroutine(Grow());
    }

    public void Close()
    {
        if (opening != null) StopCoroutine(opening);
        opening = null;
        root.SetActive(false);
        selling = null;
    }

    private AlienFilesPopup Files => filesPopup != null ? filesPopup : AlienFilesPopup.main;


    // THE BUTTONS

    private void No()
    {
        OwnedAlien alien = selling;
        Close();
        if (Files != null) Files.Open(alien); // Back to the catalog, still selected
    }

    private void Yes()
    {
        OwnedAlien alien = selling;
        Close();
        if (alien == null) return;

        // Picked now, while we still know the alien - paid out once the ship has gone
        int price = PriceList.main != null ? PriceList.main.RollAlienSellPrice(alien.species) : 0;
        if (PriceList.main == null) Debug.LogWarning("AlienSellPopup: no PriceList in the scene, so the alien sold for 0.", this);

        bool sent = ExplorationManager.main != null && ExplorationManager.main.SendAway(alien, () => Pay(price));
        if (!sent)
        {
            Debug.LogWarning($"AlienSellPopup: the ship couldn't take {alien.DisplayName}, so it wasn't sold.", this);
            if (Files != null) Files.Open(alien);
        }
    }

    // Runs once the ship is off screen
    private static void Pay(int price)
    {
        if (CoinBank.main != null) CoinBank.main.Earn(price);

        ToastPopup toast = ToastPopup.Find(ToastKind.Sold);
        if (toast != null) toast.ShowSold(price); // Coins + cha-ching
        else AudioManager.PlayPurchase();
    }


    // THE ANIMATION

    private IEnumerator Grow()
    {
        yesButton.interactable = false; // No tapping Yes before it's even showing
        noButton.interactable = false;
        panel.localScale = Vector3.zero;

        yield return new WaitForSecondsRealtime(waitForCamera);

        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / growSeconds)
        {
            panel.localScale = Vector3.one * EaseOutBack(t);
            yield return null;
        }
        panel.localScale = Vector3.one;

        yesButton.interactable = true;
        noButton.interactable = true;
        opening = null;
    }

    // Goes a bit past 1 then settles back - the "pop"
    private static float EaseOutBack(float t)
    {
        const float c = 1.7f;
        float u = Mathf.Clamp01(t) - 1f;
        return 1f + (c + 1f) * u * u * u + c * u * u;
    }
}