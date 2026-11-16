using UnityEngine;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Events;

/*
 * Runs breeding: which two aliens are being bred, the timer, and the egg
 * The baby is picked when breeding STARTS, so how long it takes can depend on how rare the baby is (the player doesn't see it yet)
 * Timers use the real-world clock (SaveSystem.Now), so breeding and hatching keep going while the game is closed:
 *   breeding that finished while closed leaves the egg waiting in the spaceship; eggs that hatched while closed are babies in their room
*/

public enum BreedingState { Idle, Breeding, EggReady }

// An egg sitting in a room, waiting to hatch
public class PlacedEgg
{
    public AlienSpecies baby;
    public int roomIndex;
    public double hatchAt; // Real-world clock time it hatches
    public OwnedAlien parentA;
    public OwnedAlien parentB;
}

public class BreedingManager : MonoBehaviour
{
    public static BreedingManager main { get; private set; }

    [Tooltip("Every alien that can exist, and the rarity weights.")]
    [SerializeField] private AlienCatalog catalog;

    [Header("Breeding time (real-world seconds) - the rarer the baby, the longer it takes")]
    [Tooltip("1800 = 30 minutes.")]
    [SerializeField] private float commonBreedingSeconds = 1800f;
    [Tooltip("3600 = 60 minutes.")]
    [SerializeField] private float rareBreedingSeconds = 3600f;
    [Tooltip("5400 = 90 minutes.")]
    [SerializeField] private float ultraRareBreedingSeconds = 5400f;

    [Header("Rooms")]
    [Tooltip("Ticked: every room in RoomManager can hold aliens, including rooms bought " +
             "during the game. Unticked: only the rooms listed in Alien Rooms.")]
    [SerializeField] private bool allRoomsHoldAliens = true;

    [Tooltip("Only used when All Rooms Hold Aliens is unticked. Which rooms aliens can live in, as positions in RoomManager's Rooms list. " +
             "Shown to the player as Room 1, Room 2... in this order. Leave out the display rooms.")]
    [SerializeField] private int[] alienRooms = { 0, 1 };

    [Tooltip("Most aliens a room can hold. An egg waiting to hatch takes up a spot.")]
    [SerializeField] private int maxPerRoom = 4;

    [Tooltip("How long an egg takes to hatch once it's in a room, in real-world seconds, whatever the baby's rarity. 120 = 2 minutes.")]
    [SerializeField] private float hatchSeconds = 120f;

    [Header("Debug")]
    [Tooltip("Print the odds and the result in the Console each time.")]
    [SerializeField] private bool logOdds = true;

    // Fires whenever the state changes, an egg is placed, or an egg hatches
    public UnityEvent onChanged = new UnityEvent();

    public BreedingState State { get; private set; } = BreedingState.Idle;
    public OwnedAlien ParentA { get; private set; }
    public OwnedAlien ParentB { get; private set; }

    // The baby inside the egg - picked when breeding starts; the player doesn't see it until it hatches
    public AlienSpecies EggContents { get; private set; }

    public float SecondsLeft => State == BreedingState.Breeding ? (float)System.Math.Max(0.0, breedingEndsAt - SaveSystem.Now) : 0f;

    public IReadOnlyList<int> AlienRooms
    {
        get
        {
            if (!allRoomsHoldAliens || RoomManager.main == null) return alienRooms;

            List<int> all = new List<int>();
            for (int i = 0; i < RoomManager.main.RoomCount; i++) all.Add(i);
            return all;
        }
    }
    public int MaxPerRoom => maxPerRoom;
    public IReadOnlyList<PlacedEgg> Eggs => eggs;

    private double breedingEndsAt; // Real-world clock time breeding finishes
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
        EggContents = PickBaby(parentA.species, parentB.species); // Picked now, so its rarity decides how long this takes

        // Taken out of their rooms while breeding
        AlienRoster.main.SetInBreedingRoom(parentA, true);
        AlienRoster.main.SetInBreedingRoom(parentB, true);

        breedingEndsAt = SaveSystem.Now + BreedingSecondsFor(EggContents);
        State = BreedingState.Breeding;
        onChanged.Invoke();
        return true;
    }

    private void Update()
    {
        if (State == BreedingState.Breeding && SaveSystem.Now >= breedingEndsAt) FinishBreeding();
        HatchReadyEggs();
    }

    private void FinishBreeding()
    {
        // Parents go home as soon as the egg appears, so the room list is accurate
        AlienRoster.main.SetInBreedingRoom(ParentA, false);
        AlienRoster.main.SetInBreedingRoom(ParentB, false);

        State = BreedingState.EggReady;
        onChanged.Invoke();
    }

    // Common 30 min, Rare 60 min, Ultra Rare 90 min (set in the Inspector)
    private float BreedingSecondsFor(AlienSpecies baby)
    {
        if (baby == null) return commonBreedingSeconds;
        switch (baby.OverallRarity)
        {
            case TraitRarity.Rare: return rareBreedingSeconds;
            case TraitRarity.UltraRare: return ultraRareBreedingSeconds;
            default: return commonBreedingSeconds;
        }
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

    // Aliens that live in the room (including ones away breeding, since they come back) plus eggs waiting to hatch there
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

        eggs.Add(new PlacedEgg
        {
            baby = EggContents,
            roomIndex = roomIndex,
            hatchAt = SaveSystem.Now + hatchSeconds,
            parentA = ParentA,
            parentB = ParentB,
        });

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
            if (SaveSystem.Now < eggs[i].hatchAt) continue;

            PlacedEgg egg = eggs[i];
            eggs.RemoveAt(i);
            OwnedAlien baby = AlienRoster.main.Add(egg.baby, egg.roomIndex, egg.parentA, egg.parentB); // Shows up in the room straight away
            Debug.Log($"An egg hatched into {baby.DisplayName} in Room {egg.roomIndex + 1} " +
                      $"(parents: {AlienRoster.main.ParentsText(baby)}).", this);
            hatchedAny = true;
        }

        if (hatchedAny) onChanged.Invoke();
    }

    // SAVING

    public void WriteTo(SaveData data)
    {
        BreedingSave save = data.breeding;
        AlienRoster roster = AlienRoster.main;
        save.saved = true;
        save.state = (int)State;
        save.parentA = roster != null ? roster.SaveIndexOf(ParentA) : -1;
        save.parentB = roster != null ? roster.SaveIndexOf(ParentB) : -1;
        save.eggContents = EggContents != null ? EggContents.name : "";
        save.breedingEndsAt = breedingEndsAt;

        save.eggs.Clear();
        foreach (PlacedEgg egg in eggs)
        {
            if (egg.baby == null) continue;
            save.eggs.Add(new EggSave
            {
                baby = egg.baby.name,
                roomIndex = egg.roomIndex,
                hatchAt = egg.hatchAt,
                parentA = roster != null ? roster.SaveIndexOf(egg.parentA) : -1,
                parentB = roster != null ? roster.SaveIndexOf(egg.parentB) : -1,
            });
        }
    }

    // Runs after the aliens are loaded. Anything that finished while the game was closed is picked up by Update on the first frame
    public void ReadFrom(SaveData data)
    {
        BreedingSave save = data.breeding;
        AlienRoster roster = AlienRoster.main;

        if (save.saved && roster != null)
        {
            ParentA = roster.FromSaveIndex(save.parentA);
            ParentB = roster.FromSaveIndex(save.parentB);
            EggContents = FindSpecies(save.eggContents);
            breedingEndsAt = save.breedingEndsAt;
            State = (BreedingState)save.state;

            // Something's missing (a parent or the baby's species) - start over rather than get stuck
            if (State != BreedingState.Idle && (ParentA == null || ParentB == null || EggContents == null))
            {
                Debug.LogWarning("The saved breeding couldn't be loaded, so the breeding room starts empty.", this);
                ParentA = ParentB = null;
                EggContents = null;
                State = BreedingState.Idle;
            }

            eggs.Clear();
            foreach (EggSave saved in save.eggs)
            {
                AlienSpecies baby = FindSpecies(saved.baby);
                if (baby == null) continue;
                eggs.Add(new PlacedEgg
                {
                    baby = baby,
                    roomIndex = saved.roomIndex,
                    hatchAt = saved.hatchAt,
                    parentA = roster.FromSaveIndex(saved.parentA),
                    parentB = roster.FromSaveIndex(saved.parentB),
                });
            }
        }

        // Only the two aliens actually breeding stay away - anyone else marked as breeding comes home
        if (roster != null)
        {
            foreach (OwnedAlien alien in roster.All)
            {
                if (alien == null || !alien.inBreedingRoom) continue;
                bool breedingNow = State == BreedingState.Breeding && (alien == ParentA || alien == ParentB);
                if (!breedingNow) roster.SetInBreedingRoom(alien, false);
            }
        }

        onChanged.Invoke();
    }

    private AlienSpecies FindSpecies(string id)
    {
        if (string.IsNullOrEmpty(id) || catalog == null) return null;
        foreach (AlienSpecies species in catalog.species)
            if (species != null && species.name == id) return species;
        return null;
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