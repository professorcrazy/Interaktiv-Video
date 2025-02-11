using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using System.Collections.Generic;

[System.Serializable]

public class InteractiveVideoManager : MonoBehaviour
{
    [Header("Video Settings")]
    [Tooltip("Reference to the VideoPlayer component in the scene.")]
    public VideoPlayer videoPlayer;

    [Header("UI Elements")]
    [Tooltip("The panel (a GameObject) that holds the option buttons.")]
    public GameObject optionsPanel;
    [Tooltip("Parent Transform where option buttons will be instantiated.")]
    public Transform optionsContainer;
    [Tooltip("A Button prefab (with a Text child) for displaying an option.")]
    public Button optionButtonPrefab;

    [Header("Graph Data")]
    [Tooltip("The exported JSON file from the editor (assigned as a TextAsset).")]
    public TextAsset graphJson;

    private VideoTreeDataRuntime treeData;
    private int currentVideoIndex = 0;
    private bool optionsShown = false;
    private bool optionsEnabled = false;

    void Start() {
        // Parse the JSON file into our runtime data structure.
        if (graphJson != null) {
            treeData = JsonUtility.FromJson<VideoTreeDataRuntime>(graphJson.text);
        }
        else {
            Debug.LogError("Graph JSON file not assigned in the inspector.");
            return;
        }

        if (treeData == null || treeData.videoNodes == null || treeData.videoNodes.Count == 0) {
            Debug.LogError("No video nodes found in graph data.");
            return;
        }

        // Hide the options panel initially.
        if (optionsPanel != null)
            optionsPanel.SetActive(false);

        // Start by playing the first video node.
        PlayVideo(currentVideoIndex);
    }
    /*
    void PlayVideo(int videoIndex) {
        VideoNodeData vData = treeData.videoNodes[videoIndex];
        vData.videoClipPath = vData.videoClipPath.Split(".", System.StringSplitOptions.None)[0];
        Debug.Log(vData.videoClipPath);
        // Load the video clip from Resources (ensure videoClipPath is correct).
        VideoClip clip = Resources.Load<VideoClip>(vData.videoClipPath);
        //Debug.Log(clip.name);
        if (clip == null) {
            Debug.LogError("Video clip not found at path: " + vData.videoClipPath);
            return;
        }
        videoPlayer.clip = clip;
        videoPlayer.Play();

        // Reset options state.
        optionsShown = false;
        optionsEnabled = false;
        if (optionsPanel != null)
            optionsPanel.SetActive(false);
    }
    */
    void PlayVideo(int videoIndex) {
        VideoNodeData vData = treeData.videoNodes[videoIndex];

        // Log the raw path from JSON.
        Debug.Log("Raw videoClipPath from JSON: " + vData.videoClipPath);

        string relativePath = vData.videoClipPath;
        string resourcesPrefix = "Assets/Resources/";
        if (relativePath.StartsWith(resourcesPrefix)) {
            relativePath = relativePath.Substring(resourcesPrefix.Length);
        }
        // Remove the extension if present.
        int extensionIndex = relativePath.LastIndexOf('.');
        if (extensionIndex >= 0) {
            relativePath = relativePath.Substring(0, extensionIndex);
        }

        Debug.Log("Loading video clip from Resources path: " + relativePath);

        VideoClip clip = Resources.Load<VideoClip>(relativePath);
        if (clip == null) {
            Debug.LogError("Failed to load VideoClip from path: " + relativePath);
            return;
        }

        videoPlayer.clip = clip;
        videoPlayer.Play();

        // Reset options state.
        optionsShown = false;
        optionsEnabled = false;
        if (optionsPanel != null)
            optionsPanel.SetActive(false);
    }

    void Update() {
        // Monitor playback progress.
        if (videoPlayer != null && videoPlayer.isPlaying && videoPlayer.length > 0) {
            float progress = (float)(videoPlayer.time / videoPlayer.length);

            // At 80% progress, show the options if not already shown.
            if (!optionsShown && progress >= 0.8f) {
                ShowOptions();
                optionsShown = true;
            }
            // At 100%, enable option buttons.
            if (optionsShown && !optionsEnabled && progress >= 0.8f) {
                EnableOptionButtons();
                optionsEnabled = true;
            }
        }
    }

    void ShowOptions() {
        // Clear any previous buttons.
        foreach (Transform child in optionsContainer) {
            Destroy(child.gameObject);
        }

        // Get the current video node data.
        VideoNodeData vData = treeData.videoNodes[currentVideoIndex];

        // For each option index defined in the video node, create a button.
        if (vData.optionChildIndices != null && vData.optionChildIndices.Count > 0) {
            foreach (int optionIndex in vData.optionChildIndices) {
                OptionNodeData optionData = treeData.optionNodes[optionIndex];
                Button btn = Instantiate(optionButtonPrefab, optionsContainer);
                btn.GetComponent<OptionBtnController>().InitializeOptoonButton(optionData.choicePrompt);
                /*
                Text btnText = btn.GetComponentInChildren<Text>();
                if (btnText != null) { 
                    btnText.text = optionData.choicePrompt;
                    Debug.Log("btn text: " + btnText.text);
                }
                //btn.interactable = false;  // Disable until video finishes.
                */
                // Capture the optionData for the callback.
                OptionNodeData capturedOption = optionData;
                btn.onClick.AddListener(() => { OnOptionSelected(capturedOption); });
            }
            if (optionsPanel != null)
                optionsPanel.SetActive(true);
        }
        else {
            Debug.Log("No options available for this video node.");
        }
    }

    void EnableOptionButtons() {
        // Enable all buttons under optionsContainer.
        foreach (Button btn in optionsContainer.GetComponentsInChildren<Button>()) {
            //btn.interactable = true;
        }
    }

    void OnOptionSelected(OptionNodeData option) {
        Debug.Log("Option selected: " + option.choicePrompt);
        // Set the next video index based on the chosen option.
        if (option.childVideoIndex >= 0 && option.childVideoIndex < treeData.videoNodes.Count) {
            currentVideoIndex = option.childVideoIndex;
            PlayVideo(currentVideoIndex);
        }
        else {
            Debug.LogError("Invalid child video index: " + option.childVideoIndex);
        }
    }
}
public class VideoTreeDataRuntime
{
    public List<VideoNodeData> videoNodes;
    public List<OptionNodeData> optionNodes;
}

[System.Serializable]
public class VideoNodeData
{
    public string title;
    public string videoClipPath;  // Must correspond to a Resources path (without extension)
    public List<int> optionChildIndices;
}

[System.Serializable]
public class OptionNodeData
{
    public string title;
    public string choicePrompt;   // Text to display on the option button.
    public int parentVideoIndex;
    public int childVideoIndex;   // -1 if none.
}
