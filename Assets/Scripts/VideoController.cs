using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class VideoController : MonoBehaviour
{
    VideoPlayer player;
    [SerializeField] private Slider progressBar;
    private float progress = 0;
   
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        player = GetComponent<VideoPlayer>();
    }

    // Update is called once per frame
    void Update()
    {
        progress = (float)player.frame / (float)player.frameCount;
        progressBar.value = progress;
    }
}
