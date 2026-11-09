using UnityEngine;
using System;
using System.Collections;
using TMPro;

/*
 * Plays a "watch to earn" ad, then says whether it was watched to the end
 * FOR NOW this is a fake: a dark panel over everything saying "Ad Playing... 30", counting down
 * When real ads are added (e.g. Unity LevelPlay), only Show() in this script changes - nothing that uses it does
*/

public class RewardedAdPlayer : MonoBehaviour
{
    public static RewardedAdPlayer main { get; private set; }

    [Header("Fake ad")]
    [Tooltip("The dark panel that covers the screen. Hidden when no ad is playing.")]
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text countdownText;
    [Tooltip("{0} = seconds left.")]
    [SerializeField] private string countdownFormat = "Ad Playing...\n{0}";
    [Tooltip("How long the fake ad lasts. Lower it while testing.")]
    [SerializeField] private float adSeconds = 30f;

    private Action<bool> whenDone; // Gets true if the ad was watched to the end

    public bool IsPlaying => whenDone != null;

    private void Awake()
    {
        if (main != null && main != this)
        {
            Debug.LogWarning("More than one RewardedAdPlayer in the scene. Destroying the extra one.", this);
            Destroy(this);
            return;
        }
        main = this;
        root.SetActive(false);
    }

    private void OnDestroy()
    {
        if (main == this) main = null;
    }

    // Interrupted (e.g. switched off): the ad didn't finish, so no reward - but nothing is left stuck waiting
    private void OnDisable()
    {
        StopAllCoroutines();
        if (root != null) root.SetActive(false);
        Finish(false);
    }

    public void Show(Action<bool> onDone)
    {
        if (IsPlaying || !isActiveAndEnabled) { onDone?.Invoke(false); return; }
        whenDone = onDone;
        StartCoroutine(PlayFakeAd());
    }

    private IEnumerator PlayFakeAd()
    {
        transform.SetAsLastSibling(); // Over everything, including the wallet panel
        root.SetActive(true);

        for (float left = adSeconds; left > 0f; left -= Time.unscaledDeltaTime)
        {
            if (countdownText != null) countdownText.text = string.Format(countdownFormat, Mathf.CeilToInt(left));
            yield return null;
        }

        root.SetActive(false);
        Finish(true);
    }

    private void Finish(bool watched)
    {
        Action<bool> callback = whenDone;
        whenDone = null; // Cleared first, so it can only ever run once
        callback?.Invoke(watched);
    }
}