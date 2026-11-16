using UnityEngine;
using System.Collections.Generic;

/*
 * Everything that goes into the save file, as plain data (turned into JSON by SaveSystem)
 * Each manager fills in its own part in WriteTo and reads it back in ReadFrom
 * Times are real-world clock times (seconds since 1970, from SaveSystem.Now), so timers keep running while the game is closed
 * New parts are added here step by step as more of the game gets saved
*/

[System.Serializable]
public class SaveData
{
    public int version = 1;

    // When this was saved (SaveSystem.Now) - used to work out how long the player was away
    public double savedAt;

    public CoinSave coins = new CoinSave();
    public PelletSave pellets = new PelletSave();
    public DailyCoinsSave dailyCoins = new DailyCoinsSave();
    public RoomsSave rooms = new RoomsSave();
    public FurnitureSave furniture = new FurnitureSave();
    public AliensSave aliens = new AliensSave();
    public BreedingSave breeding = new BreedingSave();
    public ArtifactsSave artifacts = new ArtifactsSave();
    public ExplorationSave exploration = new ExplorationSave();
}

[System.Serializable]
public class CoinSave
{
    public int wallet;
    public List<ChestSave> chests = new List<ChestSave>();
}

[System.Serializable]
public class ChestSave
{
    public int roomIndex;
    public int coins;
}

[System.Serializable]
public class PelletSave
{
    public int pellets;
}

[System.Serializable]
public class DailyCoinsSave
{
    public bool freeCollected;
    public int adsWatched;
    public double newDayAt = -1; // Clock time the 12 hours are up (-1 = not waiting)
}


// ROOMS

[System.Serializable]
public class RoomsSave
{
    public List<BoughtRoomSave> bought = new List<BoughtRoomSave>(); // Rooms bought from the shop, in order
    public int currentRoom; // The room the player was looking at
}

[System.Serializable]
public class BoughtRoomSave
{
    public string roomName;
    public string prefabName; // Found again through the Room Shop's offers
}


// FURNITURE

[System.Serializable]
public class FurnitureSave
{
    public bool saved; // False on saves made before furniture was saved - the rooms then keep their starting furniture
    public List<OwnedFurnitureSave> owned = new List<OwnedFurnitureSave>();
    public List<RoomLayoutSave> rooms = new List<RoomLayoutSave>();
}

[System.Serializable]
public class OwnedFurnitureSave
{
    public string id; // The furniture data file's name
    public int count;
}

[System.Serializable]
public class RoomLayoutSave
{
    public int roomIndex;
    public List<PlacedFurnitureSave> pieces = new List<PlacedFurnitureSave>();
}

[System.Serializable]
public class PlacedFurnitureSave
{
    public string id;
    public string origin; // "roomNumber:path" for a piece that came with a room, empty for one made from the prefab
    public Vector3 localPosition; // Measured from the room, so it's the same wherever the room is spawned
    public Quaternion localRotation;
    public Vector3 worldScale;    // Its size in the world (so a piece that was inside a scaled part of the room comes back the same size)
}


// ALIENS

[System.Serializable]
public class AliensSave
{
    public bool saved; // False on saves made before aliens were saved - the starting aliens are used instead
    public List<AlienSave> aliens = new List<AlienSave>();
}

[System.Serializable]
public class AlienSave
{
    public string species;      // The species data file's name
    public int roomIndex;
    public bool inBreedingRoom;
    public bool isExploring;
    public float fullness;
    public string customName;
    public string dateAcquired;
    public bool wasBought;
    public List<string> collectedArtifacts = new List<string>(); // Artifact data file names
    public ParentSave parentA = new ParentSave();
    public ParentSave parentB = new ParentSave();
}

// A parent is saved by its place in the aliens list - or by its species and name if it was sold, so the file still shows it
[System.Serializable]
public class ParentSave
{
    public int index = -1;      // Position in AliensSave.aliens (-1 = not owned anymore, or no parent)
    public string species;      // Empty = no parent at all
    public string customName;
}


// BREEDING

[System.Serializable]
public class BreedingSave
{
    public bool saved;
    public int state;               // 0 = Idle, 1 = Breeding, 2 = Egg waiting in the spaceship
    public int parentA = -1;        // Positions in AliensSave.aliens
    public int parentB = -1;
    public string eggContents;      // The baby's species data file name
    public double breedingEndsAt;   // Real-world clock time breeding finishes
    public List<EggSave> eggs = new List<EggSave>();
}

[System.Serializable]
public class EggSave
{
    public string baby;
    public int roomIndex;
    public double hatchAt;          // Real-world clock time it hatches
    public int parentA = -1;
    public int parentB = -1;
}


// ARTIFACTS

[System.Serializable]
public class ArtifactsSave
{
    public bool saved; // False on saves made before artifacts were saved - the starting artifacts are used instead
    public List<ArtifactSave> artifacts = new List<ArtifactSave>();
}

[System.Serializable]
public class ArtifactSave
{
    public string id;       // The artifact data file's name
    public int roomIndex;   // -1 = Unplaced
    public bool isNew;      // Still showing its NEW tag
}


// EXPLORING

[System.Serializable]
public class ExplorationSave
{
    public bool saved;
    public List<TripSave> trips = new List<TripSave>();
}

[System.Serializable]
public class TripSave
{
    public int alien = -1;     // Position in AliensSave.aliens
    public string found;       // The artifact it'll bring back (picked when it left)
    public double returnAt;    // Real-world clock time it can come back
}

/* ver.5
[System.Serializable]
public class SaveData
{
    public int version = 1;

    // When this was saved (SaveSystem.Now) - used to work out how long the player was away
    public double savedAt;

    public CoinSave coins = new CoinSave();
    public PelletSave pellets = new PelletSave();
    public DailyCoinsSave dailyCoins = new DailyCoinsSave();
    public RoomsSave rooms = new RoomsSave();
    public FurnitureSave furniture = new FurnitureSave();
    public AliensSave aliens = new AliensSave();
    public BreedingSave breeding = new BreedingSave();
    public ArtifactsSave artifacts = new ArtifactsSave();
}

[System.Serializable]
public class CoinSave
{
    public int wallet;
    public List<ChestSave> chests = new List<ChestSave>();
}

[System.Serializable]
public class ChestSave
{
    public int roomIndex;
    public int coins;
}

[System.Serializable]
public class PelletSave
{
    public int pellets;
}

[System.Serializable]
public class DailyCoinsSave
{
    public bool freeCollected;
    public int adsWatched;
    public double newDayAt = -1; // Clock time the 12 hours are up (-1 = not waiting)
}


// ROOMS

[System.Serializable]
public class RoomsSave
{
    public List<BoughtRoomSave> bought = new List<BoughtRoomSave>(); // Rooms bought from the shop, in order
    public int currentRoom; // The room the player was looking at
}

[System.Serializable]
public class BoughtRoomSave
{
    public string roomName;
    public string prefabName; // Found again through the Room Shop's offers
}


// FURNITURE

[System.Serializable]
public class FurnitureSave
{
    public bool saved; // False on saves made before furniture was saved - the rooms then keep their starting furniture
    public List<OwnedFurnitureSave> owned = new List<OwnedFurnitureSave>();
    public List<RoomLayoutSave> rooms = new List<RoomLayoutSave>();
}

[System.Serializable]
public class OwnedFurnitureSave
{
    public string id; // The furniture data file's name
    public int count;
}

[System.Serializable]
public class RoomLayoutSave
{
    public int roomIndex;
    public List<PlacedFurnitureSave> pieces = new List<PlacedFurnitureSave>();
}

[System.Serializable]
public class PlacedFurnitureSave
{
    public string id;
    public string origin; // "roomNumber:path" for a piece that came with a room, empty for one made from the prefab
    public Vector3 localPosition; // Measured from the room, so it's the same wherever the room is spawned
    public Quaternion localRotation;
    public Vector3 worldScale;    // Its size in the world (so a piece that was inside a scaled part of the room comes back the same size)
}


// ALIENS

[System.Serializable]
public class AliensSave
{
    public bool saved; // False on saves made before aliens were saved - the starting aliens are used instead
    public List<AlienSave> aliens = new List<AlienSave>();
}

[System.Serializable]
public class AlienSave
{
    public string species;      // The species data file's name
    public int roomIndex;
    public bool inBreedingRoom;
    public bool isExploring;
    public float fullness;
    public string customName;
    public string dateAcquired;
    public bool wasBought;
    public List<string> collectedArtifacts = new List<string>(); // Artifact data file names
    public ParentSave parentA = new ParentSave();
    public ParentSave parentB = new ParentSave();
}

// A parent is saved by its place in the aliens list - or by its species and name if it was sold, so the file still shows it
[System.Serializable]
public class ParentSave
{
    public int index = -1;      // Position in AliensSave.aliens (-1 = not owned anymore, or no parent)
    public string species;      // Empty = no parent at all
    public string customName;
}


// BREEDING

[System.Serializable]
public class BreedingSave
{
    public bool saved;
    public int state;               // 0 = Idle, 1 = Breeding, 2 = Egg waiting in the spaceship
    public int parentA = -1;        // Positions in AliensSave.aliens
    public int parentB = -1;
    public string eggContents;      // The baby's species data file name
    public double breedingEndsAt;   // Real-world clock time breeding finishes
    public List<EggSave> eggs = new List<EggSave>();
}

[System.Serializable]
public class EggSave
{
    public string baby;
    public int roomIndex;
    public double hatchAt;          // Real-world clock time it hatches
    public int parentA = -1;
    public int parentB = -1;
}


// ARTIFACTS

[System.Serializable]
public class ArtifactsSave
{
    public bool saved; // False on saves made before artifacts were saved - the starting artifacts are used instead
    public List<ArtifactSave> artifacts = new List<ArtifactSave>();
}

[System.Serializable]
public class ArtifactSave
{
    public string id;       // The artifact data file's name
    public int roomIndex;   // -1 = Unplaced
    public bool isNew;      // Still showing its NEW tag
}
*/

/* ver.4
[System.Serializable]
public class SaveData
{
    public int version = 1;

    // When this was saved (SaveSystem.Now) - used to work out how long the player was away
    public double savedAt;

    public CoinSave coins = new CoinSave();
    public PelletSave pellets = new PelletSave();
    public DailyCoinsSave dailyCoins = new DailyCoinsSave();
    public RoomsSave rooms = new RoomsSave();
    public FurnitureSave furniture = new FurnitureSave();
    public AliensSave aliens = new AliensSave();
    public BreedingSave breeding = new BreedingSave();
}

[System.Serializable]
public class CoinSave
{
    public int wallet;
    public List<ChestSave> chests = new List<ChestSave>();
}

[System.Serializable]
public class ChestSave
{
    public int roomIndex;
    public int coins;
}

[System.Serializable]
public class PelletSave
{
    public int pellets;
}

[System.Serializable]
public class DailyCoinsSave
{
    public bool freeCollected;
    public int adsWatched;
    public double newDayAt = -1; // Clock time the 12 hours are up (-1 = not waiting)
}


// ROOMS

[System.Serializable]
public class RoomsSave
{
    public List<BoughtRoomSave> bought = new List<BoughtRoomSave>(); // Rooms bought from the shop, in order
    public int currentRoom; // The room the player was looking at
}

[System.Serializable]
public class BoughtRoomSave
{
    public string roomName;
    public string prefabName; // Found again through the Room Shop's offers
}


// FURNITURE

[System.Serializable]
public class FurnitureSave
{
    public bool saved; // False on saves made before furniture was saved - the rooms then keep their starting furniture
    public List<OwnedFurnitureSave> owned = new List<OwnedFurnitureSave>();
    public List<RoomLayoutSave> rooms = new List<RoomLayoutSave>();
}

[System.Serializable]
public class OwnedFurnitureSave
{
    public string id; // The furniture data file's name
    public int count;
}

[System.Serializable]
public class RoomLayoutSave
{
    public int roomIndex;
    public List<PlacedFurnitureSave> pieces = new List<PlacedFurnitureSave>();
}

[System.Serializable]
public class PlacedFurnitureSave
{
    public string id;
    public string origin; // "roomNumber:path" for a piece that came with a room, empty for one made from the prefab
    public Vector3 localPosition; // Measured from the room, so it's the same wherever the room is spawned
    public Quaternion localRotation;
    public Vector3 worldScale;    // Its size in the world (so a piece that was inside a scaled part of the room comes back the same size)
}


// ALIENS

[System.Serializable]
public class AliensSave
{
    public bool saved; // False on saves made before aliens were saved - the starting aliens are used instead
    public List<AlienSave> aliens = new List<AlienSave>();
}

[System.Serializable]
public class AlienSave
{
    public string species;      // The species data file's name
    public int roomIndex;
    public bool inBreedingRoom;
    public bool isExploring;
    public float fullness;
    public string customName;
    public string dateAcquired;
    public bool wasBought;
    public List<string> collectedArtifacts = new List<string>(); // Artifact data file names
    public ParentSave parentA = new ParentSave();
    public ParentSave parentB = new ParentSave();
}

// A parent is saved by its place in the aliens list - or by its species and name if it was sold, so the file still shows it
[System.Serializable]
public class ParentSave
{
    public int index = -1;      // Position in AliensSave.aliens (-1 = not owned anymore, or no parent)
    public string species;      // Empty = no parent at all
    public string customName;
}


// BREEDING

[System.Serializable]
public class BreedingSave
{
    public bool saved;
    public int state;               // 0 = Idle, 1 = Breeding, 2 = Egg waiting in the spaceship
    public int parentA = -1;        // Positions in AliensSave.aliens
    public int parentB = -1;
    public string eggContents;      // The baby's species data file name
    public double breedingEndsAt;   // Real-world clock time breeding finishes
    public List<EggSave> eggs = new List<EggSave>();
}

[System.Serializable]
public class EggSave
{
    public string baby;
    public int roomIndex;
    public double hatchAt;          // Real-world clock time it hatches
    public int parentA = -1;
    public int parentB = -1;
}
*/

/* ver.3
[System.Serializable]
public class SaveData
{
    public int version = 1;

    // When this was saved (SaveSystem.Now) - used to work out how long the player was away
    public double savedAt;

    public CoinSave coins = new CoinSave();
    public PelletSave pellets = new PelletSave();
    public DailyCoinsSave dailyCoins = new DailyCoinsSave();
    public RoomsSave rooms = new RoomsSave();
    public FurnitureSave furniture = new FurnitureSave();
    public AliensSave aliens = new AliensSave();
}

[System.Serializable]
public class CoinSave
{
    public int wallet;
    public List<ChestSave> chests = new List<ChestSave>();
}

[System.Serializable]
public class ChestSave
{
    public int roomIndex;
    public int coins;
}

[System.Serializable]
public class PelletSave
{
    public int pellets;
}

[System.Serializable]
public class DailyCoinsSave
{
    public bool freeCollected;
    public int adsWatched;
    public double newDayAt = -1; // Clock time the 12 hours are up (-1 = not waiting)
}


// ROOMS

[System.Serializable]
public class RoomsSave
{
    public List<BoughtRoomSave> bought = new List<BoughtRoomSave>(); // Rooms bought from the shop, in order
    public int currentRoom; // The room the player was looking at
}

[System.Serializable]
public class BoughtRoomSave
{
    public string roomName;
    public string prefabName; // Found again through the Room Shop's offers
}


// FURNITURE

[System.Serializable]
public class FurnitureSave
{
    public bool saved; // False on saves made before furniture was saved - the rooms then keep their starting furniture
    public List<OwnedFurnitureSave> owned = new List<OwnedFurnitureSave>();
    public List<RoomLayoutSave> rooms = new List<RoomLayoutSave>();
}

[System.Serializable]
public class OwnedFurnitureSave
{
    public string id; // The furniture data file's name
    public int count;
}

[System.Serializable]
public class RoomLayoutSave
{
    public int roomIndex;
    public List<PlacedFurnitureSave> pieces = new List<PlacedFurnitureSave>();
}

[System.Serializable]
public class PlacedFurnitureSave
{
    public string id;
    public string origin; // "roomNumber:path" for a piece that came with a room, empty for one made from the prefab
    public Vector3 localPosition; // Measured from the room, so it's the same wherever the room is spawned
    public Quaternion localRotation;
    public Vector3 worldScale;    // Its size in the world (so a piece that was inside a scaled part of the room comes back the same size)
}


// ALIENS

[System.Serializable]
public class AliensSave
{
    public bool saved; // False on saves made before aliens were saved - the starting aliens are used instead
    public List<AlienSave> aliens = new List<AlienSave>();
}

[System.Serializable]
public class AlienSave
{
    public string species;      // The species data file's name
    public int roomIndex;
    public bool inBreedingRoom;
    public bool isExploring;
    public float fullness;
    public string customName;
    public string dateAcquired;
    public bool wasBought;
    public List<string> collectedArtifacts = new List<string>(); // Artifact data file names
    public ParentSave parentA = new ParentSave();
    public ParentSave parentB = new ParentSave();
}

// A parent is saved by its place in the aliens list - or by its species and name if it was sold, so the file still shows it
[System.Serializable]
public class ParentSave
{
    public int index = -1;      // Position in AliensSave.aliens (-1 = not owned anymore, or no parent)
    public string species;      // Empty = no parent at all
    public string customName;
}
*/

/* ver.2
[System.Serializable]
public class SaveData
{
    public int version = 1;

    // When this was saved (SaveSystem.Now) - used to work out how long the player was away
    public double savedAt;

    public CoinSave coins = new CoinSave();
    public PelletSave pellets = new PelletSave();
    public DailyCoinsSave dailyCoins = new DailyCoinsSave();
    public RoomsSave rooms = new RoomsSave();
    public FurnitureSave furniture = new FurnitureSave();
}

[System.Serializable]
public class CoinSave
{
    public int wallet;
    public List<ChestSave> chests = new List<ChestSave>();
}

[System.Serializable]
public class ChestSave
{
    public int roomIndex;
    public int coins;
}

[System.Serializable]
public class PelletSave
{
    public int pellets;
}

[System.Serializable]
public class DailyCoinsSave
{
    public bool freeCollected;
    public int adsWatched;
    public double newDayAt = -1; // Clock time the 12 hours are up (-1 = not waiting)
}


// ROOMS

[System.Serializable]
public class RoomsSave
{
    public List<BoughtRoomSave> bought = new List<BoughtRoomSave>(); // Rooms bought from the shop, in order
    public int currentRoom; // The room the player was looking at
}

[System.Serializable]
public class BoughtRoomSave
{
    public string roomName;
    public string prefabName; // Found again through the Room Shop's offers
}


// FURNITURE

[System.Serializable]
public class FurnitureSave
{
    public bool saved; // False on saves made before furniture was saved - the rooms then keep their starting furniture
    public List<OwnedFurnitureSave> owned = new List<OwnedFurnitureSave>();
    public List<RoomLayoutSave> rooms = new List<RoomLayoutSave>();
}

[System.Serializable]
public class OwnedFurnitureSave
{
    public string id; // The furniture data file's name
    public int count;
}

[System.Serializable]
public class RoomLayoutSave
{
    public int roomIndex;
    public List<PlacedFurnitureSave> pieces = new List<PlacedFurnitureSave>();
}

[System.Serializable]
public class PlacedFurnitureSave
{
    public string id;
    public Vector3 localPosition; // Measured from the room, so it's the same wherever the room is spawned
    public Quaternion localRotation;
    public Vector3 worldScale;    // Its size in the world (so a piece that was inside a scaled part of the room comes back the same size)
    public string origin; // "roomNumber:path" for a piece that came with a room, empty for one made from the prefab
}
*/

/* ver.1
[System.Serializable]
public class SaveData
{
    public int version = 1;

    // When this was saved (SaveSystem.Now) - used to work out how long the player was away
    public double savedAt;

    public CoinSave coins = new CoinSave();
    public PelletSave pellets = new PelletSave();
    public DailyCoinsSave dailyCoins = new DailyCoinsSave();
}

[System.Serializable]
public class CoinSave
{
    public int wallet;
    public List<ChestSave> chests = new List<ChestSave>();
}

[System.Serializable]
public class ChestSave
{
    public int roomIndex;
    public int coins;
}

[System.Serializable]
public class PelletSave
{
    public int pellets;
}

[System.Serializable]
public class DailyCoinsSave
{
    public bool freeCollected;
    public int adsWatched;
    public double newDayAt = -1; // Clock time the 12 hours are up (-1 = not waiting)
}
*/