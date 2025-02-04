using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class VideoController : MonoBehaviour
{
    VideoClip clip;
    VideoPlayer player;
    [SerializeField] private Slider progressBar;
    private float progress = 0;
    bool once = false;
    public VideoClip nextClip;
    public GameObject ChoiceOptions;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        player = GetComponent<VideoPlayer>();
        clip = player.clip;
    }

    // Update is called once per frame
    void Update()
    {
        progress = (float)player.frame / (float)player.frameCount;
        progressBar.value = progress;
        if (progress > 0.80f && !once) {
            ChoiceOptions.SetActive(true);
            Debug.Log("hit: " + Time.time);
            once = true;
        }
        if (progress > 0.99f) {
            if (nextClip == null) {
                ChoiceOptions.SetActive(true);
                player.Pause();
            }
            else {
                player.clip = nextClip;
                player.Stop();
                player.Play();
                once = false;
                nextClip = null;
            }
        }
    }
}
