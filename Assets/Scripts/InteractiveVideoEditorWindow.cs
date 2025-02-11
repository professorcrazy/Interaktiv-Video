using UnityEngine;
using UnityEditor;
using UnityEngine.Video;
using System.Collections.Generic;
using System.IO;

public class InteractiveVideoEditorWindow : EditorWindow
{
    // Global lists for nodes.
    public List<VideoNode> videoNodes = new List<VideoNode>();
    public List<OptionNode> optionNodes = new List<OptionNode>();

    // Pan offset for the canvas.
    public Vector2 panOffset = Vector2.zero;

    // Variables for node dragging.
    private VideoNode selectedVideoNode = null;
    private OptionNode selectedOptionNode = null;
    private bool isDraggingNode = false;
    private Vector2 dragOffset;

    // Field for pending connection mode.
    private OptionNode pendingConnectionOption = null;

    // Static instance for easy access.
    public static InteractiveVideoEditorWindow Instance;

    [MenuItem("Window/Interactive Video Editor")]
    public static void ShowWindow() {
        Instance = GetWindow<InteractiveVideoEditorWindow>("Interactive Video Editor");
    }

    private void OnEnable() {
        Instance = this;
    }

    private void OnGUI() {
        // Draw grid background.
        DrawGrid(20, 0.2f, Color.gray);

        // Toolbar with Save, Load, and Clear buttons.
        GUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Button("Save Tree", EditorStyles.toolbarButton)) {
            SaveTree();
        }
        if (GUILayout.Button("Load Tree", EditorStyles.toolbarButton)) {
            LoadTree();
        }
        if (GUILayout.Button("Clear Tree", EditorStyles.toolbarButton)) {
            if (EditorUtility.DisplayDialog("Clear Tree", "Are you sure you want to clear the tree?", "Yes", "No")) {
                videoNodes.Clear();
                optionNodes.Clear();
            }
        }
        GUILayout.EndHorizontal();

        // Use a large canvas so that nodes outside the current window are rendered.
        Rect canvasRect = new Rect(panOffset.x, panOffset.y, 5000, 5000);
        GUI.BeginGroup(canvasRect);

        // Draw connection lines behind nodes.
        DrawConnections();

        // Draw all nodes.
        DrawNodes();

        GUI.EndGroup();

        // Process events (panning, dragging, context menus, pending connection, etc.).
        ProcessEvents(Event.current);

        if (GUI.changed)
            Repaint();
    }

    #region Grid Drawing
    private void DrawGrid(float gridSpacing, float gridOpacity, Color gridColor) {
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
    #endregion

    #region Node Drawing & Connections
    void DrawNodes() {
        foreach (var v in videoNodes)
            v.Draw();
        foreach (var o in optionNodes)
            o.Draw();
    }

    void DrawConnections() {
        // Draw vertical lines from each Video Node to its Option Node children.
        foreach (var v in videoNodes) {
            foreach (var o in v.optionChildren) {
                // Connection from bottom center of Video Node to top center of Option Node.
                Vector3 start = new Vector3(v.rect.center.x, v.rect.yMax, 0);
                Vector3 end = new Vector3(o.rect.center.x, o.rect.y, 0);
                Handles.DrawLine(start, end);
            }
        }
        // Draw vertical lines from each Option Node to its child Video Node.
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
        // --- Pending Connection Mode ---
        // If we're waiting for a left-click to connect a Video Node.
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

        // --- Panning the Canvas ---
        if (e.button == 2 && (e.type == EventType.MouseDrag || e.type == EventType.MouseDown)) {
            panOffset += e.delta;
            e.Use();
        }

        // Adjust mouse position for panOffset.
        Vector2 adjustedPos = e.mousePosition - panOffset;

        // --- Context Menus ---
        if (e.type == EventType.ContextClick) {
            VideoNode vNode = GetVideoNodeAtPoint(adjustedPos);
            OptionNode oNode = GetOptionNodeAtPoint(adjustedPos);
            if (vNode != null) {
                ShowVideoNodeContextMenu(adjustedPos, vNode);
            }
            else if (oNode != null) {
                ShowOptionNodeContextMenu(adjustedPos, oNode);
            }
            else {
                ShowGlobalContextMenu(adjustedPos);
            }
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
        // Allow connecting an existing Option Node (with no parent) to this Video Node.
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

        // Only offer pending connection if no child is connected.
        if (oNode.childVideo == null) {
            menu.AddItem(new GUIContent("Connect Existing Video Node"), false, () => {
                pendingConnectionOption = oNode;
                Debug.Log("Pending connection: Click on a Video Node to connect to.");
            });
        }
        menu.ShowAsContext();
    }
    #endregion

    #region Node Creation Methods
    void AddVideoNode(Vector2 pos) {
        VideoNode newVideo = new VideoNode(pos, 220, 140);
        videoNodes.Add(newVideo);
    }

    // Option Node now created with size (250, 150)
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
    #endregion

    #region Deletion Methods
    public void DeleteVideoNode(VideoNode node) {
        // Remove any OptionNode references that use this node.
        foreach (OptionNode o in optionNodes) {
            if (o.childVideo == node)
                o.childVideo = null;
            if (o.parentVideo == node)
                o.parentVideo = null;
        }
        videoNodes.Remove(node);
    }

    public void DeleteOptionNode(OptionNode node) {
        if (node.parentVideo != null) {
            node.parentVideo.optionChildren.Remove(node);
        }
        if (node.childVideo != null) {
            node.childVideo = null;
        }
        optionNodes.Remove(node);
    }
    #endregion

    #region Saving and Loading
    [System.Serializable]
    public class VideoTreeData
    {
        public List<VideoNodeData> videoNodes = new List<VideoNodeData>();
        public List<OptionNodeData> optionNodes = new List<OptionNodeData>();
    }

    [System.Serializable]
    public class VideoNodeData
    {
        public float x, y, width, height;
        public string title;
        public string videoClipPath;
        public float progress;
        public List<int> optionChildIndices = new List<int>();
    }

    [System.Serializable]
    public class OptionNodeData
    {
        public float x, y, width, height;
        public string title;
        public string choicePrompt;
        public int parentVideoIndex;
        public int childVideoIndex;
        public bool isSelected;
    }

    void SaveTree() {
        VideoTreeData treeData = new VideoTreeData();

        for (int i = 0; i < videoNodes.Count; i++) {
            VideoNode v = videoNodes[i];
            VideoNodeData vData = new VideoNodeData();
            vData.x = v.rect.x;
            vData.y = v.rect.y;
            vData.width = v.rect.width;
            vData.height = v.rect.height;
            vData.title = v.title;
            vData.progress = v.progress;
            if (v.videoClip != null)
                vData.videoClipPath = GetRelativePath(v.videoClip);
            else
                vData.videoClipPath = "";
            foreach (OptionNode child in v.optionChildren) {
                int index = optionNodes.IndexOf(child);
                if (index >= 0)
                    vData.optionChildIndices.Add(index);
            }
            treeData.videoNodes.Add(vData);
        }

        for (int i = 0; i < optionNodes.Count; i++) {
            OptionNode o = optionNodes[i];
            OptionNodeData oData = new OptionNodeData();
            oData.x = o.rect.x;
            oData.y = o.rect.y;
            oData.width = o.rect.width;
            oData.height = o.rect.height;
            oData.title = o.title;
            oData.choicePrompt = o.choicePrompt;
            oData.isSelected = o.isSelected;
            oData.parentVideoIndex = videoNodes.IndexOf(o.parentVideo);
            oData.childVideoIndex = o.childVideo != null ? videoNodes.IndexOf(o.childVideo) : -1;
            treeData.optionNodes.Add(oData);
        }

        string json = JsonUtility.ToJson(treeData, true);
        string path = EditorUtility.SaveFilePanel("Save Video Tree", "", "VideoTree.json", "json");
        if (!string.IsNullOrEmpty(path)) {
            File.WriteAllText(path, json);
            EditorUtility.DisplayDialog("Save Tree", "Tree saved successfully!", "OK");
        }
    }

    string GetRelativePath(VideoClip clip) {
        string fullPath = AssetDatabase.GetAssetPath(clip);
        string resourcesPrefix = "Assets/Resources/";
        if (fullPath.StartsWith(resourcesPrefix)) {
            string relativePath = fullPath.Substring(resourcesPrefix.Length);
            int extensionIndex = relativePath.LastIndexOf('.');
            if (extensionIndex >= 0)
                relativePath = relativePath.Substring(0, extensionIndex);
            return relativePath;
        }
        return "";
    }

    void LoadTree() {
        string path = EditorUtility.OpenFilePanel("Load Video Tree", "", "json");
        if (!string.IsNullOrEmpty(path)) {
            string json = File.ReadAllText(path);
            VideoTreeData treeData = JsonUtility.FromJson<VideoTreeData>(json);

            videoNodes.Clear();
            optionNodes.Clear();

            for (int i = 0; i < treeData.videoNodes.Count; i++) {
                VideoNodeData vData = treeData.videoNodes[i];
                VideoNode vNode = new VideoNode(new Vector2(vData.x, vData.y), vData.width, vData.height);
                vNode.title = vData.title;
                vNode.progress = vData.progress;
                if (!string.IsNullOrEmpty(vData.videoClipPath))
                    vNode.videoClip = AssetDatabase.LoadAssetAtPath<VideoClip>("Assets/Resources/" + vData.videoClipPath + ".mp4");
                videoNodes.Add(vNode);
            }

            for (int i = 0; i < treeData.optionNodes.Count; i++) {
                OptionNodeData oData = treeData.optionNodes[i];
                OptionNode oNode = new OptionNode(new Vector2(oData.x, oData.y), oData.width, oData.height);
                oNode.title = oData.title;
                oNode.choicePrompt = oData.choicePrompt;
                oNode.isSelected = oData.isSelected;
                optionNodes.Add(oNode);
            }

            for (int i = 0; i < treeData.optionNodes.Count; i++) {
                OptionNodeData oData = treeData.optionNodes[i];
                OptionNode oNode = optionNodes[i];
                if (oData.parentVideoIndex >= 0 && oData.parentVideoIndex < videoNodes.Count) {
                    oNode.parentVideo = videoNodes[oData.parentVideoIndex];
                    videoNodes[oData.parentVideoIndex].optionChildren.Add(oNode);
                }
                if (oData.childVideoIndex >= 0 && oData.childVideoIndex < videoNodes.Count) {
                    oNode.childVideo = videoNodes[oData.childVideoIndex];
                }
            }

            EditorUtility.DisplayDialog("Load Tree", "Tree loaded successfully!", "OK");
        }
    }
    #endregion
}

/// <summary>
/// Base class for Video and Option nodes.
/// </summary>
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

/// <summary>
/// VideoNode holds a VideoClip, a simulated progress slider, and a list of child OptionNodes.
/// </summary>
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

/// <summary>
/// OptionNode represents a choice. It holds a reference to its parent VideoNode,
/// an optional child VideoNode (the next video), and an editable text prompt for the choice.
/// </summary>
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

        // If no child Video Node is connected, show a button to trigger pending connection.
        if (childVideo == null) {
            Rect connectRect = new Rect(rect.x + 10, rect.y + 65, rect.width - 20, 20);
            if (GUI.Button(connectRect, "Connect Video Node")) {
                //pendingConnectionOption = this;
                Debug.Log("Pending connection: Click on a Video Node to connect to.");
            }
            // Draw the text prompt field below the connect button.
            Rect promptRect = new Rect(rect.x + 10, rect.y + 90, rect.width - 20, 20);
            choicePrompt = EditorGUI.TextField(promptRect, "Prompt", choicePrompt);
        }
        else {
            // If already connected, simply draw the text prompt field at y = 65.
            Rect promptRect = new Rect(rect.x + 10, rect.y + 65, rect.width - 20, 20);
            choicePrompt = EditorGUI.TextField(promptRect, "Prompt", choicePrompt);
        }
    }
}
