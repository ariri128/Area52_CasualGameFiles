using UnityEngine;
using UnityEngine.EventSystems;

/*
 * One draggable part of the furniture move gizmo
 *   Move - the middle handle: slides the piece along the floor
 *   Turn - the handle on the ring: turns the piece like a dial
 * Needs an Image with Raycast Target on, so it can be grabbed
*/

public enum GizmoHandleKind { Move, Turn }

public class FurnitureGizmoHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private GizmoHandleKind kind = GizmoHandleKind.Move;

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (FurnitureMover.main != null) FurnitureMover.main.OnHandleBeginDrag(kind, eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (FurnitureMover.main != null) FurnitureMover.main.OnHandleDrag(kind, eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (FurnitureMover.main != null) FurnitureMover.main.OnHandleEndDrag();
    }
}