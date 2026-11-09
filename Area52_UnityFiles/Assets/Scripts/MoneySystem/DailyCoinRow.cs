using UnityEngine;
using TMPro;
using UnityEngine.UI;

/*
 * The "Daily Coin Collect" row at the top of the wallet panel
 *   Before the free collect:  "100" + Collect
 *   After it:                 "Watch Ad for 150" + Watch, with "3/5 left" under it
 *   Ads used up:              Watch greyed out, "Come back in 1:59" counting down - then back to Collect
 * Both versions sit in the same spot on the row; this just switches which one is showing
*/

public class DailyCoinRow : MonoBehaviour
{
    [Header("Free collect")]
    [Tooltip("The amount text + Collect button. Shown until the free coins are collected.")]
    [SerializeField] private GameObject collectGroup;
    [SerializeField] private TMP_Text collectAmountText;
    [SerializeField] private Button collectButton;

    [Header("Watch ad")]
    [Tooltip("The \"Watch Ad for 150\" text + Watch button. Shown after the free collect.")]
    [SerializeField] private GameObject watchGroup;
    [SerializeField] private TMP_Text watchText;
    [SerializeField] private Button watchButton;
    [Tooltip("Shows \"3/5 left\", or \"Come back in 1:59\" once today's ads are used up.")]
    [SerializeField] private TMP_Text adsLeftText;

    [Header("Look")]
    [Tooltip("How see-through the watch side is once today's ads are used up.")]
    [SerializeField, Range(0f, 1f)] private float usedUpAlpha = 0.45f;

    [Header("Text")]
    [Tooltip("{0} = coins.")]
    [SerializeField] private string collectFormat = "{0}";
    [Tooltip("{0} = coins.")]
    [SerializeField] private string watchFormat = "Watch Ad for\n{0}";
    [Tooltip("{0} = ads left, {1} = ads per day.")]
    [SerializeField] private string adsLeftFormat = "{0}/{1} left";
    [Tooltip("Shown once today's ads are used up. {0} = time left, like 1:59.")]
    [SerializeField] private string comeBackFormat = "Come back in {0}";

    private CanvasGroup watchGroupFade;
    private bool listening;

    private void Awake()
    {
        collectButton.onClick.AddListener(() => { if (DailyCoins.main != null) DailyCoins.main.CollectFree(); });
        watchButton.onClick.AddListener(() => { if (DailyCoins.main != null) DailyCoins.main.WatchAd(); });

        watchGroupFade = watchGroup.GetComponent<CanvasGroup>();
        if (watchGroupFade == null) watchGroupFade = watchGroup.AddComponent<CanvasGroup>();
    }

    // This row is inside the wallet panel, so this runs each time the panel opens
    private void OnEnable()
    {
        StartListening();
        Refresh();
    }

    private void Start() => StartListening();

    private void StartListening()
    {
        if (listening || DailyCoins.main == null) return;
        DailyCoins.main.onChanged.AddListener(Refresh);
        listening = true;
    }

    private void OnDisable()
    {
        if (listening && DailyCoins.main != null) DailyCoins.main.onChanged.RemoveListener(Refresh);
        listening = false;
    }

    // The countdown changes every second, so it's updated every frame while it's showing
    private void Update()
    {
        if (DailyCoins.main != null && DailyCoins.main.WaitingForNewDay) RefreshAdsLeftText();
    }

    private void Refresh()
    {
        DailyCoins daily = DailyCoins.main;
        if (daily == null) return;

        bool freeLeft = !daily.FreeCollected;
        collectGroup.SetActive(freeLeft);
        watchGroup.SetActive(!freeLeft);

        if (collectAmountText != null) collectAmountText.text = string.Format(collectFormat, daily.FreeAmount.ToString("N0"));
        if (watchText != null) watchText.text = string.Format(watchFormat, daily.AdAmount.ToString("N0"));
        RefreshAdsLeftText();

        watchButton.interactable = daily.CanWatchAd;
        watchGroupFade.alpha = daily.AdsLeft > 0 ? 1f : usedUpAlpha; // Only faded once they're all used, not while one plays
    }

    // "3/5 left", or "Come back in 1:59" once they're used up
    private void RefreshAdsLeftText()
    {
        DailyCoins daily = DailyCoins.main;
        if (adsLeftText == null || daily == null) return;

        if (daily.WaitingForNewDay)
        {
            int seconds = Mathf.CeilToInt(daily.SecondsUntilNewDay);
            adsLeftText.text = string.Format(comeBackFormat, $"{seconds / 60}:{seconds % 60:00}"); // 119 -> 1:59
        }
        else
        {
            adsLeftText.text = string.Format(adsLeftFormat, daily.AdsLeft, daily.AdsPerDay);
        }
    }
}