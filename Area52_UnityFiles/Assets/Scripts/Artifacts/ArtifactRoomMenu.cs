using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/*
 * The spaceship button in the bottom-right corner
 * Tapping it pops out the circles, each showing an artifact placed in the room you're looking at (empty if there's no artifact for it)
 * Tapping it again tucks them back under the button
 * Tapping a circle opens the Artifacts page, with that circle's artifact selected
*/

public class ArtifactRoomMenu : MonoBehaviour
{
    [Header("Parts")]
    [Tooltip("The big blue circle with the spaceship on it.")]
    [SerializeField] private Button mainButton;

    [Tooltip("The small circles, in order (Circle 1 shows the room's 1st artifact). " +
             "Each needs a child Image named Picture. Put them where they sit when open.")]
    [SerializeField] private Button[] circles = new Button[5];

    [Tooltip("The Artifacts page. Leave empty to use the one in the scene.")]
    [SerializeField] private ArtifactCollectionPopup collectionPopup;

    [Header("Animation")]
    [Tooltip("Seconds to fully open or close.")]
    [SerializeField] private float duration = 0.35f;

    [Tooltip("How spread out the circles' start times are (0 = all together, 0.5 = the last one waits half the time).")]
    [SerializeField, Range(0f, 0.8f)] private float stagger = 0.4f;

    [Tooltip("How much each circle overshoots before settling (0 = no bounce).")]
    [SerializeField] private float bounce = 1.6f;

    public bool IsOpen { get; private set; }

    private Vector3[] openPositions;
    private Image[] pictures;
    private readonly List<OwnedArtifact> shown = new List<OwnedArtifact>(); // What each circle is showing right now
    private float progress;  // 0 = tucked away, 1 = fully out
    private bool listening;

    private void Awake()
    {
        mainButton.onClick.AddListener(Toggle);

        openPositions = new Vector3[circles.Length];
        pictures = new Image[circles.Length];

        for (int i = 0; i < circles.Length; i++)
        {
            if (circles[i] == null) continue;

            // Remember where you placed it - that's its open spot
            openPositions[i] = circles[i].transform.localPosition;

            Transform picture = circles[i].transform.Find("Picture");
            pictures[i] = picture != null ? picture.GetComponent<Image>() : null;

            int circleIndex = i;
            circles[i].onClick.AddListener(() => OnCirclePressed(circleIndex));
        }

        // Start closed
        progress = 0f;
        IsOpen = false;
        ApplyProgress();
    }

    // Tries in both OnEnable and Start: the managers may not be set up yet the first time this runs
    private void OnEnable() => StartListening();
    private void Start() => StartListening();

    private void StartListening()
    {
        if (!listening && ArtifactInventory.main != null && RoomManager.main != null)
        {
            ArtifactInventory.main.onChanged.AddListener(RefreshPictures);
            RoomManager.main.onRoomChanged.AddListener(OnRoomChanged);
            listening = true;
        }
        RefreshPictures();
    }

    private void OnDisable()
    {
        if (!listening) return;
        if (ArtifactInventory.main != null) ArtifactInventory.main.onChanged.RemoveListener(RefreshPictures);
        if (RoomManager.main != null) RoomManager.main.onRoomChanged.RemoveListener(OnRoomChanged);
        listening = false;
    }

    private void OnRoomChanged(int roomIndex) => RefreshPictures();


    // OPEN / CLOSE

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    public void Open()
    {
        IsOpen = true;
        RefreshPictures();
    }

    public void Close()
    {
        IsOpen = false;
    }

    private void Update()
    {
        float target = IsOpen ? 1f : 0f;
        if (Mathf.Approximately(progress, target)) return; // Nothing moving

        float step = duration > 0f ? Time.unscaledDeltaTime / duration : 1f;
        progress = Mathf.MoveTowards(progress, target, step);
        ApplyProgress();
    }

    // Puts every circle where it should be for the current progress
    private void ApplyProgress()
    {
        Vector3 tuckedSpot = mainButton.transform.localPosition;
        int count = circles.Length;

        for (int i = 0; i < count; i++)
        {
            if (circles[i] == null) continue;

            // Each circle starts a little later than the one before it
            float delay = count > 1 ? stagger * i / (count - 1) : 0f;
            float t = Mathf.Clamp01((progress - delay) / (1f - stagger));
            float eased = EaseOutBack(t);

            Transform circle = circles[i].transform;
            circle.localPosition = Vector3.LerpUnclamped(tuckedSpot, openPositions[i], eased);
            circle.localScale = Vector3.one * Mathf.Max(0f, eased);

            // Fully tucked away = switched off, so it can't be tapped or seen
            bool visible = t > 0f;
            if (circle.gameObject.activeSelf != visible) circle.gameObject.SetActive(visible);
        }
    }

    // Goes a bit past 1 then settles back - the "pop"
    private float EaseOutBack(float t)
    {
        float c = bounce;
        float u = t - 1f;
        return 1f + (c + 1f) * u * u * u + c * u * u;
    }


    // WHAT'S IN THE CIRCLES

    private void RefreshPictures()
    {
        shown.Clear();

        List<OwnedArtifact> inRoom = null;
        if (ArtifactInventory.main != null && RoomManager.main != null)
            inRoom = ArtifactInventory.main.InRoom(RoomManager.main.CurrentRoomIndex);

        for (int i = 0; i < circles.Length; i++)
        {
            OwnedArtifact artifact = inRoom != null && i < inRoom.Count ? inRoom[i] : null;
            shown.Add(artifact);

            Image picture = pictures[i];
            if (picture == null) continue;

            Sprite sprite = artifact != null && artifact.definition != null ? artifact.definition.sprite : null;
            picture.sprite = sprite;
            picture.enabled = sprite != null; // Empty circle
            picture.preserveAspect = true;
        }
    }

    private void OnCirclePressed(int circleIndex)
    {
        if (!IsOpen) return; // Ignore taps while it's closing

        ArtifactCollectionPopup popup = collectionPopup != null ? collectionPopup : ArtifactCollectionPopup.main;
        if (popup == null)
        {
            Debug.LogWarning("ArtifactRoomMenu: no ArtifactCollectionPopup in the scene.", this);
            return;
        }

        // Opens with this circle's artifact selected, or nothing selected for an empty circle
        OwnedArtifact artifact = circleIndex < shown.Count ? shown[circleIndex] : null;
        popup.Open(artifact);
    }
}