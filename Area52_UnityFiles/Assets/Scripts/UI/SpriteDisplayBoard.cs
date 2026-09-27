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

    [Header("Label")]
    [Tooltip("Gap (canvas pixels) between the bottom of the sprite and its name.")]
    [SerializeField] private float labelGap = 8f;

    // One built base cell: its sprite image, its label, and the sprite's width / height
    private struct Cell
    {
        public RectTransform spriteRect;
        public RectTransform labelRect;
        public float aspect;
    }

    private readonly List<Cell> cells = new List<Cell>();
    private float labelHeight = 50f;

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

        // The label's height comes from the template, so it can be changed there
        Transform templateLabel = cellTemplate.transform.Find("Label");
        if (templateLabel != null) labelHeight = ((RectTransform)templateLabel).rect.height;

        foreach (Entry entry in entries)
        {
            if (entry == null || entry.sprite == null) continue;

            GameObject cellObject = Instantiate(cellTemplate, grid.transform);
            cellObject.name = $"Cell_{entry.sprite.name}";
            cellObject.SetActive(true);

            Cell cell = new Cell();
            Rect spriteSize = entry.sprite.rect;
            cell.aspect = spriteSize.height > 0f ? spriteSize.width / spriteSize.height : 1f;

            Transform spriteChild = cellObject.transform.Find("Sprite");
            Image image = spriteChild != null ? spriteChild.GetComponent<Image>() : null;
            if (image != null)
            {
                image.sprite = entry.sprite;
                image.preserveAspect = true; // Prevents the art from stretching
                image.raycastTarget = false;
                cell.spriteRect = (RectTransform)spriteChild;
            }
            else
            {
                Debug.LogWarning($"{name}: Cell Template has no child named \"Sprite\" with an Image.", this);
            }

            Transform labelChild = cellObject.transform.Find("Label");
            TMP_Text label = labelChild != null ? labelChild.GetComponent<TMP_Text>() : null;
            if (label != null)
            {
                label.text = string.IsNullOrEmpty(entry.label) ? CleanName(entry.sprite.name) : entry.label;
                label.raycastTarget = false;
                cell.labelRect = (RectTransform)labelChild;
            }

            cells.Add(cell);
        }

        if (cells.Count > 0)
        {
            Vector2 cellSize = FitCells();
            foreach (Cell cell in cells) PlaceInsideCell(cell, cellSize);
        }
    }

    // Picks the column count and cell size so everything fits inside the Grid's rectangle, and returns the cell size
    private Vector2 FitCells()
    {
        int count = cells.Count;
        Rect area = ((RectTransform)grid.transform).rect;
        float usableWidth = area.width - grid.padding.horizontal;
        float usableHeight = area.height - grid.padding.vertical;

        int bestColumns = Mathf.Clamp(columns, 1, count);
        Vector2 bestSize = CellSize(bestColumns, count, usableWidth, usableHeight);

        // Automatically tries every column count and keep the one where the sprites themselves (not the empty cells around them) cover the most space
        if (columns <= 0)
        {
            float bestScore = -1f;
            for (int c = 1; c <= count; c++)
            {
                Vector2 size = CellSize(c, count, usableWidth, usableHeight);
                float score = 0f;
                foreach (Cell cell in cells)
                {
                    Vector2 shown = SpriteSizeIn(size, cell.aspect);
                    score += shown.x * shown.y;
                }

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
        return bestSize;
    }

    private Vector2 CellSize(int cols, int count, float usableWidth, float usableHeight)
    {
        int rows = Mathf.CeilToInt(count / (float)cols);
        float width = (usableWidth - grid.spacing.x * (cols - 1)) / cols;
        float height = (usableHeight - grid.spacing.y * (rows - 1)) / rows;
        return new Vector2(Mathf.Max(1f, width), Mathf.Max(1f, height));
    }

    // Calculates how big a sprite of this shape shows in a cell, after leaving room for the label
    private Vector2 SpriteSizeIn(Vector2 cellSize, float aspect)
    {
        float maxWidth = cellSize.x;
        float maxHeight = Mathf.Max(1f, cellSize.y - labelHeight - labelGap);

        float width = maxWidth;
        float height = width / aspect;
        if (height > maxHeight)
        {
            height = maxHeight;
            width = height * aspect;
        }
        return new Vector2(width, height);
    }

    // Sizes the sprite to exactly its shown size and puts the label right under it, with the two centered together in the cell
    private void PlaceInsideCell(Cell cell, Vector2 cellSize)
    {
        Vector2 shown = SpriteSizeIn(cellSize, cell.aspect);
        float blockHeight = shown.y + labelGap + labelHeight;
        Vector2 center = new Vector2(0.5f, 0.5f);

        if (cell.spriteRect != null)
        {
            cell.spriteRect.anchorMin = center;
            cell.spriteRect.anchorMax = center;
            cell.spriteRect.pivot = center;
            cell.spriteRect.sizeDelta = shown;
            cell.spriteRect.anchoredPosition = new Vector2(0f, blockHeight * 0.5f - shown.y * 0.5f);
        }

        if (cell.labelRect != null)
        {
            cell.labelRect.anchorMin = center;
            cell.labelRect.anchorMax = center;
            cell.labelRect.pivot = center;
            cell.labelRect.sizeDelta = new Vector2(cellSize.x, labelHeight);
            cell.labelRect.anchoredPosition = new Vector2(0f, -blockHeight * 0.5f + labelHeight * 0.5f);
        }
    }

    private static string CleanName(string spriteName)
    {
        return spriteName.Replace('_', ' ').Trim();
    }
}