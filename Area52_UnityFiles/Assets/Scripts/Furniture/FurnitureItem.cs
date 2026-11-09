using UnityEngine;
using System.Collections.Generic;

/*
 * Marks an object in a room as a piece of furniture the player can tap, move, replace, or remove - relies on the pieces Box Collider
 * Detects which room the furniture is in and highlights it when selected
*/

[RequireComponent(typeof(BoxCollider))]
public class FurnitureItem : MonoBehaviour
{
    [Tooltip("What this piece is (name, icon, tab, placement type).")]
    [SerializeField] private FurnitureDefinition definition;

    [Tooltip("Other objects that belong to this piece, e.g. a pillow that's a separate mesh. " +
             "They move, turn and get removed along with it.")]
    [SerializeField] private List<Transform> attachedParts = new List<Transform>();

    [Tooltip("Only if this object's model faces a different way than the room pieces do: degrees to correct it by. " +
             "The furniture PREFABS need 180 (their models are turned around compared with the rooms'). Room pieces stay 0.")]
    [SerializeField] private float facingCorrection = 0f;

    private BoxCollider boxCollider;
    // private RoomFurnitureDetector room;

    // Outline
    private readonly List<Renderer> ownRenderers = new List<Renderer>();
    private readonly List<MeshRenderer> outlineRenderers = new List<MeshRenderer>();
    private bool renderersFound;
    private readonly List<Vector3> outlineCenters = new List<Vector3>();   // middle of each outlined mesh
    private MaterialPropertyBlock outlineBlock;
    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int CenterId = Shader.PropertyToID("_CenterOS");

    public FurnitureDefinition Definition => definition;

    // The piece this one is sitting on
    public FurnitureItem Host { get; internal set; }

    // How far the player has turned this piece (degrees), so a replacement can face the same way later
    public float PlayerYaw { get; internal set; }

    // The room this piece is in, or null while it's in storage - set by RoomFurnitureDetector
    public RoomFurnitureDetector Room { get; internal set; }

    /*
    // The room this piece is in
    public RoomFurnitureDetector Room
    {
        get
        {
            if (room == null) room = GetComponentInParent<RoomFurnitureDetector>();
            return room;
        }
    }
    */

    public BoxCollider Collider
    {
        get
        {
            if (boxCollider == null) boxCollider = GetComponent<BoxCollider>();
            return boxCollider;
        }
    }

    // Which way the piece faces along the floor, in degrees - works whatever its model's tilt is (e.g. -90 on X from Blender)
    // Measured on the MODEL (its biggest mesh), not the root, since some prefabs have the model as a turned child
    // Uses directions instead of the rotation numbers, which get muddled when a model is tipped exactly 90 degrees
    public float FacingYaw
    {
        get
        {
            Quaternion modelRotation = ModelTransform.rotation;
            Vector3 upInModel = Quaternion.Inverse(modelRotation) * Vector3.up; // Which of the model's own directions points up
            Vector3 frontInModel = Quaternion.FromToRotation(Vector3.up, upInModel) * Vector3.forward; // Its front, going by that
            Vector3 front = modelRotation * frontInModel; // That front, in the world
            return Mathf.Atan2(front.x, front.z) * Mathf.Rad2Deg + facingCorrection;
        }
    }

    // This piece's biggest mesh - the model itself, whether it's on this object or a child of it
    private Transform ModelTransform
    {
        get
        {
            Transform best = transform;
            float bestSize = -1f;
            foreach (MeshFilter filter in GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || filter.name == "Outline") continue;     // Skips the outline copies
                if (filter.GetComponentInParent<FurnitureItem>() != this) continue;     // Skips decor sitting on it

                Vector3 size = Vector3.Scale(filter.sharedMesh.bounds.size, filter.transform.lossyScale);
                float volume = Mathf.Abs(size.x * size.y * size.z);
                if (volume > bestSize) { best = filter.transform; bestSize = volume; }
            }
            return best;
        }
    }

    private void Awake()
    {
        AttachParts();
    }

    // Moves the attached parts under this piece when the game starts, so they follow it
    private void AttachParts()
    {
        foreach (Transform part in attachedParts)
        {
            if (part == null || part == transform || part.IsChildOf(transform)) continue;
            part.SetParent(transform, true);
        }
    }


    // BOX

    // Calculates the collider as a box in the world: its middle, half its size, and its rotation
    public FurnitureBox GetBox()
    {
        BoxCollider col = Collider;
        Vector3 scale = transform.lossyScale;
        Vector3 half = new Vector3(
            Mathf.Abs(col.size.x * scale.x),
            Mathf.Abs(col.size.y * scale.y),
            Mathf.Abs(col.size.z * scale.z)) * 0.5f;
        return new FurnitureBox(transform.TransformPoint(col.center), half, transform.rotation);
    }

    // Moves and turns the piece so its collider ends up exactly at "target"
    public void MoveBoxTo(FurnitureBox target)
    {
        FurnitureBox current = GetBox();
        Quaternion turn = target.rotation * Quaternion.Inverse(current.rotation);

        // Turns around the box's middle - not the boxe's pivot
        transform.rotation = turn * transform.rotation;
        transform.position = current.center + turn * (transform.position - current.center);

        // Slides the box over
        transform.position += target.center - current.center;
    }


    // HIGHLIGHT OUTLINE

    public bool IsHighlighted { get; private set; }

    // Draws an outline around the piece in "color", using a material with the Outline shader
    public void ShowHighlight(Material outlineMaterial, Color color)
    {
        if (outlineMaterial == null) return;
        if (outlineRenderers.Count == 0) CreateOutlines(outlineMaterial);

        if (outlineBlock == null) outlineBlock = new MaterialPropertyBlock();

        for (int i = 0; i < outlineRenderers.Count; i++)
        {
            MeshRenderer outline = outlineRenderers[i];
            if (outline == null) continue;

            // Swaps in the requested material (the regular or the flat outline) if it changed
            if (outline.sharedMaterial != outlineMaterial) SetAllSlots(outline, outlineMaterial);

            outlineBlock.Clear();
            outlineBlock.SetColor(OutlineColorId, color);
            outlineBlock.SetVector(CenterId, outlineCenters[i]);
            outline.SetPropertyBlock(outlineBlock);
            outline.enabled = true;
        }
        IsHighlighted = true;
    }

    public void ClearHighlight()
    {
        foreach (MeshRenderer outline in outlineRenderers)
        {
            if (outline != null) outline.enabled = false;
        }
        IsHighlighted = false;
    }

    private void CreateOutlines(Material outlineMaterial)
    {
        FindOwnRenderers();

        foreach (Renderer r in ownRenderers)
        {
            MeshFilter source = r != null ? r.GetComponent<MeshFilter>() : null;
            if (source == null || source.sharedMesh == null) continue;

            GameObject copy = new GameObject("Outline");
            copy.transform.SetParent(r.transform, false);
            copy.layer = r.gameObject.layer;

            copy.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
            MeshRenderer outline = copy.AddComponent<MeshRenderer>();
            SetAllSlots(outline, outlineMaterial);

            outline.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            outline.receiveShadows = false;
            outline.enabled = false;
            outlineRenderers.Add(outline);
            outlineCenters.Add(source.sharedMesh.bounds.center);
        }
    }

    // Sets one outline material per part of the mesh, so every part gets outlined
    private static void SetAllSlots(MeshRenderer outline, Material material)
    {
        MeshFilter filter = outline.GetComponent<MeshFilter>();
        int count = filter != null && filter.sharedMesh != null ? Mathf.Max(1, filter.sharedMesh.subMeshCount) : 1;
        Material[] materials = new Material[count];
        for (int i = 0; i < count; i++) materials[i] = material;
        outline.sharedMaterials = materials;
    }

    // This piece's meshes, including attached parts
    private void FindOwnRenderers()
    {
        if (renderersFound) return;
        renderersFound = true;
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            // Skips anything that belongs to a different piece sitting on this one, and outline copies
            if (r.GetComponentInParent<FurnitureItem>() != this) continue;
            if (outlineRenderers.Contains(r as MeshRenderer)) continue;
            ownRenderers.Add(r);
        }
    }


    // EDITOR HELPERS

    // Runs when the component is first added
    private void Reset()
    {
        FitColliderToModel();
    }

    // Sizes the Box Collider to fit this piece's model plus its attached parts
    [ContextMenu("Fit Collider To Model")]
    public void FitColliderToModel()
    {
        List<MeshFilter> meshes = new List<MeshFilter>(GetComponentsInChildren<MeshFilter>(true));
        foreach (Transform part in attachedParts)
        {
            if (part != null) meshes.AddRange(part.GetComponentsInChildren<MeshFilter>(true));
        }

        bool hasBounds = false;
        Bounds localBounds = new Bounds();

        foreach (MeshFilter filter in meshes)
        {
            if (filter.sharedMesh == null) continue;

            // Converts the 8 corners of each mesh's box into the object's own space
            Bounds meshBounds = filter.sharedMesh.bounds;
            Vector3 min = meshBounds.min;
            Vector3 max = meshBounds.max;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new Vector3(
                    (i & 1) == 0 ? min.x : max.x,
                    (i & 2) == 0 ? min.y : max.y,
                    (i & 4) == 0 ? min.z : max.z);
                Vector3 local = transform.InverseTransformPoint(filter.transform.TransformPoint(corner));

                if (!hasBounds)
                {
                    localBounds = new Bounds(local, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    localBounds.Encapsulate(local);
                }
            }
        }

        if (!hasBounds) return;
        Collider.center = localBounds.center;
        Collider.size = localBounds.size;
    }

    [ContextMenu("Print Facing")]
    private void PrintFacing()
    {
        Transform model = ModelTransform;
        string modelOn = model == transform ? "on this object" : $"on child \"{model.name}\"";
        string placement = definition != null ? definition.placement.ToString() : "NO DATA FILE";
        Debug.Log($"{name} faces {FacingYaw:0} degrees (model {modelOn}, correction {facingCorrection}). " +
                  $"Placement: {placement}. Collider: {GetBox().halfExtents * 2f:F2} m.", this);
    }

    // CALIBRATING (fixes models that were built sideways or backwards)
    // Look at the piece, pick which way its FRONT roughly points, and it sets Facing Correction for you
    // "Roughly" is fine: a couch angled 40 degrees toward the camera still counts as Faces Camera

    [ContextMenu("Calibrate Facing/Front Faces Camera")]
    private void CalibrateCamera() => Calibrate(180f, "the camera");

    [ContextMenu("Calibrate Facing/Front Faces Back Wall")]
    private void CalibrateBackWall() => Calibrate(0f, "the back wall");

    [ContextMenu("Calibrate Facing/Front Faces Left Wall")]
    private void CalibrateLeftWall() => Calibrate(-90f, "the left wall");

    [ContextMenu("Calibrate Facing/Front Faces Right Wall")]
    private void CalibrateRightWall() => Calibrate(90f, "the right wall");

    private void Calibrate(float realFacing, string description)
    {
#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(this, "Calibrate Facing"); // So Ctrl/Cmd+Z works
#endif
        float measured = FacingYaw - facingCorrection; // What the model says, with no correction

        // Models are only ever built off by a quarter or half turn, so round to the nearest 90
        // That's also why "roughly" is fine - an angled piece doesn't throw it off
        facingCorrection = Mathf.Round(Mathf.DeltaAngle(measured, realFacing) / 90f) * 90f;
        if (facingCorrection <= -180f) facingCorrection += 360f; // Keeps it between -90 and 180

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this); // Saves it on the prefab / in the room
        UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
#endif
        Debug.Log($"{name}: front faces {description} - Facing Correction set to {facingCorrection}. Now faces {FacingYaw:0} degrees.", this);
    }
}