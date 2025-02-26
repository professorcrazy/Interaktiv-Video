using UnityEngine;
using UnityEditor;
using UnityEngine.Video;
using System.Collections.Generic;
using System.IO;

public class InteractiveVideoEditorWindow : EditorWindow
{
    // In‑memory representations of nodes.
    public List<VideoNode> videoNodes = new List<VideoNode>();
    public List<OptionNode> optionNodes = new List<OptionNode>();

    // Pan offset for the canvas.
    public Vector2 panOffset = Vector2.zero;

    // For node dragging.
    private VideoNode selectedVideoNode = null;
    private OptionNode selectedOptionNode = null;
    private bool isDraggingNode = false;
    private Vector2 dragOffset;

    // Field for pending connection mode.
    public OptionNode pendingConnectionOption = null;

    // The currently selected InteractiveVideoTree component.
    private InteractiveVideoTree currentTree = null;

    public static InteractiveVideoEditorWindow Instance;

    [MenuItem("Window/Interactive Video Editor")]
    public static void ShowWindow() {
        Instance = GetWindow<InteractiveVideoEditorWindow>("Interactive Video Editor");
    }

    void OnEnable() {
        Instance = this;
        UpdateCurrentTree();
    }

    void OnSelectionChange() {
        UpdateCurrentTree();
        Repaint();
    }

    // If the selected GameObject has an InteractiveVideoTree component, load its data.
    void UpdateCurrentTree() {
        GameObject go = Selection.activeGameObject;
        if (go != null) {
            InteractiveVideoTree tree = go.GetComponent<InteractiveVideoTree>();
            if (tree != null) {
                currentTree = tree;
                LoadTreeFromComponent();
                return;
            }
        }
        currentTree = null;
        videoNodes.Clear();
        optionNodes.Clear();
    }

    private void OnGUI() {
        // If no InteractiveVideoTree is selected, show a message.
        if (currentTree == null) {
            EditorGUILayout.LabelField("Select a GameObject with an InteractiveVideoTree component in the scene.");
            return;
        }

        DrawGrid(20, 0.2f, Color.gray);

        // Toolbar: "Save to GameObject" writes the current editor tree back into the component.
        GUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Button("Save to GameObject", EditorStyles.toolbarButton)) {
            SaveTreeToComponent();
        }
        if (GUILayout.Button("Clear Tree", EditorStyles.toolbarButton)) {
            if (EditorUtility.DisplayDialog("Clear Tree", "Are you sure you want to clear the tree?", "Yes", "No")) {
                videoNodes.Clear();
                optionNodes.Clear();
                SaveTreeToComponent();
            }
        }
        GUILayout.EndHorizontal();

        // Use a large canvas so that nodes outside the window are rendered.
        Rect canvasRect = new Rect(panOffset.x, panOffset.y, 5000, 5000);
        GUI.BeginGroup(canvasRect);
        DrawConnections();
        DrawNodes();
        GUI.EndGroup();

        ProcessEvents(Event.current);

        if (GUI.changed)
            Repaint();
    }

    #region Grid & Node Drawing
    void DrawGrid(float gridSpacing, float gridOpacity, Color gridColor) {
        int widthDivs = Mathf.CeilToInt(position.width / gridSpacing);
        int heightDivs = Mathf.CeilToInt(position.height / gridSpacing);
        Handles.BeginGUI();
        Handles.color = new Color(gridColor.r, gridColor.g, gridColor.b, gridOpacity);
        Vector3 newOffset = new Vector3(panOffset.x % gridSpacing, panOffset.y % gridSpacing, 0);
        for (int i = 0; i < widthDivs; i++) {
            Vector3 start = new Vector3(gridSpacing * i, 0, 0) + newOffset;
            Vector3 end = new Vector3(gridSpacing * i, position.height, 0) + newOffset;
            Handles.DrawLine(start, end);
        }
        for (int j = 0; j < heightDivs; j++) {
            Vector3 start = new Vector3(0, gridSpacing * j, 0) + newOffset;
            Vector3 end = new Vector3(position.width, gridSpacing * j, 0) + newOffset;
            Handles.DrawLine(start, end);
        }
        Handles.color = Color.white;
        Handles.EndGUI();
    }

    void DrawNodes() {
        foreach (var v in videoNodes)
            v.Draw();
        foreach (var o in optionNodes)
            o.Draw();
    }

    void DrawConnections() {
        foreach (var v in videoNodes) {
            foreach (var o in v.optionChildren) {
                Vector3 start = new Vector3(v.rect.center.x, v.rect.yMax, 0);
                Vector3 end = new Vector3(o.rect.center.x, o.rect.y, 0);
                Handles.DrawLine(start, end);
            }
        }
        foreach (var o in optionNodes) {
            if (o.childVideo != null) {
                Vector3 start = new Vector3(o.rect.center.x, o.rect.yMax, 0);
                Vector3 end = new Vector3(o.childVideo.rect.center.x, o.childVideo.rect.y, 0);
                Handles.DrawLine(start, end);
            }
        }
    }
    #endregion

    #region Event Processing & Context Menus
    void ProcessEvents(Event e) {
        // Pending connection mode: if active, then on left‑click try to connect.
        if (pendingConnectionOption != null && e.type == EventType.MouseDown && e.button == 0) {
            Vector2 adjustedMousePos = e.mousePosition - panOffset;
            VideoNode target = GetVideoNodeAtPoint(adjustedMousePos);
            if (target != null) {
                pendingConnectionOption.childVideo = target;
                Debug.Log("Connected option '" + pendingConnectionOption.title + "' to video node '" + target.title + "'.");
            }
            else {
                Debug.Log("Click was not on a Video Node. Connection canceled.");
            }
            pendingConnectionOption = null;
            e.Use();
            return;
        }

        // Panning with middle mouse.
        if (e.button == 2 && (e.type == EventType.MouseDrag || e.type == EventType.MouseDown)) {
            panOffset += e.delta;
            e.Use();
        }

        Vector2 adjustedPos = e.mousePosition - panOffset;
        if (e.type == EventType.ContextClick) {
            VideoNode vNode = GetVideoNodeAtPoint(adjustedPos);
            OptionNode oNode = GetOptionNodeAtPoint(adjustedPos);
            if (vNode != null)
                ShowVideoNodeContextMenu(adjustedPos, vNode);
            else if (oNode != null)
                ShowOptionNodeContextMenu(adjustedPos, oNode);
            else
                ShowGlobalContextMenu(adjustedPos);
            e.Use();
        }
        ProcessNodeDragging(e, adjustedPos);
    }

    void ProcessNodeDragging(Event e, Vector2 adjustedPos) {
        if (e.type == EventType.MouseDown && e.button == 0) {
            selectedVideoNode = GetVideoNodeAtPoint(adjustedPos);
            selectedOptionNode = GetOptionNodeAtPoint(adjustedPos);
            if (selectedVideoNode != null || selectedOptionNode != null) {
                isDraggingNode = true;
                if (selectedVideoNode != null)
                    dragOffset = selectedVideoNode.rect.position - adjustedPos;
                else if (selectedOptionNode != null)
                    dragOffset = selectedOptionNode.rect.position - adjustedPos;
                e.Use();
            }
        }
        if (e.type == EventType.MouseDrag && isDraggingNode) {
            if (selectedVideoNode != null) {
                selectedVideoNode.rect.position = adjustedPos + dragOffset;
                e.Use();
            }
            else if (selectedOptionNode != null) {
                selectedOptionNode.rect.position = adjustedPos + dragOffset;
                e.Use();
            }
            GUI.changed = true;
        }
        if (e.type == EventType.MouseUp) {
            isDraggingNode = false;
            selectedVideoNode = null;
            selectedOptionNode = null;
        }
    }

    VideoNode GetVideoNodeAtPoint(Vector2 point) {
        for (int i = videoNodes.Count - 1; i >= 0; i--) {
            if (videoNodes[i].rect.Contains(point))
                return videoNodes[i];
        }
        return null;
    }

    OptionNode GetOptionNodeAtPoint(Vector2 point) {
        for (int i = optionNodes.Count - 1; i >= 0; i--) {
            if (optionNodes[i].rect.Contains(point))
                return optionNodes[i];
        }
        return null;
    }

    void ShowGlobalContextMenu(Vector2 mousePos) {
        GenericMenu menu = new GenericMenu();
        menu.AddItem(new GUIContent("Add Video Node"), false, () => AddVideoNode(mousePos));
        menu.ShowAsContext();
    }

    void ShowVideoNodeContextMenu(Vector2 mousePos, VideoNode vNode) {
        GenericMenu menu = new GenericMenu();
        menu.AddItem(new GUIContent("Add Option Child"), false, () => AddOptionChild(vNode));
        menu.AddSeparator("");
        menu.AddItem(new GUIContent("Delete Video Node"), false, () => DeleteVideoNode(vNode));
        // Allow connecting an existing Option Node (that has no parent) to this Video Node.
        menu.AddItem(new GUIContent("Connect Existing Option Node"), false, () => {
            GenericMenu connectMenu = new GenericMenu();
            foreach (var option in optionNodes) {
                if (option.parentVideo == null) {
                    connectMenu.AddItem(new GUIContent(option.title), false, () => {
                        option.parentVideo = vNode;
                        vNode.optionChildren.Add(option);
                    });
                }
            }
            connectMenu.ShowAsContext();
        });
        menu.ShowAsContext();
    }

    void ShowOptionNodeContextMenu(Vector2 mousePos, OptionNode oNode) {
        GenericMenu menu = new GenericMenu();
        menu.AddItem(new GUIContent("Add Child Video Node"), false, () => AddChildVideoNode(oNode));
        menu.AddSeparator("");
        menu.AddItem(new GUIContent("Delete Option Node"), false, () => DeleteOptionNode(oNode));
        if (oNode.childVideo == null) {
            menu.AddItem(new GUIContent("Connect Existing Video Node"), false, () => {
                pendingConnectionOption = oNode;
                Debug.Log("Pending connection: Click on a Video Node to connect to.");
            });
        }
        menu.ShowAsContext();
    }
    #endregion

    #region Node Creation & Deletion
    void AddVideoNode(Vector2 pos) {
        VideoNode newVideo = new VideoNode(pos, 220, 140);
        videoNodes.Add(newVideo);
    }

    // Option nodes are created with size (250, 150).
    void AddOptionChild(VideoNode parent) {
        Vector2 pos = parent.rect.position + new Vector2(0, parent.rect.height + 50);
        OptionNode newOption = new OptionNode(pos, 250, 150);
        newOption.parentVideo = parent;
        parent.optionChildren.Add(newOption);
        optionNodes.Add(newOption);
    }

    void AddChildVideoNode(OptionNode option) {
        Vector2 pos = option.rect.position + new Vector2(0, option.rect.height + 50);
        VideoNode newVideo = new VideoNode(pos, 220, 140);
        option.childVideo = newVideo;
        videoNodes.Add(newVideo);
    }

    public void DeleteVideoNode(VideoNode node) {
        foreach (OptionNode o in optionNodes) {
            if (o.childVideo == node)
                o.childVideo = null;
            if (o.parentVideo == node)
                o.parentVideo = null;
        }
        videoNodes.Remove(node);
    }

    public void DeleteOptionNode(OptionNode node) {
        if (node.parentVideo != null)
            node.parentVideo.optionChildren.Remove(node);
        if (node.childVideo != null)
            node.childVideo = null;
        optionNodes.Remove(node);
    }
    #endregion

    #region Component Linking (Scene Data)
    void LoadTreeFromComponent() {
        if (currentTree == null)
            return;

        videoNodes.Clear();
        optionNodes.Clear();

        // Create VideoNode instances.
        for (int i = 0; i < currentTree.videoNodes.Count; i++) {
            VideoNodeData data = currentTree.videoNodes[i];
            VideoNode node = new VideoNode(new Vector2(data.x, data.y), data.width, data.height);
            node.title = data.title;
            node.videoClip = data.videoClip;
            node.progress = data.progress;
            videoNodes.Add(node);
        }
        // Create OptionNode instances.
        for (int i = 0; i < currentTree.optionNodes.Count; i++) {
            OptionNodeData data = currentTree.optionNodes[i];
            OptionNode node = new OptionNode(new Vector2(data.x, data.y), data.width, data.height);
            node.title = data.title;
            node.choicePrompt = data.choicePrompt;
            node.isSelected = data.isSelected;
            optionNodes.Add(node);
        }
        // Re-link relationships.
        for (int i = 0; i < currentTree.optionNodes.Count; i++) {
            OptionNodeData data = currentTree.optionNodes[i];
            OptionNode option = optionNodes[i];
            if (data.parentVideoIndex >= 0 && data.parentVideoIndex < videoNodes.Count) {
                option.parentVideo = videoNodes[data.parentVideoIndex];
                videoNodes[data.parentVideoIndex].optionChildren.Add(option);
            }
            if (data.childVideoIndex >= 0 && data.childVideoIndex < videoNodes.Count) {
                option.childVideo = videoNodes[data.childVideoIndex];
            }
        }
    }

    void SaveTreeToComponent() {
        if (currentTree == null)
            return;

        currentTree.videoNodes.Clear();
        currentTree.optionNodes.Clear();

        // Save VideoNodes.
        for (int i = 0; i < videoNodes.Count; i++) {
            VideoNode node = videoNodes[i];
            VideoNodeData data = new VideoNodeData();
            data.x = node.rect.x;
            data.y = node.rect.y;
            data.width = node.rect.width;
            data.height = node.rect.height;
            data.title = node.title;
            data.videoClip = node.videoClip;
            data.progress = node.progress;
            data.optionChildIndices = new List<int>();
            for (int j = 0; j < node.optionChildren.Count; j++) {
                int index = optionNodes.IndexOf(node.optionChildren[j]);
                if (index >= 0)
                    data.optionChildIndices.Add(index);
            }
            currentTree.videoNodes.Add(data);
        }
        // Save OptionNodes.
        for (int i = 0; i < optionNodes.Count; i++) {
            OptionNode node = optionNodes[i];
            OptionNodeData data = new OptionNodeData();
            data.x = node.rect.x;
            data.y = node.rect.y;
            data.width = node.rect.width;
            data.height = node.rect.height;
            data.title = node.title;
            data.choicePrompt = node.choicePrompt;
            data.isSelected = node.isSelected;
            data.parentVideoIndex = videoNodes.IndexOf(node.parentVideo);
            data.childVideoIndex = node.childVideo != null ? videoNodes.IndexOf(node.childVideo) : -1;
            currentTree.optionNodes.Add(data);
        }
    }
    #endregion
}

#region Node Classes

[System.Serializable]
public abstract class BaseNode
{
    public Rect rect;
    public string title;

    public BaseNode(Vector2 pos, float width, float height, string title) {
        rect = new Rect(pos.x, pos.y, width, height);
        this.title = title;
    }

    public abstract void Draw();
}

[System.Serializable]
public class VideoNode : BaseNode
{
    public VideoClip videoClip;
    public float progress = 0f;
    public List<OptionNode> optionChildren = new List<OptionNode>();

    public VideoNode(Vector2 pos, float width, float height)
        : base(pos, width, height, "Video Node") { }

    public override void Draw() {
        GUI.Box(rect, title);
        Rect clipRect = new Rect(rect.x + 10, rect.y + 20, rect.width - 20, 20);
        videoClip = (VideoClip)EditorGUI.ObjectField(clipRect, videoClip, typeof(VideoClip), false);
        Rect sliderRect = new Rect(rect.x + 10, rect.y + 45, rect.width - 20, 20);
        progress = EditorGUI.Slider(sliderRect, "Progress", progress, 0f, 1f);
        if (progress >= 0.8f) {
            Rect labelRect = new Rect(rect.x + 10, rect.y + 70, rect.width - 20, 20);
            GUI.Label(labelRect, "Options appear at runtime");
        }
    }
}

[System.Serializable]
public class OptionNode : BaseNode
{
    public VideoNode parentVideo;
    public VideoNode childVideo;
    public bool isSelected = false;
    public string choicePrompt = "Choice";

    public OptionNode(Vector2 pos, float width, float height)
        : base(pos, width, height, "Option Node") { }

    public override void Draw() {
        GUI.Box(rect, title);
        Rect parentRect = new Rect(rect.x + 10, rect.y + 20, rect.width - 20, 20);
        GUI.Label(parentRect, (parentVideo != null ? "Parent: " + parentVideo.title : "No Parent"));
        Rect childRect = new Rect(rect.x + 10, rect.y + 40, rect.width - 20, 20);
        GUI.Label(childRect, (childVideo != null ? "Child: " + childVideo.title : "No Child"));
        if (childVideo == null) {
            Rect connectRect = new Rect(rect.x + 10, rect.y + 65, rect.width - 20, 20);
            if (GUI.Button(connectRect, "Connect Video Node")) {
                InteractiveVideoEditorWindow.Instance.pendingConnectionOption = this;
                Debug.Log("Pending connection: Click on a Video Node to connect to.");
            }
            Rect promptRect = new Rect(rect.x + 10, rect.y + 90, rect.width - 20, 20);
            choicePrompt = EditorGUI.TextField(promptRect, "Prompt", choicePrompt);
        }
        else {
            Rect promptRect = new Rect(rect.x + 10, rect.y + 65, rect.width - 20, 20);
            choicePrompt = EditorGUI.TextField(promptRect, "Prompt", choicePrompt);
        }
    }
}
#endregion
