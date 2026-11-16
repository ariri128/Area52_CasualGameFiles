using UnityEngine;
using TMPro;

/*
 * The "Exploration Timer: 00:00" text at the top middle of the screen
 * Shows how long until the alien from the room the player is looking at gets back from exploring
 * Hidden when no alien from this room is out (only one alien per room can explore at a time)
 * Put it on the text object itself; it hides and shows its own text
*/

[RequireComponent(typeof(TMP_Text))]
public class ExplorationTimerText : MonoBehaviour
{
    [Tooltip("{0} = the time left, like 14:59.")]
    [SerializeField] private string format = "Exploration Timer: {0}";

    private TMP_Text label;
    private int lastSecondShown = -1;

    private void Awake()
    {
        label = GetComponent<TMP_Text>();
        label.enabled = false;
    }

    // Checked every frame: the room, the timer and whether anyone is out can all change at any moment
    private void Update()
    {
        bool show = false;
        float secondsLeft = 0f;

        if (RoomManager.main != null && ExplorationManager.main != null)
            show = ExplorationManager.main.TryGetRoomTimer(RoomManager.main.CurrentRoomIndex, out secondsLeft);

        if (label.enabled != show) label.enabled = show;
        if (!show) { lastSecondShown = -1; return; }

        int seconds = Mathf.CeilToInt(secondsLeft);
        if (seconds == lastSecondShown) return;
        lastSecondShown = seconds;

        string time = seconds >= 3600
            ? $"{seconds / 3600}:{seconds / 60 % 60:00}:{seconds % 60:00}"
            : $"{seconds / 60:00}:{seconds % 60:00}";
        label.text = string.Format(format, time);
    }
}