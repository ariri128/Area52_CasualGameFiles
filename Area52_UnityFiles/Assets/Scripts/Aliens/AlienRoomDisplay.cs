using UnityEngine;
using System.Collections.Generic;

/*
 * Shows each owned alien floating in its room, using the AlienRoster
 * Each spawned alien is told which owned alien it is, so tapping it can open the right file
*/

public class AlienRoomDisplay : MonoBehaviour
{
    public static AlienRoomDisplay main { get; private set; }

    [Tooltip("Prefab with a Sprite Renderer and Floating Alien. The sprite is swapped per alien.")]
    [SerializeField] private FloatingAlien alienPrefab;

    [Tooltip("Where the 1st, 2nd, 3rd... alien in a room floats, from the room's origin.")]
    [SerializeField]
    private Vector3[] spots =
    {
        new Vector3(0f, 0.30f, 0.6f),
        new Vector3(0f, 0.40f, 1.0f),
        new Vector3(0f, 0.50f, 1.4f),
    };

    private readonly List<FloatingAlien> spawned = new List<FloatingAlien>();

    private void Awake()
    {
        main = this;
    }

    private void OnDestroy()
    {
        if (main == this) main = null;
        if (AlienRoster.main != null) AlienRoster.main.onChanged.RemoveListener(Refresh);
    }

    // Rooms spawn in RoomManager's Awake, so by Start they all exist
    private void Start()
    {
        if (AlienRoster.main == null || RoomManager.main == null || alienPrefab == null)
        {
            Debug.LogWarning("AlienRoomDisplay needs an AlienRoster, a RoomManager and the Alien Prefab.", this);
            return;
        }

        AlienRoster.main.onChanged.AddListener(Refresh);
        Refresh();
    }

    // The spawned alien showing this owned alien right now (null if it isn't in a room)
    public FloatingAlien Find(OwnedAlien owner)
    {
        foreach (FloatingAlien alien in spawned)
            if (alien != null && alien.Owner == owner) return alien;
        return null;
    }

    // Clears every room's aliens and spawns them again
    private void Refresh()
    {
        foreach (FloatingAlien alien in spawned)
            if (alien != null) Destroy(alien.gameObject);
        spawned.Clear();

        for (int roomIndex = 0; roomIndex < RoomManager.main.RoomCount; roomIndex++)
        {
            GameObject room = RoomManager.main.GetRoomObject(roomIndex);
            if (room == null) continue;

            List<OwnedAlien> aliensHere = AlienRoster.main.InRoom(roomIndex);
            for (int i = 0; i < aliensHere.Count; i++)
            {
                Vector3 localSpot = spots.Length > 0 ? spots[i % spots.Length] : Vector3.zero;
                Vector3 worldSpot = room.transform.TransformPoint(localSpot);

                // Spawned at its spot, so FloatingAlien centers its path there
                FloatingAlien alien = Instantiate(alienPrefab, worldSpot, Quaternion.identity, room.transform);
                alien.name = $"Alien_{aliensHere[i].DisplayName}";
                alien.Setup(aliensHere[i]);

                spawned.Add(alien);
            }
        }
    }
}