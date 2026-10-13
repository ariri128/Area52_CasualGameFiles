using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;

/*
 * Adds a "Cut Sheet and Make Babies" button to Combo Sheet files
*/

[CustomEditor(typeof(ComboSheet))]
public class ComboSheetEditor : Editor
{
    private const byte AlphaCutoff = 10;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();

        if (GUILayout.Button("Cut Sheet and Make Babies", GUILayout.Height(32)))
            Build((ComboSheet)target);
    }

    private static void Build(ComboSheet comboSheet)
    {
        if (comboSheet.sheet == null || comboSheet.bodyFrom == null || comboSheet.otherParent == null)
        {
            Debug.LogError("Combo Sheet needs Sheet, Body From and Other Parent filled in.", comboSheet);
            return;
        }

        string sheetPath = AssetDatabase.GetAssetPath(comboSheet.sheet);
        TextureImporter importer = AssetImporter.GetAtPath(sheetPath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError($"{sheetPath} isn't an image Unity can import.", comboSheet);
            return;
        }

        // Import settings
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = comboSheet.pixelsPerUnit;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.maxTextureSize = 4096; // Keepa the sheet full size so the cuts line up
        importer.SaveAndReimport();

        // Reada the original PNG to find where each drawing is
        Texture2D pixelsSource = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        pixelsSource.LoadImage(File.ReadAllBytes(sheetPath));
        int width = pixelsSource.width;
        int height = pixelsSource.height;
        Color32[] pixels = pixelsSource.GetPixels32();
        Object.DestroyImmediate(pixelsSource);

        SpriteDataProviderFactories factories = new SpriteDataProviderFactories();
        factories.Init();
        ISpriteEditorDataProvider provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();

        // Reuses the old IDs so anything already using these sprites keeps working
        Dictionary<string, SpriteRect> oldRects = new Dictionary<string, SpriteRect>();
        foreach (SpriteRect old in provider.GetSpriteRects())
            if (!oldRects.ContainsKey(old.name)) oldRects.Add(old.name, old);

        int cellCount = comboSheet.columns * comboSheet.rows;
        List<SpriteRect> rects = new List<SpriteRect>();
        string[] spriteNames = new string[cellCount];

        // Finds the empty gaps between babies - the drawings aren't always on an exact grid, so the cuts go in the gaps instead of at fixed fifths and thirds
        List<Vector2Int> columnBands = FindBands(pixels, width, height, true);
        List<Vector2Int> rowBands = FindBands(pixels, width, height, false);
        rowBands.Reverse();

        bool useBands = columnBands.Count == comboSheet.columns && rowBands.Count == comboSheet.rows;
        if (!useBands)
            Debug.LogWarning($"{comboSheet.sheet.name}: found {columnBands.Count} x {rowBands.Count} drawings " +
                             $"instead of {comboSheet.columns} x {comboSheet.rows}, so it's cut on an even grid instead. " +
                             "Check the babies don't get cut off.", comboSheet);

        float cellWidth = width / (float)comboSheet.columns;
        float cellHeight = height / (float)comboSheet.rows;

        for (int cell = 0; cell < cellCount; cell++)
        {
            int column = cell % comboSheet.columns;
            int row = cell / comboSheet.columns;

            int left, right, bottom, top;
            if (useBands)
            {
                left = columnBands[column].x;
                right = columnBands[column].y + 1;
                bottom = rowBands[row].x;
                top = rowBands[row].y + 1;
            }
            else
            {
                left = Mathf.RoundToInt(column * cellWidth);
                right = Mathf.RoundToInt((column + 1) * cellWidth);
                top = height - Mathf.RoundToInt(row * cellHeight);
                bottom = height - Mathf.RoundToInt((row + 1) * cellHeight);
            }

            // Smallest box around everything drawn in this cell (antenna bits included)
            int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
            for (int y = bottom; y < top; y++)
            {
                for (int x = left; x < right; x++)
                {
                    if (pixels[y * width + x].a <= AlphaCutoff) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
            if (maxX < 0) continue;

            minX = Mathf.Max(0, minX - 2);
            minY = Mathf.Max(0, minY - 2);
            maxX = Mathf.Min(width - 1, maxX + 2);
            maxY = Mathf.Min(height - 1, maxY + 2);

            string spriteName = $"{comboSheet.sheet.name}_{cell}";
            SpriteRect rect = oldRects.TryGetValue(spriteName, out SpriteRect existing)
                ? existing
                : new SpriteRect { spriteID = GUID.Generate() };

            rect.name = spriteName;
            rect.rect = new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
            rect.alignment = SpriteAlignment.BottomCenter; // Stands on its feet
            rect.pivot = new Vector2(0.5f, 0f);
            rects.Add(rect);
            spriteNames[cell] = spriteName;
        }

        provider.SetSpriteRects(rects.ToArray());

        ISpriteNameFileIdDataProvider nameIds = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        if (nameIds != null)
            nameIds.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));

        provider.Apply();
        importer.SaveAndReimport();

        // One baby file per cell
        Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        foreach (Sprite sprite in AssetDatabase.LoadAllAssetsAtPath(sheetPath).OfType<Sprite>())
            sprites[sprite.name] = sprite;

        EnsureFolder(comboSheet.outputFolder);

        AlienSpecies body = comboSheet.bodyFrom;
        AlienSpecies other = comboSheet.otherParent;
        int made = 0, updated = 0, skipped = 0;

        for (int cell = 0; cell < cellCount && cell < ComboSheet.Layout.Length; cell++)
        {
            if (spriteNames[cell] == null || !sprites.ContainsKey(spriteNames[cell])) continue;

            string layout = ComboSheet.Layout[cell];
            AlienSpecies antenna = Pick(layout[0], comboSheet.reversed, body, other);
            AlienSpecies eyes = Pick(layout[1], comboSheet.reversed, body, other);
            AlienSpecies mouth = Pick(layout[2], comboSheet.reversed, body, other);
            AlienSpecies legs = Pick(layout[3], comboSheet.reversed, body, other);

            if (antenna == body && eyes == body && mouth == body && legs == body)
            {
                Debug.Log($"{comboSheet.sheet.name} cell {cell} is identical to {body.displayName}, " +
                          $"so it's skipped and the original {body.displayName} is used instead.", comboSheet);
                skipped++;
                continue;
            }

            string assetPath = $"{comboSheet.outputFolder}/AS_{Clean(body.displayName)}{Clean(other.displayName)}_{cell + 1:00}.asset";
            AlienSpecies baby = AssetDatabase.LoadAssetAtPath<AlienSpecies>(assetPath);
            bool isNew = baby == null;
            if (isNew) baby = ScriptableObject.CreateInstance<AlienSpecies>();

            baby.displayName = $"{body.displayName} x {other.displayName} {cell + 1}";
            baby.sprite = sprites[spriteNames[cell]];
            baby.traitsFrom = new TraitOrigins { body = body, antenna = antenna, legs = legs, eyes = eyes, mouth = mouth };

            if (isNew) { AssetDatabase.CreateAsset(baby, assetPath); made++; }
            else { EditorUtility.SetDirty(baby); updated++; }

            if (comboSheet.catalog != null && !comboSheet.catalog.species.Contains(baby))
                comboSheet.catalog.species.Add(baby);
        }

        if (comboSheet.catalog != null) EditorUtility.SetDirty(comboSheet.catalog);
        AssetDatabase.SaveAssets();

        Debug.Log($"{comboSheet.sheet.name}: {made} babies made, {updated} updated, {skipped} skipped." +
                  (comboSheet.catalog == null ? " No catalog set, so they weren't added to one." : ""), comboSheet);
    }

    // Stretches of columns (or rows) that have any drawing in them, separated by empty gaps
    private static List<Vector2Int> FindBands(Color32[] pixels, int width, int height, bool alongX)
    {
        int length = alongX ? width : height;
        bool[] filled = new bool[length];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (pixels[y * width + x].a <= AlphaCutoff) continue;
                filled[alongX ? x : y] = true;
            }
        }

        const int minGap = 4;
        List<Vector2Int> bands = new List<Vector2Int>();
        int start = -1, lastFilled = -1;
        for (int i = 0; i < length; i++)
        {
            if (!filled[i]) continue;
            if (start < 0) start = i;
            else if (i - lastFilled > minGap) { bands.Add(new Vector2Int(start, lastFilled)); start = i; }
            lastFilled = i;
        }
        if (start >= 0) bands.Add(new Vector2Int(start, lastFilled));
        return bands;
    }

    private static AlienSpecies Pick(char letter, bool reversed, AlienSpecies body, AlienSpecies other)
    {
        bool fromBody = letter == 'B';
        if (reversed) fromBody = !fromBody;
        return fromBody ? body : other;
    }

    private static string Clean(string name)
    {
        return new string(name.Where(char.IsLetterOrDigit).ToArray());
    }

    // Makes every folder in the path that doesn't exist yet
    private static void EnsureFolder(string folder)
    {
        string[] parts = folder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}