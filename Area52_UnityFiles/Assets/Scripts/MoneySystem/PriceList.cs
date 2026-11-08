using UnityEngine;

/*
 * Every buy and sell price for aliens and artifacts, in one place so they're easy to change
 * Rooms keep their prices on RoomShop, and furniture on each Furniture Definition
 * Alien sell prices are a range: the actual amount is picked at random when the alien is sold
*/

public class PriceList : MonoBehaviour
{
    [System.Serializable]
    public class PriceRange
    {
        public int min;
        public int max;

        public PriceRange(int min, int max) { this.min = min; this.max = max; }

        // Random.Range with ints leaves out the top number, so +1 lets max be picked too
        public int Roll() => Random.Range(min, Mathf.Max(min, max) + 1);
    }

    public static PriceList main { get; private set; }

    [Header("Aliens - buying")]
    [SerializeField] private int alienBuyCommon = 500;
    [SerializeField] private int alienBuyRare = 1000;
    [SerializeField] private int alienBuyUltraRare = 1500;

    [Header("Aliens - selling (random between min and max)")]
    [SerializeField] private PriceRange alienSellCommon = new PriceRange(300, 500);
    [SerializeField] private PriceRange alienSellRare = new PriceRange(700, 1000);
    [SerializeField] private PriceRange alienSellUltraRare = new PriceRange(1200, 1500);

    [Header("Artifacts - buying")]
    [SerializeField] private int artifactBuyCommon = 100;
    [SerializeField] private int artifactBuyUncommon = 200;
    [SerializeField] private int artifactBuyRare = 300;

    [Header("Artifacts - selling")]
    [SerializeField] private int artifactSellCommon = 50;
    [SerializeField] private int artifactSellUncommon = 100;
    [SerializeField] private int artifactSellRare = 150;

    [Header("Testing")]
    [Tooltip("Only used by the Print Alien Rarities button.")]
    [SerializeField] private AlienCatalog testCatalog;

    private void Awake()
    {
        if (main != null && main != this)
        {
            Debug.LogWarning("More than one PriceList in the scene. Destroying the extra one.", this);
            Destroy(this);
            return;
        }
        main = this;
    }

    private void OnDestroy()
    {
        if (main == this) main = null;
    }


    // ALIENS

    public int AlienBuyPrice(TraitRarity rarity)
    {
        switch (rarity)
        {
            case TraitRarity.Common: return alienBuyCommon;
            case TraitRarity.Rare: return alienBuyRare;
            default: return alienBuyUltraRare;
        }
    }

    public PriceRange AlienSellRange(TraitRarity rarity)
    {
        switch (rarity)
        {
            case TraitRarity.Common: return alienSellCommon;
            case TraitRarity.Rare: return alienSellRare;
            default: return alienSellUltraRare;
        }
    }

    // The actual amount an alien sells for - a new random number each time
    public int RollAlienSellPrice(AlienSpecies species)
    {
        return species != null ? AlienSellRange(species.OverallRarity).Roll() : 0;
    }


    // ARTIFACTS

    public int ArtifactBuyPrice(ArtifactRarity rarity)
    {
        switch (rarity)
        {
            case ArtifactRarity.Common: return artifactBuyCommon;
            case ArtifactRarity.Uncommon: return artifactBuyUncommon;
            default: return artifactBuyRare;
        }
    }

    public int ArtifactSellPrice(ArtifactRarity rarity)
    {
        switch (rarity)
        {
            case ArtifactRarity.Common: return artifactSellCommon;
            case ArtifactRarity.Uncommon: return artifactSellUncommon;
            default: return artifactSellRare;
        }
    }


    // TESTING

    [ContextMenu("Testing: Print Alien Rarities")]
    private void PrintAlienRarities()
    {
        if (testCatalog == null) { Debug.LogWarning("Drag the AlienCatalog into Test Catalog first.", this); return; }

        int common = 0, rare = 0, ultraRare = 0;
        foreach (AlienSpecies species in testCatalog.species)
        {
            if (species == null) continue;
            TraitRarity rarity = species.OverallRarity;
            if (rarity == TraitRarity.Common) common++;
            else if (rarity == TraitRarity.Rare) rare++;
            else ultraRare++;
            Debug.Log($"{species.displayName}: {AlienSpecies.RarityName(rarity)}", species);
        }
        Debug.Log($"Totals - Common: {common}, Rare: {rare}, Ultra Rare: {ultraRare}", this);
    }
}