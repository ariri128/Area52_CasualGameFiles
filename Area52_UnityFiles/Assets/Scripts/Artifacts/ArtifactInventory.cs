using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Events;

/*
 * Every artifact the player owns, and which room each one is placed in (or Unplaced)
 * Owning two of the same artifact = two entries, so each gets its own box in the collection page
 * A room can hold up to Max Per Room artifacts
*/

[System.Serializable]
public class OwnedArtifact
{
    [HideInInspector] public string name; // Inspector label

    public ArtifactDefinition definition;

    [Tooltip("Which room it's placed in: its position in RoomManager's Rooms list (0 = first room). -1 = Unplaced.")]
    public int roomIndex = -1;

    [Tooltip("Shows NEW on the Artifacts page until the player has scrolled to it.")]
    public bool isNew;

    public bool IsPlaced => roomIndex >= 0;

    public string DisplayName => definition != null ? definition.displayName : "(no artifact)";

    public void RefreshLabel()
    {
        name = IsPlaced ? $"{DisplayName} - room {roomIndex}" : $"{DisplayName} - unplaced";
    }
}

public class ArtifactInventory : MonoBehaviour
{
    public static ArtifactInventory main { get; private set; }

    [Tooltip("Every artifact that can be found.")]
    [SerializeField] private ArtifactCatalog catalog;

    [Tooltip("Most artifacts one room can hold.")]
    [SerializeField] private int maxPerRoom = 4;

    [Tooltip("Artifacts the player owns. Add some here to test before exploring works.")]
    [SerializeField] private List<OwnedArtifact> artifacts = new List<OwnedArtifact>();

    // Fires whenever an artifact is added or moved to a room
    public UnityEvent onChanged = new UnityEvent();

    public IReadOnlyList<OwnedArtifact> All => artifacts;
    public int MaxPerRoom => maxPerRoom;

    private void Awake()
    {
        if (main != null && main != this)
        {
            Debug.LogWarning("More than one ArtifactInventory in the scene. Destroying the extra one.", this);
            Destroy(this);
            return;
        }
        main = this;

        foreach (OwnedArtifact artifact in artifacts)
            if (artifact != null) artifact.RefreshLabel();
    }

    private void OnDestroy()
    {
        if (main == this) main = null;
    }


    // ROOMS

    // Artifacts placed in this room, in the order they were placed
    public List<OwnedArtifact> InRoom(int roomIndex)
    {
        List<OwnedArtifact> result = new List<OwnedArtifact>();
        foreach (OwnedArtifact artifact in artifacts)
            if (artifact != null && artifact.definition != null && artifact.roomIndex == roomIndex)
                result.Add(artifact);
        return result;
    }

    public int CountInRoom(int roomIndex) => InRoom(roomIndex).Count;

    public bool IsRoomFull(int roomIndex) => CountInRoom(roomIndex) >= maxPerRoom;

    public bool CanPlace(OwnedArtifact artifact, int roomIndex)
    {
        if (artifact == null || artifact.roomIndex == roomIndex) return false;
        if (RoomManager.main != null && (roomIndex < 0 || roomIndex >= RoomManager.main.RoomCount)) return false;
        return !IsRoomFull(roomIndex);
    }

    // Puts an artifact in a room (or moves it there from another room) - returns false (and changes nothing) if it can't
    public bool PlaceInRoom(OwnedArtifact artifact, int roomIndex)
    {
        if (!CanPlace(artifact, roomIndex)) return false;

        artifact.roomIndex = roomIndex;
        artifact.RefreshLabel();
        onChanged.Invoke();
        return true;
    }

    // Takes an artifact out of its room - it goes back to Unplaced
    public bool Unplace(OwnedArtifact artifact)
    {
        if (artifact == null || !artifact.IsPlaced) return false;

        artifact.roomIndex = -1;
        artifact.RefreshLabel();
        onChanged.Invoke();
        return true;
    }


    // GETTING NEW ONES

    // New artifacts start Unplaced
    public OwnedArtifact Add(ArtifactDefinition definition)
    {
        if (definition == null) return null;

        OwnedArtifact artifact = new OwnedArtifact { definition = definition, roomIndex = -1, isNew = true };
        artifact.RefreshLabel();
        artifacts.Add(artifact);
        onChanged.Invoke();
        return artifact;
    }

    // What an exploring alien brings back
    public OwnedArtifact AddRandom()
    {
        if (catalog == null)
        {
            Debug.LogWarning("ArtifactInventory has no Catalog, so no artifact could be picked.", this);
            return null;
        }

        ArtifactDefinition found = catalog.PickRandom();
        if (found == null)
        {
            Debug.LogWarning("The Artifact Catalog has nothing that can be found.", this);
            return null;
        }
        return Add(found);
    }

    // Called by the Artifacts page once the player has actually seen this one on screen
    // Doesn't fire onChanged on purpose: the NEW tag stays up until the page is closed
    public void MarkSeen(OwnedArtifact artifact)
    {
        if (artifact != null) artifact.isNew = false;
    }


    // TESTING

    [ContextMenu("Testing: Add Random Artifact")]
    private void AddRandomForTesting()
    {
        if (!Application.isPlaying) { Debug.LogWarning("Press Play first.", this); return; }
        OwnedArtifact artifact = AddRandom();
        if (artifact != null) Debug.Log($"Got {artifact.DisplayName}.", this);
    }

    [ContextMenu("Testing: Print Artifacts")]
    private void PrintArtifacts()
    {
        foreach (OwnedArtifact artifact in artifacts)
        {
            if (artifact == null) continue;
            Debug.Log(artifact.IsPlaced ? $"{artifact.DisplayName} - Room {artifact.roomIndex + 1}"
                                        : $"{artifact.DisplayName} - Unplaced", this);
        }
    }

    // Keeps the Inspector labels readable, and lets you test by changing rooms during Play
    private void OnValidate()
    {
        foreach (OwnedArtifact artifact in artifacts)
            if (artifact != null) artifact.RefreshLabel();

        if (Application.isPlaying && main == this) onChanged.Invoke();
    }
}