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

    [Header("Babies only")]
    [Tooltip("Which original alien each trait was drawn from. Leave empty on the four originals. " +
             "The Combo Sheet tool fills this in for babies.")]
    public TraitOrigins traitsFrom = new TraitOrigins();

    // The original alien this trait came from
    public AlienSpecies TraitOrigin(TraitSlot slot)
    {
        AlienSpecies origin = traitsFrom != null ? traitsFrom.Get(slot) : null;
        return origin != null ? origin : this;
    }

    // The rarity this alien's version of a trait is
    public TraitRarity RarityOf(TraitSlot slot)
    {
        AlienSpecies origin = TraitOrigin(slot);
        return origin.rarities != null ? origin.rarities.Get(slot) : TraitRarity.Common;
    }
}