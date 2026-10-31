using UnityEngine;
using TMPro;
using System.Collections;

/*
 * The "Out of pellets!" message when the player presses Feed with no pellets:
 *   it pops up in the middle of the screen, pulses a couple of times, then shrinks and fades away
 *   then the pellet bag in the HUD bumps, so the player sees where to get more
 * Never blocks taps. Pressing Feed again while it's showing just pulses it again
*/

public class NoPelletsPopup : MonoBehaviour
{
    public static NoPelletsPopup main { get; private set; }

    [Header("Parts")]
    [Tooltip("The message box, anchored to the middle of the screen at full size. Hidden when not showing.")]
    [SerializeField] private RectTransform card;
    [Tooltip("Optional. The text on the card - filled in with Message.")]
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private string message = "Out of pellets!";
    [Tooltip("Optional (hooked up in Step 8). The pellet bag button in the HUD - bumps after the message goes.")]
    [SerializeField] private RectTransform pelletBagButton;

    [Header("Timing (seconds)")]
    [SerializeField] private float growSeconds = 0.25f;
    [SerializeField] private int pulseCount = 2;
    [Tooltip("How long one pulse takes.")]
    [SerializeField] private float pulseSeconds = 0.35f;
    [Tooltip("Pause after the pulses, before it fades.")]
    [SerializeField] private float holdSeconds = 0.2f;
    [SerializeField] private float fadeSeconds = 0.25f;

    [Header("Look")]
    [Tooltip("How much bigger it gets on each pulse (0.12 = 12% bigger).")]
    [SerializeField] private float pulseAmount = 0.12f;
    [Tooltip("How much the pellet bag bumps (0.15 = 15% bigger).")]
    [SerializeField] private float bagBump = 0.15f;
    [SerializeField] private float bagBumpSeconds = 0.2f;

    private CanvasGroup cardGroup;
    private Vector3 bagScale = Vector3.one;
    private Coroutine playing;

    private void Awake()
    {
        if (main != null && main != this)
        {
            Debug.LogWarning("More than one NoPelletsPopup in the scene. Destroying the extra one.", this);
            Destroy(this);
            return;
        }
        main = this;

        if (pelletBagButton != null) bagScale = pelletBagButton.localScale;

        // Fades the whole box at once, and lets taps go straight through it
        cardGroup = card.GetComponent<CanvasGroup>();
        if (cardGroup == null) cardGroup = card.gameObject.AddComponent<CanvasGroup>();
        cardGroup.blocksRaycasts = false;
        cardGroup.interactable = false;

        if (messageText != null) messageText.text = message;
        card.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (main == this) main = null;
    }

    // Hidden partway (e.g. off to the breeding room): stop cleanly
    private void OnDisable()
    {
        StopAllCoroutines();
        playing = null;
        card.gameObject.SetActive(false);
        if (pelletBagButton != null) pelletBagButton.localScale = bagScale;
    }

    public void Show()
    {
        if (!isActiveAndEnabled) return;

        bool alreadyShowing = card.gameObject.activeSelf;
        if (playing != null) StopCoroutine(playing);
        if (pelletBagButton != null) pelletBagButton.localScale = bagScale;

        playing = StartCoroutine(Play(alreadyShowing));
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

        // The bag bumps, so the player sees where to buy more
        if (pelletBagButton != null)
        {
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / bagBumpSeconds)
            {
                pelletBagButton.localScale = bagScale * (1f + bagBump * Mathf.Sin(t * Mathf.PI));
                yield return null;
            }
            pelletBagButton.localScale = bagScale;
        }

        playing = null;
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
        Show();
    }
}