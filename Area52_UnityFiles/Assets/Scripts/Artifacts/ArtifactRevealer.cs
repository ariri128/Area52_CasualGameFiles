using UnityEngine;
using System.Collections.Generic;

/*
 * Shows the player what an exploring alien brought back
 * When an alien comes home, its artifact waits for the room the alien lives in:
 *   Looking at that room (in the apartments)  -> the Artifacts page opens straight away with it selected
 *   In another room, or the breeding room     -> it opens as soon as they're looking at that room again
 * If more than one is waiting in the same room, the page opens once with the newest selected (the rest are already in the list)
*/

public class ArtifactRevealer : MonoBehaviour
{
    [Tooltip("The Artifacts page. Leave empty to use the one in the scene.")]
    [SerializeField] private ArtifactCollectionPopup collectionPopup;

    [Tooltip("Optional. While this is open (the player is picking a room), the reveal waits so it doesn't interrupt.")]
    [SerializeField] private RoomPickerPopup roomPicker;

    [Tooltip("Print each reveal in the Console.")]
    [SerializeField] private bool logReveals = true;

    // Room index -> artifacts waiting to be shown in that room, oldest first
    private readonly Dictionary<int, List<OwnedArtifact>> waiting = new Dictionary<int, List<OwnedArtifact>>();
    private bool listening;

    private ArtifactCollectionPopup Popup => collectionPopup != null ? collectionPopup : ArtifactCollectionPopup.main;

    // Tries in both OnEnable and Start: ExplorationManager may not be set up yet the first time this runs
    private void OnEnable() => StartListening();
    private void Start() => StartListening();

    private void StartListening()
    {
        if (listening || ExplorationManager.main == null) return;
        ExplorationManager.main.onReturned.AddListener(OnAlienReturned);
        listening = true;
    }

    private void OnDisable()
    {
        if (listening && ExplorationManager.main != null) ExplorationManager.main.onReturned.RemoveListener(OnAlienReturned);
        listening = false;
    }

    // Holds the artifact for the room the alien lives in
    private void OnAlienReturned(OwnedAlien alien, OwnedArtifact found)
    {
        if (alien == null || found == null) return;

        if (!waiting.TryGetValue(alien.roomIndex, out List<OwnedArtifact> list))
        {
            list = new List<OwnedArtifact>();
            waiting[alien.roomIndex] = list;
        }
        list.Add(found);
    }

    private void Update()
    {
        if (waiting.Count == 0 || !CanRevealNow()) return;

        int room = RoomManager.main.CurrentRoomIndex;
        if (!waiting.TryGetValue(room, out List<OwnedArtifact> list)) return;
        waiting.Remove(room);

        // Newest one that's still owned
        OwnedArtifact newest = null;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (StillOwned(list[i])) { newest = list[i]; break; }
        }
        if (newest == null) return;

        Popup.Open(newest);
        if (logReveals) Debug.Log($"Showing {newest.DisplayName} in Room {room + 1}.", this);
    }

    // The player is in the apartments, the page exists, and they're not in the middle of picking a room
    private bool CanRevealNow()
    {
        if (RoomManager.main == null || Popup == null) return false;
        if (SceneFlow.main != null && SceneFlow.main.InBreedingRoom) return false;
        if (roomPicker != null && roomPicker.IsOpen) return false;
        return true;
    }

    private static bool StillOwned(OwnedArtifact artifact)
    {
        if (artifact == null || ArtifactInventory.main == null) return false;
        foreach (OwnedArtifact owned in ArtifactInventory.main.All)
            if (owned == artifact) return true;
        return false;
    }
}