using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/*
 * Shows what an exploring alien brought back, once it's been dropped off:
 *   a big copy of the artifact's box (picture, name, green border) pops up in the middle of the screen,
 *   pulses a few times, then shrinks away into the artifacts button in the bottom-right corner
 * It waits until no popup or other UI is open before starting, and never blocks taps
 * The artifact is already in the inventory with its NEW tag - this is only the animation
*/

public class ArtifactReveal : MonoBehaviour
{
    [Header("Parts")]
    [Tooltip("The big box (a copy of the Artifacts page's slot), anchored to the middle of the screen at its full size. " +
             "Hidden when nothing is being revealed.")]
    [SerializeField] private RectTransform card;
    [SerializeField] private Image picture;
    [SerializeField] private TMP_Text nameText;
    [Tooltip("Optional. The green selected border - switched on while revealing.")]
    [SerializeField] private GameObject selectedBorder;
    [Tooltip("The artifacts button in the bottom-right corner (the Artifact Menu's main button). The box flies into it.")]
    [SerializeField] private RectTransform artifactsButton;

    [Header("Timing (seconds)")]
    [Tooltip("Wait after the alien is dropped off before the box appears.")]
    [SerializeField] private float delayAfterReturn = 0.2f;
    [SerializeField] private float growSeconds = 0.35f;
    [SerializeField] private int pulseCount = 3;
    [Tooltip("How long one pulse takes.")]
    [SerializeField] private float pulseSeconds = 0.4f;
    [Tooltip("Pause after the pulses, before it flies off.")]
    [SerializeField] private float holdSeconds = 0.15f;
    [SerializeField] private float flySeconds = 0.5f;
    [Tooltip("When bought from the shop: how long it takes to fly into its box on the Artifacts page.")]
    [SerializeField] private float flyIntoSlotSeconds = 0.45f;

    [Header("Look")]
    [Tooltip("How much bigger it gets on each pulse (0.12 = 12% bigger).")]
    [SerializeField] private float pulseAmount = 0.12f;
    [Tooltip("How big it is (compared to full size) when it reaches the button.")]
    [SerializeField] private float endScale = 0.15f;
    [Tooltip("How much the button bumps when the box lands in it (0.15 = 15% bigger).")]
    [SerializeField] private float buttonBump = 0.15f;
    [SerializeField] private float buttonBumpSeconds = 0.2f;

    private readonly Queue<OwnedArtifact> waiting = new Queue<OwnedArtifact>();
    private CanvasGroup cardGroup;
    private Vector2 cardHome;
    private Vector3 buttonScale;
    private bool playing;
    private bool listening;
    private bool purchasePlaying; // A bought artifact is being revealed - found ones wait until it's done

    private void Awake()
    {
        cardHome = card.anchoredPosition;
        if (artifactsButton != null) buttonScale = artifactsButton.localScale;

        // Fades the whole box at once, and lets taps go straight through it
        cardGroup = card.GetComponent<CanvasGroup>();
        if (cardGroup == null) cardGroup = card.gameObject.AddComponent<CanvasGroup>();
        cardGroup.blocksRaycasts = false;
        cardGroup.interactable = false;

        card.gameObject.SetActive(false);
    }

    // Tries in both OnEnable and Start: ExplorationManager may not be set up yet the first time this runs
    private void OnEnable()
    {
        StartListening();
        if (waiting.Count > 0 && !playing) StartCoroutine(PlayWaiting()); // Carries on after being hidden
    }

    private void Start() => StartListening();

    private void StartListening()
    {
        if (listening || ExplorationManager.main == null) return;
        ExplorationManager.main.onReturned.AddListener(OnAlienReturned);
        listening = true;
    }

    // Hidden partway through (e.g. off to the breeding room): stop cleanly - anything waiting plays when it's shown again
    private void OnDisable()
    {
        if (listening && ExplorationManager.main != null) ExplorationManager.main.onReturned.RemoveListener(OnAlienReturned);
        listening = false;

        StopAllCoroutines();
        playing = false;
        purchasePlaying = false;
        card.gameObject.SetActive(false);
        if (artifactsButton != null) artifactsButton.localScale = buttonScale;

        ScreenBlocker.SetBlocked(this, false);
    }

    private void OnAlienReturned(OwnedAlien alien, OwnedArtifact found)
    {
        if (found != null) Show(found);
    }

    // Adds an artifact to the line to be revealed
    public void Show(OwnedArtifact artifact)
    {
        if (artifact == null || artifact.definition == null) return;
        waiting.Enqueue(artifact);
        if (!playing && isActiveAndEnabled) StartCoroutine(PlayWaiting());
    }

    // For a bought artifact: plays straight away (no waiting), then flies into its box on the Artifacts page
    // openPageAndFindSlot is called once the pulsing is done: it should open the page and return the new artifact's box
    public void ShowPurchase(OwnedArtifact artifact, System.Func<RectTransform> openPageAndFindSlot)
    {
        if (artifact == null || artifact.definition == null) return;

        if (!isActiveAndEnabled)
        {
            openPageAndFindSlot?.Invoke(); // Can't animate - just open the page
            return;
        }
        StartCoroutine(RevealInto(artifact, openPageAndFindSlot));
    }

    private IEnumerator PlayWaiting()
    {
        playing = true;

        while (waiting.Count > 0)
        {
            yield return new WaitForSecondsRealtime(delayAfterReturn);

            // Waits until nothing is open (a popup, the alien's bubble, the circles...)
            while ((ExplorationManager.main != null && ExplorationManager.main.IsAnyUIOpen()) || purchasePlaying) yield return null;

            yield return Reveal(waiting.Dequeue());
        }

        playing = false;
    }


    // THE ANIMATION

    // A found artifact: pops up, pulses, then flies into the artifacts button
    private IEnumerator Reveal(OwnedArtifact artifact)
    {
        ScreenBlocker.SetBlocked(this, true);

        yield return GrowAndPulse(artifact);

        // Fly into the button: speeds up, shrinks, and fades out at the very end
        Vector3 from = card.position;
        Vector3 to = artifactsButton != null ? artifactsButton.TransformPoint(artifactsButton.rect.center) : from;

        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / flySeconds)
        {
            float eased = t * t; // Ease in: slow, then fast
            card.position = Vector3.LerpUnclamped(from, to, eased);
            card.localScale = Vector3.one * Mathf.Lerp(1f, endScale, eased);
            cardGroup.alpha = 1f - Mathf.Clamp01((t - 0.7f) / 0.3f); // Fades over the last 30%
            yield return null;
        }

        card.gameObject.SetActive(false);

        // The button bumps, so the player sees where it went
        if (artifactsButton != null)
        {
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / buttonBumpSeconds)
            {
                artifactsButton.localScale = buttonScale * (1f + buttonBump * Mathf.Sin(t * Mathf.PI));
                yield return null;
            }
            artifactsButton.localScale = buttonScale;
        }

        ScreenBlocker.SetBlocked(this, false);
    }

    // The part both versions share: fill in the box, pop it up in the middle, pulse, short pause
    private IEnumerator GrowAndPulse(OwnedArtifact artifact)
    {
        picture.sprite = artifact.definition.sprite;
        picture.enabled = artifact.definition.sprite != null;
        picture.preserveAspect = true;
        if (nameText != null) nameText.text = artifact.DisplayName;
        if (selectedBorder != null) selectedBorder.SetActive(true);

        card.SetAsLastSibling(); // In front of any popup
        card.anchoredPosition = cardHome;
        card.localScale = Vector3.zero;
        cardGroup.alpha = 1f;
        card.gameObject.SetActive(true);

        // Grow, with a little overshoot
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / growSeconds)
        {
            card.localScale = Vector3.one * EaseOutBack(t);
            yield return null;
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
    }

    // A bought artifact: pops up, pulses, then the Artifacts page opens and the box flies into its new slot
    private IEnumerator RevealInto(OwnedArtifact artifact, System.Func<RectTransform> openPageAndFindSlot)
    {
        purchasePlaying = true;
        ScreenBlocker.SetBlocked(this, true);

        yield return GrowAndPulse(artifact);

        RectTransform slot = openPageAndFindSlot != null ? openPageAndFindSlot() : null;
        card.SetAsLastSibling(); // The page just opened on top - get back in front of it

        // The real box stays invisible until the card lands on it, so it looks like the card becomes the box
        CanvasGroup slotGroup = null;
        float slotAlpha = 1f;
        if (slot != null)
        {
            slotGroup = slot.GetComponent<CanvasGroup>();
            if (slotGroup == null) slotGroup = slot.gameObject.AddComponent<CanvasGroup>();
            slotAlpha = slotGroup.alpha;
            slotGroup.alpha = 0f;
        }

        Vector3 from = card.position;
        Vector3 to = slot != null ? slot.TransformPoint(slot.rect.center) : from;
        float toScale = slot != null ? ScaleToMatch(slot) : endScale;

        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / flyIntoSlotSeconds)
        {
            float eased = t * t * (3f - 2f * t); // Smooth start and smooth landing
            card.position = Vector3.LerpUnclamped(from, to, eased);
            card.localScale = Vector3.one * Mathf.Lerp(1f, toScale, eased);
            yield return null;
        }

        card.gameObject.SetActive(false);
        if (slot != null && slotGroup != null) slotGroup.alpha = slotAlpha; // "slot != null" also catches it being rebuilt mid-flight

        ScreenBlocker.SetBlocked(this, false);
        purchasePlaying = false;
    }

    // The scale that makes the card exactly as wide as the target box
    private float ScaleToMatch(RectTransform target)
    {
        float parentScale = card.parent != null ? card.parent.lossyScale.x : 1f;
        float cardWidth = card.rect.width * parentScale;     // Card's width on screen at scale 1
        float targetWidth = target.rect.width * target.lossyScale.x;
        return cardWidth > 0f ? targetWidth / cardWidth : endScale;
    }

    // Goes a bit past 1 then settles back - the "pop"
    private static float EaseOutBack(float t)
    {
        const float c = 1.7f;
        float u = Mathf.Clamp01(t) - 1f;
        return 1f + (c + 1f) * u * u * u + c * u * u;
    }


    // TESTING

    [ContextMenu("Testing: Reveal Random Artifact")]
    private void RevealForTesting()
    {
        if (!Application.isPlaying || ArtifactInventory.main == null) { Debug.LogWarning("Press Play first.", this); return; }
        Show(ArtifactInventory.main.AddRandom());
    }
}