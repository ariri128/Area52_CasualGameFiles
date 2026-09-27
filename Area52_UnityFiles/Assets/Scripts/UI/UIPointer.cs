using UnityEngine;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/*
 * Detects whether player is clicking the room screen or the on-screen UI
*/

public static class UIPointer
{
    private static readonly List<RaycastResult> results = new List<RaycastResult>();

    public static bool IsOverScreenUI(Vector2 screenPosition)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null) return false;

        PointerEventData data = new PointerEventData(eventSystem) { position = screenPosition };
        results.Clear();
        eventSystem.RaycastAll(data, results);

        foreach (RaycastResult result in results)
        {
            GraphicRaycaster raycaster = result.module as GraphicRaycaster;
            if (raycaster == null) continue; // 3D physics hits aren't detected as UI

            Canvas canvas = raycaster.GetComponent<Canvas>();
            if (canvas != null && canvas.renderMode == RenderMode.WorldSpace) continue;

            return true;
        }
        return false;
    }
}