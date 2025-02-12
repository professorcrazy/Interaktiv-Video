using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using System.Collections.Generic;
using Unity.VisualScripting;

public class InteractiveVideoManager : MonoBehaviour
{
    [Header("Video Settings")]
    [Tooltip("The VideoPlayer component that will play the video clips.")]
    public VideoPlayer videoPlayer;

    [Header("UI Elements")]
    [Tooltip("The panel that will contain the option buttons.")]
    public GameObject optionsPanel;
    [Tooltip("The parent transform under the options panel where buttons will be instantiated.")]
    public Transform optionsContainer;
    [Tooltip("A prefab for the option button (should have a Button component and a child Text).")]
    public Button optionButtonPrefab;
    [Tooltip("A panel for the end of the story options")]
    public GameObject storyFinishedPanel;

    [Header("Tree Data")]
    [Tooltip("The InteractiveVideoTree component from your scene that stores your graph.")]
    public InteractiveVideoTree videoTree;

    // Index of the currently playing video node.
    private int currentVideoIndex = 0;
    private bool optionsShown = false;
    private bool optionsEnabled = false;

    void Start() {
        if(storyFinishedPanel != null) {
            storyFinishedPanel.SetActive(false);
        }
        if (videoTree == null) {
            Debug.LogError("InteractiveVideoManager: No InteractiveVideoTree component assigned!");
            return;
        }
        if (videoTree.videoNodes == null || videoTree.videoNodes.Count == 0) {
            Debug.LogError("InteractiveVideoManager: The InteractiveVideoTree has no video nodes!");
            return;
        }
    }
    public void StartVideo() {
        if (optionsPanel != null)
            optionsPanel.SetActive(false);
        PlayVideo(currentVideoIndex);
    }

    void Update() {
        if (videoPlayer == null || !videoPlayer.isPlaying || videoPlayer.clip == null)
            return;

        float progress = (float)(videoPlayer.time / videoPlayer.clip.length);
        if (!optionsShown && progress >= 0.8f) {
            ShowOptions();
            optionsShown = true;
        }
        if (optionsShown && !optionsEnabled && progress >= 0.95f) {
            EnableOptionButtons();
            optionsEnabled = true;
        }
    }

    void PlayVideo(int videoIndex) {
        if (videoIndex < 0 || videoIndex >= videoTree.videoNodes.Count) {
            Debug.LogError("InteractiveVideoManager: Invalid video index: " + videoIndex);
            return;
        }
        VideoNodeData videoData = videoTree.videoNodes[videoIndex];
        if (videoData.videoClip == null) {
            Debug.LogError("InteractiveVideoManager: Video clip is null for video node: " + videoData.title);
            return;
        }
        videoPlayer.clip = videoData.videoClip;
        videoPlayer.Play();
        optionsShown = false;
        optionsEnabled = false;
        if (optionsPanel != null)
            optionsPanel.GetComponent<OptionController>()?.Disablepanel();//SetActive(false);
    }

    void ShowOptions() {
        foreach (Transform child in optionsContainer) {
            Destroy(child.gameObject);
        }
        VideoNodeData videoData = videoTree.videoNodes[currentVideoIndex];
        if (videoData.optionChildIndices == null || videoData.optionChildIndices.Count == 0) {
            Debug.Log("InteractiveVideoManager: No options available for video node: " + videoData.title);
            StoryEnded();
            return;
        }
        foreach (int optionIndex in videoData.optionChildIndices) {
            if (optionIndex < 0 || optionIndex >= videoTree.optionNodes.Count)
                continue;
            OptionNodeData optionData = videoTree.optionNodes[optionIndex];
            Button btn = Instantiate(optionButtonPrefab, optionsContainer);
            btn.GetComponent<OptionBtnController>()?.InitializeOptoonButton(optionData.choicePrompt);
            

            btn.interactable = false;
            btn.onClick.AddListener(() => { OnOptionSelected(optionData); });
        }
        if (optionsPanel != null)
            optionsPanel.GetComponent<OptionController>()?.EnablePanel();//SetActive(true);
    }

    void EnableOptionButtons() {
        Button[] buttons = optionsContainer.GetComponentsInChildren<Button>();
        foreach (Button btn in buttons) {
            btn.interactable = true;
        }
    }

    void OnOptionSelected(OptionNodeData optionData) {
        Debug.Log("InteractiveVideoManager: Option selected: " + optionData.choicePrompt);
        if (optionData.childVideoIndex < 0 || optionData.childVideoIndex >= videoTree.videoNodes.Count) {
            Debug.LogError("InteractiveVideoManager: Invalid child video index for option: " + optionData.title);
            return;
        }
        currentVideoIndex = optionData.childVideoIndex;
        PlayVideo(currentVideoIndex);
    }

    private void StoryEnded() {
        if (storyFinishedPanel != null) {
            storyFinishedPanel.SetActive(true);
        }
        videoPlayer.Stop();
    }

}
