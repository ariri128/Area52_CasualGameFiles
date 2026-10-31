using UnityEngine;
using UnityEngine.UI;
using TMPro;

/*
 * Goes on the pellet bag button in the HUD, next to the egg - opens the pellet shop
 * Can also show how many pellets the player has
 * In the breeding room it first goes back to the apartments, since the shop lives there (same as the Alien Files button)
*/

[RequireComponent(typeof(Button))]
public class OpenPelletShopButton : MonoBehaviour
{
    [Tooltip("Optional. Text on or under the bag showing how many pellets the player has.")]
    [SerializeField] private TMP_Text countText;

    private bool listening;

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(Open);
    }

    // Tries in both OnEnable and Start: when the game starts, PelletBank may not be set up yet the first time this runs
    private void OnEnable() => StartListening();
    private void Start() => StartListening();

    private void StartListening()
    {
        if (!listening && PelletBank.main != null)
        {
            PelletBank.main.onChanged.AddListener(RefreshCount);
            listening = true;
        }
        RefreshCount();
    }

    private void OnDisable()
    {
        if (listening && PelletBank.main != null) PelletBank.main.onChanged.RemoveListener(RefreshCount);
        listening = false;
    }

    private void RefreshCount()
    {
        if (countText != null && PelletBank.main != null) countText.text = PelletBank.main.Pellets.ToString("N0");
    }

    private void Open()
    {
        if (SceneFlow.main != null && SceneFlow.main.InBreedingRoom)
            SceneFlow.main.ReturnToApartments();

        if (PelletShopPopup.main != null) PelletShopPopup.main.Open();
        else Debug.LogWarning("OpenPelletShopButton: no PelletShopPopup in the Rooms scene.", this);
    }
}