using UnityEngine;
using UnityEngine.UI;

/*
 * Sends the player from the breeding room back to the apartments
*/

[RequireComponent(typeof(Button))]
public class BackToApartmentsButton : MonoBehaviour
{
    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(Back);
    }

    private void Back()
    {
        if (SceneFlow.main != null)
            SceneFlow.main.ReturnToApartments();
        else
            Debug.Log("No SceneFlow found. Open the Rooms scene and press Play from there.");
    }
}