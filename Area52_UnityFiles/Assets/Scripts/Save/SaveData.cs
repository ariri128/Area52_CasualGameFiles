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