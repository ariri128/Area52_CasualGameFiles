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

    private BoxCollider boxCollider;
    private RoomFurnitureDetector room;

    // Outline
    private readonly List<Renderer> ownRenderers = new List<Renderer>();
    private readonly List<MeshRenderer> outlineRenderers = new List<MeshRenderer>();
    private bool renderersFound;
    private readonly List<Vector3> outlineCenters = new List<Vector3>();   // middle of each outlined mesh
    private MaterialPropertyBlock outlineBlock;
    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int CenterId = Shader.PropertyToID("_CenterOS");

    public FurnitureDefinition Definition => definition;

    // The room this piece is in
    public RoomFurnitureDetector Room
    {
        get
        {
            if (room == null) room = GetComponentInParent<RoomFurnitureDetector>();
            return room;
        }
    }

    public BoxCollider Collider
    {
        get
        {
            if (boxCollider == null) boxCollider = GetComponent<BoxCollider>();
            return boxCollider;
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

            // Swaps in the requested material if it changed
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
            copy.transform.SetParent(r.transform, false); // Keeps the same position, rotation and scale as the mesh
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


    // EDITOR HELPER

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
}

/* ver.4
[RequireComponent(typeof(BoxCollider))]
public class FurnitureItem : MonoBehaviour
{
    [Tooltip("What this piece is (name, icon, tab, placement type).")]
    [SerializeField] private FurnitureDefinition definition;

    [Tooltip("Other objects that belong to this piece, e.g. a pillow that's a separate mesh. " +
             "They move, turn and get removed along with it.")]
    [SerializeField] private List<Transform> attachedParts = new List<Transform>();

    private BoxCollider boxCollider;
    private RoomFurnitureDetector room;

    // Outline
    private readonly List<Renderer> ownRenderers = new List<Renderer>();
    private readonly List<MeshRenderer> outlineRenderers = new List<MeshRenderer>();
    private bool renderersFound;
    private MaterialPropertyBlock colorBlock;
    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");

    public FurnitureDefinition Definition => definition;

    // The room this piece is in
    public RoomFurnitureDetector Room
    {
        get
        {
            if (room == null) room = GetComponentInParent<RoomFurnitureDetector>();
            return room;
        }
    }

    public BoxCollider Collider
    {
        get
        {
            if (boxCollider == null) boxCollider = GetComponent<BoxCollider>();
            return boxCollider;
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


    // HIGHLIGHTED OUTLINE

    public bool IsHighlighted { get; private set; }

    // Draws an outline around the piece in "color", using a material with the Outline shader
    public void ShowHighlight(Material outlineMaterial, Color color)
    {
        if (outlineMaterial == null) return;
        if (outlineRenderers.Count == 0) CreateOutlines(outlineMaterial);

        if (colorBlock == null) colorBlock = new MaterialPropertyBlock();
        colorBlock.Clear();
        colorBlock.SetColor(OutlineColorId, color);

        foreach (MeshRenderer outline in outlineRenderers)
        {
            if (outline == null) continue;
            outline.SetPropertyBlock(colorBlock);
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
            copy.transform.SetParent(r.transform, false); // Keeps the same position, rotation and scale as the mesh
            copy.layer = r.gameObject.layer;

            copy.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
            MeshRenderer outline = copy.AddComponent<MeshRenderer>();

            // One outline material per part of the mesh, so every part gets outlined
            Material[] materials = new Material[source.sharedMesh.subMeshCount];
            for (int i = 0; i < materials.Length; i++) materials[i] = outlineMaterial;
            outline.sharedMaterials = materials;

            outline.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            outline.receiveShadows = false;
            outline.enabled = false;
            outlineRenderers.Add(outline);
        }
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
}
/*

/* ver.3
[RequireComponent(typeof(BoxCollider))]
public class FurnitureItem : MonoBehaviour
{
    [Tooltip("What this piece is (name, icon, tab, placement type).")]
    [SerializeField] private FurnitureDefinition definition;

    [Tooltip("Other objects that belong to this piece, e.g. a pillow that's a separate mesh. " +
             "They move, turn and get removed along with it.")]
    [SerializeField] private List<Transform> attachedParts = new List<Transform>();

    private BoxCollider boxCollider;
    private RoomFurnitureDetector room;

    // Highlight
    private readonly List<Renderer> ownRenderers = new List<Renderer>();
    private readonly Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();
    private bool renderersFound;
    private MaterialPropertyBlock colorBlock;
    private MaterialPropertyBlock emptyBlock;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");   // URP Lit's color

    public FurnitureDefinition Definition => definition;

    // The room this piece is in
    public RoomFurnitureDetector Room
    {
        get
        {
            if (room == null) room = GetComponentInParent<RoomFurnitureDetector>();
            return room;
        }
    }

    public BoxCollider Collider
    {
        get
        {
            if (boxCollider == null) boxCollider = GetComponent<BoxCollider>();
            return boxCollider;
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


    // HIGHLIGHT

    public bool IsHighlighted => originalMaterials.Count > 0;

    public void ShowHighlight(Material material, Color color)
    {
        if (material == null) return;
        FindOwnRenderers();
        if (colorBlock == null) colorBlock = new MaterialPropertyBlock();

        colorBlock.Clear();
        colorBlock.SetColor(BaseColorId, color);

        foreach (Renderer r in ownRenderers)
        {
            if (r == null) continue;

            // Remembers the real materials the first time, so it can be put back
            if (!originalMaterials.ContainsKey(r)) originalMaterials[r] = r.sharedMaterials;

            Material[] swapped = new Material[originalMaterials[r].Length];
            for (int i = 0; i < swapped.Length; i++) swapped[i] = material;
            r.sharedMaterials = swapped;

            // One highlight material can show any color
            r.SetPropertyBlock(colorBlock);
        }
    }

    // Puts the piece's own materials back
    public void ClearHighlight()
    {
        if (emptyBlock == null) emptyBlock = new MaterialPropertyBlock();

        foreach (KeyValuePair<Renderer, Material[]> entry in originalMaterials)
        {
            if (entry.Key == null) continue;
            entry.Key.sharedMaterials = entry.Value;
            entry.Key.SetPropertyBlock(emptyBlock);
        }
        originalMaterials.Clear();
    }

    // This piece's meshes, including attached parts
    private void FindOwnRenderers()
    {
        if (renderersFound) return;
        renderersFound = true;
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            // Skips anything that belongs to a different piece sitting on this one
            if (r.GetComponentInParent<FurnitureItem>() == this) ownRenderers.Add(r);
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
}
*/

/* ver.2
[RequireComponent(typeof(BoxCollider))]
public class FurnitureItem : MonoBehaviour
{
    [Tooltip("What this piece is (name, icon, tab, placement type).")]
    [SerializeField] private FurnitureDefinition definition;

    [Tooltip("Other objects that belong to this piece, e.g. a pillow that's a separate mesh. " +
             "They move, turn and get removed along with it.")]
    [SerializeField] private List<Transform> attachedParts = new List<Transform>();

    private BoxCollider boxCollider;
    private RoomFurnitureDetector room;

    // Tinting
    private readonly List<Renderer> ownRenderers = new List<Renderer>();
    private bool renderersFound;
    private MaterialPropertyBlock tintBlock;
    private MaterialPropertyBlock emptyBlock;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");   // URP Lit's color

    public FurnitureDefinition Definition => definition;

    // Detects the room this piece is in
    public RoomFurnitureDetector Room
    {
        get
        {
            if (room == null) room = GetComponentInParent<RoomFurnitureDetector>();
            return room;
        }
    }

    public BoxCollider Collider
    {
        get
        {
            if (boxCollider == null) boxCollider = GetComponent<BoxCollider>();
            return boxCollider;
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


    // TINT

    // Colors the piece by multiplying its material color - Color.white puts it back to normal
    // Uses a MaterialPropertyBlock, so the shared room material itself is never changed
    public void SetTint(Color tint)
    {
        FindOwnRenderers();
        if (tintBlock == null) tintBlock = new MaterialPropertyBlock();
        if (emptyBlock == null) emptyBlock = new MaterialPropertyBlock();

        bool backToNormal = tint == Color.white;

        foreach (Renderer r in ownRenderers)
        {
            if (r == null) continue;
            Material[] materials = r.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                if (backToNormal || materials[i] == null || !materials[i].HasProperty(BaseColorId))
                {
                    r.SetPropertyBlock(emptyBlock, i);
                    continue;
                }

                tintBlock.Clear();
                tintBlock.SetColor(BaseColorId, materials[i].GetColor(BaseColorId) * tint);
                r.SetPropertyBlock(tintBlock, i);
            }
        }
    }

    // This piece's meshes, including attached parts
    private void FindOwnRenderers()
    {
        if (renderersFound) return;
        renderersFound = true;
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            if (r.GetComponentInParent<FurnitureItem>() == this) ownRenderers.Add(r);
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
}
*/

/* ver.1
[RequireComponent(typeof(BoxCollider))]
public class FurnitureItem : MonoBehaviour
{
    [Tooltip("What this piece is (name, icon, tab, placement type).")]
    [SerializeField] private FurnitureDefinition definition;

    [Tooltip("Other objects that belong to this piece, e.g. a pillow that's a separate mesh. " +
             "They move, turn and get removed along with it.")]
    [SerializeField] private List<Transform> attachedParts = new List<Transform>();

    private BoxCollider boxCollider;

    public FurnitureDefinition Definition => definition;

    public BoxCollider Collider
    {
        get
        {
            if (boxCollider == null) boxCollider = GetComponent<BoxCollider>();
            return boxCollider;
        }
    }

    private void Awake()
    {
        AttachParts();
    }

    // Moves the attached parts, that don't have their own data file, under this piece when the game starts, so they follow it
    private void AttachParts()
    {
        foreach (Transform part in attachedParts)
        {
            if (part == null || part == transform || part.IsChildOf(transform)) continue;
            part.SetParent(transform, true);
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
}
*/