using UnityEngine;

/*
 * This is a data file for the aliens, not something in the scene
*/

public enum TraitSlot { Body, Antenna, Legs, Eyes, Mouth }

public enum TraitRarity { Common, Rare, UltraRare }

[System.Serializable]
public class TraitRarities
{
    public TraitRarity body;
    public TraitRarity antenna;
    public TraitRarity legs;
    public TraitRarity eyes;
    public TraitRarity mouth;

    public TraitRarity Get(TraitSlot slot)
    {
        switch (slot)
        {
            case TraitSlot.Body: return body;
            case TraitSlot.Antenna: return antenna;
            case TraitSlot.Legs: return legs;
            case TraitSlot.Eyes: return eyes;
            default: return mouth;
        }
    }
}

[System.Serializable]
public class TraitOrigins
{
    public AlienSpecies body;
    public AlienSpecies antenna;
    public AlienSpecies legs;
    public AlienSpecies eyes;
    public AlienSpecies mouth;

    public AlienSpecies Get(TraitSlot slot)
    {
        switch (slot)
        {
            case TraitSlot.Body: return body;
            case TraitSlot.Antenna: return antenna;
            case TraitSlot.Legs: return legs;
            case TraitSlot.Eyes: return eyes;
            default: return mouth;
        }
    }
}

[CreateAssetMenu(menuName = "Alien Apartments/Alien Species", fileName = "AS_NewAlien")]
public class AlienSpecies : ScriptableObject
{
    [Tooltip("Name shown to the player, e.g. \"Bloobus\".")]
    public string displayName = "New Alien";

    [Tooltip("Picture used in the rooms and in the breeding boxes. For now, the left pose.")]
    public Sprite sprite;

    [Header("Original aliens only")]
    [Tooltip("How rare each of this alien's traits is. Only used on the four original aliens.")]
    public TraitRarities rarities = new TraitRarities();

    [Tooltip("Coins this alien adds to its room's chest every drop. Babies use the value of " +
             "the original their body came from, so this is only filled in on the originals.")]
    public int coinsPerDrop;

    [Tooltip("The artifact this alien likes. When it's placed in the alien's room, the alien drops more coins. " +
             "Babies like whatever the original their body came from likes, so this is only filled in on the originals.")]
    public ArtifactDefinition likedArtifact;

    [Header("Babies only")]
    [Tooltip("Which original alien each trait was drawn from. Leave empty on the four originals. " +
             "The Combo Sheet tool fills this in for babies.")]
    public TraitOrigins traitsFrom = new TraitOrigins();

    // The original alien this trait came from (an original's traits all come from itself)
    public AlienSpecies TraitOrigin(TraitSlot slot)
    {
        AlienSpecies origin = traitsFrom != null ? traitsFrom.Get(slot) : null;
        return origin != null ? origin : this;
    }

    // Coins dropped each time: decided by the body, so a baby with Smeeeg's body drops like Smeeeg
    public int CoinsPerDrop => TraitOrigin(TraitSlot.Body).coinsPerDrop;

    // Also decided by the body, so a baby with Bloobus's body likes the Fish Bowl too
    public ArtifactDefinition LikedArtifact => TraitOrigin(TraitSlot.Body).likedArtifact;

    // How rare this alien's version of a trait is (looked up on the original it came from)
    public TraitRarity RarityOf(TraitSlot slot)
    {
        AlienSpecies origin = TraitOrigin(slot);
        return origin.rarities != null ? origin.rarities.Get(slot) : TraitRarity.Common;
    }
}

/* ver.1
public enum TraitSlot { Body, Antenna, Legs, Eyes, Mouth }

public enum TraitRarity { Common, Rare, UltraRare }

[System.Serializable]
public class TraitRarities
{
    public TraitRarity body;
    public TraitRarity antenna;
    public TraitRarity legs;
    public TraitRarity eyes;
    public TraitRarity mouth;

    public TraitRarity Get(TraitSlot slot)
    {
        switch (slot)
        {
            case TraitSlot.Body: return body;
            case TraitSlot.Antenna: return antenna;
            case TraitSlot.Legs: return legs;
            case TraitSlot.Eyes: return eyes;
            default: return mouth;
        }
    }
}

[System.Serializable]
public class TraitOrigins
{
    public AlienSpecies body;
    public AlienSpecies antenna;
    public AlienSpecies legs;
    public AlienSpecies eyes;
    public AlienSpecies mouth;

    public AlienSpecies Get(TraitSlot slot)
    {
        switch (slot)
        {
            case TraitSlot.Body: return body;
            case TraitSlot.Antenna: return antenna;
            case TraitSlot.Legs: return legs;
            case TraitSlot.Eyes: return eyes;
            default: return mouth;
        }
    }
}

[CreateAssetMenu(menuName = "Alien Apartments/Alien Species", fileName = "AS_NewAlien")]
public class AlienSpecies : ScriptableObject
{
    [Tooltip("Name shown to the player, e.g. \"Bloobus\".")]
    public string displayName = "New Alien";

    [Tooltip("Picture used in the rooms and in the breeding boxes. For now, the left pose.")]
    public Sprite sprite;

    [Header("Original aliens only")]
    [Tooltip("How rare each of this alien's traits is. Only used on the four original aliens.")]
    public TraitRarities rarities = new TraitRarities();

    [Tooltip("Coins this alien adds to its room's chest every drop. Babies use the value of " +
             "the original their body came from, so this is only filled in on the originals.")]
    public int coinsPerDrop;

    [Header("Babies only")]
    [Tooltip("Which original alien each trait was drawn from. Leave empty on the four originals. " +
             "The Combo Sheet tool fills this in for babies.")]
    public TraitOrigins traitsFrom = new TraitOrigins();

    // The original alien this trait came from (an original's traits all come from itself)
    public AlienSpecies TraitOrigin(TraitSlot slot)
    {
        AlienSpecies origin = traitsFrom != null ? traitsFrom.Get(slot) : null;
        return origin != null ? origin : this;
    }

    // Coins dropped each time: decided by the body, so a baby with Smeeeg's body drops like Smeeeg
    public int CoinsPerDrop => TraitOrigin(TraitSlot.Body).coinsPerDrop;

    // How rare this alien's version of a trait is (looked up on the original it came from)
    public TraitRarity RarityOf(TraitSlot slot)
    {
        AlienSpecies origin = TraitOrigin(slot);
        return origin.rarities != null ? origin.rarities.Get(slot) : TraitRarity.Common;
    }
}
*/