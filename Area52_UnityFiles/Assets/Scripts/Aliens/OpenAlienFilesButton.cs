using UnityEngine;
using UnityEngine.UI;

/*
 * Goes on the folder button in the HUD prefab - opens Alien Files
 * In the breeding room it first goes back to the apartments, since Alien Files lives there
*/

[RequireComponent(typeof(Button))]
public class OpenAlienFilesButton : MonoBehaviour
{
    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(Open);
    }

    private void Open()
    {
        if (SceneFlow.main != null && SceneFlow.main.InBreedingRoom)
            SceneFlow.main.ReturnToApartments();

        if (AlienFilesPopup.main != null) AlienFilesPopup.main.Open();
        else Debug.LogWarning("OpenAlienFilesButton: no AlienFilesPopup in the Rooms scene.", this);
    }
}