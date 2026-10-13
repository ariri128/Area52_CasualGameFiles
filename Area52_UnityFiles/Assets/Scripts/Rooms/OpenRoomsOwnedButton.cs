using UnityEngine;
using UnityEngine.UI;

/*
 * Goes on the camera button in the HUD prefab - Opens Rooms Owned
 * In the breeding room it first goes back to the apartments, since Rooms Owned lives there
*/

[RequireComponent(typeof(Button))]
public class OpenRoomsOwnedButton : MonoBehaviour
{
    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(Open);
    }

    private void Open()
    {
        if (SceneFlow.main != null && SceneFlow.main.InBreedingRoom)
            SceneFlow.main.ReturnToApartments();

        if (RoomsOwnedPopup.main != null) RoomsOwnedPopup.main.Open();
        else Debug.LogWarning("OpenRoomsOwnedButton: no RoomsOwnedPopup in the Rooms scene.", this);
    }
}