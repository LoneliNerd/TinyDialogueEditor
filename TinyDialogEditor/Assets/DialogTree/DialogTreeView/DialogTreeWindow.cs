using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Callbacks;
using UnityEditor.IMGUI.Controls;
using UnityEngine;


namespace UnityEditor.TreeViewExamples
{
    class DialogTreeWindow : EditorWindow
    {
        [NonSerialized] bool m_Initialized;
        [SerializeField] TreeViewState m_TreeViewState; // Serialized in the window layout file so it survives assembly reloading
        [SerializeField] MultiColumnHeaderState m_MultiColumnHeaderState;
        SearchField m_SearchField;
        DialogTreeView m_TreeView;
        DialogTreeAsset m_TreeAsset;

        [MenuItem("DialogEditor/DialogTreeView")]
        public static DialogTreeWindow GetWindow()
        {
            var window = GetWindow<DialogTreeWindow>();
            window.titleContent = new GUIContent("DialogTreeView");
            window.Focus();
            window.Repaint();
            return window;
        }

        [OnOpenAsset]
        public static bool OnOpenAsset(int instanceID, int line)
        {
            var myTreeAsset = EditorUtility.InstanceIDToObject(instanceID) as DialogTreeAsset;
            if (myTreeAsset != null)
            {
                var window = GetWindow();
                window.SetTreeAsset(myTreeAsset);
                return true;
            }
            return false; // we did not handle the open
        }

        void SetTreeAsset(DialogTreeAsset myTreeAsset)
        {
            m_TreeAsset = myTreeAsset;
            EditorUtility.SetDirty(m_TreeAsset);
            m_Initialized = false;
        }

        Rect multiColumnTreeViewRect
        {
            get { return new Rect(20, 30, position.width - 40, position.height - 60); }
        }

        Rect toolbarRect
        {
            get { return new Rect(20f, 10f, position.width - 40f, 20f); }
        }

        Rect bottomToolbarRect
        {
            get { return new Rect(20f, position.height - 18f, position.width - 40f, 16f); }
        }

        public DialogTreeView treeView
        {
            get { return m_TreeView; }
        }

        void InitIfNeeded()
        {
            if (!m_Initialized)
            {
                // Check if it already exists (deserialized from window layout file or scriptable object)
                if (m_TreeViewState == null)
                    m_TreeViewState = new TreeViewState();

                bool firstInit = m_MultiColumnHeaderState == null;
                var headerState = DialogTreeView.CreateDefaultMultiColumnHeaderState(multiColumnTreeViewRect.width);
                if (MultiColumnHeaderState.CanOverwriteSerializedFields(m_MultiColumnHeaderState, headerState))
                    MultiColumnHeaderState.OverwriteSerializedFields(m_MultiColumnHeaderState, headerState);
                m_MultiColumnHeaderState = headerState;

                MultiColumnHeader multiColumnHeader = new MultiColumnHeader(headerState);
                if (firstInit)
                    multiColumnHeader.ResizeToFit();

                var treeModel = new TreeModel<DialogTreeElement>(GetData());

                m_TreeView = new DialogTreeView(m_TreeViewState, multiColumnHeader, treeModel);

                m_SearchField = new SearchField();
                m_SearchField.downOrUpArrowKeyPressed += m_TreeView.SetFocusAndEnsureSelectedItem;

                m_Initialized = true;
            }
        }

        IList<DialogTreeElement> GetData()
        {
            if (m_TreeAsset != null && m_TreeAsset.treeElements != null && m_TreeAsset.treeElements.Count > 0)
                return m_TreeAsset.treeElements;
            return null;
        }

        void OnSelectionChange()
        {
            if (!m_Initialized)
                return;

            var myTreeAsset = Selection.activeObject as DialogTreeAsset;
            if (myTreeAsset != null && myTreeAsset != m_TreeAsset)
            {
                m_TreeAsset = myTreeAsset;
                m_TreeView.treeModel.SetData(GetData());
                m_TreeView.Reload();
            }
        }

        void OnGUI()
        {
            InitIfNeeded();

            SearchBar(toolbarRect);
            DoTreeView(multiColumnTreeViewRect);
            BottomToolBar(bottomToolbarRect);
        }

        void SearchBar(Rect rect)
        {
            treeView.searchString = m_SearchField.OnGUI(rect, treeView.searchString);
        }

        void DoTreeView(Rect rect)
        {
            m_TreeView.OnGUI(rect);
        }

        void BottomToolBar(Rect rect)
        {
            GUILayout.BeginArea(rect);

            using (new EditorGUILayout.HorizontalScope())
            {

                var style = "miniButton";
                if (GUILayout.Button("Expand All", style))
                {
                    treeView.ExpandAll();
                }

                if (GUILayout.Button("Collapse All", style))
                {
                    treeView.CollapseAll();
                }

                GUILayout.FlexibleSpace();

                GUILayout.Label(m_TreeAsset != null ? AssetDatabase.GetAssetPath(m_TreeAsset) : string.Empty);

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Add Item", style))
                {
                    Undo.RecordObject(m_TreeAsset, "Add Item To Asset");

                    // Add item as child of selection
                    var selection = m_TreeView.GetSelection();
                    TreeElement parent = (selection.Count == 1 ? m_TreeView.treeModel.Find(selection[0]) : null) ?? m_TreeView.treeModel.root;
                    int depth = parent != null ? parent.depth + 1 : 0;
                    int id = m_TreeView.treeModel.GenerateUniqueID();
                    DialogTreeElement element = new DialogTreeElement(string.Format("Item{0}", id), depth, id);
                    m_TreeView.treeModel.AddElement(element, parent, 0);
                    DialogNodeGraph dialogNodeGraph = CreateInstance<DialogNodeGraph>();
                    AssetDatabase.CreateAsset(dialogNodeGraph, string.Format("Assets/{0}.asset", id));
                    AssetDatabase.Refresh();
                    element.DialogNodeGraph = dialogNodeGraph;
                    // Select newly created element
                    m_TreeView.SetSelection(new[] { id }, TreeViewSelectionOptions.RevealAndFrame);
                }

                if (GUILayout.Button("Remove Item", style))
                {
                    Undo.RecordObject(m_TreeAsset, "Remove Item From Asset");
                    IList<int> elementIDs = m_TreeView.GetSelection();
                    IList<DialogTreeElement> elements = m_TreeView.treeModel.GetData().Where(element => elementIDs.Contains(element.id)).ToArray();
                    foreach (DialogTreeElement item in elements)
                    {
                        DeleAsset(item);
                    }
                    m_TreeView.treeModel.RemoveElements(elementIDs);
                    AssetDatabase.Refresh();
                }
            }
            GUILayout.EndArea();
        }

        private void DeleAsset(DialogTreeElement element)
        {
            if (element.hasChildren)
            {
                foreach (DialogTreeElement item in element.children)
                {
                    DeleAsset(item);
                }
            }
            string path = AssetDatabase.GetAssetPath(element.DialogNodeGraph);
            AssetDatabase.DeleteAsset(path);
        }
    }
}
