using UnityEngine;
using System.Collections.Generic;
using System.Text;

/*
 * The list of every alien that can exist plus the rarity weights
*/

[CreateAssetMenu(menuName = "Alien Apartments/Alien Catalog", fileName = "AlienCatalog")]
public class AlienCatalog : ScriptableObject
{
    [Tooltip("How strongly each rarity is passed on: common 50, rare 30, ultra rare 20.")]
    public RarityWeights weights = new RarityWeights();

    [Tooltip("Every alien that can exist. Put the four originals first, then the babies.")]
    public List<AlienSpecies> species = new List<AlienSpecies>();

    [Header("Testing")]
    public AlienSpecies testParentA;
    public AlienSpecies testParentB;

    public List<BabyChance> OddsFor(AlienSpecies parentA, AlienSpecies parentB)
    {
        return BreedingOdds.Calculate(parentA, parentB, species, weights);
    }

    [ContextMenu("Print Breeding Odds")]
    private void PrintOdds()
    {
        if (testParentA == null || testParentB == null)
        {
            Debug.LogWarning("Fill in Test Parent A and Test Parent B first.", this);
            return;
        }

        List<BabyChance> odds = OddsFor(testParentA, testParentB);
        StringBuilder text = new StringBuilder();
        text.AppendLine($"{testParentA.displayName} x {testParentB.displayName}: {odds.Count} possible babies");
        foreach (BabyChance entry in odds)
            text.AppendLine($"  {entry.chance * 100f:0.00}%  {entry.species.displayName}");
        Debug.Log(text.ToString(), this);
    }
}