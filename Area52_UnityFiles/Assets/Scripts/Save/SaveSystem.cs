using UnityEngine;
using UnityEngine.Events;
using System;
using System.IO;

/*
 * Saves the game to a file on the device and loads it when the game starts, so the player carries on where they left off
 * Saves every few seconds, and whenever the game is closed or put in the background (phones often close apps without warning)
 * Uses the real-world clock (Now), so timers keep running while the game is closed
 * Runs before the other scripts' Start, so everything is loaded before the rest of the game starts using it
 * Managers that only switch on later (inside a panel that starts closed) read their part when they wake up (TryGetLoaded)
 * Keeps those managers' saved parts even while they're switched off
 *
 * For developers (Editor only):
 *   Use Save In Editor - untick to always start fresh in Play mode (nothing is loaded or saved)
 *   Menu: Alien Apartments > Save > Delete Save - resets the game to its original state
 *   Test Hours Ahead - pretends that many hours have passed, to test long timers without waiting
*/

[DefaultExecutionOrder(-1000)]
public class SaveSystem : MonoBehaviour
{
    public static SaveSystem main { get; private set; }

    [Tooltip("Name of the save file. It lives in the device's app data folder (Application.persistentDataPath).")]
    [SerializeField] private string fileName = "alien_apartments_save.json";

    [Tooltip("Seconds between automatic saves.")]
    [SerializeField] private float autoSaveSeconds = 10f;

    [Header("Developers (Editor only - builds always save)")]
    [Tooltip("Unticked: Play mode always starts fresh and nothing is saved.")]
    [SerializeField] private bool useSaveInEditor = true;

    [Tooltip("Pretends this many hours have passed - for testing long timers. Can be changed during Play.")]
    [SerializeField] private float testHoursAhead = 0f;

    [Tooltip("Print when the game saves and loads.")]
    [SerializeField] private bool logSaves = false;

    // Fires once loading is done (also when there was nothing to load)
    public UnityEvent onLoaded = new UnityEvent();

    public bool Loaded { get; private set; }

    // The last save loaded or written (null on a brand new game before the first save)
    // Kept so a manager that's switched off (e.g. inside a closed panel) keeps its saved part instead of losing it
    public SaveData Data { get; private set; }

    // For managers that switch on AFTER the game has loaded (their Awake ran late): gives them the save to read from
    public static bool TryGetLoaded(out SaveData data)
    {
        data = main != null && main.Loaded ? main.Data : null;
        return data != null;
    }

    // Seconds between the last save and this start (0 on a fresh game) - for things that kept going while closed
    public double SecondsAway { get; private set; }

    public static string FilePath => Path.Combine(Application.persistentDataPath, CurrentFileName);
    private static string CurrentFileName => main != null ? main.fileName : "alien_apartments_save.json";

    // The real-world clock, in seconds since 1970. Every saved timer uses this instead of Time.time
    public static double Now
    {
        get
        {
            double now = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
#if UNITY_EDITOR
            if (main != null) now += main.testHoursAhead * 3600.0;
#endif
            return now;
        }
    }

    private float nextAutoSave;
    private bool savingBlocked; // Set after Delete Save during Play, so quitting doesn't write the old game back

    private bool SavingEnabled
    {
        get
        {
#if UNITY_EDITOR
            if (!useSaveInEditor) return false;
#endif
            return !savingBlocked;
        }
    }

    private void Awake()
    {
        if (main != null && main != this)
        {
            Debug.LogWarning("More than one SaveSystem in the scene. Destroying the extra one.", this);
            Destroy(this);
            return;
        }
        main = this;
    }

    private void OnDestroy()
    {
        if (main == this) main = null;
    }

    // Every other script's Awake has run by now, so all the managers exist and have their starting values
    private void Start()
    {
        Load();
        nextAutoSave = Time.unscaledTime + autoSaveSeconds;
    }

    private void Update()
    {
        if (!Loaded || Time.unscaledTime < nextAutoSave) return;
        nextAutoSave = Time.unscaledTime + autoSaveSeconds;
        Save();
    }

    // Phones: the game going into the background is often the last chance to save
    private void OnApplicationPause(bool paused)
    {
        if (paused) Save();
    }

    private void OnApplicationQuit() => Save();


    // SAVING

    public void Save()
    {
        if (!Loaded || !SavingEnabled) return;

        // Starts from the last save, so the part of any manager that isn't awake right now is kept as it was
        SaveData data = Data ?? new SaveData();
        data.savedAt = Now;

        // Each manager that exists right now writes its own part
        if (RoomManager.main != null) RoomManager.main.WriteTo(data);
        if (FurnitureInventory.main != null) FurnitureInventory.main.WriteTo(data);
        if (ArtifactInventory.main != null) ArtifactInventory.main.WriteTo(data);
        if (AlienRoster.main != null) AlienRoster.main.WriteTo(data);
        if (BreedingManager.main != null) BreedingManager.main.WriteTo(data);
        if (ExplorationManager.main != null) ExplorationManager.main.WriteTo(data);
        if (CoinBank.main != null) CoinBank.main.WriteTo(data);
        if (PelletBank.main != null) PelletBank.main.WriteTo(data);
        if (DailyCoins.main != null) DailyCoins.main.WriteTo(data);
        if (MusicShop.main != null) MusicShop.main.WriteTo(data);

        try
        {
            // Written to a temporary file first, then swapped in, so a crash mid-save can't wreck the old save
            string path = FilePath;
            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(data, true));
            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);
            Data = data;
            if (logSaves) Debug.Log($"Saved to {path}", this);
        }
        catch (Exception e)
        {
            Debug.LogError($"Couldn't save the game: {e.Message}", this);
        }
    }


    // LOADING

    private void Load()
    {
        SaveData data = ReadFile();
        Data = data;

        if (data != null)
        {
            SecondsAway = Math.Max(0.0, Now - data.savedAt);

            // Order matters: rooms first (bought rooms are spawned), then the furniture in them, then everything else
            if (RoomManager.main != null) RoomManager.main.ReadFrom(data);
            if (FurnitureInventory.main != null) FurnitureInventory.main.ReadFrom(data);
            if (ArtifactInventory.main != null) ArtifactInventory.main.ReadFrom(data); // Before the catch-up: auto-feeders and liked artifacts count
            if (AlienRoster.main != null) AlienRoster.main.ReadFrom(data);
            if (BreedingManager.main != null) BreedingManager.main.ReadFrom(data); // After the aliens - it points at the parents
            if (ExplorationManager.main != null) ExplorationManager.main.ReadFrom(data); // After the aliens and artifacts
            if (CoinBank.main != null) CoinBank.main.ReadFrom(data);
            if (PelletBank.main != null) PelletBank.main.ReadFrom(data);
            if (DailyCoins.main != null) DailyCoins.main.ReadFrom(data);
            if (MusicShop.main != null) MusicShop.main.ReadFrom(data);

            // What happened while the game was closed: aliens got hungrier (never past Sad), chests kept filling
            // Hunger first, so a hungry alien's mood (and so its coins) is taken into account
            if (AlienHunger.main != null) AlienHunger.main.CatchUp(SecondsAway);
            if (CoinBank.main != null) CoinBank.main.CatchUp(SecondsAway);

            if (logSaves) Debug.Log($"Loaded the save. The player was away for {SecondsAway / 60.0:0.0} minutes.", this);
        }

        Loaded = true;
        onLoaded.Invoke();
    }

    private SaveData ReadFile()
    {
#if UNITY_EDITOR
        if (!useSaveInEditor) return null;
#endif
        string path = FilePath;
        if (!File.Exists(path)) return null; // First time playing

        try
        {
            return JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
        }
        catch (Exception e)
        {
            // Keeps the broken file to look at, and starts fresh instead of crashing
            Debug.LogError($"The save file couldn't be read, so the game starts fresh. The old file was kept as .broken. ({e.Message})", this);
            File.Copy(path, path + ".broken", true);
            return null;
        }
    }


    // DEVELOPERS

    // Deletes the save so the next Play starts from the original state
    public static void DeleteSave()
    {
        string path = FilePath;
        if (File.Exists(path)) File.Delete(path);
        if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");

        // In Play mode: stop saving, or quitting would write the current game straight back
        if (main != null) main.savingBlocked = true;

        Debug.Log("Save deleted - the next Play starts fresh." + (main != null ? " Saving is off until you stop Play." : ""));
    }

    [ContextMenu("Delete Save")]
    private void DeleteSaveFromInspector() => DeleteSave();

    [ContextMenu("Save Now")]
    private void SaveFromInspector()
    {
        if (!Application.isPlaying) { Debug.LogWarning("Press Play first.", this); return; }
        Save();
        Debug.Log($"Saved to {FilePath}", this);
    }

    [ContextMenu("Testing: Skip Ahead 1 Hour")]
    private void SkipHour() => testHoursAhead += 1f;

    [ContextMenu("Testing: Skip Ahead 12 Hours")]
    private void SkipTwelveHours() => testHoursAhead += 12f;
}