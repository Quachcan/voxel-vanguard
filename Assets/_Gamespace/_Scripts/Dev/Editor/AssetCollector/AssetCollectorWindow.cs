using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace _Workspace.Dev.EditorTools
{
    public class AssetCollectorWindow : EditorWindow
    {
        [System.Flags]
        public enum TypeMask
        {
            Model    = 1 << 0,   // FBX/ModelImporter => t:Model
            Prefab   = 1 << 1,   // t:Prefab
            Texture  = 1 << 2,   // t:Texture2D
            Material = 1 << 3,   // t:Material
            Audio    = 1 << 4,   // t:AudioClip
            Shader   = 1 << 5,   // t:Shader
            Other    = 1 << 20,  // không lọc theo type (chỉ dùng từ khoá)
            AllCommon = Model | Prefab | Texture | Material | Audio | Shader
        }

        private AssetCollection targetCollection;

        private TypeMask typeMask = TypeMask.Model | TypeMask.Prefab;
        private string[] searchFolders = new[] {"Assets"};
        private string labelFilter = "";       // lọc theo label (Unity labels)
        private string nameContains = "";      // lọc theo từ khoá trong tên
        private bool includeSubFolders = true;
        private bool replaceInsteadOfAppend = false;

        private Vector2 _scroll;
        private readonly List<string> _foundGuids = new();
        private readonly List<Object> _preview = new();

        [MenuItem("Tools/Asset Collector")]
        public static void Open()
        {
            GetWindow<AssetCollectorWindow>("Asset Collector").Show();
        }

        void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Target Collection", EditorStyles.boldLabel);
            targetCollection = (AssetCollection)EditorGUILayout.ObjectField("AssetCollection",
                targetCollection, typeof(AssetCollection), false);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Search Filters", EditorStyles.boldLabel);

            typeMask = (TypeMask)EditorGUILayout.EnumFlagsField("Types", typeMask);

            includeSubFolders = EditorGUILayout.Toggle("Include Sub-Folders", includeSubFolders);

            EditorGUILayout.LabelField("Search Folders");
            using (new EditorGUI.IndentLevelScope())
            {
                int newCount = Mathf.Max(1, EditorGUILayout.IntField("Count", searchFolders.Length));
                System.Array.Resize(ref searchFolders, newCount);
                for (int i = 0; i < searchFolders.Length; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    searchFolders[i] = EditorGUILayout.TextField(searchFolders[i]);
                    if (GUILayout.Button("Pick", GUILayout.Width(50)))
                    {
                        string picked = EditorUtility.OpenFolderPanel("Pick folder under Assets", "Assets", "");
                        if (!string.IsNullOrEmpty(picked))
                        {
                            // Convert absolute to relative under Assets
                            var projPath = System.IO.Path.GetFullPath(Application.dataPath + "/..").Replace("\\","/");
                            if (picked.StartsWith(projPath))
                            {
                                var rel = picked.Substring(projPath.Length+1);
                                if (!rel.StartsWith("Assets")) rel = "Assets";
                                searchFolders[i] = rel;
                            }
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }

            labelFilter   = EditorGUILayout.TextField(new GUIContent("Label Filter", "Unity labels (optional)"), labelFilter);
            nameContains  = EditorGUILayout.TextField(new GUIContent("Name Contains", "substring in asset name"), nameContains);

            EditorGUILayout.Space();
            replaceInsteadOfAppend = EditorGUILayout.ToggleLeft("Replace collection (instead of Append)", replaceInsteadOfAppend);

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Scan"))
            {
                Scan();
            }
            EditorGUI.BeginDisabledGroup(_preview.Count == 0 || targetCollection == null);
            if (GUILayout.Button("Apply to Collection"))
            {
                Apply();
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Found: {_preview.Count} assets", EditorStyles.boldLabel);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var obj in _preview.Take(200)) // giới hạn cho nhẹ Inspector
            {
                EditorGUILayout.ObjectField(obj, typeof(Object), false);
            }
            if (_preview.Count > 200) EditorGUILayout.HelpBox("Showing 200 / " + _preview.Count, MessageType.Info);
            EditorGUILayout.EndScrollView();
        }

        void Scan()
        {
            _foundGuids.Clear();
            _preview.Clear();

            var folders = searchFolders
                .Where(f => !string.IsNullOrEmpty(f))
                .Select(f => includeSubFolders ? f : f.TrimEnd('/') )
                .ToArray();

            if (folders.Length == 0) folders = new[] { "Assets" };

            // Tạo filter string cho AssetDatabase.FindAssets
            List<string> typeFilters = new();

            if ((typeMask & TypeMask.Model) != 0)    typeFilters.Add("t:Model");
            if ((typeMask & TypeMask.Prefab) != 0)   typeFilters.Add("t:Prefab");
            if ((typeMask & TypeMask.Texture) != 0)  typeFilters.Add("t:Texture2D");
            if ((typeMask & TypeMask.Material) != 0) typeFilters.Add("t:Material");
            if ((typeMask & TypeMask.Audio) != 0)    typeFilters.Add("t:AudioClip");
            if ((typeMask & TypeMask.Shader) != 0)   typeFilters.Add("t:Shader");

            // Build query - chỉ thêm type filter nếu có
            List<string> queryParts = new();
            
            if (typeFilters.Count > 0)
            {
                // Nối các type filter bằng khoảng trắng = OR logic
                queryParts.Add(string.Join(" ", typeFilters));
            }
            
            if (!string.IsNullOrEmpty(labelFilter))
            {
                queryParts.Add($"l:{labelFilter}");
            }
            
            if (!string.IsNullOrEmpty(nameContains))
            {
                queryParts.Add(nameContains);
            }

            // Nếu không có filter gì cả và không chọn Other, return luôn
            if (queryParts.Count == 0 && (typeMask & TypeMask.Other) == 0)
            {
                Debug.LogWarning("No filter selected. Please select at least one type or use Other with name/label filter.");
                Repaint();
                return;
            }

            string query = string.Join(" ", queryParts);

            var guids = AssetDatabase.FindAssets(query, folders);
            foreach (var g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                var obj = AssetDatabase.LoadAssetAtPath<Object>(path);
                if (!obj) continue;

                // Filter thêm theo name nếu có
                if (!string.IsNullOrEmpty(nameContains) &&
                    !obj.name.ToLowerInvariant().Contains(nameContains.ToLowerInvariant()))
                    continue;

                _foundGuids.Add(g);
                _preview.Add(obj);
            }

            var distinctList = _preview.Distinct().ToList();
            _preview.Clear();
            _preview.AddRange(distinctList);

            Repaint();
        }

        void Apply()
        {
            if (!targetCollection) return;

            Undo.RecordObject(targetCollection, "Asset Collector Apply");

            if (replaceInsteadOfAppend)
                targetCollection.items.Clear();

            // append + dedup
            var set = new HashSet<Object>(targetCollection.items.Where(x => x));
            foreach (var o in _preview)
                set.Add(o);

            targetCollection.items = set.Where(x => x).ToList();

            EditorUtility.SetDirty(targetCollection);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(targetCollection);
            Debug.Log($"[AssetCollector] Collection now has {targetCollection.items.Count} items.");
        }
    }
}