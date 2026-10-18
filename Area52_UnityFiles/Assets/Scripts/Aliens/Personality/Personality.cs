using UnityEngine;
using System.Collections.Generic;

/*
 * One personality: its name, which personalities it likes, and which it dislikes
 * Decided by an alien's eyes + mouth - see PersonalityTable
*/

[CreateAssetMenu(menuName = "Alien Apartments/Personality", fileName = "P_NewPersonality")]
public class Personality : ScriptableObject
{
    [Tooltip("Shown in the alien's file, e.g. \"Head empty - Curious\".")]
    public string displayName = "New Personality";

    [Tooltip("Short name shown in the Likes / Dislikes lines, e.g. \"curious\".")]
    public string shortName = "new";

    [Tooltip("Likes every personality. Not used right now.")]
    public bool likesEveryone;

    public List<Personality> likes = new List<Personality>();
    public List<Personality> dislikes = new List<Personality>();

    public bool Likes(Personality other) => other != null && (likesEveryone || likes.Contains(other));
    public bool Dislikes(Personality other) => other != null && dislikes.Contains(other);

    // For the file: "Own" / "Like" / "Dislike"
    public string LikesText => likesEveryone ? "all" : ListText(likes);
    public string DislikesText => ListText(dislikes);

    private static string ListText(List<Personality> list)
    {
        List<string> names = new List<string>();
        foreach (Personality p in list) if (p != null) names.Add(p.shortName);
        return names.Count > 0 ? string.Join(", ", names) : "none";
    }
}