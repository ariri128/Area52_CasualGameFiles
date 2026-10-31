using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Events;

/*
 * The pellets the player owns, for feeding aliens
 * Feeding uses one pellet (TryUse), the pellet shop sells packs of them for coins (TryBuy)
 * The packs and their prices live here so the shop panel just reads them
*/

[System.Serializable]
public class PelletPack
{
    [HideInInspector] public string name; // Inspector label

    [Tooltip("Pellets the player gets.")]
    public int pellets = 1;

    [Tooltip("What it costs in coins.")]
    public int cost = 5;

    public void RefreshLabel() => name = $"{pellets} pellets for {cost} coins";
}

public class PelletBank : MonoBehaviour
{
    public static PelletBank main { get; private set; }

    [Tooltip("Pellets the player has when the game starts.")]
    [SerializeField] private int startingPellets = 8;

    [Tooltip("What the pellet shop sells, top to bottom.")]
    [SerializeField]
    private List<PelletPack> packs = new List<PelletPack>
    {
        new PelletPack { pellets = 1,  cost = 5  },
        new PelletPack { pellets = 5,  cost = 15 },
        new PelletPack { pellets = 15, cost = 30 },
        new PelletPack { pellets = 25, cost = 50 },
        new PelletPack { pellets = 50, cost = 75 },
    };

    // Fires whenever the number of pellets changes
    public UnityEvent onChanged = new UnityEvent();

    public int Pellets { get; private set; }
    public IReadOnlyList<PelletPack> Packs => packs;
    public bool HasPellets => Pellets > 0;

    private void Awake()
    {
        if (main != null && main != this)
        {
            Debug.LogWarning("More than one PelletBank in the scene. Destroying the extra one.", this);
            Destroy(this);
            return;
        }
        main = this;

        Pellets = Mathf.Max(0, startingPellets);
    }

    private void OnDestroy()
    {
        if (main == this) main = null;
    }


    // FEEDING

    // Uses one pellet - returns false (and uses nothing) if there are none left
    public bool TryUse()
    {
        if (Pellets <= 0) return false;
        Pellets--;
        onChanged.Invoke();
        return true;
    }

    public void Add(int amount)
    {
        if (amount <= 0) return;
        Pellets += amount;
        onChanged.Invoke();
    }


    // SHOP

    public PelletPack GetPack(int index) =>
        index >= 0 && index < packs.Count ? packs[index] : null;

    public bool CanAfford(int index)
    {
        PelletPack pack = GetPack(index);
        return pack != null && CoinBank.main != null && CoinBank.main.CanAfford(pack.cost);
    }

    // Spends the coins and adds the pellets - returns false (and changes nothing) if it can't
    public bool TryBuy(int index)
    {
        PelletPack pack = GetPack(index);
        if (pack == null || CoinBank.main == null) return false;
        if (!CoinBank.main.TrySpend(pack.cost)) return false;

        Add(pack.pellets);
        return true;
    }


    // TESTING

    [ContextMenu("Testing: Add 10 Pellets")]
    private void AddTen()
    {
        if (!Application.isPlaying) { Debug.LogWarning("Press Play first.", this); return; }
        Add(10);
        Debug.Log($"Pellets: {Pellets}", this);
    }

    [ContextMenu("Testing: Empty Pellets")]
    private void EmptyPellets()
    {
        if (!Application.isPlaying) { Debug.LogWarning("Press Play first.", this); return; }
        Pellets = 0;
        onChanged.Invoke();
        Debug.Log("Pellets: 0", this);
    }

    [ContextMenu("Testing: Buy Pack 3 (15 for 30)")]
    private void BuyPackThree()
    {
        if (!Application.isPlaying) { Debug.LogWarning("Press Play first.", this); return; }
        bool bought = TryBuy(2);
        Debug.Log(bought
            ? $"Bought. Pellets: {Pellets}, Coins: {CoinBank.main.Wallet}"
            : $"Couldn't afford it. Coins: {(CoinBank.main != null ? CoinBank.main.Wallet : 0)}", this);
    }

    private void OnValidate()
    {
        foreach (PelletPack pack in packs)
            if (pack != null) pack.RefreshLabel();
    }
}