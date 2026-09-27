using UnityEngine;
using UnityEngine.UI;

/*
 * Makes a button play the click sound when it's pressed
*/

[RequireComponent(typeof(Button))]
public class ButtonClickSound : MonoBehaviour
{
    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(AudioManager.PlayClick);
    }
}