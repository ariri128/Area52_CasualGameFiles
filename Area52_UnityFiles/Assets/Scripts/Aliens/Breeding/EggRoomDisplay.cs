using UnityEngine;
using System.Collections.Generic;

/*
 * Shows each egg that's waiting to hatch sitting in its room
 * When it hatches it disappears, and AlienRoomDisplay shows the baby instead
*/

public class EggRoomDisplay : MonoBehaviour
{
    [Tooltip("Prefab with a Sprite Renderer showing the egg.")]
    [SerializeField] private GameObject eggPrefab;

    [Tooltip("Where the 1st, 2nd... egg in a room sits, from the room's origin.")]
    [SerializeField]
    private Vector3[] spots =
    {
        new Vector3(-1.8f, 0f, 0.5f),
        new Vector3(1.8f, 0f, 0.5f),
        new Vector3(-1.2f, 0f, 1.2f),
        new Vector3(1.2f, 0f, 1.2f),
    };

    private readonly List<GameObject> spawned = new List<GameObject>();

    private void Start()
    {
        if (BreedingManager.main == null || RoomManager.main == null || eggPrefab == null)
        {
            Debug.LogWarning("EggRoomDisplay needs a BreedingManager, a RoomManager and the Egg Prefab.", this);
            return;
        }

        BreedingManager.main.onChanged.AddListener(Refresh);
        Refresh();
    }

    private void OnDestroy()
    {
        if (BreedingManager.main != null) BreedingManager.main.onChanged.RemoveListener(Refresh);
    }

    private void Refresh()
    {
        foreach (GameObject egg in spawned)
            if (egg != null) Destroy(egg);
        spawned.Clear();

        Dictionary<int, int> eggsPerRoom = new Dictionary<int, int>();

        foreach (PlacedEgg egg in BreedingManager.main.Eggs)
        {
            GameObject room = RoomManager.main.GetRoomObject(egg.roomIndex);
            if (room == null) continue;

            eggsPerRoom.TryGetValue(egg.roomIndex, out int countHere);
            eggsPerRoom[egg.roomIndex] = countHere + 1;

            Vector3 localSpot = spots.Length > 0 ? spots[countHere % spots.Length] : Vector3.zero;
            Vector3 worldSpot = room.transform.TransformPoint(localSpot);

            GameObject eggObject = Instantiate(eggPrefab, worldSpot, Quaternion.identity, room.transform);
            eggObject.name = "Egg";
            spawned.Add(eggObject);
        }
    }
}