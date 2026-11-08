using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Events;

/*
 * The list of every alien the player owns, which room each one lives in, and whether it's currently away in the breeding room
*/

[System.Serializable]
public class OwnedAlien
{
    [HideInInspector] public string name; // Inspector label

    public AlienSpecies species;

    [Tooltip("Which room it lives in: its position in RoomManager's Rooms list (0 = first room).")]
    public int roomIndex;

    [Tooltip("True while it's away being bred. It isn't shown in its room then.")]
    public bool inBreedingRoom;

    [Tooltip("True while it's away exploring. Set by ExplorationManager - don't tick by hand.")]
    public bool isExploring;

    [Tooltip("How full its belly is: 1 = full, 0 = empty. Starts full. Drag it during Play to test hunger.")]
    [Range(0f, 1f)] public float fullness = 1f;

    [Tooltip("Name the player gave it. Empty = uses the species name.")]
    public string customName;

    [Tooltip("Filled in automatically: the day the game started for starting aliens, the hatch day for babies.")]
    public string dateAcquired;

    [Tooltip("Every artifact it has brought back from exploring, in the order it found them. " +
             "Added by ExplorationManager. Kept even if the artifact is sold later.")]
    public List<ArtifactDefinition> collectedArtifacts = new List<ArtifactDefinition>();

    [Tooltip("True if it was bought from the shop. Its file shows \"Bought\" as its parents.")]
    public bool wasBought;

    // The two aliens that made this one (null for starting aliens)
    // NonSerialized: Unity can't save an alien inside another alien, and it doesn't need to
    [System.NonSerialized] public OwnedAlien parentA;
    [System.NonSerialized] public OwnedAlien parentB;

    public string DisplayName =>
        !string.IsNullOrEmpty(customName) ? customName : (species != null ? species.displayName : "(no species)");

    public bool HasParents => parentA != null || parentB != null;

    // Away breeding or away exploring: not in its room right now, but it's coming back
    public bool IsAway => inBreedingRoom || isExploring;

    public void RefreshLabel()
    {
        if (inBreedingRoom) name = $"{DisplayName} - breeding";
        else if (isExploring) name = $"{DisplayName} - exploring";
        else name = $"{DisplayName} - room {roomIndex}";
    }
}

public class AlienRoster : MonoBehaviour
{
    public static AlienRoster main { get; private set; }

    [Tooltip("Every alien the player owns. Fill in the starting aliens here.")]
    [SerializeField] private List<OwnedAlien> aliens = new List<OwnedAlien>();

    [Tooltip("Longest name the player can give an alien.")]
    [SerializeField] private int maxNameLength = 16;

    [Tooltip("Shown as the parents of the starting aliens.")]
    [SerializeField] private string noParentsText = "Original";

    [Tooltip("Shown as the parents of aliens bought from the shop.")]
    [SerializeField] private string boughtText = "Bought";

    [Tooltip("How dates are written, e.g. \"MMM d, yyyy\" = Oct 18, 2026.")]
    [SerializeField] private string dateFormat = "MMM d, yyyy";

    // Fires whenever an alien is added, sold, renamed, moved, or goes to / comes back from breeding or exploring
    public UnityEvent onChanged = new UnityEvent();

    public IReadOnlyList<OwnedAlien> All => aliens;
    public int MaxNameLength => maxNameLength;

    private void Awake()
    {
        if (main != null && main != this)
        {
            Debug.LogWarning("More than one AlienRoster in the scene. Destroying the extra one.", this);
            Destroy(this);
            return;
        }
        main = this;

        // Starting aliens were "acquired" today (no saving yet, so this is every time Play starts)
        foreach (OwnedAlien alien in aliens)
        {
            if (alien == null) continue;
            if (string.IsNullOrEmpty(alien.dateAcquired)) alien.dateAcquired = Today();
            alien.RefreshLabel();
        }
    }

    private void OnDestroy()
    {
        if (main == this) main = null;
    }

    private string Today() => System.DateTime.Now.ToString(dateFormat);

    // Aliens that should be showing in this room right now
    public List<OwnedAlien> InRoom(int roomIndex)
    {
        List<OwnedAlien> result = new List<OwnedAlien>();
        foreach (OwnedAlien alien in aliens)
        {
            if (alien != null && alien.species != null && !alien.IsAway && alien.roomIndex == roomIndex)
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
            if (alien != null && alien.species != null && !alien.IsAway)
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

    public void SetExploring(OwnedAlien alien, bool exploring)
    {
        if (alien == null || alien.isExploring == exploring) return;
        alien.isExploring = exploring;
        alien.RefreshLabel();
        onChanged.Invoke();
    }

    // For new babies, and aliens bought from the shop (bought: true)
    public OwnedAlien Add(AlienSpecies species, int roomIndex, OwnedAlien parentA = null, OwnedAlien parentB = null, bool bought = false)
    {
        OwnedAlien alien = new OwnedAlien
        {
            species = species,
            roomIndex = roomIndex,
            dateAcquired = Today(),
            parentA = parentA,
            parentB = parentB,
            wasBought = bought,
        };
        alien.RefreshLabel();
        aliens.Add(alien);
        onChanged.Invoke();
        return alien;
    }


    // NAMES, PARENTS, MOVING

    // Empty or blank goes back to the species name. Returns the name it ended up with.
    public string Rename(OwnedAlien alien, string newName)
    {
        if (alien == null) return "";

        newName = (newName ?? "").Trim();
        if (newName.Length > maxNameLength) newName = newName.Substring(0, maxNameLength);

        // Typing the species name itself is the same as no custom name
        bool isSpeciesName = alien.species != null && newName == alien.species.displayName;
        alien.customName = isSpeciesName ? "" : newName;

        alien.RefreshLabel();
        onChanged.Invoke();
        return alien.DisplayName;
    }

    // "Parent 1 x Parent 2", or "Original" for starting aliens
    public string ParentsText(OwnedAlien alien)
    {
        if (alien != null && alien.wasBought) return boughtText;
        if (alien == null || !alien.HasParents) return noParentsText;
        string a = alien.parentA != null ? alien.parentA.DisplayName : "?";
        string b = alien.parentB != null ? alien.parentB.DisplayName : "?";
        return $"{a} x {b}";
    }

    public bool CanMoveTo(OwnedAlien alien, int roomIndex)
    {
        if (alien == null || alien.roomIndex == roomIndex) return false;
        if (RoomManager.main != null && (roomIndex < 0 || roomIndex >= RoomManager.main.RoomCount)) return false;
        return BreedingManager.main == null || !BreedingManager.main.IsRoomFull(roomIndex);
    }

    // Moves an alien to a room with space - returns false (and changes nothing) if it can't
    public bool MoveTo(OwnedAlien alien, int roomIndex)
    {
        if (!CanMoveTo(alien, roomIndex)) return false;

        alien.roomIndex = roomIndex;
        alien.RefreshLabel();
        onChanged.Invoke();
        return true;
    }

    // For selling - the alien is gone for good
    // Babies it had still show it as a parent (they keep their own link to it)
    public bool Remove(OwnedAlien alien)
    {
        if (alien == null || !aliens.Remove(alien)) return false;
        onChanged.Invoke(); // Moods, coins, room counts, the Alien Files... all update
        return true;
    }


    // TESTING

    [ContextMenu("Testing: Print Roster")]
    private void PrintRoster()
    {
        foreach (OwnedAlien alien in aliens)
        {
            if (alien == null) continue;
            Debug.Log($"{alien.DisplayName} ({alien.species?.displayName}) - Room {alien.roomIndex + 1}, " +
                      $"acquired {alien.dateAcquired}, parents: {ParentsText(alien)}", this);
        }
    }

    // Keeps the Inspector labels readable, and lets you test by changing values during Play
    private void OnValidate()
    {
        foreach (OwnedAlien alien in aliens)
            if (alien != null) alien.RefreshLabel();

        if (Application.isPlaying && main == this) onChanged.Invoke();
    }
}