using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Events;

/*
 * Inventory for the furniture the players owns, and the storage for pieces that aren't placed in a room
*/

public class FurnitureInventory : MonoBehaviour
{
    public static FurnitureInventory main { get; private set; }

    [System.Serializable]
    public class OwnedEntry
    {
        public FurnitureDefinition definition;
        [Min(1)] public int count = 1;
    }

    [Tooltip("Pieces the player owns at the start that aren't placed in any room.")]
    [SerializeField] private List<OwnedEntry> startingOwned = new List<OwnedEntry>();

    [Tooltip("The Furniture Catalog (the same one the shop uses). Needed to find saved furniture again when loading.")]
    [SerializeField] private FurnitureCatalog catalog;

    // Fired whenever what's owned, placed or stored changes
    public UnityEvent onChanged = new UnityEvent();

    private readonly Dictionary<FurnitureDefinition, int> owned = new Dictionary<FurnitureDefinition, int>();
    private readonly List<FurnitureDefinition> ownedOrder = new List<FurnitureDefinition>();
    private readonly Dictionary<FurnitureDefinition, List<FurnitureItem>> stored = new Dictionary<FurnitureDefinition, List<FurnitureItem>>();
    private Transform storage;
    private bool counted;

    private void Awake()
    {
        if (main != null && main != this)
        {
            Debug.LogWarning("More than one FurnitureInventory in the scene. Destroying the extra one.", this);
            Destroy(this);
            return;
        }
        main = this;

        // Anything parented under an inactive object is hidden, so storing a piece is just moving it here
        storage = new GameObject("FurnitureStorage").transform;
        storage.SetParent(transform, false);
        storage.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (main == this) main = null;
    }

    // Rooms spawn in RoomManager's Awake, so by Start every starting piece exists
    private void Start()
    {
        CountStartingPieces();
        if (RoomManager.main != null) RoomManager.main.onRoomAdded.AddListener(CountBoughtRoom);
    }

    // A bought room comes with its own furniture - the player owns those pieces too
    private void CountBoughtRoom(int roomIndex)
    {
        GameObject roomObject = RoomManager.main.GetRoomObject(roomIndex);
        RoomFurnitureDetector room = roomObject != null ? roomObject.GetComponent<RoomFurnitureDetector>() : null;
        if (room == null) return;

        foreach (FurnitureItem item in room.Items)
            if (item != null && item.Definition != null) AddOwned(item.Definition, 1);
        onChanged.Invoke();
    }

    private void CountStartingPieces()
    {
        if (counted) return;
        counted = true;

        foreach (OwnedEntry entry in startingOwned)
        {
            if (entry.definition != null) AddOwned(entry.definition, entry.count);
        }

        foreach (RoomFurnitureDetector room in RoomFurnitureDetector.All)
        {
            foreach (FurnitureItem item in room.Items)
            {
                if (item != null && item.Definition != null) AddOwned(item.Definition, 1);
            }
        }

        onChanged.Invoke();
    }

    private void AddOwned(FurnitureDefinition definition, int count)
    {
        if (!owned.ContainsKey(definition))
        {
            owned[definition] = 0;
            ownedOrder.Add(definition);
        }
        owned[definition] += count;
    }


    // OWNERSHIP DEFINITIONS

    // Everything owned, in the order it was first owned
    public List<FurnitureDefinition> GetOwnedDefinitions()
    {
        CountStartingPieces();
        return new List<FurnitureDefinition>(ownedOrder);
    }

    public int GetOwnedCount(FurnitureDefinition definition)
    {
        CountStartingPieces();
        return owned.TryGetValue(definition, out int count) ? count : 0;
    }

    // Checks how many of this piece are in rooms right now
    public int GetPlacedCount(FurnitureDefinition definition)
    {
        int count = 0;
        foreach (RoomFurnitureDetector room in RoomFurnitureDetector.All)
        {
            foreach (FurnitureItem item in room.Items)
            {
                if (item != null && item.Definition == definition) count++;
            }
        }
        return count;
    }

    // Owned but not in a room
    public int GetAvailableCount(FurnitureDefinition definition)
    {
        return Mathf.Max(0, GetOwnedCount(definition) - GetPlacedCount(definition));
    }

    // For the Furniture Catalog: pays for one more of this piece
    // It counts as owned straight away and is made from its prefab the first time it's used with Replace
    public bool Buy(FurnitureDefinition definition)
    {
        if (definition == null || CoinBank.main == null) return false;
        CountStartingPieces(); // Make sure the starting count is in before adding to it

        if (!CoinBank.main.TrySpend(definition.price)) return false; // Plays the cha-ching
        AddOwned(definition, 1);
        onChanged.Invoke();
        return true;
    }


    // STORAGE

    // Takes a piece out of storage, or makes a new one from the prefab if none is stored - returns null if there's nothing available
    public FurnitureItem Take(FurnitureDefinition definition)
    {
        if (GetAvailableCount(definition) <= 0) return null;

        if (stored.TryGetValue(definition, out List<FurnitureItem> list) && list.Count > 0)
        {
            FurnitureItem piece = list[list.Count - 1];
            list.RemoveAt(list.Count - 1);
            return piece;
        }

        if (definition.prefab == null)
        {
            Debug.LogWarning($"{definition.name} has no prefab, so a new one can't be made.", definition);
            return null;
        }

        GameObject made = Instantiate(definition.prefab);
        made.name = definition.prefab.name;
        FurnitureItem item = made.GetComponent<FurnitureItem>();
        if (item == null)
        {
            Debug.LogWarning($"{definition.prefab.name} has no FurnitureItem on its root.", definition.prefab);
            Destroy(made);
            return null;
        }
        return item;
    }

    // Hides a piece and keeps it for later
    public void Store(FurnitureItem item)
    {
        item.ClearHighlight();
        item.Room = null;
        item.transform.SetParent(storage, true);

        if (!stored.ContainsKey(item.Definition)) stored[item.Definition] = new List<FurnitureItem>();
        stored[item.Definition].Add(item);
    }

    // Takes this exact piece back out of storage (for undoing a replace) - false if it isn't stored
    public bool TakeBack(FurnitureItem item)
    {
        if (item == null || item.Definition == null) return false;
        return stored.TryGetValue(item.Definition, out List<FurnitureItem> list) && list.Remove(item);
    }

    public void NotifyChanged()
    {
        onChanged.Invoke();
    }


    // SAVING

    public void WriteTo(SaveData data)
    {
        CountStartingPieces();
        FurnitureSave save = data.furniture;
        save.saved = true;

        save.owned.Clear();
        foreach (FurnitureDefinition definition in ownedOrder)
            save.owned.Add(new OwnedFurnitureSave { id = definition.name, count = owned[definition] });

        save.rooms.Clear();
        if (RoomManager.main == null) return;

        for (int i = 0; i < RoomManager.main.RoomCount; i++)
        {
            GameObject roomObject = RoomManager.main.GetRoomObject(i);
            RoomFurnitureDetector room = roomObject != null ? roomObject.GetComponent<RoomFurnitureDetector>() : null;
            if (room == null) continue;

            RoomLayoutSave layout = new RoomLayoutSave { roomIndex = i };
            foreach (FurnitureItem item in room.Items)
            {
                if (item == null || item.Definition == null) continue;
                Transform piece = item.transform;
                layout.pieces.Add(new PlacedFurnitureSave
                {
                    id = item.Definition.name,
                    origin = OriginKey(item),
                    localPosition = room.transform.InverseTransformPoint(piece.position),
                    localRotation = Quaternion.Inverse(room.transform.rotation) * piece.rotation,
                    worldScale = piece.lossyScale,
                });
            }
            save.rooms.Add(layout);
        }
    }

    public void ReadFrom(SaveData data)
    {
        FurnitureSave save = data.furniture;
        if (!save.saved) return; // A save from before furniture was saved - rooms keep their starting furniture

        // What's owned (this replaces the starting count)
        counted = true;
        owned.Clear();
        ownedOrder.Clear();
        foreach (OwnedFurnitureSave entry in save.owned)
        {
            FurnitureDefinition definition = FindDefinition(entry.id);
            if (definition != null) AddOwned(definition, entry.count);
            else Debug.LogWarning($"Saved furniture \"{entry.id}\" isn't in the Furniture Catalog, so it was skipped.", this);
        }

        // Every room is emptied FIRST, so a piece moved from one room to another is free to use
        List<(RoomFurnitureDetector room, RoomLayoutSave layout)> toFill = new List<(RoomFurnitureDetector, RoomLayoutSave)>();
        foreach (RoomLayoutSave layout in save.rooms)
        {
            GameObject roomObject = RoomManager.main != null ? RoomManager.main.GetRoomObject(layout.roomIndex) : null;
            RoomFurnitureDetector room = roomObject != null ? roomObject.GetComponent<RoomFurnitureDetector>() : null;
            if (room == null) continue;

            room.ClearForLoad();
            toFill.Add((room, layout));
        }

        // Then each saved piece goes back exactly where it was
        foreach ((RoomFurnitureDetector room, RoomLayoutSave layout) in toFill)
        {
            foreach (PlacedFurnitureSave saved in layout.pieces)
            {
                FurnitureDefinition definition = FindDefinition(saved.id);
                if (definition == null) continue;

                // The save says it's in the room - trust the room, even if the owned count is behind
                if (GetAvailableCount(definition) <= 0) AddOwned(definition, 1);

                // The exact piece it was: one that came with a room, or one made from the prefab - never mixed up
                FurnitureItem piece = !string.IsNullOrEmpty(saved.origin) ? TakeByOrigin(definition, saved.origin) : null;
                if (piece == null) piece = TakeFromPrefab(definition);
                if (piece != null) room.PlaceForLoad(piece, saved.localPosition, saved.localRotation);
            }
        }

        onChanged.Invoke();
    }

    // Finds a furniture data file by its name
    private FurnitureDefinition FindDefinition(string id)
    {
        if (catalog != null)
            foreach (FurnitureDefinition definition in catalog.furniture)
                if (definition != null && definition.name == id) return definition;

        foreach (OwnedEntry entry in startingOwned)
            if (entry.definition != null && entry.definition.name == id) return entry.definition;

        foreach (FurnitureDefinition definition in stored.Keys) // The rooms' starting pieces, after ClearForLoad
            if (definition != null && definition.name == id) return definition;

        return null;
    }

    // "2:Furniture/AlienChair" - the room it came with and its path there, or "" if it was made from the prefab
    private static string OriginKey(FurnitureItem item)
    {
        if (item == null || item.OriginRoom == null || RoomManager.main == null) return "";
        for (int i = 0; i < RoomManager.main.RoomCount; i++)
            if (RoomManager.main.GetRoomObject(i) == item.OriginRoom.gameObject) return $"{i}:{item.OriginPath}";
        return "";
    }

    // For loading: this exact room piece, out of storage (null if it isn't there)
    private FurnitureItem TakeByOrigin(FurnitureDefinition definition, string origin)
    {
        if (!stored.TryGetValue(definition, out List<FurnitureItem> list)) return null;
        for (int i = 0; i < list.Count; i++)
        {
            if (OriginKey(list[i]) != origin) continue;
            FurnitureItem piece = list[i];
            list.RemoveAt(i);
            return piece;
        }
        return null;
    }

    // For loading: a piece made from the prefab - a stored one if there is one, otherwise a new one
    private FurnitureItem TakeFromPrefab(FurnitureDefinition definition)
    {
        if (stored.TryGetValue(definition, out List<FurnitureItem> list))
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].OriginRoom != null) continue; // Came with a room - not this one
                FurnitureItem piece = list[i];
                list.RemoveAt(i);
                return piece;
            }
        }

        if (definition.prefab == null) return null;
        GameObject made = Instantiate(definition.prefab);
        made.name = definition.prefab.name;
        FurnitureItem item = made.GetComponent<FurnitureItem>();
        if (item == null) Destroy(made);
        return item;
    }
}