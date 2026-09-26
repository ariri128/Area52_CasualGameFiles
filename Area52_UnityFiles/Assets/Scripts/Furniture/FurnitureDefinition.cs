using UnityEngine;

/*
 * Data file that gets made for each piece of furniture defining what type of furniture it is
 * 
 * Decides how each type of furniture behaves in a room:
    * Floor furniture - stands on the floor, can't touch other pieces (to prevent clipping), counts towards the room's limit of 6 pieces of furniture
    * Rug - lies flat on the floor, other pieces can stand on it, doesn't count toward the limit
    * Decor - small iteams (vases, radios, etc); only sit on tables, benches, dressers, and shelves, doesn't count toward the limit
 * Windows, walls, and anything on the ceiling aren't considered furniture - they can't be changed
*/

// The order here is the order the tabs appear in the Furniture Owned panel
public enum FurnitureCategory
{
    Couches,
    Chairs,
    Beds,
    Tables,
    Benches,
    Dressers,
    Shelves,
    Lamps,
    Rugs,
    Decor,
    Other
}

public enum FurniturePlacement
{
    Floor,
    Rug,
    Decor
}

[CreateAssetMenu(menuName = "Alien Apartments/Furniture Definition", fileName = "FD_NewFurniture")]
public class FurnitureDefinition : ScriptableObject
{
    [Tooltip("Name shown under the icon in the Furniture Owned panel.")]
    public string displayName = "New Furniture";

    [Tooltip("Picture shown in the Furniture Owned panel. Can be left empty for now.")]
    public Sprite icon;

    [Tooltip("Which tab this shows up under.")]
    public FurnitureCategory category = FurnitureCategory.Other;

    [Tooltip("Floor = normal furniture, Rug = flat on the floor, Decor = small items that sit on tables, benches, dressers and shelves.")]
    public FurniturePlacement placement = FurniturePlacement.Floor;

    [Tooltip("The furniture's prefab, used when it's added to a room. Filled in during step 2.")]
    public GameObject prefab;

    // Only standing floor furniture counts toward the room's limit
    public bool CountsTowardLimit => placement == FurniturePlacement.Floor;

    public bool IsDecor => placement == FurniturePlacement.Decor;

    // Decides what this piece of furniture is allows to replace - decor can only be swapped with decor while floor furniture and rugs can be swapped with each other
    public bool CanReplace(FurnitureDefinition current)
    {
        if (current == null) return false;
        return IsDecor == current.IsDecor;
    }
}