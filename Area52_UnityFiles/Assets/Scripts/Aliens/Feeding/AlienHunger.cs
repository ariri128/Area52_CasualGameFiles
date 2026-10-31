using UnityEngine;
using UnityEngine.Events;

/*
 * How full each alien is (0 = empty, 1 = full) - the number lives on the OwnedAlien as "fullness"
 * Every few seconds every alien in its room gets a little hungrier
 * Half full or less = Sad, nearly empty = Angry, no matter who it lives with (AlienMoods checks here first)
 * One pellet fills it by 1/8, never past full (the pellet is still used up - PelletBank handles that later)
*/

public class AlienHunger : MonoBehaviour
{
    public static AlienHunger main { get; private set; }

    [Header("Getting hungry")]
    [Tooltip("Seconds between each time the aliens get hungrier.")]
    [SerializeField] private float drainInterval = 10f;

    [Tooltip("How much fullness each alien loses each time. 0.025 = full to empty in 40 drains (about 6.5 minutes at 10 seconds).")]
    [Range(0f, 1f)] [SerializeField] private float drainAmount = 0.025f;

    [Tooltip("Do aliens away exploring or breeding get hungry too?")]
    [SerializeField] private bool drainWhileAway = false;

    [Header("Feeding")]
    [Tooltip("Pellets it takes to go from empty to full.")]
    [SerializeField] private int pelletsToFill = 8;

    [Header("Moods")]
    [Tooltip("At or below this the alien is Sad, whatever its roommates are like (0.5 = half full). The red glow starts here too.")]
    [Range(0f, 1f)] [SerializeField] private float sadAt = 0.5f;

    [Tooltip("At or below this the alien is Angry, whatever its roommates are like.")]
    [Range(0f, 1f)] [SerializeField] private float angryAt = 0.1f;

    [Tooltip("Print each drain in the Console.")]
    [SerializeField] private bool logDrains = false;

    // Fires whenever any alien gets hungrier or is fed
    public UnityEvent onChanged = new UnityEvent();

    // How much one pellet fills (1/8)
    public float FeedAmount => pelletsToFill > 0 ? 1f / pelletsToFill : 1f;

    private float nextDrainAt;

    private void Awake()
    {
        if (main != null && main != this)
        {
            Debug.LogWarning("More than one AlienHunger in the scene. Destroying the extra one.", this);
            Destroy(this);
            return;
        }
        main = this;
        nextDrainAt = Time.time + drainInterval;
    }

    private void OnDestroy()
    {
        if (main == this) main = null;
    }

    private void Update()
    {
        if (Time.time < nextDrainAt) return;
        nextDrainAt += drainInterval;
        Drain();
    }

    private void Drain()
    {
        if (AlienRoster.main == null) return;

        bool changed = false;
        foreach (OwnedAlien alien in AlienRoster.main.All)
        {
            if (alien == null || alien.species == null) continue;
            if (alien.IsAway && !drainWhileAway) continue;
            if (alien.fullness <= 0f) continue; // Already empty

            alien.fullness = Mathf.Max(0f, alien.fullness - drainAmount);
            changed = true;
            if (logDrains) Debug.Log($"{alien.DisplayName} is now {alien.fullness:P0} full.", this);
        }

        if (changed) onChanged.Invoke();
    }


    // ASKING ABOUT AN ALIEN

    public float FullnessOf(OwnedAlien alien) => alien != null ? Mathf.Clamp01(alien.fullness) : 1f;
    public bool IsHungry(OwnedAlien alien) => alien != null && alien.fullness <= sadAt + 0.0001f;
    public bool IsStarving(OwnedAlien alien) => alien != null && alien.fullness <= angryAt + 0.0001f;
    public bool IsFull(OwnedAlien alien) => alien != null && alien.fullness >= 1f - 0.0001f;


    // FEEDING

    // Adds one pellet's worth
	// Doesn't spend the pellet - the Feed button will do that first
    public void Feed(OwnedAlien alien)
    {
        if (alien == null) return;
        alien.fullness = Mathf.Min(1f, alien.fullness + FeedAmount);
        onChanged.Invoke();
    }


    // TESTING

    [ContextMenu("Testing: Make All Hungry (Sad)")]
    private void MakeAllHungry() => SetAll(sadAt);

    [ContextMenu("Testing: Make All Starving (Angry)")]
    private void MakeAllStarving() => SetAll(angryAt);

    [ContextMenu("Testing: Fill All")]
    private void FillAll() => SetAll(1f);

    [ContextMenu("Testing: Feed All One Pellet")]
    private void FeedAllOne()
    {
        if (AlienRoster.main == null) { Debug.LogWarning("Press Play first.", this); return; }
        foreach (OwnedAlien alien in AlienRoster.main.All) Feed(alien);
    }

    private void SetAll(float value)
    {
        if (AlienRoster.main == null) { Debug.LogWarning("Press Play first.", this); return; }
        foreach (OwnedAlien alien in AlienRoster.main.All)
            if (alien != null) alien.fullness = value;
        onChanged.Invoke();
    }
}