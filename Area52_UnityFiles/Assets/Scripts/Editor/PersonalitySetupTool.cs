using UnityEngine;
using System.Collections.Generic;
using UnityEditor;

/*
 * Editor tools for personalities:
 *   Makes the 36 personality files with likes/dislikes linked
 *   Personality Table's - adds one row per eyes + mouth combo that an alien in the catalog has
*/

public static class PersonalitySetupTool
{
    private const string Folder = "Assets/Data/Aliens/Personalities";

    // Key, display name, likes (key), dislikes (key)
    private static readonly string[,] Rows =
    {
        { "curious",        "Head empty - Curious",            "gossip",          "confused" },
        { "happy",          "Happy",                           "goofy",           "sad" },
        { "smug",           "Smug",                            "upset",           "unbothered" },
        { "cautious",       "Apprehensive / cautious",         "intent",          "mischievous" },
        { "bored",          "Bored",                           "cool guy",        "system overload" },
        { "scared",         "Scared",                          "cautious",        "evil" },
        { "evil",           "The void - evil",                 "mischievous",     "hopeful" },
        { "disgust",        "Disgust",                         "horrified",       "dummy" },
        { "angry",          "Angry",                           "jealous",         "happy" },
        { "system overload","System overload",                 "dummy",           "head empty" },
        { "goofy",          "Goofy",                           "funny",           "smug" },
        { "irritated",      "Irritated / annoyed",             "disgust",         "goofy" },
        { "dummy",          "Dummy",                           "head empty",      "disapproving" },
        { "disapproving",   "Uh - side eye - disapproving",    "irritated",       "upset" },
        { "mischievous",    "Tehe - mischievous",              "unhinged",        "scared" },
        { "hurt",           "Hurt",                            "sad",             "angry" },
        { "sad",            "Sad",                             "emo girl",        "funny" },
        { "confused",       "Confused / puzzled",              "shocked",         "irritated" },
        { "upset",          "Upset",                           "concerned",       "disgust" },
        { "unbothered",     "Dingus - unbothered",             "bored",           "gossip" },
        { "emo girl",       "\"Doesn't care\" - emo girl",     "hurt",            "shocked" },
        { "bugz",           "Bugz",                            "hopeful",         "jealous" },
        { "cool guy",       "Cool guy",                        "bugz",            "crazy" },
        { "jealous",        "Grumbling - jealous",             "evil",            "concerned" },
        { "head empty",     "No thoughts, head empty - doesn't know of existence", "uncertain", "curious" },
        { "shocked",        "Shocked",                         "surprized",       "cool guy" },
        { "horrified",      "Horrified",                       "scared",          "unhinged" },
        { "unhinged",       "Unhinged",                        "crazy",           "cautious" },
        { "intent",         "A lil guy - intent",              "curious",         "uncertain" },
        { "funny",          "Rawr - funny side kick",          "smug",            "horrified" },
        { "concerned",      "Concerned",                       "disapproving",    "surprized" },
        { "crazy",          "Crazy / unhinged",                "angry",           "bored" },
        { "hopeful",        "Hopeful",                         "happy",           "hurt" },
        { "surprized",      "Surprized",                       "system overload", "intent" },
        { "uncertain",      "Uncertain",                       "confused",        "emo girl" },
        { "gossip",         "Gossip",                          "unbothered",      "bugz" },
    };

    [MenuItem("Alien Apartments/Personalities/Create All Personalities")]
    private static void CreateAll()
    {
        EnsureFolder();
        Dictionary<string, Personality> byKey = new Dictionary<string, Personality>();

        // Pass 1: make (or find) every file
        for (int i = 0; i < Rows.GetLength(0); i++)
        {
            string key = Rows[i, 0];
            string path = $"{Folder}/P_{Clean(key)}.asset";
            Personality p = AssetDatabase.LoadAssetAtPath<Personality>(path);
            if (p == null)
            {
                p = ScriptableObject.CreateInstance<Personality>();
                AssetDatabase.CreateAsset(p, path);
            }
            p.displayName = Rows[i, 1];
            p.shortName = key;
            byKey[key] = p;
        }

        // Pass 2: link likes and dislikes now that every file exists
        for (int i = 0; i < Rows.GetLength(0); i++)
        {
            Personality p = byKey[Rows[i, 0]];
            p.likesEveryone = false;
            p.likes = Link(Rows[i, 2], byKey, p);
            p.dislikes = Link(Rows[i, 3], byKey, p);
            EditorUtility.SetDirty(p);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Made/updated {byKey.Count} personalities in {Folder}.");
    }

    private static List<Personality> Link(string key, Dictionary<string, Personality> byKey, Personality owner)
    {
        List<Personality> list = new List<Personality>();
        if (string.IsNullOrEmpty(key)) return list;
        if (byKey.TryGetValue(key, out Personality found)) list.Add(found);
        else Debug.LogWarning($"{owner.displayName}: no personality called \"{key}\".", owner);
        return list;
    }

    [MenuItem("CONTEXT/PersonalityTable/Fill Rows From Catalog")]
    private static void FillRows(MenuCommand command)
    {
        PersonalityTable table = (PersonalityTable)command.context;
        if (table.catalog == null) { Debug.LogWarning("Drag the AlienCatalog into Catalog first.", table); return; }

        // One row per eyes + mouth combo that some alien in the catalog actually has
        int added = 0;
        foreach (AlienSpecies s in table.catalog.species)
        {
            if (s == null) continue;
            AlienSpecies eyes = s.TraitOrigin(TraitSlot.Eyes);
            AlienSpecies mouth = s.TraitOrigin(TraitSlot.Mouth);

            PersonalityTable.Entry entry = table.entries.Find(e => e.eyesFrom == eyes && e.mouthFrom == mouth);
            if (entry == null)
            {
                entry = new PersonalityTable.Entry { eyesFrom = eyes, mouthFrom = mouth };
                table.entries.Add(entry);
                added++;
            }
            if (entry.example == null) entry.example = s.sprite;
        }

        EditorUtility.SetDirty(table);
        AssetDatabase.SaveAssets();
        Debug.Log($"Personality Table: {added} rows added, {table.entries.Count} total.", table);
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Data/Aliens")) AssetDatabase.CreateFolder("Assets/Data", "Aliens");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Data/Aliens", "Personalities");
    }

    private static string Clean(string s) => s.Replace(" ", "");
}