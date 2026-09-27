using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

/*
 * Art-prototype 2D asset display board
*/

public class SpriteDisplayBoard : MonoBehaviour
{
    [System.Serializable]
    public class Entry
    {
        public Sprite sprite;

        [Tooltip("Name shown under the sprite. Leave empty to use the sprite's file name.")]
        public string label;
    }

    [Header("Content")]
    [Tooltip("Heading at the top of the board, e.g. \"Eggplant\".")]
    [SerializeField] private string title = "Display";

    [Tooltip("Sprites to show, in reading order (left to right, top to bottom).")]
    [SerializeField] private List<Entry> entries = new List<Entry>();

    [Tooltip("Sprites per row. 0 = pick whatever makes them biggest.")]
    [SerializeField] private int columns = 0;

    [Header("Parts")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private GridLayoutGroup grid;
    [Tooltip("One cell: an empty object with a \"Sprite\" Image child and a \"Label\" text child.")]
    [SerializeField] private GameObject cellTemplate;

    private void Start()
    {
        Build();
    }

    private void Build()
    {
        if (titleText != null) titleText.text = title;

        if (grid == null || cellTemplate == null)
        {
            Debug.LogWarning($"{name}: SpriteDisplayBoard needs Grid and Cell Template filled in.", this);
            return;
        }

        cellTemplate.SetActive(false);

        int count = 0;
        foreach (Entry entry in entries)
        {
            if (entry == null || entry.sprite == null) continue;

            GameObject cell = Instantiate(cellTemplate, grid.transform);
            cell.name = $"Cell_{entry.sprite.name}";
            cell.SetActive(true);

            Transform spriteChild = cell.transform.Find("Sprite");
            Image image = spriteChild != null ? spriteChild.GetComponent<Image>() : null;
            if (image != null)
            {
                image.sprite = entry.sprite;
                image.preserveAspect = true; // Prevents the art from stretching
                image.raycastTarget = false;
            }
            else
            {
                Debug.LogWarning($"{name}: Cell Template has no child named \"Sprite\" with an Image.", this);
            }

            Transform labelChild = cell.transform.Find("Label");
            TMP_Text label = labelChild != null ? labelChild.GetComponent<TMP_Text>() : null;
            if (label != null)
            {
                label.text = string.IsNullOrEmpty(entry.label) ? CleanName(entry.sprite.name) : entry.label;
                label.raycastTarget = false;
            }

            count++;
        }

        if (count > 0) FitCells(count);
    }

    // Sizes the cells so all of them fit inside the Grid's rectangle
    private void FitCells(int count)
    {
        Rect area = ((RectTransform)grid.transform).rect;
        float usableWidth = area.width - grid.padding.horizontal;
        float usableHeight = area.height - grid.padding.vertical;

        int bestColumns = Mathf.Clamp(columns, 1, count);
        Vector2 bestSize = CellSize(bestColumns, count, usableWidth, usableHeight);

        // Automatically tries every column count and keep the one with the biggest cells
        if (columns <= 0)
        {
            float bestScore = -1f;
            for (int c = 1; c <= count; c++)
            {
                Vector2 size = CellSize(c, count, usableWidth, usableHeight);
                float score = Mathf.Min(size.x, size.y);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestColumns = c;
                    bestSize = size;
                }
            }
        }

        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = bestColumns;
        grid.cellSize = bestSize;
    }

    private Vector2 CellSize(int cols, int count, float usableWidth, float usableHeight)
    {
        int rows = Mathf.CeilToInt(count / (float)cols);
        float width = (usableWidth - grid.spacing.x * (cols - 1)) / cols;
        float height = (usableHeight - grid.spacing.y * (rows - 1)) / rows;
        return new Vector2(Mathf.Max(1f, width), Mathf.Max(1f, height));
    }

    private static string CleanName(string spriteName)
    {
        return spriteName.Replace('_', ' ').Trim();
    }
}