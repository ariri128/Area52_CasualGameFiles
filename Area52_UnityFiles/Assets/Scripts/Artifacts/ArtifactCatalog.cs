using UnityEngine;
using System.Collections.Generic;

/*
 * The list of every artifact an exploring alien can bring back
 * New artifacts only need to be added to this list - nothing else has to change
*/

[CreateAssetMenu(menuName = "Alien Apartments/Artifact Catalog", fileName = "ArtifactCatalog")]
public class ArtifactCatalog : ScriptableObject
{
    [Tooltip("Every artifact that can be found. Add new ones here once they're drawn.")]
    public List<ArtifactDefinition> artifacts = new List<ArtifactDefinition>();

    // Picks one at random, using each artifact's Find Weight - null if there's nothing to pick
    public ArtifactDefinition PickRandom()
    {
        float total = 0f;
        foreach (ArtifactDefinition artifact in artifacts)
            if (artifact != null) total += artifact.findWeight;
        if (total <= 0f) return null;

        // Lay the weights end to end, throw a dart, and see whose stretch it lands in
        float roll = Random.Range(0f, total);
        foreach (ArtifactDefinition artifact in artifacts)
        {
            if (artifact == null || artifact.findWeight <= 0f) continue;
            if (roll < artifact.findWeight) return artifact;
            roll -= artifact.findWeight;
        }

        // The roll can land exactly on the end - give the last one that can be found
        for (int i = artifacts.Count - 1; i >= 0; i--)
            if (artifacts[i] != null && artifacts[i].findWeight > 0f) return artifacts[i];
        return null;
    }
}