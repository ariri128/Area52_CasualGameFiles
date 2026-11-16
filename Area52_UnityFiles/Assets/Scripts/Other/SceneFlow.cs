using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

/*
 * Moves the player between the apartments and the BreedingRoom scene without ever unloading the apartments
 * The BreedingRoom is loaded on top (additively) the first time, then just shown and hidden after that
*/

public class SceneFlow : MonoBehaviour
{
    public static SceneFlow main { get; private set; }

    [Tooltip("Name of the breeding room scene file, exactly.")]
    [SerializeField] private string breedingSceneName = "BreedingRoom";

    [Tooltip("Apartment objects hidden while the player is in the breeding room " +
             "(Main Camera, UI_Canvas, Directional Light, Global Volume).")]
    [SerializeField] private GameObject[] hideWhileAway;

    [Tooltip("Apartment scripts switched off while away, e.g. Furniture Editor, " +
             "so taps in the breeding room can't select furniture.")]
    [SerializeField] private Behaviour[] pauseWhileAway;

    public bool InBreedingRoom { get; private set; }

    private Scene apartmentScene;
    private Scene breedingScene;
    private bool busy;

    private void Awake()
    {
        if (main != null && main != this)
        {
            Debug.LogWarning("More than one SceneFlow in the scene. Destroying the extra one.", this);
            Destroy(this);
            return;
        }
        main = this;
        apartmentScene = gameObject.scene;
    }

    private void OnDestroy()
    {
        if (main == this) main = null;
    }

    public void OpenBreedingRoom()
    {
        if (InBreedingRoom || busy) return;
        StartCoroutine(OpenRoutine());
    }

    private IEnumerator OpenRoutine()
    {
        busy = true;

        // First visit: loads it on top of the apartments - Later visits: it's already there
        if (!breedingScene.IsValid() || !breedingScene.isLoaded)
        {
            AsyncOperation loading = SceneManager.LoadSceneAsync(breedingSceneName, LoadSceneMode.Additive);
            if (loading == null)
            {
                Debug.LogError($"SceneFlow: couldn't load \"{breedingSceneName}\". " +
                               "Check the name, and that it's in File > Build Profiles > Scene List.", this);
                busy = false;
                yield break;
            }
            yield return loading;
            breedingScene = SceneManager.GetSceneByName(breedingSceneName);
        }

        SetApartmentShown(false);
        SetSceneShown(breedingScene, true);
        SceneManager.SetActiveScene(breedingScene);

        InBreedingRoom = true;
        busy = false;
    }

    public void ReturnToApartments()
    {
        if (!InBreedingRoom || busy) return;

        // Hidden, not unloaded, so whatever is going on in there carries on
        SetSceneShown(breedingScene, false);
        SetApartmentShown(true);
        SceneManager.SetActiveScene(apartmentScene);

        InBreedingRoom = false;
    }

    private void SetApartmentShown(bool shown)
    {
        // Closes the furniture popup / panel before leaving
        if (!shown && FurnitureEditor.main != null) FurnitureEditor.main.Deselect();

        foreach (GameObject target in hideWhileAway)
            if (target != null) target.SetActive(shown);

        foreach (Behaviour script in pauseWhileAway)
            if (script != null) script.enabled = shown;
    }

    private static void SetSceneShown(Scene scene, bool shown)
    {
        if (!scene.IsValid() || !scene.isLoaded) return;
        foreach (GameObject root in scene.GetRootGameObjects()) root.SetActive(shown);
    }
}