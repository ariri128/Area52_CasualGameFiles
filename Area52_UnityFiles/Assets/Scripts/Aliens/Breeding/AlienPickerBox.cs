using UnityEngine;
using TMPro;
using UnityEngine.Events;
using UnityEngine.UI;

/*
 * One of the two alien boxes that's next to the spaceship: a picture, a name under it, and an arrow button on each side
*/

public class AlienPickerBox : MonoBehaviour
{
    [SerializeField] private Image picture;
    [Tooltip("Optional. Leave empty if you don't want a name under the picture.")]
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;

    // Passes -1 for the left arrow, +1 for the right arrow
    public UnityEvent<int> onArrowPressed = new UnityEvent<int>();

    private void Awake()
    {
        previousButton.onClick.AddListener(() => onArrowPressed.Invoke(-1));
        nextButton.onClick.AddListener(() => onArrowPressed.Invoke(1));
    }

    public void Show(Sprite sprite, string label)
    {
        picture.sprite = sprite;
        picture.enabled = sprite != null;
        picture.preserveAspect = true;
        if (nameLabel != null) nameLabel.text = label;
    }

    // Locked = the arrows disappear so the shown alien can't be changed
    public void SetLocked(bool locked)
    {
        previousButton.gameObject.SetActive(!locked);
        nextButton.gameObject.SetActive(!locked);
    }
}