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