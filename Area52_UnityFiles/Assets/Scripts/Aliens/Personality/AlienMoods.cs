using UnityEngine;
using System.Collections.Generic;
using System.Text;

/*
 * Works out each alien's mood from who it shares a room with, and how many coins that mood lets it drop:
 *   Alone                         -> Sad      (1/3 coins)
 *   Likes more roommates          -> Happy    (all coins)
 *   Dislikes more roommates       -> Angry    (no coins)
 *   Same number of each (or none) -> Neutral  (2/3 coins)
 * Moods are worked out when asked for, so they're always up to date
 * If the alien's liked artifact is placed in its room, it gets a bonus before the mood scales it
*/

public enum AlienMood { Happy, Neutral, Sad, Angry }

public class AlienMoods : MonoBehaviour
{
    public static AlienMoods main { get; private set; }

    [Tooltip("Which personality each eyes + mouth combo has.")]
    [SerializeField] private PersonalityTable personalities;

    [Header("Emotion sprites")]
    [SerializeField] private Sprite happySprite;
    [SerializeField] private Sprite neutralSprite;
    [SerializeField] private Sprite sadSprite;
    [SerializeField] private Sprite angrySprite;

    [Header("Coins (fraction of the alien's normal drop)")]
    [Range(0f, 1f)] [SerializeField] private float happyCoins = 1f;
    [Range(0f, 1f)] [SerializeField] private float neutralCoins = 2f / 3f;
    [Range(0f, 1f)] [SerializeField] private float sadCoins = 1f / 3f;
    [Range(0f, 1f)] [SerializeField] private float angryCoins = 0f;

    [Header("Artifacts")]
    [Tooltip("Extra coins per drop when the alien's liked artifact is placed in its room. Added before the mood scales it.")]
    [SerializeField] private int likedArtifactBonus = 5;

    private void Awake()
    {
        if (main != null && main != this)
        {
            Debug.LogWarning("More than one AlienMoods in the scene. Destroying the extra one.", this);
            Destroy(this);
            return;
        }
        main = this;

        if (personalities == null)
            Debug.LogWarning("AlienMoods has no Personality Table, so every alien will be Neutral or Sad.", this);
    }

    private void OnDestroy()
    {
        if (main == this) main = null;
    }

    public Personality PersonalityOf(OwnedAlien alien)
    {
        if (alien == null || personalities == null) return null;
        return personalities.Get(alien.species);
    }

    // The other aliens in its room right now (aliens away breeding aren't there)
    public List<OwnedAlien> RoommatesOf(OwnedAlien alien)
    {
        List<OwnedAlien> roommates = new List<OwnedAlien>();
        if (alien == null || AlienRoster.main == null) return roommates;

        foreach (OwnedAlien other in AlienRoster.main.InRoom(alien.roomIndex))
            if (other != alien) roommates.Add(other);
        return roommates;
    }

    public AlienMood MoodOf(OwnedAlien alien)
    {
        // Hunger beats everything else: nearly empty = Angry, half or less = Sad
        if (AlienHunger.main != null)
        {
            if (AlienHunger.main.IsStarving(alien)) return AlienMood.Angry;
            if (AlienHunger.main.IsHungry(alien)) return AlienMood.Sad;
        }

        List<OwnedAlien> roommates = RoommatesOf(alien);
        if (roommates.Count == 0) return AlienMood.Sad;

        Personality mine = PersonalityOf(alien);
        int liked = 0, disliked = 0;

        foreach (OwnedAlien other in roommates)
        {
            Personality theirs = PersonalityOf(other);
            if (mine == null || theirs == null) continue; // Unknown personality counts as neither
            if (mine.Likes(theirs)) liked++;
            if (mine.Dislikes(theirs)) disliked++;
        }

        // Majority decides; a tie (including 0 vs 0) is Neutral
        if (liked > disliked) return AlienMood.Happy;
        if (disliked > liked) return AlienMood.Angry;
        return AlienMood.Neutral;
    }

    public Sprite SpriteFor(AlienMood mood)
    {
        switch (mood)
        {
            case AlienMood.Happy: return happySprite;
            case AlienMood.Sad: return sadSprite;
            case AlienMood.Angry: return angrySprite;
            default: return neutralSprite;
        }
    }

    public float CoinFraction(AlienMood mood)
    {
        switch (mood)
        {
            case AlienMood.Happy: return happyCoins;
            case AlienMood.Sad: return sadCoins;
            case AlienMood.Angry: return angryCoins;
            default: return neutralCoins;
        }
    }

    // Is the artifact this alien likes placed in its room? (Two copies still only count once)
    public bool HasLikedArtifact(OwnedAlien alien)
    {
        if (alien == null || alien.species == null || ArtifactInventory.main == null) return false;

        ArtifactDefinition liked = alien.species.LikedArtifact;
        if (liked == null) return false;

        foreach (OwnedArtifact artifact in ArtifactInventory.main.InRoom(alien.roomIndex))
            if (artifact.definition == liked) return true;
        return false;
    }

    // The alien's coins before mood: its normal amount, plus the bonus if its liked artifact is in the room
    public int BaseCoinsPerDrop(OwnedAlien alien)
    {
        if (alien == null || alien.species == null) return 0;
        return alien.species.CoinsPerDrop + (HasLikedArtifact(alien) ? likedArtifactBonus : 0);
    }

    // What this alien drops this time, rounded to whole coins (5 coins Sad = 1.67 -> 2)
    public int CoinsPerDrop(OwnedAlien alien)
    {
        if (alien == null || alien.species == null) return 0;
        return Mathf.RoundToInt(BaseCoinsPerDrop(alien) * CoinFraction(MoodOf(alien))); // BaseCoinsPerDrop instead of species.CoinsPerDrop
    }


    // TESTING

    [ContextMenu("Testing: Print Moods")]
    private void PrintMoods()
    {
        if (AlienRoster.main == null) { Debug.LogWarning("Press Play first.", this); return; }

        StringBuilder text = new StringBuilder("Moods:\n");
        foreach (OwnedAlien alien in AlienRoster.main.All)
        {
            if (alien == null || alien.species == null) continue;
            Personality p = PersonalityOf(alien);
            List<string> names = new List<string>();
            foreach (OwnedAlien other in RoommatesOf(alien)) names.Add(other.DisplayName);

            text.AppendLine($"  {alien.DisplayName} (Room {alien.roomIndex + 1}) - " +
                            $"{(p != null ? p.shortName : "no personality")}, " +
                            $"with [{string.Join(", ", names)}], {alien.fullness:P0} full -> {MoodOf(alien)}, " +
                            $"{CoinsPerDrop(alien)}/{BaseCoinsPerDrop(alien)} coins" + // BaseCoinsPerDrop
                            (HasLikedArtifact(alien) ? $" (has its {alien.species.LikedArtifact.displayName})" : "") +
                            (alien.inBreedingRoom ? " (away breeding)" : ""));
        }
        Debug.Log(text.ToString(), this);
    }
}

/* ver.1
public enum AlienMood { Happy, Neutral, Sad, Angry }

public class AlienMoods : MonoBehaviour
{
    public static AlienMoods main { get; private set; }

    [Tooltip("Which personality each eyes + mouth combo has.")]
    [SerializeField] private PersonalityTable personalities;

    [Header("Emotion sprites")]
    [SerializeField] private Sprite happySprite;
    [SerializeField] private Sprite neutralSprite;
    [SerializeField] private Sprite sadSprite;
    [SerializeField] private Sprite angrySprite;

    [Header("Coins (fraction of the alien's normal drop)")]
    [Range(0f, 1f)] [SerializeField] private float happyCoins = 1f;
    [Range(0f, 1f)] [SerializeField] private float neutralCoins = 2f / 3f;
    [Range(0f, 1f)] [SerializeField] private float sadCoins = 1f / 3f;
    [Range(0f, 1f)] [SerializeField] private float angryCoins = 0f;

    [Header("Artifacts")]
    [Tooltip("Extra coins per drop when the alien's liked artifact is placed in its room. Added before the mood scales it.")]
    [SerializeField] private int likedArtifactBonus = 5;

    private void Awake()
    {
        if (main != null && main != this)
        {
            Debug.LogWarning("More than one AlienMoods in the scene. Destroying the extra one.", this);
            Destroy(this);
            return;
        }
        main = this;

        if (personalities == null)
            Debug.LogWarning("AlienMoods has no Personality Table, so every alien will be Neutral or Sad.", this);
    }

    private void OnDestroy()
    {
        if (main == this) main = null;
    }

    public Personality PersonalityOf(OwnedAlien alien)
    {
        if (alien == null || personalities == null) return null;
        return personalities.Get(alien.species);
    }

    // The other aliens in its room right now (aliens away breeding aren't there)
    public List<OwnedAlien> RoommatesOf(OwnedAlien alien)
    {
        List<OwnedAlien> roommates = new List<OwnedAlien>();
        if (alien == null || AlienRoster.main == null) return roommates;

        foreach (OwnedAlien other in AlienRoster.main.InRoom(alien.roomIndex))
            if (other != alien) roommates.Add(other);
        return roommates;
    }

    public AlienMood MoodOf(OwnedAlien alien)
    {
        List<OwnedAlien> roommates = RoommatesOf(alien);
        if (roommates.Count == 0) return AlienMood.Sad;

        Personality mine = PersonalityOf(alien);
        int liked = 0, disliked = 0;

        foreach (OwnedAlien other in roommates)
        {
            Personality theirs = PersonalityOf(other);
            if (mine == null || theirs == null) continue; // Unknown personality counts as neither
            if (mine.Likes(theirs)) liked++;
            if (mine.Dislikes(theirs)) disliked++;
        }

        // Majority decides; a tie (including 0 vs 0) is Neutral
        if (liked > disliked) return AlienMood.Happy;
        if (disliked > liked) return AlienMood.Angry;
        return AlienMood.Neutral;
    }

    public Sprite SpriteFor(AlienMood mood)
    {
        switch (mood)
        {
            case AlienMood.Happy: return happySprite;
            case AlienMood.Sad: return sadSprite;
            case AlienMood.Angry: return angrySprite;
            default: return neutralSprite;
        }
    }

    public float CoinFraction(AlienMood mood)
    {
        switch (mood)
        {
            case AlienMood.Happy: return happyCoins;
            case AlienMood.Sad: return sadCoins;
            case AlienMood.Angry: return angryCoins;
            default: return neutralCoins;
        }
    }

    // Is the artifact this alien likes placed in its room? (Two copies still only count once)
    public bool HasLikedArtifact(OwnedAlien alien)
    {
        if (alien == null || alien.species == null || ArtifactInventory.main == null) return false;

        ArtifactDefinition liked = alien.species.LikedArtifact;
        if (liked == null) return false;

        foreach (OwnedArtifact artifact in ArtifactInventory.main.InRoom(alien.roomIndex))
            if (artifact.definition == liked) return true;
        return false;
    }

    // The alien's coins before mood: its normal amount, plus the bonus if its liked artifact is in the room
    public int BaseCoinsPerDrop(OwnedAlien alien)
    {
        if (alien == null || alien.species == null) return 0;
        return alien.species.CoinsPerDrop + (HasLikedArtifact(alien) ? likedArtifactBonus : 0);
    }

    // What this alien drops this time, rounded to whole coins (5 coins Sad = 1.67 -> 2)
    public int CoinsPerDrop(OwnedAlien alien)
    {
        if (alien == null || alien.species == null) return 0;
        return Mathf.RoundToInt(BaseCoinsPerDrop(alien) * CoinFraction(MoodOf(alien))); // BaseCoinsPerDrop instead of species.CoinsPerDrop
    }


    // TESTING

    [ContextMenu("Testing: Print Moods")]
    private void PrintMoods()
    {
        if (AlienRoster.main == null) { Debug.LogWarning("Press Play first.", this); return; }

        StringBuilder text = new StringBuilder("Moods:\n");
        foreach (OwnedAlien alien in AlienRoster.main.All)
        {
            if (alien == null || alien.species == null) continue;
            Personality p = PersonalityOf(alien);
            List<string> names = new List<string>();
            foreach (OwnedAlien other in RoommatesOf(alien)) names.Add(other.DisplayName);

            text.AppendLine($"  {alien.DisplayName} (Room {alien.roomIndex + 1}) - " +
                            $"{(p != null ? p.shortName : "no personality")}, " +
                            $"with [{string.Join(", ", names)}] -> {MoodOf(alien)}, " +
                            $"{CoinsPerDrop(alien)}/{BaseCoinsPerDrop(alien)} coins" + // BaseCoinsPerDrop
                            (HasLikedArtifact(alien) ? $" (has its {alien.species.LikedArtifact.displayName})" : "") +
                            (alien.inBreedingRoom ? " (away breeding)" : ""));
        }
        Debug.Log(text.ToString(), this);
    }
}
*/