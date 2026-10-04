using UnityEngine;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Events;

/*
 * Runs breeding: which two aliens are being bred, the timer, and the egg
*/

public enum BreedingState { Idle, Breeding, EggReady }

// An egg sitting in a room, waiting to hatch
public class PlacedEgg
{
    public AlienSpecies baby;
    public int roomIndex;
    public float hatchAt;
}

public class BreedingManager : MonoBehaviour
{
    public static BreedingManager main { get; private set; }

    [Tooltip("Every alien that can exist, and the rarity weights.")]
    [SerializeField] private AlienCatalog catalog;

    [Tooltip("How long breeding takes, in seconds.")]
    [SerializeField] private float breedingSeconds = 5f;

    [Header("Rooms")]
    [Tooltip("Which rooms aliens can live in, as positions in RoomManager's Rooms list. " +
             "Shown to the player as Room 1, Room 2... in this order. Leave out the display rooms.")]
    [SerializeField] private int[] alienRooms = { 0, 1 };

    [Tooltip("Most aliens a room can hold. An egg waiting to hatch takes up a spot.")]
    [SerializeField] private int maxPerRoom = 4;

    [Tooltip("How long an egg takes to hatch once it's in a room, in seconds. " +
             "(Later this will depend on the baby's rarity.)")]
    [SerializeField] private float hatchSeconds = 5f;

    [Header("Debug")]
    [Tooltip("Print the odds and the result in the Console each time.")]
    [SerializeField] private bool logOdds = true;

    // Fires whenever the state changes, an egg is placed, or an egg hatches
    public UnityEvent onChanged = new UnityEvent();

    public BreedingState State { get; private set; } = BreedingState.Idle;
    public OwnedAlien ParentA { get; private set; }
    public OwnedAlien ParentB { get; private set; }

    // The baby inside the egg. Picked when breeding finishes; the player doesn't see it yet.
    public AlienSpecies EggContents { get; private set; }

    public float SecondsLeft => State == BreedingState.Breeding ? Mathf.Max(0f, breedingEndsAt - Time.time) : 0f;

    public IReadOnlyList<int> AlienRooms => alienRooms;
    public int MaxPerRoom => maxPerRoom;
    public IReadOnlyList<PlacedEgg> Eggs => eggs;

    private float breedingEndsAt;
    private readonly List<PlacedEgg> eggs = new List<PlacedEgg>();

    private void Awake()
    {
        if (main != null && main != this)
        {
            Debug.LogWarning("More than one BreedingManager in the scene. Destroying the extra one.", this);
            Destroy(this);
            return;
        }
        main = this;
    }

    private void OnDestroy()
    {
        if (main == this) main = null;
    }

    public bool StartBreeding(OwnedAlien parentA, OwnedAlien parentB)
    {
        if (State != BreedingState.Idle || parentA == null || parentB == null || parentA == parentB) return false;
        if (AlienRoster.main == null) return false;

        ParentA = parentA;
        ParentB = parentB;
        EggContents = null;

        // Taken out of their rooms while breeding
        AlienRoster.main.SetInBreedingRoom(parentA, true);
        AlienRoster.main.SetInBreedingRoom(parentB, true);

        breedingEndsAt = Time.time + breedingSeconds;
        State = BreedingState.Breeding;
        onChanged.Invoke();
        return true;
    }

    private void Update()
    {
        if (State == BreedingState.Breeding && Time.time >= breedingEndsAt) FinishBreeding();
        HatchReadyEggs();
    }

    private void FinishBreeding()
    {
        EggContents = PickBaby(ParentA.species, ParentB.species);

        // Parents go home as soon as the egg appears, so the room list is accurate
        AlienRoster.main.SetInBreedingRoom(ParentA, false);
        AlienRoster.main.SetInBreedingRoom(ParentB, false);

        State = BreedingState.EggReady;
        onChanged.Invoke();
    }

    private AlienSpecies PickBaby(AlienSpecies speciesA, AlienSpecies speciesB)
    {
        if (catalog == null)
        {
            Debug.LogWarning("BreedingManager has no Catalog, so the baby is a copy of the first parent.", this);
            return speciesA;
        }

        List<BabyChance> odds = catalog.OddsFor(speciesA, speciesB);
        AlienSpecies baby = BreedingOdds.Roll(odds);
        if (baby == null)
        {
            Debug.LogWarning($"No drawn babies for {speciesA.displayName} x {speciesB.displayName}, " +
                             "so the baby is a copy of the first parent.", this);
            baby = speciesA;
        }

        if (logOdds)
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine($"Bred {speciesA.displayName} x {speciesB.displayName}: got {baby.displayName}");
            foreach (BabyChance entry in odds)
                text.AppendLine($"  {entry.chance * 100f:0.00}%  {entry.species.displayName}");
            Debug.Log(text.ToString(), this);
        }

        return baby;
    }


    // ROOMS AND EGGS

    // Aliens that live in the room plus eggs waiting to hatch there
    public int Occupancy(int roomIndex)
    {
        int count = 0;
        if (AlienRoster.main != null)
        {
            foreach (OwnedAlien alien in AlienRoster.main.All)
                if (alien != null && alien.species != null && alien.roomIndex == roomIndex) count++;
        }
        foreach (PlacedEgg egg in eggs)
            if (egg.roomIndex == roomIndex) count++;
        return count;
    }

    public bool IsRoomFull(int roomIndex) => Occupancy(roomIndex) >= maxPerRoom;

    public bool PlaceEgg(int roomIndex)
    {
        if (State != BreedingState.EggReady || EggContents == null || IsRoomFull(roomIndex)) return false;

        eggs.Add(new PlacedEgg { baby = EggContents, roomIndex = roomIndex, hatchAt = Time.time + hatchSeconds });

        // Breeding is free again
        ParentA = null;
        ParentB = null;
        EggContents = null;
        State = BreedingState.Idle;
        onChanged.Invoke();
        return true;
    }

    private void HatchReadyEggs()
    {
        if (eggs.Count == 0 || AlienRoster.main == null) return;

        bool hatchedAny = false;
        for (int i = eggs.Count - 1; i >= 0; i--)
        {
            if (Time.time < eggs[i].hatchAt) continue;

            PlacedEgg egg = eggs[i];
            eggs.RemoveAt(i);
            AlienRoster.main.Add(egg.baby, egg.roomIndex); // Shows up in the room straight away
            Debug.Log($"An egg hatched into {egg.baby.displayName} in room {egg.roomIndex}.", this);
            hatchedAny = true;
        }

        if (hatchedAny) onChanged.Invoke();
    }

    // For testing: throws the egg away and unlocks the boxes
    [ContextMenu("Testing: Clear Egg")]
    public void ClearEggForTesting()
    {
        if (State == BreedingState.Breeding)
        {
            AlienRoster.main.SetInBreedingRoom(ParentA, false);
            AlienRoster.main.SetInBreedingRoom(ParentB, false);
        }

        ParentA = null;
        ParentB = null;
        EggContents = null;
        State = BreedingState.Idle;
        onChanged.Invoke();
    }
}

/* ver.1
public enum BreedingState { Idle, Breeding, EggReady }

public class BreedingManager : MonoBehaviour
{
    public static BreedingManager main { get; private set; }

    [Tooltip("Every alien that can exist, and the rarity weights.")]
    [SerializeField] private AlienCatalog catalog;

    [Tooltip("How long breeding takes, in seconds.")]
    [SerializeField] private float breedingSeconds = 5f;

    [Tooltip("Print the odds and the result in the Console each time.")]
    [SerializeField] private bool logOdds = true;

    // Fires whenever the state changes
    public UnityEvent onChanged = new UnityEvent();

    public BreedingState State { get; private set; } = BreedingState.Idle;
    public OwnedAlien ParentA { get; private set; }
    public OwnedAlien ParentB { get; private set; }

    // The baby inside the egg. Picked when breeding finishes; the player doesn't see it yet.
    public AlienSpecies EggContents { get; private set; }

    public float SecondsLeft => State == BreedingState.Breeding ? Mathf.Max(0f, breedingEndsAt - Time.time) : 0f;

    private float breedingEndsAt;

    private void Awake()
    {
        if (main != null && main != this)
        {
            Debug.LogWarning("More than one BreedingManager in the scene. Destroying the extra one.", this);
            Destroy(this);
            return;
        }
        main = this;
    }

    private void OnDestroy()
    {
        if (main == this) main = null;
    }

    public bool StartBreeding(OwnedAlien parentA, OwnedAlien parentB)
    {
        if (State != BreedingState.Idle || parentA == null || parentB == null || parentA == parentB) return false;
        if (AlienRoster.main == null) return false;

        ParentA = parentA;
        ParentB = parentB;
        EggContents = null;

        // Taken out of their rooms while breeding
        AlienRoster.main.SetInBreedingRoom(parentA, true);
        AlienRoster.main.SetInBreedingRoom(parentB, true);

        breedingEndsAt = Time.time + breedingSeconds;
        State = BreedingState.Breeding;
        onChanged.Invoke();
        return true;
    }

    private void Update()
    {
        if (State == BreedingState.Breeding && Time.time >= breedingEndsAt) FinishBreeding();
    }

    private void FinishBreeding()
    {
        EggContents = PickBaby(ParentA.species, ParentB.species);

        // Parents go home as soon as the egg appears, so the room list is accurate
        AlienRoster.main.SetInBreedingRoom(ParentA, false);
        AlienRoster.main.SetInBreedingRoom(ParentB, false);

        State = BreedingState.EggReady;
        onChanged.Invoke();
    }

    private AlienSpecies PickBaby(AlienSpecies speciesA, AlienSpecies speciesB)
    {
        if (catalog == null)
        {
            Debug.LogWarning("BreedingManager has no Catalog, so the baby is a copy of the first parent.", this);
            return speciesA;
        }

        List<BabyChance> odds = catalog.OddsFor(speciesA, speciesB);
        AlienSpecies baby = BreedingOdds.Roll(odds);
        if (baby == null)
        {
            Debug.LogWarning($"No drawn babies for {speciesA.displayName} x {speciesB.displayName}, " +
                             "so the baby is a copy of the first parent.", this);
            baby = speciesA;
        }

        if (logOdds)
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine($"Bred {speciesA.displayName} x {speciesB.displayName}: got {baby.displayName}");
            foreach (BabyChance entry in odds)
                text.AppendLine($"  {entry.chance * 100f:0.00}%  {entry.species.displayName}");
            Debug.Log(text.ToString(), this);
        }

        return baby;
    }

    // For testing until the room popup exists: throws the egg away and unlocks the boxes
    [ContextMenu("Testing: Clear Egg")]
    public void ClearEggForTesting()
    {
        if (State == BreedingState.Breeding)
        {
            AlienRoster.main.SetInBreedingRoom(ParentA, false);
            AlienRoster.main.SetInBreedingRoom(ParentB, false);
        }

        ParentA = null;
        ParentB = null;
        EggContents = null;
        State = BreedingState.Idle;
        onChanged.Invoke();
    }
}
*/