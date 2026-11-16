using UnityEngine;

/*
 * The photo of a room base, shown in the room panels (Rooms Owned, the room picker, the room shop, the security cameras)
 * Put this on the ROOT of each room prefab and drag in the picture you took of that room
 * Every room made from that prefab (starting or bought) uses it, so nothing extra needs saving
*/

public class RoomPicture : MonoBehaviour
{
    [Tooltip("The picture of this room base.")]
    public Sprite picture;

    // The picture for a spawned room or a room prefab (null if it doesn't have one yet)
    public static Sprite Of(GameObject room)
    {
        RoomPicture found = room != null ? room.GetComponent<RoomPicture>() : null;
        return found != null ? found.picture : null;
    }
}