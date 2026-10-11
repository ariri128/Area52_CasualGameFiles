using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Events;

/*
 * All the money in the game:
 *   Wallet - the coins the player owns (shown in the coin popup, spent in the shops)
 *   Chests - one per room; aliens in each room drop coins for that room's chest; chest doesn't fill up past max, has to be collected before it starts filling up again
 *              
 * Aliens away breeding don't drop coins, and eggs don't either
*/

public class CoinBank : MonoBehaviour
{
    public static CoinBank main { get; private set; }

    [Tooltip("Coins the player has when the game starts. Raise it to test the shops.")]
    [SerializeField] private int startingCoins = 0;

    [Tooltip("Most coins one room's chest can hold.")]
    [SerializeField] private int chestCapacity = 5000;

    [Tooltip("Seconds between drops.")]
    [SerializeField] private float dropInterval = 10f;

    [Tooltip("Print each drop in the Console.")]
    [SerializeField] private bool logDrops = false;

    // Fires whenever the wallet or any chest changes
    public UnityEvent onChanged = new UnityEvent();

    public int Wallet { get; private set; }
    public int ChestCapacity => chestCapacity;

    // Room index -> coins waiting in that room's chest
    private readonly Dictionary<int, int> chests = new Dictionary<int, int>();
    private float nextDropAt;

    private void Awake()
    {
        if (main != null && main != this)
        {
            Debug.LogWarning("More than one CoinBank in the scene. Destroying the extra one.", this);
            Destroy(this);
            return;
        }
        main = this;

        Wallet = startingCoins;
        nextDropAt = Time.time + dropInterval;
    }

    private void OnDestroy()
    {
        if (main == this) main = null;
    }

    private void Update()
    {
        if (Time.time < nextDropAt) return;
        nextDropAt += dropInterval;
        Drop();
    }

    private void Drop()
    {
        if (AlienRoster.main == null) return;

        bool changed = false;
        foreach (OwnedAlien alien in AlienRoster.main.All)
        {
            if (alien == null || alien.species == null || alien.inBreedingRoom) continue;

            int coins = alien.species.CoinsPerDrop;
            if (coins <= 0) continue;

            int before = ChestAmount(alien.roomIndex);
            int after = Mathf.Min(chestCapacity, before + coins); // Full chests stop filling
            if (after == before) continue;

            chests[alien.roomIndex] = after;
            changed = true;
            if (logDrops) Debug.Log($"{alien.species.displayName} dropped {after - before} coins in room {alien.roomIndex} ({after}/{chestCapacity}).", this);
        }

        if (changed) onChanged.Invoke();
    }

    public int ChestAmount(int roomIndex)
    {
        return chests.TryGetValue(roomIndex, out int amount) ? amount : 0;
    }

    // 0 = empty, 1 = full
    public float ChestFill(int roomIndex)
    {
        return chestCapacity > 0 ? ChestAmount(roomIndex) / (float)chestCapacity : 0f;
    }

    // Moves everything in this room's chest into the wallet - returns how much was collected
    public int Collect(int roomIndex)
    {
        int amount = ChestAmount(roomIndex);
        if (amount <= 0) return 0;

        chests[roomIndex] = 0;
        Wallet += amount;
        onChanged.Invoke();
        return amount;
    }

    public bool CanAfford(int cost) => Wallet >= cost;

    // For the shops - returns false (and spends nothing) if there isn't enough
    public bool TrySpend(int cost)
    {
        if (cost < 0 || Wallet < cost) return false;
        Wallet -= cost;
        onChanged.Invoke();
        return true;
    }
}