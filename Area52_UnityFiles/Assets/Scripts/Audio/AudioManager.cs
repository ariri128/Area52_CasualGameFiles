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
    private AudioSource previewSource;

    public AudioClip CurrentMusic => musicSource != null ? musicSource.clip : null;
    public bool IsPreviewing => previewSource != null && previewSource.isPlaying;
    public AudioClip PreviewClip => IsPreviewing ? previewSource.clip : null;

    private void Awake()
    {
        if (main != null && main != this)
        {
            Debug.LogWarning("More than one AudioManager in the scene. Destroying the extra one.", this);
            Destroy(this);
            return;
        }
        main = this;

        // Separate speakers, so a click never cuts off the music
        musicSource = CreateSource();
        musicSource.loop = true;

        clickSource = CreateSource();

        previewSource = CreateSource();
        previewSource.loop = true; // Keeps going until the player presses pause
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
        if (previewSource != null) previewSource.volume = musicVolume;
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

    // One-off sound effects (exploring, etc.)
    public static void PlaySound(AudioClip clip, float volume = 1f)
    {
        if (main == null || clip == null) return;
        main.clickSource.PlayOneShot(clip, volume);
    }

    // Changes the background track. Starts it from the beginning, unless a preview is playing, in which case it starts when the preview stops.
    public void SetMusic(AudioClip clip)
    {
        if (clip == null || musicSource.clip == clip) return;

        musicSource.Stop();
        musicSource.clip = clip;
        musicSource.volume = musicVolume;
        if (!IsPreviewing) musicSource.Play();
    }

    public void PlayPreview(AudioClip clip)
    {
        if (clip == null) return;

        musicSource.Pause();
        previewSource.clip = clip;
        previewSource.volume = musicVolume;
        previewSource.Play();
    }

    public void StopPreview()
    {
        if (previewSource.clip == null) return;

        previewSource.Stop();
        previewSource.clip = null;

        // Carry on where the background music was (or start it if the track changed)
        musicSource.UnPause();
        if (!musicSource.isPlaying && musicSource.clip != null) musicSource.Play();
    }
}