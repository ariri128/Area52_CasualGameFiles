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

/* ver.4
public class BreedingSelectionUI : MonoBehaviour
{
    [Header("Parts")]
    [SerializeField] private TMP_Dropdown leftBox;
    [SerializeField] private TMP_Dropdown rightBox;
    [SerializeField] private Button breedButton;
    [Tooltip("The text on the Breed button.")]
    [SerializeField] private TMP_Text breedButtonLabel;
    [Tooltip("The egg sprite inside the spaceship's glass. Hidden until the egg is ready.")]
    [SerializeField] private GameObject eggInShip;
    [Tooltip("The popup that asks which room the egg goes in.")]
    [SerializeField] private RoomPickerPopup roomPicker;

    [Header("Empty box")]
    [Tooltip("What a box says before an alien is picked.")]
    [SerializeField] private string emptyLabel = "Choose an alien";

    [Tooltip("Optional picture for an empty box (e.g. a question mark). Leave empty for no picture.")]
    [SerializeField] private Sprite emptySprite;

    [Header("Button text")]
    [SerializeField] private string breedText = "Breed";
    [Tooltip("{0} is replaced by the seconds left.")]
    [SerializeField] private string breedingText = "Breeding... {0}";
    [SerializeField] private string eggReadyText = "Put egg in room";

    public OwnedAlien LeftPick { get; private set; }
    public OwnedAlien RightPick { get; private set; }

    // What each option in a box points to
    private readonly List<OwnedAlien> leftChoices = new List<OwnedAlien>();
    private readonly List<OwnedAlien> rightChoices = new List<OwnedAlien>();

    private int lastSecondShown = -1;

    private BreedingState CurrentState =>
        BreedingManager.main != null ? BreedingManager.main.State : BreedingState.Idle;

    private void Awake()
    {
        leftBox.onValueChanged.AddListener(index => OnPicked(true, index));
        rightBox.onValueChanged.AddListener(index => OnPicked(false, index));
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

    private void OnPicked(bool isLeft, int index)
    {
        if (CurrentState != BreedingState.Idle) return;

        List<OwnedAlien> choices = isLeft ? leftChoices : rightChoices;
        OwnedAlien pick = (index >= 1 && index <= choices.Count) ? choices[index - 1] : null;

        if (isLeft) LeftPick = pick;
        else RightPick = pick;

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
            ShowPicking();
        }
        else
        {
            // Locked on the two parents until the egg is put in a room
            ShowLocked(leftBox, BreedingManager.main.ParentA);
            ShowLocked(rightBox, BreedingManager.main.ParentB);
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

    private void ShowPicking()
    {
        List<OwnedAlien> available = AlienRoster.main.AvailableForBreeding();

        // A pick that's no longer available is cleared
        if (LeftPick != null && !available.Contains(LeftPick)) LeftPick = null;
        if (RightPick != null && !available.Contains(RightPick)) RightPick = null;

        FillBox(leftBox, leftChoices, available, LeftPick, RightPick);
        FillBox(rightBox, rightChoices, available, RightPick, LeftPick);
        leftBox.interactable = true;
        rightBox.interactable = true;

        breedButton.interactable = LeftPick != null && RightPick != null;
        SetButtonText(breedText);
    }

    // Rebuilds one box's options: the empty choice, then every available alien except the one picked in the other box
    private void FillBox(TMP_Dropdown box, List<OwnedAlien> choices, List<OwnedAlien> available,
                         OwnedAlien current, OwnedAlien pickedInOtherBox)
    {
        choices.Clear();
        List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>
        {
            new TMP_Dropdown.OptionData(emptyLabel, emptySprite, Color.white)
        };

        foreach (OwnedAlien alien in available)
        {
            if (alien == pickedInOtherBox) continue;
            choices.Add(alien);
            options.Add(new TMP_Dropdown.OptionData(alien.species.displayName, alien.species.sprite, Color.white));
        }

        box.ClearOptions();
        box.AddOptions(options);

        // Show the current pick again without firing onValueChanged
        int index = current != null ? choices.IndexOf(current) + 1 : 0;
        box.SetValueWithoutNotify(index);
        box.RefreshShownValue();
    }

    // A box that only shows this parent and can't be opened
    private void ShowLocked(TMP_Dropdown box, OwnedAlien parent)
    {
        box.ClearOptions();
        if (parent != null && parent.species != null)
            box.AddOptions(new List<TMP_Dropdown.OptionData>
            {
                new TMP_Dropdown.OptionData(parent.species.displayName, parent.species.sprite, Color.white)
            });

        box.SetValueWithoutNotify(0);
        box.RefreshShownValue();
        box.interactable = false;
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
*/

/* ver.3
public class BreedingSelectionUI : MonoBehaviour
{
    [Header("Parts")]
    [SerializeField] private TMP_Dropdown leftBox;
    [SerializeField] private TMP_Dropdown rightBox;
    [SerializeField] private Button breedButton;
    [Tooltip("The text on the Breed button.")]
    [SerializeField] private TMP_Text breedButtonLabel;
    [Tooltip("The egg sprite inside the spaceship's glass. Hidden until the egg is ready.")]
    [SerializeField] private GameObject eggInShip;

    [Header("Empty box")]
    [Tooltip("What a box says before an alien is picked.")]
    [SerializeField] private string emptyLabel = "Choose an alien";

    [Tooltip("Optional picture for an empty box (e.g. a question mark). Leave empty for no picture.")]
    [SerializeField] private Sprite emptySprite;

    [Header("Button text")]
    [SerializeField] private string breedText = "Breed";
    [Tooltip("{0} is replaced by the seconds left.")]
    [SerializeField] private string breedingText = "Breeding... {0}";
    [SerializeField] private string eggReadyText = "Put egg in room";

    public OwnedAlien LeftPick { get; private set; }
    public OwnedAlien RightPick { get; private set; }

    // What each option in a box points to
    private readonly List<OwnedAlien> leftChoices = new List<OwnedAlien>();
    private readonly List<OwnedAlien> rightChoices = new List<OwnedAlien>();

    private int lastSecondShown = -1;

    private BreedingState CurrentState =>
        BreedingManager.main != null ? BreedingManager.main.State : BreedingState.Idle;

    private void Awake()
    {
        leftBox.onValueChanged.AddListener(index => OnPicked(true, index));
        rightBox.onValueChanged.AddListener(index => OnPicked(false, index));
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

    private void OnPicked(bool isLeft, int index)
    {
        if (CurrentState != BreedingState.Idle) return;

        List<OwnedAlien> choices = isLeft ? leftChoices : rightChoices;
        OwnedAlien pick = (index >= 1 && index <= choices.Count) ? choices[index - 1] : null;

        if (isLeft) LeftPick = pick;
        else RightPick = pick;

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
            ShowPicking();
        }
        else
        {
            // Locked on the two parents until the egg is put in a room
            ShowLocked(leftBox, BreedingManager.main.ParentA);
            ShowLocked(rightBox, BreedingManager.main.ParentB);
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

    private void ShowPicking()
    {
        List<OwnedAlien> available = AlienRoster.main.AvailableForBreeding();

        // A pick that's no longer available is cleared
        if (LeftPick != null && !available.Contains(LeftPick)) LeftPick = null;
        if (RightPick != null && !available.Contains(RightPick)) RightPick = null;

        FillBox(leftBox, leftChoices, available, LeftPick, RightPick);
        FillBox(rightBox, rightChoices, available, RightPick, LeftPick);
        leftBox.interactable = true;
        rightBox.interactable = true;

        breedButton.interactable = LeftPick != null && RightPick != null;
        SetButtonText(breedText);
    }

    // Rebuilds one box's options: the empty choice, then every available alien except the one picked in the other box
    private void FillBox(TMP_Dropdown box, List<OwnedAlien> choices, List<OwnedAlien> available,
                         OwnedAlien current, OwnedAlien pickedInOtherBox)
    {
        choices.Clear();
        List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>
        {
            new TMP_Dropdown.OptionData(emptyLabel, emptySprite, Color.white)
        };

        foreach (OwnedAlien alien in available)
        {
            if (alien == pickedInOtherBox) continue;
            choices.Add(alien);
            options.Add(new TMP_Dropdown.OptionData(alien.species.displayName, alien.species.sprite, Color.white));
        }

        box.ClearOptions();
        box.AddOptions(options);

        // Show the current pick again without firing onValueChanged
        int index = current != null ? choices.IndexOf(current) + 1 : 0;
        box.SetValueWithoutNotify(index);
        box.RefreshShownValue();
    }

    // A box that only shows this parent and can't be opened
    private void ShowLocked(TMP_Dropdown box, OwnedAlien parent)
    {
        box.ClearOptions();
        if (parent != null && parent.species != null)
            box.AddOptions(new List<TMP_Dropdown.OptionData>
            {
                new TMP_Dropdown.OptionData(parent.species.displayName, parent.species.sprite, Color.white)
            });

        box.SetValueWithoutNotify(0);
        box.RefreshShownValue();
        box.interactable = false;
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
                Debug.Log("Put egg in room pressed");
                //TODO: next step - open the room popup
                break;
        }
    }

    private void SetButtonText(string text)
    {
        if (breedButtonLabel != null) breedButtonLabel.text = text;
    }
}
*/

/* ver.2
public class BreedingSelectionUI : MonoBehaviour
{
    [Header("Parts")]
    [SerializeField] private TMP_Dropdown leftBox;
    [SerializeField] private TMP_Dropdown rightBox;
    [SerializeField] private Button breedButton;
    [Tooltip("The text on the Breed button.")]
    [SerializeField] private TMP_Text breedButtonLabel;
    [Tooltip("The egg sprite inside the spaceship's glass. Hidden until the egg is ready.")]
    [SerializeField] private GameObject eggInShip;

    [Header("Empty box")]
    [Tooltip("What a box says before an alien is picked.")]
    [SerializeField] private string emptyLabel = "Choose an alien";

    [Tooltip("Optional picture for an empty box (e.g. a question mark). Leave empty for no picture.")]
    [SerializeField] private Sprite emptySprite;

    [Header("Button text")]
    [SerializeField] private string breedText = "Breed";
    [Tooltip("{0} is replaced by the seconds left.")]
    [SerializeField] private string breedingText = "Breeding... {0}";
    [SerializeField] private string eggReadyText = "Put egg in room";

    public OwnedAlien LeftPick { get; private set; }
    public OwnedAlien RightPick { get; private set; }

    // What each option in a box points to
    private readonly List<OwnedAlien> leftChoices = new List<OwnedAlien>();
    private readonly List<OwnedAlien> rightChoices = new List<OwnedAlien>();

    private int lastSecondShown = -1;

    private BreedingState CurrentState =>
        BreedingManager.main != null ? BreedingManager.main.State : BreedingState.Idle;

    private void Awake()
    {
        leftBox.onValueChanged.AddListener(index => OnPicked(true, index));
        rightBox.onValueChanged.AddListener(index => OnPicked(false, index));
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

    private void OnPicked(bool isLeft, int index)
    {
        if (CurrentState != BreedingState.Idle) return;

        List<OwnedAlien> choices = isLeft ? leftChoices : rightChoices;
        OwnedAlien pick = (index >= 1 && index <= choices.Count) ? choices[index - 1] : null;

        if (isLeft) LeftPick = pick;
        else RightPick = pick;

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
            ShowPicking();
        }
        else
        {
            // Locked on the two parents until the egg is put in a room
            ShowLocked(leftBox, BreedingManager.main.ParentA);
            ShowLocked(rightBox, BreedingManager.main.ParentB);
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

    private void ShowPicking()
    {
        List<OwnedAlien> available = AlienRoster.main.AvailableForBreeding();

        // A pick that's no longer available is cleared
        if (LeftPick != null && !available.Contains(LeftPick)) LeftPick = null;
        if (RightPick != null && !available.Contains(RightPick)) RightPick = null;

        FillBox(leftBox, leftChoices, available, LeftPick, RightPick);
        FillBox(rightBox, rightChoices, available, RightPick, LeftPick);
        leftBox.interactable = true;
        rightBox.interactable = true;

        breedButton.interactable = LeftPick != null && RightPick != null;
        SetButtonText(breedText);
    }

    // Rebuilds one box's options: the empty choice, then every available alien except the one picked in the other box
    private void FillBox(TMP_Dropdown box, List<OwnedAlien> choices, List<OwnedAlien> available,
                         OwnedAlien current, OwnedAlien pickedInOtherBox)
    {
        choices.Clear();
        List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>
        {
            new TMP_Dropdown.OptionData(emptyLabel, emptySprite)
        };

        foreach (OwnedAlien alien in available)
        {
            if (alien == pickedInOtherBox) continue;
            choices.Add(alien);
            options.Add(new TMP_Dropdown.OptionData(alien.species.displayName, alien.species.sprite));
        }

        box.ClearOptions();
        box.AddOptions(options);

        // Show the current pick again without firing onValueChanged
        int index = current != null ? choices.IndexOf(current) + 1 : 0;
        box.SetValueWithoutNotify(index);
        box.RefreshShownValue();
    }

    // A box that only shows this parent and can't be opened
    private void ShowLocked(TMP_Dropdown box, OwnedAlien parent)
    {
        box.ClearOptions();
        if (parent != null && parent.species != null)
            box.AddOptions(new List<TMP_Dropdown.OptionData>
            {
                new TMP_Dropdown.OptionData(parent.species.displayName, parent.species.sprite)
            });

        box.SetValueWithoutNotify(0);
        box.RefreshShownValue();
        box.interactable = false;
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
                Debug.Log("Put egg in room pressed");
                //TODO: next step - open the room popup
                break;
        }
    }

    private void SetButtonText(string text)
    {
        if (breedButtonLabel != null) breedButtonLabel.text = text;
    }
}
*/

/* ver.1
public class BreedingSelectionUI : MonoBehaviour
{
    [Header("Parts")]
    [SerializeField] private TMP_Dropdown leftBox;
    [SerializeField] private TMP_Dropdown rightBox;
    [SerializeField] private Button breedButton;

    [Header("Empty box")]
    [Tooltip("What a box says before an alien is picked.")]
    [SerializeField] private string emptyLabel = "Choose an alien";

    [Tooltip("Optional picture for an empty box (e.g. a question mark). Leave empty for no picture.")]
    [SerializeField] private Sprite emptySprite;

    public OwnedAlien LeftPick { get; private set; }
    public OwnedAlien RightPick { get; private set; }

    // What each option in a box points to
    private readonly List<OwnedAlien> leftChoices = new List<OwnedAlien>();
    private readonly List<OwnedAlien> rightChoices = new List<OwnedAlien>();

    private void Awake()
    {
        leftBox.onValueChanged.AddListener(index => OnPicked(true, index));
        rightBox.onValueChanged.AddListener(index => OnPicked(false, index));
        breedButton.onClick.AddListener(OnBreedPressed);
    }

    // Runs every time the breeding room is shown, so the lists are always current
    private void OnEnable()
    {
        if (AlienRoster.main != null) AlienRoster.main.onChanged.AddListener(Refresh);
        Refresh();
    }

    private void OnDisable()
    {
        if (AlienRoster.main != null) AlienRoster.main.onChanged.RemoveListener(Refresh);
    }

    private void OnPicked(bool isLeft, int index)
    {
        List<OwnedAlien> choices = isLeft ? leftChoices : rightChoices;
        OwnedAlien pick = (index >= 1 && index <= choices.Count) ? choices[index - 1] : null;

        if (isLeft) LeftPick = pick;
        else RightPick = pick;

        Refresh();
    }

    public void Refresh()
    {
        if (AlienRoster.main == null)
        {
            Debug.LogWarning("BreedingSelectionUI: no AlienRoster. Press Play from the Rooms scene.", this);
            return;
        }

        List<OwnedAlien> available = AlienRoster.main.AvailableForBreeding();

        // A pick that's no longer available is cleared
        if (LeftPick != null && !available.Contains(LeftPick)) LeftPick = null;
        if (RightPick != null && !available.Contains(RightPick)) RightPick = null;

        FillBox(leftBox, leftChoices, available, LeftPick, RightPick);
        FillBox(rightBox, rightChoices, available, RightPick, LeftPick);

        breedButton.interactable = LeftPick != null && RightPick != null;
    }

    // Rebuilds one box's options: the empty choice, then every available alien except the one picked in the other box
    private void FillBox(TMP_Dropdown box, List<OwnedAlien> choices, List<OwnedAlien> available,
                         OwnedAlien current, OwnedAlien pickedInOtherBox)
    {
        choices.Clear();
        List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>
        {
            new TMP_Dropdown.OptionData(emptyLabel, emptySprite)
        };

        foreach (OwnedAlien alien in available)
        {
            if (alien == pickedInOtherBox) continue;
            choices.Add(alien);
            options.Add(new TMP_Dropdown.OptionData(alien.species.displayName, alien.species.sprite));
        }

        box.ClearOptions();
        box.AddOptions(options);

        // Show the current pick again without firing onValueChanged
        int index = current != null ? choices.IndexOf(current) + 1 : 0;
        box.SetValueWithoutNotify(index);
        box.RefreshShownValue();
    }

    private void OnBreedPressed()
    {
        if (LeftPick == null || RightPick == null) return;
        Debug.Log($"Breed pressed: {LeftPick.species.displayName} x {RightPick.species.displayName}");
        //TODO: next step - lock the boxes, send both aliens to the breeding room, start the timer
    }
}
*/