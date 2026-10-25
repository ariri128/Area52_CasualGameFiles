using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Events;

/*
 * Sends aliens out exploring and brings them back with a random artifact
 *   Send(alien) - the alien flies up off screen and is away for Explore Seconds
 *   When time's up it drops back into its room, and a random artifact is added to the inventory (Unplaced)
 * The timer keeps running whichever room the player is in, including the breeding room
 * The sound plays when an alien leaves and when one comes back, wherever the player is
*/

public class ExplorationManager : MonoBehaviour
{
    public static ExplorationManager main { get; private set; }

    // One alien that's out exploring
    private class Expedition
    {
        public OwnedAlien alien;
        public float returnAt;
    }

    [Tooltip("How long an alien is away, in seconds (10 for testing).")]
    [SerializeField] private float exploreSeconds = 15f;

    [Header("Sound")]
    [Tooltip("Plays when an alien leaves, and again when it comes back.")]
    [SerializeField] private AudioClip exploreSound;
    [Range(0f, 1f)]
    [SerializeField] private float soundVolume = 1f;

    // Fires when an alien comes home: the alien, and the artifact it found (null if nothing could be found)
    public UnityEvent<OwnedAlien, OwnedArtifact> onReturned = new UnityEvent<OwnedAlien, OwnedArtifact>();

    private readonly List<Expedition> away = new List<Expedition>();

    private void Awake()
    {
        if (main != null && main != this)
        {
            Debug.LogWarning("More than one ExplorationManager in the scene. Destroying the extra one.", this);
            Destroy(this);
            return;
        }
        main = this;
    }

    private void OnDestroy()
    {
        if (main == this) main = null;
    }

    public bool IsExploring(OwnedAlien alien) => FindExpedition(alien) != null;

    // For later (e.g. a countdown if added in the future)
    public float SecondsLeft(OwnedAlien alien)
    {
        Expedition expedition = FindExpedition(alien);
        return expedition != null ? Mathf.Max(0f, expedition.returnAt - Time.time) : 0f;
    }

    public bool CanExplore(OwnedAlien alien)
    {
        return alien != null && alien.species != null && !alien.IsAway && AlienRoster.main != null;
    }

    public bool Send(OwnedAlien alien)
    {
        if (!CanExplore(alien)) return false;

        // Let go of its sprite first, so the room respawning its aliens doesn't delete it mid-flight
        FloatingAlien visual = AlienRoomDisplay.main != null ? AlienRoomDisplay.main.Detach(alien) : null;
        if (visual != null) visual.FlyAway();

        away.Add(new Expedition { alien = alien, returnAt = Time.time + exploreSeconds });
        AlienRoster.main.SetExploring(alien, true); // Leaves the room: moods, coins and breeding all update

        AudioManager.PlaySound(exploreSound, soundVolume);
        Debug.Log($"{alien.DisplayName} went exploring from Room {alien.roomIndex + 1} ({exploreSeconds}s).", this);
        return true;
    }

    private void Update()
    {
        for (int i = away.Count - 1; i >= 0; i--)
        {
            if (Time.time < away[i].returnAt) continue;

            OwnedAlien alien = away[i].alien;
            away.RemoveAt(i);
            Return(alien);
        }
    }

    private void Return(OwnedAlien alien)
    {
        // Back in the roster -> the room spawns it again, then that new sprite drops in from above
        if (AlienRoster.main != null) AlienRoster.main.SetExploring(alien, false);
        FloatingAlien visual = AlienRoomDisplay.main != null ? AlienRoomDisplay.main.Find(alien) : null;
        if (visual != null) visual.FlyIn();

        OwnedArtifact found = ArtifactInventory.main != null ? ArtifactInventory.main.AddRandom() : null;

        AudioManager.PlaySound(exploreSound, soundVolume);
        Debug.Log($"{alien.DisplayName} came back to Room {alien.roomIndex + 1} with " +
                  $"{(found != null ? found.DisplayName : "nothing")}.", this);

        onReturned.Invoke(alien, found);
    }

    private Expedition FindExpedition(OwnedAlien alien)
    {
        foreach (Expedition expedition in away)
            if (expedition.alien == alien) return expedition;
        return null;
    }


    // TESTING

    [ContextMenu("Testing: Bring Everyone Back Now")]
    private void ReturnAllForTesting()
    {
        foreach (Expedition expedition in away) expedition.returnAt = Time.time;
    }
}