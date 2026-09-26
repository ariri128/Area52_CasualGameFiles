using UnityEngine;
using System.Collections.Generic;

/*
 * Describes one slot in the apartment's swipe row
 * Helps decide which room prefab to show there and which alien sprites should start in it
 * RoomManager reads a list of these and spawns the visuals from them
*/

[System.Serializable]
public class RoomData
{
    [Tooltip("Display name, also used in the spawned GameObject's name.")]
    public string roomName = "Room";

    [Tooltip("Room prefab to spawn. Its root should sit at the room's front-center floor, with the open side facing -Z.")]
    public GameObject roomPrefab;

    [Tooltip("Alien sprites placed in this room at start. Needs a RoomView with enough Alien Spots on the prefab.")]
    public List<Sprite> startingAliens = new List<Sprite>();
}
