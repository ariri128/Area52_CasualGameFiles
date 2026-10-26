using UnityEngine;
using System.Collections.Generic;

/*
 * An invisible full-screen panel on top of all the UI that catches every tap while something is animating
 * (the exploration ship, the artifact reveal)
 * Because taps land on UI, aliens can't be tapped and rooms can't be swiped either (both ignore presses that start on UI)
 * Anything can block it:  ScreenBlocker.SetBlocked(this, true)  ...  ScreenBlocker.SetBlocked(this, false)
 * Put this on an object that stays switched on, with the panel as its child
*/

public class ScreenBlocker : MonoBehaviour
{
    private static ScreenBlocker main;
    private static readonly HashSet<Object> blockers = new HashSet<Object>();

    [Tooltip("A child Image covering the whole screen: transparent (alpha 0), Raycast Target ticked. Switched on while blocked.")]
    [SerializeField] private GameObject panel;

    // Clears the blockers when Play starts
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        blockers.Clear();
        main = null;
    }

    // Anything that was deleted without unblocking is dropped, so the screen can't get stuck blocked
    public static bool IsBlocked
    {
        get
        {
            blockers.RemoveWhere(o => o == null);
            return blockers.Count > 0;
        }
    }

    public static void SetBlocked(Object owner, bool blocked)
    {
        if (owner == null) return;
        if (blocked) blockers.Add(owner);
        else blockers.Remove(owner);

        if (main != null) main.Refresh(); // Straight away, not next frame
    }

    private void Awake()
    {
        main = this;
        Refresh();
    }

    private void OnDestroy()
    {
        if (main == this) main = null;
    }

    private void LateUpdate() => Refresh();

    private void Refresh()
    {
        if (panel == null) return;
        bool blocked = IsBlocked;
        if (panel.activeSelf != blocked) panel.SetActive(blocked);
    }
}