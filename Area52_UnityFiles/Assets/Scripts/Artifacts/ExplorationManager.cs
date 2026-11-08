using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Events;

/*
 * Sends aliens out exploring and brings them back with a random artifact
 *   Send(alien) - the spaceship comes down into the room, takes the alien, and flies off (the sound plays as it leaves)
 *   After Explore Seconds, the ship brings it back and drops it off in its room, and a random artifact is added (Unplaced)
 * Only one alien per room can be out at a time: from the moment the ship is called until it has dropped the alien off and left
 * The sound also plays when the ship brings an alien back, wherever the player is
*/

public class ExplorationManager : MonoBehaviour
{
    public static ExplorationManager main { get; private set; }

    private enum Phase { PickingUp, Away, Returning }

    // One alien's trip, from calling the ship to being dropped back off
    private class Expedition
    {
        public OwnedAlien alien;
        public Phase phase;
        public float returnAt;
        public float readySince = -1f; // When the player started looking at its room with nothing open (-1 = not right now)
    }

    [Tooltip("How long an alien is away, in seconds, counted from when the ship leaves the room.")]
    [SerializeField] private float exploreSeconds = 15f;

    [Header("Coming back")]
    [Tooltip("Once the time's up, the alien only comes back while the player is looking at its room with nothing open. " +
             "That has to be true for this many seconds first.")]
    [SerializeField] private float returnDelay = 0.3f;

    [Tooltip("UI that stops aliens coming back while it's showing. Drag in the part of each popup that's shown and hidden " +
             "(its PanelRoot or Root), not the popup object itself. The thought bubble is already checked.")]
    [SerializeField] private GameObject[] blockingUI;

    [Header("Spaceship")]
    [Tooltip("The spaceship prefab (has the ExplorationShip script).")]
    [SerializeField] private ExplorationShip shipPrefab;

    [Tooltip("Where the ship stops in the room, from the room's origin (front-center of the floor). " +
             "Y = how high, Z = how far back. Can be changed during Play - the next ship uses it.")]
    [SerializeField] private Vector3 shipStop = new Vector3(0f, 2f, 0.4f);

    [Header("Sound")]
    [Tooltip("Plays as the ship leaves with an alien, and again when it brings one back.")]
    [SerializeField] private AudioClip exploreSound;
    [Range(0f, 1f)]
    [SerializeField] private float soundVolume = 1f;

    // Fires when an alien is dropped back off: the alien, and the artifact it found (null if nothing could be found)
    public UnityEvent<OwnedAlien, OwnedArtifact> onReturned = new UnityEvent<OwnedAlien, OwnedArtifact>();

    private readonly List<Expedition> expeditions = new List<Expedition>();

    private int shipsTakingAway; // Sold aliens' ships still on screen - returning ships wait for them

    private void Awake()
    {
        if (main != null && main != this)
        {
            Debug.LogWarning("More than one ExplorationManager in the scene. Destroying the extra one.", this);
            Destroy(this);
            return;
        }
        main = this;

        if (shipPrefab == null) Debug.LogWarning("ExplorationManager has no Ship Prefab, so aliens can't go exploring.", this);
    }

    private void OnDestroy()
    {
        if (main == this) main = null;
    }


    // QUESTIONS

    public bool IsExploring(OwnedAlien alien) => FindExpedition(alien) != null;

    // Seconds until its time is up (0 while it's still picking up, waiting for the player, or already returning)
    public float SecondsLeft(OwnedAlien alien)
    {
        Expedition expedition = FindExpedition(alien);
        return expedition != null && expedition.phase == Phase.Away ? Mathf.Max(0f, expedition.returnAt - Time.time) : 0f;
    }

    // Is an alien from this room already out (or the ship still coming / going)?
    public bool IsRoomBusy(int roomIndex)
    {
        foreach (Expedition expedition in expeditions)
            if (expedition.alien.roomIndex == roomIndex) return true;
        return false;
    }

    public bool CanExplore(OwnedAlien alien)
    {
        return alien != null && alien.species != null && !alien.IsAway && AlienRoster.main != null
               && shipPrefab != null && !IsRoomBusy(alien.roomIndex);
    }

    // Is the player looking at this room right now (in the apartments, not the breeding room)?
    private bool IsPlayerLookingAt(int roomIndex)
    {
        if (RoomManager.main == null || RoomManager.main.CurrentRoomIndex != roomIndex) return false;
        return SceneFlow.main == null || !SceneFlow.main.InBreedingRoom;
    }

    // Is any popup, the thought bubble, or anything else in Blocking UI showing?
    public bool IsAnyUIOpen()
    {
        if (AlienTapper.main != null && AlienTapper.main.Selected != null) return true; // Thought bubble is up

        if (blockingUI != null)
            foreach (GameObject ui in blockingUI)
                if (ui != null && ui.activeInHierarchy) return true;

        return false;
    }


    // LEAVING

    public bool Send(OwnedAlien alien)
    {
        if (!CanExplore(alien)) return false;

        GameObject room = RoomManager.main != null ? RoomManager.main.GetRoomObject(alien.roomIndex) : null;
        if (room == null) return false;

        // Its sprite stays in the room, waiting, until the ship takes it
        FloatingAlien visual = AlienRoomDisplay.main != null ? AlienRoomDisplay.main.Detach(alien) : null;
        if (visual != null) visual.Paused = true;

        Expedition expedition = new Expedition { alien = alien, phase = Phase.PickingUp };
        expeditions.Add(expedition);
        AlienRoster.main.SetExploring(alien, true); // Counts as gone straight away: moods, coins and breeding all update

        ExplorationShip ship = Instantiate(shipPrefab, room.transform);
        ship.PickUp(shipStop, visual, alien.species.sprite, () => OnShipLeaving(expedition));

        Debug.Log($"The ship is coming for {alien.DisplayName} in Room {alien.roomIndex + 1}.", this);
        return true;
    }

    // The ship has the alien and is flying off - the timer starts now
    private void OnShipLeaving(Expedition expedition)
    {
        expedition.phase = Phase.Away;
        expedition.returnAt = Time.time + exploreSeconds;
        AudioManager.PlaySound(exploreSound, soundVolume);
        Debug.Log($"{expedition.alien.DisplayName} went exploring ({exploreSeconds}s).", this);
    }


    // SELLING

    public bool CanSendAway(OwnedAlien alien)
    {
        return alien != null && alien.species != null && !alien.IsAway && AlienRoster.main != null && shipPrefab != null;
    }

    // For selling: the ship comes down, takes the alien, and flies off - it never comes back
    // The alien is taken out of the roster straight away; onGone runs once the ship is off screen
    public bool SendAway(OwnedAlien alien, System.Action onGone)
    {
        if (!CanSendAway(alien)) return false;

        GameObject room = RoomManager.main != null ? RoomManager.main.GetRoomObject(alien.roomIndex) : null;
        if (room == null) return false;

        // Taken out of the room's list FIRST, so the roster change below doesn't delete it - the ship does that
        FloatingAlien visual = AlienRoomDisplay.main != null ? AlienRoomDisplay.main.Detach(alien) : null;
        if (visual != null) visual.Paused = true;

        Sprite sprite = alien.species.sprite;
        AlienRoster.main.Remove(alien); // Gone straight away: it can't be picked for breeding, counted for moods, etc.

        shipsTakingAway++;
        ExplorationShip ship = Instantiate(shipPrefab, room.transform);
        ship.PickUp(shipStop, visual, sprite,
                    () => AudioManager.PlaySound(exploreSound, soundVolume), // Same sound as exploring, as it leaves
                    () =>
                    {
                        shipsTakingAway--;
                        onGone?.Invoke();
                    });

        Debug.Log($"The ship is taking {alien.DisplayName} away for good.", this);
        return true;
    }


    // COMING BACK

    // Replaces the old Update
    private void Update()
    {
        bool uiOpen = IsAnyUIOpen() || shipsTakingAway > 0; // Never two ships on screen at once

        // Backwards, because bringing one back can remove it from the list
        for (int i = expeditions.Count - 1; i >= 0; i--)
        {
            Expedition expedition = expeditions[i];
            if (expedition.phase != Phase.Away) continue;

            // Keeps track of how long the player has been looking at its room with nothing open
            // (also while it's still away, so if they've been there all along it comes back right on time)
            if (uiOpen || !IsPlayerLookingAt(expedition.alien.roomIndex))
            {
                expedition.readySince = -1f;
                continue;
            }
            if (expedition.readySince < 0f) expedition.readySince = Time.time;

            bool timeUp = Time.time >= expedition.returnAt;
            bool readyLongEnough = Time.time - expedition.readySince >= returnDelay;
            if (timeUp && readyLongEnough) BringBack(expedition);
        }
    }

    private void BringBack(Expedition expedition)
    {
        expedition.phase = Phase.Returning;

        // Goes to the room it lives in NOW (it may have been moved while it was away)
        GameObject room = RoomManager.main != null ? RoomManager.main.GetRoomObject(expedition.alien.roomIndex) : null;
        if (room == null || shipPrefab == null)
        {
            DropOff(expedition, null);
            expeditions.Remove(expedition);
            return;
        }

        AudioManager.PlaySound(exploreSound, soundVolume);
        ExplorationShip ship = Instantiate(shipPrefab, room.transform);
        ship.DropOff(shipStop, expedition.alien.species.sprite,
                     glass => DropOff(expedition, glass),
                     () => expeditions.Remove(expedition)); // The room is free again once the ship has left
    }

    // The alien is back in its room, with what it found
    private void DropOff(Expedition expedition, Vector3? glass)
    {
        OwnedAlien alien = expedition.alien;

        // Picked BEFORE the alien is marked as back, so the roster's onChanged (from SetExploring) also
        // refreshes its file with the new artifact - no second onChanged, which would respawn the room's
        // aliens and cut off the fly-in below
        OwnedArtifact found = ArtifactInventory.main != null ? ArtifactInventory.main.AddRandom() : null;
        if (found != null && found.definition != null) alien.collectedArtifacts.Add(found.definition);

        // Back in the roster -> the room spawns it again, then it comes out of the glass to its spot
        if (AlienRoster.main != null) AlienRoster.main.SetExploring(alien, false);
        FloatingAlien visual = AlienRoomDisplay.main != null ? AlienRoomDisplay.main.Find(alien) : null;
        if (visual != null && glass.HasValue) visual.FlyInFrom(glass.Value);

        Debug.Log($"{alien.DisplayName} came back to Room {alien.roomIndex + 1} with " +
                  $"{(found != null ? found.DisplayName : "nothing")}.", this);

        onReturned.Invoke(alien, found);
    }

    private Expedition FindExpedition(OwnedAlien alien)
    {
        foreach (Expedition expedition in expeditions)
            if (expedition.alien == alien) return expedition;
        return null;
    }


    // TESTING

    // Only skips the time - it still waits for the player to be in the room with nothing open
    [ContextMenu("Testing: Bring Everyone Back Now")]
    private void ReturnAllForTesting()
    {
        foreach (Expedition expedition in expeditions)
            if (expedition.phase == Phase.Away) expedition.returnAt = Time.time;
    }

    [ContextMenu("Testing: Print Expeditions")]
    private void PrintExpeditions()
    {
        if (expeditions.Count == 0) { Debug.Log("No one is out exploring.", this); return; }

        foreach (Expedition expedition in expeditions)
        {
            string status = expedition.phase.ToString();
            if (expedition.phase == Phase.Away)
            {
                float left = expedition.returnAt - Time.time;
                if (left > 0f) status += $", {left:0.0}s left";
                else if (!IsPlayerLookingAt(expedition.alien.roomIndex)) status += ", time's up - waiting for the player to go to its room";
                else if (IsAnyUIOpen()) status += ", time's up - waiting for the UI to close";
                else status += ", time's up - coming back now";
            }
            Debug.Log($"{expedition.alien.DisplayName} (Room {expedition.alien.roomIndex + 1}): {status}", this);
        }
    }
}