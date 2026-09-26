using UnityEngine;
using System.Collections.Generic;

/*
 * Visual side of the room that knows where the aliens can spawn and stand/roam inside the room
*/

public class RoomView : MonoBehaviour
{
    [Tooltip("Where aliens stand. Put front spots first, back spots last.")]
    [SerializeField] private Transform[] alienSpots;

    public int RoomIndex { get; private set; }

    // Called by RoomManager right after this room is spawned
    public void Init(int roomIndex, RoomData data, Material alienMaterial)
    {
        RoomIndex = roomIndex;
        SpawnStartingAliens(data.startingAliens, alienMaterial);
    }

    private void SpawnStartingAliens(List<Sprite> sprites, Material alienMaterial)
    {
        if (sprites == null || sprites.Count == 0) return;

        if (alienSpots == null || alienSpots.Length < sprites.Count)
        {
            Debug.LogWarning($"{name}: has {sprites.Count} starting aliens but only " +
                             $"{(alienSpots == null ? 0 : alienSpots.Length)} Alien Spots. Extra aliens are skipped.", this);
        }

        int count = Mathf.Min(sprites.Count, alienSpots == null ? 0 : alienSpots.Length);
        for (int i = 0; i < count; i++)
        {
            if (sprites[i] == null || alienSpots[i] == null) continue;
            SpawnAlienSprite(sprites[i], alienSpots[i], alienMaterial);
        }
    }

    private void SpawnAlienSprite(Sprite sprite, Transform spot, Material alienMaterial)
    {
        GameObject alien = new GameObject($"Alien_{sprite.name}");
        alien.transform.SetParent(spot, false);

        SpriteRenderer spriteRenderer = alien.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = sprite;
        if (alienMaterial != null) spriteRenderer.sharedMaterial = alienMaterial;
    }
}
