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

    public void NotifyChanged()
    {
        onChanged.Invoke();
    }
}