using UnityEngine;
using System.Collections.Generic;

/*
 * Which personality each eyes + mouth combination has
 * Eyes and mouth are looked up by the base alien they came from, so babies work automatically
*/

[CreateAssetMenu(menuName = "Alien Apartments/Personality Table", fileName = "PersonalityTable")]
public class PersonalityTable : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        [HideInInspector] public string name; // Inspector label
        public AlienSpecies eyesFrom;
        public AlienSpecies mouthFrom;
        public Personality personality;
        [Tooltip("An alien with these eyes and mouth, so you can see which row of the doc it is. Filled in by the tool.")]
        public Sprite example;
    }

    [Tooltip("Used by the Fill Rows tool to find every eyes + mouth combo.")]
    public AlienCatalog catalog;

    public List<Entry> entries = new List<Entry>();

    public Personality Get(AlienSpecies species)
    {
        if (species == null) return null;
        AlienSpecies eyes = species.TraitOrigin(TraitSlot.Eyes);
        AlienSpecies mouth = species.TraitOrigin(TraitSlot.Mouth);

        foreach (Entry entry in entries)
            if (entry.eyesFrom == eyes && entry.mouthFrom == mouth) return entry.personality;

        Debug.LogWarning($"No personality set for {eyes.displayName} eyes + {mouth.displayName} mouth.", this);
        return null;
    }

    private void OnValidate()
    {
        foreach (Entry entry in entries)
        {
            string eyes = entry.eyesFrom != null ? entry.eyesFrom.displayName : "?";
            string mouth = entry.mouthFrom != null ? entry.mouthFrom.displayName : "?";
            string who = entry.personality != null ? entry.personality.displayName : "(not set)";
            entry.name = $"{eyes} eyes + {mouth} mouth = {who}";
        }
    }
}