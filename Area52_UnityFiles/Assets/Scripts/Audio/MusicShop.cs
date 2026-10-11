using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Events;

/*
 * The radio's tracks: which ones the player owns, what the others cost, and which one is playing as background music
 * Buying spends coins and makes the track owned; using a track makes it the background music (owned tracks only)
*/

public class MusicShop : MonoBehaviour
{
    [System.Serializable]
    public class Track
    {
        public string displayName = "Track";
        public AudioClip clip;
        public int cost = 350;
        [Tooltip("The player owns this from the start (the default track).")]
        public bool ownedAtStart;
    }

    public static MusicShop main { get; private set; }

    [SerializeField] private List<Track> tracks = new List<Track>();

    [Tooltip("Position in Tracks of the music the game starts with.")]
    [SerializeField] private int startingTrack = 0;

    // Fires when a track is bought or the background track changes
    public UnityEvent onChanged = new UnityEvent();

    public IReadOnlyList<Track> Tracks => tracks;
    public Track CurrentTrack { get; private set; }

    private readonly HashSet<Track> owned = new HashSet<Track>();

    private void Awake()
    {
        if (main != null && main != this)
        {
            Debug.LogWarning("More than one MusicShop in the scene. Destroying the extra one.", this);
            Destroy(this);
            return;
        }
        main = this;

        foreach (Track track in tracks)
            if (track.ownedAtStart) owned.Add(track);

        if (startingTrack >= 0 && startingTrack < tracks.Count)
        {
            CurrentTrack = tracks[startingTrack];
            owned.Add(CurrentTrack);
        }
    }

    private void OnDestroy()
    {
        if (main == this) main = null;
    }

    private void Start()
    {
        // Makes sure the AudioManager is playing the starting track
        if (CurrentTrack != null && AudioManager.main != null) AudioManager.main.SetMusic(CurrentTrack.clip);
    }

    public bool IsOwned(Track track) => track != null && owned.Contains(track);

    public bool CanAfford(Track track) =>
        track != null && CoinBank.main != null && CoinBank.main.CanAfford(track.cost);

    public bool Buy(Track track)
    {
        if (track == null || IsOwned(track) || CoinBank.main == null) return false;
        if (!CoinBank.main.TrySpend(track.cost)) return false; // Not enough coins: nothing happens

        owned.Add(track);
        Debug.Log($"Bought {track.displayName} for {track.cost} coins.", this);
        onChanged.Invoke();
        return true;
    }

    // Makes an owned track the background music
    public bool Use(Track track)
    {
        if (!IsOwned(track) || track.clip == null) return false;

        CurrentTrack = track;
        if (AudioManager.main != null) AudioManager.main.SetMusic(track.clip);
        onChanged.Invoke();
        return true;
    }
}