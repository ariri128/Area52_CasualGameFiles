using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/*
 * The feeding animation: a pellet pops out of the Feed button, arcs over to the alien, spins and shrinks into it,
 * and the alien does a little chomp
 * The alien only gets fuller when the pellet lands (the pellet was already spent when Feed was pressed)
 * Several pellets can be in the air at once if the player taps quickly. Never blocks taps
*/

public class PelletFeeder : MonoBehaviour
{
    public static PelletFeeder main { get; private set; }

    [SerializeField] private Camera cam;
    [Tooltip("A single-pellet Image on the same canvas as the thought bubble, switched off, Raycast Target off. " +
             "Copied for each pellet. Keep it below the bubble in the Hierarchy so it draws on top.")]
    [SerializeField] private RectTransform pelletTemplate;

    [Header("Flight")]
    [SerializeField] private float flySeconds = 0.45f;
    [Tooltip("How high (UI units) the pellet arcs above a straight line.")]
    [SerializeField] private float arcHeight = 120f;
    [Tooltip("Where on the alien it lands: 0 = bottom of the sprite, 0.5 = middle, 1 = top.")]
    [Range(0f, 1f)] [SerializeField] private float aimHeight = 0.45f;
    [Tooltip("Size when it reaches the alien, compared to its start size.")]
    [SerializeField] private float endScale = 0.35f;
    [Tooltip("Degrees it spins on the way.")]
    [SerializeField] private float spin = 270f;

    private class Flight
    {
        public OwnedAlien alien;
        public RectTransform pellet;
    }

    private readonly List<Flight> flights = new List<Flight>();
    private Canvas canvas;

    private void Awake()
    {
        if (main != null && main != this)
        {
            Debug.LogWarning("More than one PelletFeeder in the scene. Destroying the extra one.", this);
            Destroy(this);
            return;
        }
        main = this;

        if (cam == null) cam = Camera.main;
        canvas = GetComponentInParent<Canvas>().rootCanvas;
        if (pelletTemplate != null) pelletTemplate.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (main == this) main = null;
    }

    // Switched off partway (e.g. off to the breeding room): every pellet in the air lands straight away, so none are lost
    private void OnDisable()
    {
        StopAllCoroutines();
        foreach (Flight flight in new List<Flight>(flights)) Land(flight, false);
        flights.Clear();
    }

    // Sends one pellet from the button to the alien. The pellet should already be spent
    public void Feed(OwnedAlien alien, RectTransform fromButton)
    {
        if (alien == null) return;

        // No template or button - just feed it with no animation
        if (pelletTemplate == null || fromButton == null || !isActiveAndEnabled)
        {
            if (AlienHunger.main != null) AlienHunger.main.Feed(alien);
            return;
        }

        RectTransform pellet = Instantiate(pelletTemplate, pelletTemplate.parent);
        pellet.gameObject.SetActive(true);
        pellet.SetAsLastSibling(); // On top of everything, including the bubble

        Flight flight = new Flight { alien = alien, pellet = pellet };
        flights.Add(flight);
        StartCoroutine(Fly(flight, fromButton.TransformPoint(fromButton.rect.center)));
    }

    private IEnumerator Fly(Flight flight, Vector3 start)
    {
        RectTransform pellet = flight.pellet;

        CanvasGroup group = pellet.GetComponent<CanvasGroup>();
        if (group == null) group = pellet.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.alpha = 1f;

        Vector3 end = start;
        float lift = arcHeight * canvas.scaleFactor; // UI units -> screen pixels

        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / flySeconds)
        {
            if (TryGetMouth(flight.alien, out Vector3 mouth)) end = mouth; // Follows the alien if it moves

            float eased = Mathf.SmoothStep(0f, 1f, t);
            Vector3 top = (start + end) * 0.5f + Vector3.up * lift; // The highest point of the arc pulls the path up
            pellet.position = Arc(start, top, end, eased);
            pellet.localScale = Vector3.one * Mathf.Lerp(1f, endScale, eased);
            pellet.localRotation = Quaternion.Euler(0f, 0f, -spin * eased);
            group.alpha = 1f - Mathf.Clamp01((t - 0.85f) / 0.15f); // Fades over the last 15%
            yield return null;
        }

        Land(flight, true);
        flights.Remove(flight);
    }

    // A curve from a to c that bends towards b
    private static Vector3 Arc(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        Vector3 ab = Vector3.Lerp(a, b, t);
        Vector3 bc = Vector3.Lerp(b, c, t);
        return Vector3.Lerp(ab, bc, t);
    }

    // Where the alien is on screen (the canvas is Screen Space - Overlay, so screen pixels = UI position)
    private bool TryGetMouth(OwnedAlien alien, out Vector3 mouth)
    {
        mouth = Vector3.zero;
        FloatingAlien visual = AlienRoomDisplay.main != null ? AlienRoomDisplay.main.Find(alien) : null;
        if (visual == null || cam == null) return false;

        Bounds b = visual.SpriteRenderer.bounds;
        Vector3 world = new Vector3(b.center.x, Mathf.Lerp(b.min.y, b.max.y, aimHeight), b.center.z);
        Vector3 screen = cam.WorldToScreenPoint(world);
        if (screen.z < 0f) return false;

        mouth = new Vector3(screen.x, screen.y, 0f);
        return true;
    }

    private void Land(Flight flight, bool chomp)
    {
        if (AlienHunger.main != null) AlienHunger.main.Feed(flight.alien);

        if (chomp)
        {
            FloatingAlien visual = AlienRoomDisplay.main != null ? AlienRoomDisplay.main.Find(flight.alien) : null;
            if (visual != null) visual.Chomp();
        }

        if (flight.pellet != null) Destroy(flight.pellet.gameObject);
    }
}