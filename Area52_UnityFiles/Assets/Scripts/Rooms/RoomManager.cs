using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Events;

/*
 * Owns the list of rooms (RoomData), spawns each room's prefab in a row along the X axis, and tracks which room the player is currently looking at
 * Anything that wants to change rooms (swipe, the room directory, the apartment exterior view) should call RoomManager.main.GoToRoom(index)
 * Anything that needs to react to the room changing (the camera, UI labels) listens to onRoomChanged
*/

public class RoomManager : MonoBehaviour
{
    public static RoomManager main { get; private set; }

    [Header("Rooms")]
    [Tooltip("Rooms in swipe order, left to right.")]
    [SerializeField] private List<RoomData> rooms = new List<RoomData>();

    [Tooltip("Distance between room origins. Must be wider than what the camera can see on the widest phone, or the next room will peek in.")]
    [SerializeField] private float roomSpacing = 15f;

    [SerializeField] private int startRoomIndex = 0;

    [Header("Aliens")]
    [Tooltip("Material for alien sprites. Use Sprite-Unlit-Default so room lighting doesn't dim them.")]
    [SerializeField] private Material alienMaterial;

    [Header("Scene View Preview")]
    [Tooltip("Rough room size, only used to draw boxes in the Scene view so you can see where rooms will spawn.")]
    [SerializeField] private Vector3 roomGizmoSize = new Vector3(10f, 5f, 6f);

    // Passes the new room index
    public UnityEvent<int> onRoomChanged = new UnityEvent<int>();

    private readonly List<GameObject> spawnedRooms = new List<GameObject>();

    public int CurrentRoomIndex { get; private set; }
    public int RoomCount => rooms.Count;
    public float RoomSpacing => roomSpacing;

    private void Awake()
    {
        if (main != null && main != this)
        {
            Debug.LogWarning("More than one RoomManager in the scene. Destroying the extra one.", this);
            Destroy(gameObject);
            return;
        }
        main = this;

        SpawnRooms();
        CurrentRoomIndex = Mathf.Clamp(startRoomIndex, 0, Mathf.Max(0, rooms.Count - 1));
    }

    private void OnDestroy()
    {
        if (main == this) main = null;
    }

    // For the prototype every room is spawned up front - once there are many rooms, only the current room and its neighbors will be spawned
    private void SpawnRooms()
    {
        for (int i = 0; i < rooms.Count; i++)
        {
            RoomData data = rooms[i];
            if (data.roomPrefab == null)
            {
                Debug.LogWarning($"Room {i} ({data.roomName}) has no prefab assigned.", this);
                spawnedRooms.Add(null);
                continue;
            }

            // Keeps the prefab's own rotation in case its root isn't at (0,0,0)
            GameObject room = Instantiate(data.roomPrefab, GetRoomPosition(i), data.roomPrefab.transform.rotation, transform);
            room.name = $"Room_{i:00}_{data.roomName}";

            RoomView view = room.GetComponent<RoomView>();
            if (view != null) view.Init(i, data, alienMaterial);

            spawnedRooms.Add(room);
        }
    }

    public Vector3 GetRoomPosition(int index)
    {
        return transform.position + Vector3.right * (index * roomSpacing);
    }

    public GameObject GetRoomObject(int index)
    {
        return (index >= 0 && index < spawnedRooms.Count) ? spawnedRooms[index] : null;
    }

    // Always fires onRoomChanged, even for the same index, so the camera can snap back after a short drag
    public void GoToRoom(int index)
    {
        if (rooms.Count == 0) return;

        CurrentRoomIndex = Mathf.Clamp(index, 0, rooms.Count - 1);
        onRoomChanged.Invoke(CurrentRoomIndex);
    }

    public void NextRoom() => GoToRoom(CurrentRoomIndex + 1);
    public void PreviousRoom() => GoToRoom(CurrentRoomIndex - 1);

    // Draws a box for each room slot so the layout is visible without pressing Play
    private void OnDrawGizmos()
    {
        for (int i = 0; i < rooms.Count; i++)
        {
            // Room origin is the front-center floor, so the box extends up and back (+Z)
            Vector3 center = GetRoomPosition(i) + new Vector3(0f, roomGizmoSize.y * 0.5f, roomGizmoSize.z * 0.5f);
            Gizmos.color = (i == startRoomIndex) ? Color.green : Color.cyan;
            Gizmos.DrawWireCube(center, roomGizmoSize);
        }
    }
}
