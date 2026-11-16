using UnityEngine;
using UnityEditor;

/*
 * Developer menu for the save file: Alien Apartments > Save
 * Delete Save resets the game to its original state (only affects this computer - players' saves are on their own devices)
*/

public static class SaveSystemMenu
{
    [MenuItem("Alien Apartments/Save/Delete Save (Start Fresh)")]
    private static void DeleteSave()
    {
        if (!EditorUtility.DisplayDialog("Delete Save",
                "Delete the save file? The next Play will start from the game's original state.", "Delete", "Cancel"))
            return;

        SaveSystem.DeleteSave();
    }

    [MenuItem("Alien Apartments/Save/Show Save File")]
    private static void ShowSaveFile()
    {
        if (System.IO.File.Exists(SaveSystem.FilePath)) EditorUtility.RevealInFinder(SaveSystem.FilePath);
        else EditorUtility.RevealInFinder(Application.persistentDataPath);
    }
}