using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace _Workspace.Dev.EditorTools
{
    [CustomEditor(typeof(AssetCollection))]
    public class AssetCollectionInspector : Editor
    {
        private Vector2 scrollPos;
        private string searchFilter = "";
        private bool showPreview = true;
        private bool showStats = true;
        private int previewSize = 64;

        public override void OnInspectorGUI()
        {
            var col = (AssetCollection)target;

            // Header
            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Asset Collection - {col.items.Count} items", EditorStyles.boldLabel);
            
            // Stats Section
            if (showStats = EditorGUILayout.Foldout(showStats, "Statistics", true))
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    DrawStatistics(col);
                }
            }

            EditorGUILayout.Space();
            
            // Quick Actions
            EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🗑️ Remove Missing", GUILayout.Height(25)))
            {
                RemoveMissing(col);
            }
            if (GUILayout.Button("🔄 Remove Duplicates", GUILayout.Height(25)))
            {
                RemoveDuplicates(col);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("📝 Sort by Name", GUILayout.Height(25)))
            {
                SortByName(col);
            }
            if (GUILayout.Button("📁 Sort by Type", GUILayout.Height(25)))
            {
                SortByType(col);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🔀 Shuffle", GUILayout.Height(25)))
            {
                Shuffle(col);
            }
            if (GUILayout.Button("🔄 Reverse", GUILayout.Height(25)))
            {
                Reverse(col);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // Batch Operations
            EditorGUILayout.LabelField("Batch Operations", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("📋 Copy Paths", GUILayout.Height(25)))
            {
                CopyPathsToClipboard(col);
            }
            if (GUILayout.Button("📊 Export to CSV", GUILayout.Height(25)))
            {
                ExportToCSV(col);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🔍 Select All in Project", GUILayout.Height(25)))
            {
                SelectAllInProject(col);
            }
            if (GUILayout.Button("🏷️ Add Label...", GUILayout.Height(25)))
            {
                AddLabelToAll(col);
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("⚠️ Clear All", GUILayout.Height(25)))
            {
                if (EditorUtility.DisplayDialog("Clear Collection", 
                    $"Remove all {col.items.Count} items?", "Yes", "Cancel"))
                {
                    ClearAll(col);
                }
            }

            EditorGUILayout.Space();

            // Search & Filter
            EditorGUILayout.LabelField("Search & Preview", EditorStyles.boldLabel);
            searchFilter = EditorGUILayout.TextField("Search", searchFilter);
            
            EditorGUILayout.BeginHorizontal();
            showPreview = EditorGUILayout.Toggle("Show Preview", showPreview);
            if (showPreview)
            {
                previewSize = EditorGUILayout.IntSlider("Size", previewSize, 32, 128);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            
            DrawDefaultInspector();
            
            if (showPreview && col.items.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Preview Grid", EditorStyles.boldLabel);
                DrawPreviewGrid(col);
            }
        }

        void DrawStatistics(AssetCollection col)
        {
            var items = col.items.Where(x => x != null).ToList();
            int missing = col.items.Count - items.Count;

            EditorGUILayout.LabelField($"Total: {col.items.Count}");
            EditorGUILayout.LabelField($"Valid: {items.Count}");
            
            if (missing > 0)
            {
                var oldColor = GUI.color;
                GUI.color = Color.red;
                EditorGUILayout.LabelField($"Missing: {missing}");
                GUI.color = oldColor;
            }
            
            var typeGroups = items.GroupBy(x => x.GetType().Name)
                                  .OrderByDescending(g => g.Count());
            
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("By Type:", EditorStyles.miniBoldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                foreach (var group in typeGroups.Take(5))
                {
                    EditorGUILayout.LabelField($"{group.Key}: {group.Count()}");
                }
            }
        }

        void DrawPreviewGrid(AssetCollection cols)
        {
            var items = cols.items.Where(x => x != null).ToList();
            
            if (!string.IsNullOrEmpty(searchFilter))
            {
                items = items.Where(x => x.name.ToLower().Contains(searchFilter.ToLower())).ToList();
            }

            if (items.Count == 0)
            {
                EditorGUILayout.HelpBox("No items to preview", MessageType.Info);
                return;
            }

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.MaxHeight(300));
            
            int columns = Mathf.Max(1, (int)(EditorGUIUtility.currentViewWidth - 40) / (previewSize + 10));
            int rows = Mathf.CeilToInt((float)items.Count / columns);

            for (int row = 0; row < rows; row++)
            {
                EditorGUILayout.BeginHorizontal();
                
                for (int col = 0; col < columns; col++)
                {
                    int index = row * columns + col;
                    if (index >= items.Count) break;

                    var item = items[index];
                    
                    EditorGUILayout.BeginVertical(GUILayout.Width(previewSize));
                    
                    // Preview icon
                    var preview = AssetPreview.GetAssetPreview(item);
                    if (preview)
                    {
                        if (GUILayout.Button(preview, GUILayout.Width(previewSize), GUILayout.Height(previewSize)))
                        {
                            Selection.activeObject = item;
                            EditorGUIUtility.PingObject(item);
                        }
                    }
                    else
                    {
                        if (GUILayout.Button(item.name, GUILayout.Width(previewSize), GUILayout.Height(previewSize)))
                        {
                            Selection.activeObject = item;
                            EditorGUIUtility.PingObject(item);
                        }
                    }
                    
                    // Name label
                    var style = new GUIStyle(EditorStyles.miniLabel);
                    style.alignment = TextAnchor.MiddleCenter;
                    style.wordWrap = true;
                    EditorGUILayout.LabelField(item.name, style, GUILayout.Width(previewSize), GUILayout.Height(30));
                    
                    EditorGUILayout.EndVertical();
                }
                
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        void RemoveMissing(AssetCollection col)
        {
            Undo.RecordObject(col, "Remove Missing");
            int before = col.items.Count;
            col.items = col.items.Where(x => x != null).ToList();
            int removed = before - col.items.Count;
            EditorUtility.SetDirty(col);
            AssetDatabase.SaveAssets();
            Debug.Log($"Removed {removed} missing references");
        }

        void RemoveDuplicates(AssetCollection col)
        {
            Undo.RecordObject(col, "Remove Duplicates");
            int before = col.items.Count;
            col.items = col.items.Where(x => x != null).Distinct().ToList();
            int removed = before - col.items.Count;
            EditorUtility.SetDirty(col);
            AssetDatabase.SaveAssets();
            Debug.Log($"Removed {removed} duplicates");
        }

        void SortByName(AssetCollection col)
        {
            Undo.RecordObject(col, "Sort by Name");
            col.items = col.items.Where(x => x != null)
                .OrderBy(x => x.name)
                .ToList();
            EditorUtility.SetDirty(col);
            AssetDatabase.SaveAssets();
            Debug.Log("Sorted by name");
        }

        void SortByType(AssetCollection col)
        {
            Undo.RecordObject(col, "Sort by Type");
            col.items = col.items.Where(x => x != null)
                .OrderBy(x => x.GetType().Name)
                .ThenBy(x => x.name)
                .ToList();
            EditorUtility.SetDirty(col);
            AssetDatabase.SaveAssets();
            Debug.Log("Sorted by type");
        }

        void Shuffle(AssetCollection col)
        {
            Undo.RecordObject(col, "Shuffle");
            var rnd = new System.Random();
            col.items = col.items.OrderBy(x => rnd.Next()).ToList();
            EditorUtility.SetDirty(col);
            AssetDatabase.SaveAssets();
            Debug.Log("Shuffled collection");
        }

        void Reverse(AssetCollection col)
        {
            Undo.RecordObject(col, "Reverse");
            col.items.Reverse();
            EditorUtility.SetDirty(col);
            AssetDatabase.SaveAssets();
            Debug.Log("Reversed collection");
        }

        void ClearAll(AssetCollection col)
        {
            Undo.RecordObject(col, "Clear All");
            col.items.Clear();
            EditorUtility.SetDirty(col);
            AssetDatabase.SaveAssets();
            Debug.Log("Cleared collection");
        }

        void CopyPathsToClipboard(AssetCollection col)
        {
            var paths = col.items
                .Where(x => x != null)
                .Select(x => AssetDatabase.GetAssetPath(x))
                .ToList();
            
            string text = string.Join("\n", paths);
            EditorGUIUtility.systemCopyBuffer = text;
            Debug.Log($"Copied {paths.Count} paths to clipboard");
        }

        void ExportToCSV(AssetCollection col)
        {
            string path = EditorUtility.SaveFilePanel("Export CSV", "", col.name + ".csv", "csv");
            if (string.IsNullOrEmpty(path)) return;

            var lines = new List<string> { "Name,Type,Path,Size" };
            
            foreach (var item in col.items.Where(x => x != null))
            {
                string assetPath = AssetDatabase.GetAssetPath(item);
                string fullPath = System.IO.Path.GetFullPath(assetPath);
                long size = System.IO.File.Exists(fullPath) ? new System.IO.FileInfo(fullPath).Length : 0;
                
                lines.Add($"{item.name},{item.GetType().Name},{assetPath},{size}");
            }

            System.IO.File.WriteAllLines(path, lines);
            Debug.Log($"Exported to {path}");
            EditorUtility.RevealInFinder(path);
        }

        void SelectAllInProject(AssetCollection col)
        {
            Selection.objects = col.items.Where(x => x != null).ToArray();
            Debug.Log($"Selected {Selection.objects.Length} objects");
        }

        void AddLabelToAll(AssetCollection col)
        {
            string label = EditorInputDialog.Show("Add Label", "Enter label name:", "");
            if (string.IsNullOrEmpty(label)) return;

            foreach (var item in col.items.Where(x => x != null))
            {
                string path = AssetDatabase.GetAssetPath(item);
                var labels = AssetDatabase.GetLabels(item).ToList();
                if (!labels.Contains(label))
                {
                    labels.Add(label);
                    AssetDatabase.SetLabels(item, labels.ToArray());
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Added label '{label}' to all items");
        }
    }

    // Helper class for input dialog
    public class EditorInputDialog : EditorWindow
    {
        private string description;
        private string inputText;
        private string defaultText;
        private System.Action<string> onComplete;

        public static string Show(string title, string description, string defaultText)
        {
            var window = CreateInstance<EditorInputDialog>();
            window.titleContent = new GUIContent(title);
            window.description = description;
            window.inputText = defaultText;
            window.defaultText = defaultText;
            window.minSize = new Vector2(300, 100);
            window.maxSize = new Vector2(300, 100);
            window.ShowModal();
            return window.inputText;
        }

        void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(description);
            inputText = EditorGUILayout.TextField(inputText);
            
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("OK"))
            {
                Close();
            }
            if (GUILayout.Button("Cancel"))
            {
                inputText = defaultText;
                Close();
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}