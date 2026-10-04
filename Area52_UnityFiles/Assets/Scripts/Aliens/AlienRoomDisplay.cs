using UnityEngine;
using System.Collections.Generic;

/*
 * Shows each owned alien floating in its room, using the AlienRoster
*/

public class AlienRoomDisplay : MonoBehaviour
{
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

    private readonly List<GameObject> spawned = new List<GameObject>();

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

    private void OnDestroy()
    {
        if (AlienRoster.main != null) AlienRoster.main.onChanged.RemoveListener(Refresh);
    }

    // Clears every room's aliens and spawn them again
    private void Refresh()
    {
        foreach (GameObject alien in spawned)
            if (alien != null) Destroy(alien);
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
                alien.name = $"Alien_{aliensHere[i].species.displayName}";
                alien.GetComponent<SpriteRenderer>().sprite = aliensHere[i].species.sprite;

                spawned.Add(alien.gameObject);
            }
        }
    }
}