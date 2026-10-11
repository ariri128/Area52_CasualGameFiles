using UnityEngine;
using System.Collections.Generic;

/*
 * The rooms the player can buy
 * The player isn't unlocking a theme, they're buying one more room space and choosing which theme (base prefab) it has
 * Each theme has its own price
*/

public class RoomShop : MonoBehaviour
{
    [System.Serializable]
    public class Offer
    {
        [Tooltip("Shown under the picture, e.g. \"Log Room\".")]
        public string displayName = "Room";
        public GameObject roomPrefab;
        public int cost = 200;
        [Tooltip("Picture of the room for the shop. Optional.")]
        public Sprite preview;
    }

    public static RoomShop main { get; private set; }

    [SerializeField] private List<Offer> offers = new List<Offer>();

    public IReadOnlyList<Offer> Offers => offers;

    private void Awake()
    {
        if (main != null && main != this)
        {
            Debug.LogWarning("More than one RoomShop in the scene. Destroying the extra one.", this);
            Destroy(this);
            return;
        }
        main = this;
    }

    private void OnDestroy()
    {
        if (main == this) main = null;
    }

    public bool CanAfford(Offer offer)
    {
        return offer != null && CoinBank.main != null && CoinBank.main.CanAfford(offer.cost);
    }

    // Returns the new room's index, or -1 if it couldn't be bought
    public int Buy(Offer offer)
    {
        if (offer == null || offer.roomPrefab == null || RoomManager.main == null || CoinBank.main == null) return -1;
        if (!CoinBank.main.TrySpend(offer.cost)) return -1; // Not enough coins: nothing happens

        int roomNumber = RoomManager.main.RoomCount + 1;
        int index = RoomManager.main.AddRoom($"Room_{roomNumber}", offer.roomPrefab);
        Debug.Log($"Bought {offer.displayName} for {offer.cost} coins. It's Room {roomNumber}.", this);
        return index;
    }
}