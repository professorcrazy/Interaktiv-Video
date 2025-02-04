using TMPro;
using UnityEngine;
using UnityEngine.Video;

public class NextClip : MonoBehaviour
{
    public VideoClip clip;
    public string choice = "";
    VideoController vPlayer;
    public TMP_Text optionText;
    private void Start() {
        vPlayer = GameObject.FindAnyObjectByType<VideoController>();
    }
    public void SetupNextClip() {
        vPlayer.nextClip = clip;
        optionText.text = choice;
    }
}
