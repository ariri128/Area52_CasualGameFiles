using UnityEngine;

/*
 * Describes one sheet of pre-drawn babies (15 babies): which alien gave every baby on it its BODY, who the other parent is, and whether the layout is the usual one or reversed
 * The tool button on this file cuts the sheet into 15 sprites, standing on their feet, and makes a baby data file for each
*/

[CreateAssetMenu(menuName = "Alien Apartments/Combo Sheet", fileName = "CS_NewSheet")]
public class ComboSheet : ScriptableObject
{
    [Tooltip("The sheet PNG.")]
    public Texture2D sheet;

    [Tooltip("The alien whose BODY every baby on this sheet has.")]
    public AlienSpecies bodyFrom;

    [Tooltip("The other parent.")]
    public AlienSpecies otherParent;

    [Tooltip("Tick if cells 0-7 have the BODY alien's antenna instead of the other parent's.")]
    public bool reversed;

    [Tooltip("Babies are added to this catalog.")]
    public AlienCatalog catalog;

    [Header("Sheet")]
    public int columns = 5;
    public int rows = 3;

    [Tooltip("Lower = bigger in the rooms. Change it until the babies are the size you want.")]
    public float pixelsPerUnit = 800f;

    [Tooltip("Where the baby data files are saved.")]
    public string outputFolder = "Assets/Data/Aliens/Babies";

    // Antenna, eyes, mouth, legs for each cell. B = body alien, O = other parent.
    public static readonly string[] Layout =
    {
        "OBBB", "OBOB", "OOBB", "OBBO", "OOOB",
        "OBOO", "OOBO", "OOOO", "BBOB", "BOOB",
        "BBOO", "BOOO", "BOBB", "BOBO", "BBBO",
    };
}