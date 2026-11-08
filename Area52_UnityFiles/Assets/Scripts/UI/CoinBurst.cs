using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/*
 * A handful of coins bursting out of the middle of the screen
 * Each coin flies out in a random direction, spins, and gets bigger as it goes (so it looks like it's coming at the player), then fades
 * Never blocks taps
*/

public class CoinBurst : MonoBehaviour
{
    [Header("Parts")]
    [Tooltip("One coin: a child Image anchored to the middle. It's copied Coin Count times and hidden itself.")]
    [SerializeField] private RectTransform coinTemplate;

    [Header("Burst")]
    [SerializeField] private int coinCount = 14;
    [Tooltip("How far each coin flies (random between X and Y), in canvas units.")]
    [SerializeField] private Vector2 distance = new Vector2(250f, 650f);
    [Tooltip("Size when it starts, compared to the template.")]
    [SerializeField] private float startScale = 0.5f;
    [Tooltip("Size when it finishes (random between X and Y). Bigger = more 'at the player'.")]
    [SerializeField] private Vector2 endScale = new Vector2(1.8f, 2.8f);
    [Tooltip("Seconds one coin takes.")]
    [SerializeField] private float seconds = 0.9f;
    [Tooltip("Coins don't all leave at once - each waits up to this long first.")]
    [SerializeField] private float maxDelay = 0.12f;
    [Tooltip("Most a coin spins while flying, in degrees (random each way).")]
    [SerializeField] private float maxSpin = 360f;

    private readonly List<RectTransform> coins = new List<RectTransform>();
    private readonly List<CanvasGroup> groups = new List<CanvasGroup>();

    private void Awake()
    {
        coinTemplate.gameObject.SetActive(false);

        // Made once, then reused every burst
        for (int i = 0; i < coinCount; i++)
        {
            RectTransform coin = Instantiate(coinTemplate, coinTemplate.parent);
            coin.name = $"Coin_{i + 1}";

            CanvasGroup group = coin.GetComponent<CanvasGroup>();
            if (group == null) group = coin.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            coin.gameObject.SetActive(false);
            coins.Add(coin);
            groups.Add(group);
        }
    }

    // Hidden partway (e.g. off to the breeding room): stop cleanly
    private void OnDisable()
    {
        StopAllCoroutines();
        foreach (RectTransform coin in coins)
            if (coin != null) coin.gameObject.SetActive(false);
    }

    public void Play()
    {
        if (!isActiveAndEnabled) return;
        StopAllCoroutines(); // A new burst restarts every coin
        for (int i = 0; i < coins.Count; i++) StartCoroutine(Fly(coins[i], groups[i]));
    }

    private IEnumerator Fly(RectTransform coin, CanvasGroup group)
    {
        coin.gameObject.SetActive(false);
        yield return new WaitForSecondsRealtime(Random.Range(0f, maxDelay));

        // Random direction all the way round
        Vector2 direction = Random.insideUnitCircle.normalized;
        if (direction == Vector2.zero) direction = Vector2.up;

        Vector2 end = direction * Random.Range(distance.x, distance.y);
        float finalScale = Random.Range(endScale.x, endScale.y);
        float spin = Random.Range(-maxSpin, maxSpin);

        coin.anchoredPosition = Vector2.zero;
        coin.localScale = Vector3.one * startScale;
        coin.localRotation = Quaternion.identity;
        group.alpha = 1f;
        coin.gameObject.SetActive(true);

        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / seconds)
        {
            float eased = 1f - Mathf.Pow(1f - t, 3f); // Fast out, then slows down
            coin.anchoredPosition = Vector2.LerpUnclamped(Vector2.zero, end, eased);
            coin.localScale = Vector3.one * Mathf.Lerp(startScale, finalScale, t);
            coin.localRotation = Quaternion.Euler(0f, 0f, spin * t);
            group.alpha = 1f - Mathf.Clamp01((t - 0.6f) / 0.4f); // Fades over the last 40%
            yield return null;
        }

        coin.gameObject.SetActive(false);
    }


    // TESTING

    [ContextMenu("Testing: Play")]
    private void PlayForTesting()
    {
        if (!Application.isPlaying) { Debug.LogWarning("Press Play first.", this); return; }
        Play();
    }
}