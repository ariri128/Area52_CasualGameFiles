using UnityEngine;
using System.Collections.Generic;

/*
 * Marks an object in a room as a piece of furniture the player can tap, move, replace, or remove - relies on the pieces Box Collider
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