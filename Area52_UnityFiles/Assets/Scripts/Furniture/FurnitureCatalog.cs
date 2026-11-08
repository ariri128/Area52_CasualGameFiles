using UnityEngine;
using System.Collections.Generic;

/*
 * Every piece of furniture that exists - what the Furniture Catalog sells
 * Right-click it -> Fill With Every Furniture Data File, and run it again whenever a new one is made
*/

[CreateAssetMenu(menuName = "Alien Apartments/Furniture Catalog", fileName = "FurnitureCatalog")]
public class FurnitureCatalog : ScriptableObject
{
    [Tooltip("Every furniture data file. Use the right-click button to fill this in automatically.")]
    public List<FurnitureDefinition> furniture = new List<FurnitureDefinition>();

#if UNITY_EDITOR // Only exists in the editor - finding files in the project isn't possible in a built game
    [ContextMenu("Fill With Every Furniture Data File")]
    private void FillFromProject()
    {
        furniture.Clear();
        foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:FurnitureDefinition"))
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            FurnitureDefinition definition = UnityEditor.AssetDatabase.LoadAssetAtPath<FurnitureDefinition>(path);
            if (definition != null) furniture.Add(definition);
        }

        // Grouped by tab, then A-Z
        furniture.Sort((a, b) => a.category != b.category
            ? a.category.CompareTo(b.category)
            : string.Compare(a.displayName, b.displayName, System.StringComparison.OrdinalIgnoreCase));

        UnityEditor.EditorUtility.SetDirty(this); // Tells Unity to save the change
        Debug.Log($"Furniture Catalog now has {furniture.Count} pieces.", this);
    }
#endif
}