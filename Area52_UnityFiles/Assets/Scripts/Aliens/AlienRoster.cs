using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Events;

/*
 * The list of every alien the player owns, which room each one lives in, and whether it's currently away in the breeding room
*/

[System.Serializable]
public class OwnedAlien
{
    [HideInInspector] public string name;

    public AlienSpecies species;

    [Tooltip("Which room it lives in: its position in RoomManager's Rooms list (0 = first room).")]
    public int roomIndex;

    [Tooltip("True while it's away being bred. It isn't shown in its room then.")]
    public bool inBreedingRoom;

    public void RefreshLabel()
    {
        string speciesName = species != null ? species.displayName : "(no species)";
        name = inBreedingRoom ? $"{speciesName} - breeding" : $"{speciesName} - room {roomIndex}";
    }
}

public class AlienRoster : MonoBehaviour
{
    public static AlienRoster main { get; private set; }

    [Tooltip("Every alien the player owns. Fill in the starting aliens here.")]
    [SerializeField] private List<OwnedAlien> aliens = new List<OwnedAlien>();

    // Fires whenever an alien is added, moved, or goes to / comes back from breeding
    public UnityEvent onChanged = new UnityEvent();

    public IReadOnlyList<OwnedAlien> All => aliens;

    private void Awake()
    {
        if (main != null && main != this)
        {
            Debug.LogWarning("More than one AlienRoster in the scene. Destroying the extra one.", this);
            Destroy(this);
            return;
        }
        main = this;
    }

    private void OnDestroy()
    {
        if (main == this) main = null;
    }

    // Aliens that should be showing in this room right now
    public List<OwnedAlien> InRoom(int roomIndex)
    {
        List<OwnedAlien> result = new List<OwnedAlien>();
        foreach (OwnedAlien alien in aliens)
        {
            if (alien != null && alien.species != null && !alien.inBreedingRoom && alien.roomIndex == roomIndex)
                result.Add(alien);
        }
        return result;
    }

    // Aliens that can be picked in the breeding boxes
    public List<OwnedAlien> AvailableForBreeding()
    {
        List<OwnedAlien> result = new List<OwnedAlien>();
        foreach (OwnedAlien alien in aliens)
        {
            if (alien != null && alien.species != null && !alien.inBreedingRoom)
                result.Add(alien);
        }
        return result;
    }

    public void SetInBreedingRoom(OwnedAlien alien, bool inBreedingRoom)
    {
        if (alien == null || alien.inBreedingRoom == inBreedingRoom) return;
        alien.inBreedingRoom = inBreedingRoom;
        alien.RefreshLabel();
        onChanged.Invoke();
    }

    // For new babies
    public OwnedAlien Add(AlienSpecies species, int roomIndex)
    {
        OwnedAlien alien = new OwnedAlien { species = species, roomIndex = roomIndex };
        alien.RefreshLabel();
        aliens.Add(alien);
        onChanged.Invoke();
        return alien;
    }

    // Keeps the Inspector labels readable, and lets you test by ticking boxes during Play
    private void OnValidate()
    {
        foreach (OwnedAlien alien in aliens)
            if (alien != null) alien.RefreshLabel();

        if (Application.isPlaying && main == this) onChanged.Invoke();
    }
}