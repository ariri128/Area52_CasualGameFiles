using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

/*
 * The two alien boxes on either side of the spaceship and the Breed button
*/

public class BreedingSelectionUI : MonoBehaviour
{
    [Header("Parts")]
    [SerializeField] private AlienPickerBox leftBox;
    [SerializeField] private AlienPickerBox rightBox;
    [SerializeField] private Button breedButton;
    [Tooltip("The text on the Breed button.")]
    [SerializeField] private TMP_Text breedButtonLabel;
    [Tooltip("The egg sprite inside the spaceship's glass. Hidden until the egg is ready.")]
    [SerializeField] private GameObject eggInShip;
    [Tooltip("The popup that asks which room the egg goes in.")]
    [SerializeField] private RoomPickerPopup roomPicker;

    [Header("Nothing selected")]
    [Tooltip("Picture a box shows before an alien is picked (icon_nothing_selected).")]
    [SerializeField] private Sprite nothingSelectedSprite;
    [Tooltip("Name shown under it. Can be left empty.")]
    [SerializeField] private string nothingSelectedLabel = "";

    [Header("Button text")]
    [SerializeField] private string breedText = "Breed";
    [Tooltip("{0} is replaced by the seconds left.")]
    [SerializeField] private string breedingText = "Breeding... {0}";
    [SerializeField] private string eggReadyText = "Put egg in room";

    public OwnedAlien LeftPick { get; private set; }
    public OwnedAlien RightPick { get; private set; }

    private int lastSecondShown = -1;

    private BreedingState CurrentState =>
        BreedingManager.main != null ? BreedingManager.main.State : BreedingState.Idle;

    private void Awake()
    {
        leftBox.onArrowPressed.AddListener(step => Scroll(true, step));
        rightBox.onArrowPressed.AddListener(step => Scroll(false, step));
        breedButton.onClick.AddListener(OnButtonPressed);
    }

    // Runs every time the breeding room is shown, so everything is always current
    private void OnEnable()
    {
        if (AlienRoster.main != null) AlienRoster.main.onChanged.AddListener(Refresh);
        if (BreedingManager.main != null) BreedingManager.main.onChanged.AddListener(Refresh);
        Refresh();
    }

    private void OnDisable()
    {
        if (AlienRoster.main != null) AlienRoster.main.onChanged.RemoveListener(Refresh);
        if (BreedingManager.main != null) BreedingManager.main.onChanged.RemoveListener(Refresh);
    }

    // Only the countdown needs updating every frame
    private void Update()
    {
        if (CurrentState != BreedingState.Breeding) return;

        int seconds = Mathf.CeilToInt(BreedingManager.main.SecondsLeft);
        if (seconds != lastSecondShown)
        {
            lastSecondShown = seconds;
            SetButtonText(string.Format(breedingText, seconds));
        }
    }

    // What one box can scroll through: "nothing selected" (null) first, then every available alien except the one the other box is showing
    private List<OwnedAlien> ChoicesFor(bool isLeft)
    {
        OwnedAlien shownInOtherBox = isLeft ? RightPick : LeftPick;
        List<OwnedAlien> choices = new List<OwnedAlien> { null };

        foreach (OwnedAlien alien in AlienRoster.main.AvailableForBreeding())
            if (alien != shownInOtherBox) choices.Add(alien);

        return choices;
    }

    private void Scroll(bool isLeft, int step)
    {
        if (CurrentState != BreedingState.Idle || AlienRoster.main == null) return;

        List<OwnedAlien> choices = ChoicesFor(isLeft);
        OwnedAlien current = isLeft ? LeftPick : RightPick;

        int index = Mathf.Max(0, choices.IndexOf(current));
        index = (index + step + choices.Count) % choices.Count; // Wraps around both ways

        if (isLeft) LeftPick = choices[index];
        else RightPick = choices[index];

        Refresh();
    }

    public void Refresh()
    {
        if (AlienRoster.main == null || BreedingManager.main == null)
        {
            Debug.LogWarning("BreedingSelectionUI: no AlienRoster or BreedingManager. Press Play from the Rooms scene.", this);
            return;
        }

        BreedingState state = CurrentState;
        if (eggInShip != null) eggInShip.SetActive(state == BreedingState.EggReady);

        if (state == BreedingState.Idle)
        {
            // A pick that's no longer available goes back to nothing selected
            List<OwnedAlien> available = AlienRoster.main.AvailableForBreeding();
            if (LeftPick != null && !available.Contains(LeftPick)) LeftPick = null;
            if (RightPick != null && !available.Contains(RightPick)) RightPick = null;

            ShowAlien(leftBox, LeftPick);
            ShowAlien(rightBox, RightPick);
            leftBox.SetLocked(false);
            rightBox.SetLocked(false);

            breedButton.interactable = LeftPick != null && RightPick != null;
            SetButtonText(breedText);
        }
        else
        {
            // Locked on the two parents until the egg is put in a room
            ShowAlien(leftBox, BreedingManager.main.ParentA);
            ShowAlien(rightBox, BreedingManager.main.ParentB);
            leftBox.SetLocked(true);
            rightBox.SetLocked(true);
            LeftPick = null;
            RightPick = null;

            if (state == BreedingState.Breeding)
            {
                breedButton.interactable = false;
                lastSecondShown = -1; // Update writes the countdown
            }
            else
            {
                breedButton.interactable = true;
                SetButtonText(eggReadyText);
            }
        }
    }

    private void ShowAlien(AlienPickerBox box, OwnedAlien alien)
    {
        if (alien != null && alien.species != null)
            box.Show(alien.species.sprite, alien.species.displayName);
        else
            box.Show(nothingSelectedSprite, nothingSelectedLabel);
    }

    private void OnButtonPressed()
    {
        switch (CurrentState)
        {
            case BreedingState.Idle:
                if (LeftPick == null || RightPick == null) return;
                BreedingManager.main.StartBreeding(LeftPick, RightPick);
                break;

            case BreedingState.EggReady:
                if (roomPicker != null) roomPicker.Open();
                else Debug.LogWarning("BreedingSelectionUI: no Room Picker set.", this);
                break;
        }
    }

    private void SetButtonText(string text)
    {
        if (breedButtonLabel != null) breedButtonLabel.text = text;
    }
}