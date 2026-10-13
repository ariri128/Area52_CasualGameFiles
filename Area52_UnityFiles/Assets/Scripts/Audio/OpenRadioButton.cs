using UnityEngine;
using UnityEngine.UI;

/*
 * Goes on the radio button in the HUD prefab - opens the Radio Tunes popup
*/

[RequireComponent(typeof(Button))]
public class OpenRadioButton : MonoBehaviour
{
    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(Open);
    }

    private void Open()
    {
        if (RadioPopup.main != null) RadioPopup.main.Open();
        else Debug.LogWarning("OpenRadioButton: no RadioPopup in the Rooms scene.", this);
    }
}