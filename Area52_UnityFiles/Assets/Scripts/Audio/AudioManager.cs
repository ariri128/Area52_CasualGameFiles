using UnityEngine;

/*
 * Plays the background music on a loop and the click sound for buttons
*/

public class AudioManager : MonoBehaviour
{
    public static AudioManager main { get; private set; }

    [Header("Music")]
    [Tooltip("Plays on a loop from the moment the game starts.")]
    [SerializeField] private AudioClip music;
    [Range(0f, 1f)]
    [SerializeField] private float musicVolume = 0.5f;

    [Header("Click")]
    [Tooltip("Plays whenever the player presses a button.")]
    [SerializeField] private AudioClip click;
    [Range(0f, 1f)]
    [SerializeField] private float clickVolume = 1f;

    private AudioSource musicSource;
    private AudioSource clickSource;

    private void Awake()
    {
        if (main != null && main != this)
        {
            Debug.LogWarning("More than one AudioManager in the scene. Destroying the extra one.", this);
            Destroy(this);
            return;
        }
        main = this;

        // Uses two separate speakers, so a click never cuts off the music
        musicSource = CreateSource();
        musicSource.loop = true;

        clickSource = CreateSource();
    }

    private void OnDestroy()
    {
        if (main == this) main = null;
    }

    private void Start()
    {
        if (music == null) return;

        musicSource.clip = music;
        musicSource.volume = musicVolume;
        musicSource.Play();
    }

    // Keeps the music volume in sync if you drag the slider while playing
    private void OnValidate()
    {
        if (musicSource != null) musicSource.volume = musicVolume;
    }

    private AudioSource CreateSource()
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f; // 2D: same volume wherever the camera is
        return source;
    }

    public static void PlayClick()
    {
        if (main == null || main.click == null) return;
        main.clickSource.PlayOneShot(main.click, main.clickVolume);
    }
}