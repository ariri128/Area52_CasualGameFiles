using UnityEngine;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;

/*
 * A short message that pops up in the middle of the screen, pulses, then shrinks and fades away (like "Out of pellets!")
 * There's one per Kind in the scene, found with ToastPopup.Find(kind):
 *   Sold     - "Sold for ___ coins!" with the coin burst and the cha-ching
 *   RoomFull - "Room Full"
 * Never blocks taps - moves itself in front of every other UI each time it shows
 * Show can be told what to do once the message is gone (e.g. go back to a page)
*/

public enum ToastKind { Sold, RoomFull, Other }

public class ToastPopup : MonoBehaviour
{
    private static readonly Dictionary<ToastKind, ToastPopup> byKind = new Dictionary<ToastKind, ToastPopup>();

    // The toast of this kind in the scene - null if there isn't one
    public static ToastPopup Find(ToastKind kind) => byKind.TryGetValue(kind, out ToastPopup toast) ? toast : null;

    [Header("Parts")]
    [SerializeField] private ToastKind kind = ToastKind.Other;
    [Tooltip("The message box, anchored to the middle of the screen at full size. Hidden when not showing.")]
    [SerializeField] private RectTransform card;
    [SerializeField] private TMP_Text messageText;
    [Tooltip("Optional. Only the Sold toast needs one.")]
    [SerializeField] private CoinBurst coinBurst;

    [Header("Text")]
    [Tooltip("Used by Show() with no message, e.g. \"Room Full\".")]
    [SerializeField] private string defaultMessage = "Room Full";
    [Tooltip("{0} = how many coins.")]
    [SerializeField] private string soldFormat = "Sold for {0} coins!";

    [Header("Timing (seconds)")]
    [SerializeField] private float growSeconds = 0.25f;
    [SerializeField] private int pulseCount = 2;
    [Tooltip("How long one pulse takes.")]
    [SerializeField] private float pulseSeconds = 0.35f;
    [Tooltip("Pause after the pulses, before it fades.")]
    [SerializeField] private float holdSeconds = 0.6f;
    [SerializeField] private float fadeSeconds = 0.25f;

    [Header("Look")]
    [Tooltip("How much bigger it gets on each pulse (0.12 = 12% bigger).")]
    [SerializeField] private float pulseAmount = 0.12f;

    private CanvasGroup cardGroup;
    private Coroutine playing;
    private Action whenDone;

    public bool IsShowing => card != null && card.gameObject.activeSelf;

    private void Awake()
    {
        if (byKind.TryGetValue(kind, out ToastPopup other) && other != null && other != this)
            Debug.LogWarning($"More than one {kind} ToastPopup in the scene. Only the newest one is used.", this);
        byKind[kind] = this;

        // Fades the whole box at once, and lets taps go straight through it
        cardGroup = card.GetComponent<CanvasGroup>();
        if (cardGroup == null) cardGroup = card.gameObject.AddComponent<CanvasGroup>();
        cardGroup.blocksRaycasts = false;
        cardGroup.interactable = false;

        card.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (byKind.TryGetValue(kind, out ToastPopup registered) && registered == this) byKind.Remove(kind);
    }

    // Hidden partway (e.g. off to the breeding room): stop cleanly, and still finish whatever was waiting on it
    private void OnDisable()
    {
        StopAllCoroutines();
        playing = null;
        card.gameObject.SetActive(false);
        Finish();
    }


    // SHOWING

    public void Show(Action onDone = null) => Show(defaultMessage, onDone);

    public void Show(string message, Action onDone = null)
    {
        if (!isActiveAndEnabled) { onDone?.Invoke(); return; } // Can't show - don't leave anything stuck waiting

        Finish(); // A message already showing is replaced, but whatever was waiting on it still happens

        bool alreadyShowing = card.gameObject.activeSelf;
        if (playing != null) StopCoroutine(playing);

        whenDone = onDone;
        if (messageText != null) messageText.text = message;
        transform.SetAsLastSibling(); // In front of every popup

        playing = StartCoroutine(Play(alreadyShowing));
    }

    // "Sold for 420 coins!" + coins flying out + cha-ching
    public void ShowSold(int coins, Action onDone = null)
    {
        Show(string.Format(soldFormat, coins.ToString("N0")), onDone);
        AudioManager.PlayPurchase();
        if (coinBurst != null) coinBurst.Play();
    }

    private void Finish()
    {
        Action callback = whenDone;
        whenDone = null; // Cleared first, so it can only ever run once
        callback?.Invoke();
    }


    // THE ANIMATION

    private IEnumerator Play(bool skipGrow)
    {
        cardGroup.alpha = 1f;
        card.gameObject.SetActive(true);

        // Grow, with a little overshoot (skipped if it's already on screen)
        if (!skipGrow)
        {
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / growSeconds)
            {
                card.localScale = Vector3.one * EaseOutBack(t);
                yield return null;
            }
        }
        card.localScale = Vector3.one;

        // Pulse: half a sine wave each time, so it swells and settles smoothly
        for (int pulse = 0; pulse < pulseCount; pulse++)
        {
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / pulseSeconds)
            {
                card.localScale = Vector3.one * (1f + pulseAmount * Mathf.Sin(t * Mathf.PI));
                yield return null;
            }
            card.localScale = Vector3.one;
        }

        yield return new WaitForSecondsRealtime(holdSeconds);

        // Shrink a little and fade out
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / fadeSeconds)
        {
            card.localScale = Vector3.one * Mathf.Lerp(1f, 0.8f, t);
            cardGroup.alpha = 1f - t;
            yield return null;
        }
        card.gameObject.SetActive(false);

        playing = null;
        Finish();
    }

    // Goes a bit past 1 then settles back - the "pop"
    private static float EaseOutBack(float t)
    {
        const float c = 1.7f;
        float u = Mathf.Clamp01(t) - 1f;
        return 1f + (c + 1f) * u * u * u + c * u * u;
    }


    // TESTING

    [ContextMenu("Testing: Show")]
    private void ShowForTesting()
    {
        if (!Application.isPlaying) { Debug.LogWarning("Press Play first.", this); return; }
        Show(() => Debug.Log($"{kind} toast finished.", this));
    }

    [ContextMenu("Testing: Show Sold (420)")]
    private void ShowSoldForTesting()
    {
        if (!Application.isPlaying) { Debug.LogWarning("Press Play first.", this); return; }
        ShowSold(420, () => Debug.Log("Sold toast finished.", this));
    }
}