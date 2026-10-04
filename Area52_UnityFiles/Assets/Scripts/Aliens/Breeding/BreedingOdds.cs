using UnityEngine;
using System.Collections.Generic;

/*
 * Works out which baby two parents can have, and how likely each one is
*/

[System.Serializable]
public class RarityWeights
{
    public float common = 50f;
    public float rare = 30f;
    public float ultraRare = 20f;

    public float Get(TraitRarity rarity)
    {
        switch (rarity)
        {
            case TraitRarity.Common: return common;
            case TraitRarity.Rare: return rare;
            default: return ultraRare;
        }
    }
}

public struct BabyChance
{
    public AlienSpecies species;
    public float chance; // 0 to 1
}

public static class BreedingOdds
{
    private static readonly TraitSlot[] Slots =
        { TraitSlot.Body, TraitSlot.Antenna, TraitSlot.Legs, TraitSlot.Eyes, TraitSlot.Mouth };

    // Every baby these two parents can have, most likely first, chances adding to 1
    public static List<BabyChance> Calculate(AlienSpecies parentA, AlienSpecies parentB,
                                             IEnumerable<AlienSpecies> allSpecies, RarityWeights weights)
    {
        List<BabyChance> result = new List<BabyChance>();
        if (parentA == null || parentB == null || allSpecies == null) return result;

        HashSet<string> seen = new HashSet<string>();
        float total = 0f;

        foreach (AlienSpecies candidate in allSpecies)
        {
            if (candidate == null) continue;

            // Two files with the exact same traits count once (the first one listed wins)
            string signature = Signature(candidate);
            if (seen.Contains(signature)) continue;

            float chance = 1f;
            foreach (TraitSlot slot in Slots)
            {
                chance *= TraitChance(parentA, parentB, slot, candidate.TraitOrigin(slot), weights);
                if (chance <= 0f) break;
            }

            if (chance > 0f)
            {
                seen.Add(signature);
                result.Add(new BabyChance { species = candidate, chance = chance });
                total += chance;
            }
        }

        // Scale so the drawn babies add up to 100%
        for (int i = 0; i < result.Count; i++)
        {
            BabyChance entry = result[i];
            entry.chance /= total;
            result[i] = entry;
        }

        result.Sort((x, y) => y.chance.CompareTo(x.chance));
        return result;
    }

    // The chance the baby's trait in this slot comes from the given original alien
    private static float TraitChance(AlienSpecies parentA, AlienSpecies parentB, TraitSlot slot,
                                     AlienSpecies wanted, RarityWeights weights)
    {
        AlienSpecies fromA = parentA.TraitOrigin(slot);
        AlienSpecies fromB = parentB.TraitOrigin(slot);

        float weightA = weights.Get(parentA.RarityOf(slot));
        float weightB = weights.Get(parentB.RarityOf(slot));
        float sum = weightA + weightB;
        if (sum <= 0f) { weightA = weightB = 1f; sum = 2f; }

        float chance = 0f;
        if (wanted == fromA) chance += weightA / sum;
        if (wanted == fromB) chance += weightB / sum; // If the trait is the same on both parents, the baby will 100% get that trait
        return chance;
    }

    // Picks one baby using the chances - returns null if there's nothing to pick.
    public static AlienSpecies Roll(List<BabyChance> odds)
    {
        if (odds == null || odds.Count == 0) return null;

        float roll = Random.value;
        float running = 0f;
        foreach (BabyChance entry in odds)
        {
            running += entry.chance;
            if (roll <= running) return entry.species;
        }
        return odds[odds.Count - 1].species;
    }

    private static string Signature(AlienSpecies species)
    {
        string signature = "";
        foreach (TraitSlot slot in Slots)
            signature += species.TraitOrigin(slot).GetInstanceID() + "|";
        return signature;
    }
}