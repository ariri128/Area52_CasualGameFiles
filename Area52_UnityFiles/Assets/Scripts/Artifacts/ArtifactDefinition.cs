using UnityEngine;

/*
 * This is a data file for one kind of artifact, not something in the scene
*/

[CreateAssetMenu(menuName = "Alien Apartments/Artifact", fileName = "AR_NewArtifact")]
public class ArtifactDefinition : ScriptableObject
{
    [Tooltip("Name shown to the player.")]
    public string displayName = "New Artifact";

    [Tooltip("Picture used on the collection page and on the room's artifact circles.")]
    public Sprite sprite;

    [Tooltip("How likely an exploring alien is to bring this one back, compared to the others. " +
             "1 = normal, 2 = twice as likely, 0 = never. Leave them all at 1 for an even chance.")]
    [Min(0f)] public float findWeight = 1f;
}