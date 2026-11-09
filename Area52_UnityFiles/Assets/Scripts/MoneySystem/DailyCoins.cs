using UnityEngine;
using UnityEngine.Events;

/*
 * The daily coin collect - a way back from going broke:
 *   First: collect Free Amount coins for free
 *   After that: watch an ad for Ad Amount coins, up to Ads Per Day times
 *   Once they're all used: a short wait (New Day Seconds), then it all resets - free collect first again
 * There's no saving yet, so for now a "day" is that short wait instead of a real day (and it also resets each time the game starts)
*/

public class DailyCoins : MonoBehaviour
{
    public static DailyCoins main { get; private set; }

    [SerializeField] private int freeAmount = 100;
    [SerializeField] private int adAmount = 150;
    [SerializeField] private int adsPerDay = 5;

    [Tooltip("For now (no saving yet): how long after the last ad is used until it all resets. 120 = 2 minutes.")]
    [SerializeField] private float newDaySeconds = 120f;

    // Fires whenever the free collect is used, an ad starts or finishes, or a new day starts
    public UnityEvent onChanged = new UnityEvent();

    public int FreeAmount => freeAmount;
    public int AdAmount => adAmount;
    public int AdsPerDay => adsPerDay;

    public bool FreeCollected { get; private set; }
    public int AdsWatched { get; private set; }
    public int AdsLeft => Mathf.Max(0, adsPerDay - AdsWatched);
    public bool AdPlaying { get; private set; }

    public bool CanWatchAd => FreeCollected && AdsLeft > 0 && !AdPlaying && RewardedAdPlayer.main != null;

    // Counting down to a new day (all of today's ads are used up)
    public bool WaitingForNewDay => newDayAt >= 0f;
    public float SecondsUntilNewDay => WaitingForNewDay ? Mathf.Max(0f, newDayAt - Time.realtimeSinceStartup) : 0f;

    private float newDayAt = -1f; // Real time the new day starts (-1 = not waiting)

    private void Awake()
    {
        if (main != null && main != this)
        {
            Debug.LogWarning("More than one DailyCoins in the scene. Destroying the extra one.", this);
            Destroy(this);
            return;
        }
        main = this;
        //TODO (once there's a save system): only reset if the saved date isn't today, and swap the short wait for real days
    }

    private void OnDestroy()
    {
        if (main == this) main = null;
    }

    private void Update()
    {
        if (WaitingForNewDay && Time.realtimeSinceStartup >= newDayAt) StartNewDay();
    }


    // COLLECTING

    // The free one - returns false if it's already been used today
    public bool CollectFree()
    {
        if (FreeCollected || CoinBank.main == null) return false;

        FreeCollected = true;
        CoinBank.main.Earn(freeAmount);
        AudioManager.PlayPurchase();
        onChanged.Invoke();
        return true;
    }

    // Plays an ad, and pays out if it was watched to the end - returns false if it can't start
    public bool WatchAd()
    {
        if (!CanWatchAd) return false;

        AdPlaying = true;
        onChanged.Invoke(); // Greys the button out while the ad plays

        RewardedAdPlayer.main.Show(watched =>
        {
            AdPlaying = false;
            if (watched && CoinBank.main != null)
            {
                AdsWatched++;
                CoinBank.main.Earn(adAmount);
                AudioManager.PlayPurchase();

                if (AdsLeft == 0) newDayAt = Time.realtimeSinceStartup + newDaySeconds; // Start the countdown
            }
            onChanged.Invoke();
        });
        return true;
    }

    // Everything back to the start: free collect first, then the ads
    private void StartNewDay()
    {
        FreeCollected = false;
        AdsWatched = 0;
        newDayAt = -1f;
        onChanged.Invoke();
    }


    // TESTING

    [ContextMenu("Testing: Start A New Day")]
    private void NewDayForTesting()
    {
        StartNewDay();
        Debug.Log("Daily coins reset - it's a new day.", this);
    }
}