using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace _Workspace.Dev.EditorTools
{
    public class UsedAssetCollectorWindow : EditorWindow
    {
        [System.Flags]
        public enum TypeMask
        {
            Model    = 1 << 0,
            Prefab   = 1 << 1,
            Texture  = 1 << 2,
            Material = 1 << 3,
            Audio    = 1 << 4,
            Shader   = 1 << 5,
            Animation= 1 << 6,
            Sprite   = 1 << 7,
            All = ~0
        }

        enum RootsMode { ScenesInBuild, OpenScenes, Selection, CustomPaths }

        [Header("Target")]
        [SerializeField] AssetCollection targetCollection;

        private RootsMode _rootsMode = RootsMode.ScenesInBuild;
        private TypeMask _typeMask = TypeMask.Prefab | TypeMask.Material | TypeMask.Texture | TypeMask.Shader | TypeMask.Model;
        private bool _replaceInsteadOfAppend;
        private bool _excludePackages = true;
        private bool _excludeEditorAssets = true;

        // Restrict dependency results to specific folders (e.g., Assets/OutAssets)
        private bool _restrictDependenciesToFolders;
        private readonly List<string> _dependencyFolders = new() { "Assets/OutAssets" };

        private bool _moveAssetsToFolder;
        private string _targetMoveFolder = "Assets/_Workspace/OHGameAssets";

        private readonly List<string> _customPaths = new() { "Assets" };

        private readonly List<string> _foundPaths = new();
        private readonly List<Object> _previewObjs = new();
        private Vector2 _sv;

        [MenuItem("Tools/Asset Collector (Used Assets)")]
        static void Open() => GetWindow<UsedAssetCollectorWindow>("Used Assets").Show();

        private void OnGUI()
        {
            EditorGUILayout.Space();
            targetCollection = (AssetCollection)EditorGUILayout.ObjectField("Target Collection", targetCollection, typeof(AssetCollection), false);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Roots", EditorStyles.boldLabel);
            _rootsMode = (RootsMode)EditorGUILayout.EnumPopup("Source", _rootsMode);

            if (_rootsMode == RootsMode.CustomPaths)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    int newCount = Mathf.Max(1, EditorGUILayout.IntField("Count", _customPaths.Count));
                    while (_customPaths.Count < newCount) _customPaths.Add("Assets");
                    while (_customPaths.Count > newCount) _customPaths.RemoveAt(_customPaths.Count - 1);

                    for (int i = 0; i < _customPaths.Count; i++)
                    {
                        EditorGUILayout.BeginHorizontal();
                        _customPaths[i] = EditorGUILayout.TextField(_customPaths[i]);
                        if (GUILayout.Button("Pick", GUILayout.Width(60)))
                        {
                            var abs = EditorUtility.OpenFolderPanel("Pick root (under Assets)", "Assets", "");
                            if (!string.IsNullOrEmpty(abs))
                            {
                                var root = System.IO.Path.GetFullPath(Application.dataPath + "/..").Replace("\\", "/");
                                if (abs.StartsWith(root))
                                {
                                    var rel = abs.Substring(root.Length + 1);
                                    if (!rel.StartsWith("Assets"))
                                        rel = "Assets";
                                    _customPaths[i] = rel;
                                }
                            }
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                }
            }
            else if (_rootsMode == RootsMode.Selection)
            {
                EditorGUILayout.HelpBox("Dùng các object đang SELECTED trong Project (Scene/Prefab/Folder/Asset).", MessageType.Info);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Filters", EditorStyles.boldLabel);
            _typeMask = (TypeMask)EditorGUILayout.EnumFlagsField("Types", _typeMask);
            _excludePackages = EditorGUILayout.ToggleLeft("Exclude Packages/*", _excludePackages);
            _excludeEditorAssets = EditorGUILayout.ToggleLeft("Exclude Editor-only assets (Editor/*)", _excludeEditorAssets);
            _replaceInsteadOfAppend = EditorGUILayout.ToggleLeft("Replace collection (instead of Append)", _replaceInsteadOfAppend);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Dependency Scope", EditorStyles.boldLabel);
            _restrictDependenciesToFolders = EditorGUILayout.ToggleLeft("Restrict dependencies to folder(s) below", _restrictDependenciesToFolders);
            if (_restrictDependenciesToFolders)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    int newCount = Mathf.Max(1, EditorGUILayout.IntField("Folder Count", _dependencyFolders.Count));
                    while (_dependencyFolders.Count < newCount) _dependencyFolders.Add("Assets/OutAssets");
                    while (_dependencyFolders.Count > newCount) _dependencyFolders.RemoveAt(_dependencyFolders.Count - 1);

                    for (int i = 0; i < _dependencyFolders.Count; i++)
                    {
                        EditorGUILayout.BeginHorizontal();
                        _dependencyFolders[i] = EditorGUILayout.TextField(_dependencyFolders[i]);
                        if (GUILayout.Button("Pick", GUILayout.Width(60)))
                        {
                            var abs = EditorUtility.OpenFolderPanel("Pick dependency root (under Assets)", "Assets", "");
                            if (!string.IsNullOrEmpty(abs))
                            {
                                var root = System.IO.Path.GetFullPath(Application.dataPath + "/..").Replace("\\", "/");
                                if (abs.StartsWith(root))
                                {
                                    var rel = abs.Substring(root.Length + 1);
                                    if (!rel.StartsWith("Assets")) rel = "Assets";
                                    _dependencyFolders[i] = rel;
                                }
                            }
                        }
                        EditorGUILayout.EndHorizontal();
                    }

                    EditorGUILayout.HelpBox("Only dependencies whose paths are under the specified folder(s) will be kept (e.g., Assets/OutAssets).", MessageType.Info);
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Move Assets", EditorStyles.boldLabel);
            _moveAssetsToFolder = EditorGUILayout.ToggleLeft("Move assets to folder (instead of reference only)", _moveAssetsToFolder);
            
            if (_moveAssetsToFolder)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.BeginHorizontal();
                    _targetMoveFolder = EditorGUILayout.TextField("Target Folder", _targetMoveFolder);
                    if (GUILayout.Button("Pick", GUILayout.Width(60)))
                    {
                        var abs = EditorUtility.OpenFolderPanel("Pick target folder", "Assets", "");
                        if (!string.IsNullOrEmpty(abs))
                        {
                            var root = System.IO.Path.GetFullPath(Application.dataPath + "/..").Replace("\\", "/");
                            if (abs.StartsWith(root))
                            {
                                var rel = abs.Substring(root.Length + 1);
                                if (!rel.StartsWith("Assets"))
                                    rel = "Assets";
                                _targetMoveFolder = rel;
                            }
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.HelpBox("⚠️ Assets sẽ được MOVE (không phải copy) sang folder này. References trong scene sẽ được Unity tự động update.", MessageType.Warning);
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Scan Used Assets")) Scan();
            EditorGUI.BeginDisabledGroup(_previewObjs.Count == 0 || !targetCollection);
            if (GUILayout.Button("Apply to Collection")) Apply();
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Found: {_previewObjs.Count} assets", EditorStyles.boldLabel);
            _sv = EditorGUILayout.BeginScrollView(_sv);
            foreach (var o in _previewObjs.Take(200))
                EditorGUILayout.ObjectField(o, typeof(Object), false);
            if (_previewObjs.Count > 200)
                EditorGUILayout.HelpBox($"Showing 200 / {_previewObjs.Count}", MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        void Scan()
        {
            _foundPaths.Clear();
            _previewObjs.Clear();

            var roots = GatherRootsPaths();
            if (roots.Count == 0)
            {
                EditorUtility.DisplayDialog("Used Assets", "Không có roots nào để quét.", "OK");
                return;
            }

            var allDeps = AssetDatabase.GetDependencies(roots.ToArray(), true)
                .Where(p => p.StartsWith("Assets/"))
                .ToList();

            if (_excludePackages)
                allDeps = allDeps.Where(p => !p.StartsWith("Packages/")).ToList();

            if (_excludeEditorAssets)
                allDeps = allDeps.Where(p => !p.Contains("/Editor/")).ToList();

            if (_restrictDependenciesToFolders)
            {
                allDeps = allDeps.Where(p => IsUnderAny(p, _dependencyFolders)).ToList();
            }

            var filtered = new List<string>();
            foreach (var path in allDeps)
            {
                var obj = AssetDatabase.LoadAssetAtPath<Object>(path);
                if (!obj) continue;
                if (!PassTypeFilter(obj)) continue;
                filtered.Add(path);
                _previewObjs.Add(obj);
            }

            _foundPaths.AddRange(filtered.Distinct().OrderBy(p => p));
            var dedupSorted = _previewObjs.Distinct().OrderBy(x => x.name).ToList();
            _previewObjs.Clear();
            _previewObjs.AddRange(dedupSorted);

            Repaint();
            Debug.Log($"[UsedAssets] Roots: {roots.Count}, Deps: {allDeps.Count}, Kept: {_foundPaths.Count}");
        }

        List<string> GatherRootsPaths()
        {
            var list = new List<string>();

            switch (_rootsMode)
            {
                case RootsMode.ScenesInBuild:
                {
                    foreach (var s in EditorBuildSettings.scenes)
                        if (s.enabled)
                            list.Add(s.path);
                    break;
                }
                case RootsMode.OpenScenes:
                {
                    for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                    {
                        var scn = EditorSceneManager.GetSceneAt(i);
                        if (scn.IsValid() && scn.path.StartsWith("Assets/"))
                            list.Add(scn.path);
                    }
                    break;
                }
                case RootsMode.Selection:
                {
                    foreach (var o in Selection.objects)
                    {
                        string p = AssetDatabase.GetAssetPath(o);
                        if (string.IsNullOrEmpty(p)) continue;

                        if (AssetDatabase.IsValidFolder(p))
                        {
                            var sub = AssetDatabase.FindAssets("", new[] { p })
                                                   .Select(g => AssetDatabase.GUIDToAssetPath(g));
                            list.AddRange(sub);
                        }
                        else list.Add(p);
                    }
                    break;
                }
                case RootsMode.CustomPaths:
                {
                    foreach (var p in _customPaths)
                    {
                        if (string.IsNullOrEmpty(p)) continue;
                        if (AssetDatabase.IsValidFolder(p))
                        {
                            var sub = AssetDatabase.FindAssets("", new[] { p })
                                                   .Select(g => AssetDatabase.GUIDToAssetPath(g));
                            list.AddRange(sub);
                        }
                        else list.Add(p);
                    }
                    break;
                }
            }

            return list.Where(p => p.StartsWith("Assets/")).Distinct().ToList();
        }

        bool PassTypeFilter(Object obj)
        {
            if (_typeMask == TypeMask.All) return true;

            var objType = obj.GetType();
            string path = AssetDatabase.GetAssetPath(obj);

            if ((_typeMask & TypeMask.Model) != 0)
            {
                var importer = AssetImporter.GetAtPath(path);
                if (importer is ModelImporter)
                    return true;
            }

            if ((_typeMask & TypeMask.Prefab) != 0)
            {
                if (obj is GameObject)
                {
                    var importer = AssetImporter.GetAtPath(path);
                    if (importer != null && !(importer is ModelImporter))
                        return true;
                }
            }

            if ((_typeMask & TypeMask.Texture) != 0)
            {
                if (obj is Texture2D texture)
                {
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer != null)
                    {
                        if (importer.textureType == TextureImporterType.Sprite)
                        {
                            if ((_typeMask & TypeMask.Sprite) != 0)
                                return true;
                        }
                        else
                        {
                            return true;
                        }
                    }
                }
            }

            if ((_typeMask & TypeMask.Sprite) != 0)
            {
                if (obj is Sprite)
                    return true;
                if (obj is Texture2D)
                {
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer?.textureType == TextureImporterType.Sprite)
                        return true;
                }
            }

            if ((_typeMask & TypeMask.Material) != 0 && obj is Material)
                return true;

            if ((_typeMask & TypeMask.Audio) != 0 && obj is AudioClip)
                return true;

            if ((_typeMask & TypeMask.Shader) != 0 && obj is Shader)
                return true;

            if ((_typeMask & TypeMask.Animation) != 0)
            {
                if (obj is AnimationClip || obj is RuntimeAnimatorController)
                    return true;
            }

            return false;
        }

        void Apply()
        {
            if (!targetCollection) return;

            var movedPaths = new List<string>();
            if (_moveAssetsToFolder)
            {
                if (!AssetDatabase.IsValidFolder(_targetMoveFolder))
                {
                    if (!EditorUtility.DisplayDialog("Create Folder?", 
                        $"Folder '{_targetMoveFolder}' không tồn tại. Tạo mới?", "Yes", "Cancel"))
                        return;
                    
                    CreateFolderRecursive(_targetMoveFolder);
                }

                EditorUtility.DisplayProgressBar("Moving Assets", "Moving assets...", 0f);
                
                try
                {
                    for (int i = 0; i < _foundPaths.Count; i++)
                    {
                        var oldPath = _foundPaths[i];
                        var fileName = System.IO.Path.GetFileName(oldPath);
                        var newPath = $"{_targetMoveFolder}/{fileName}";

                        int counter = 1;
                        while (AssetDatabase.LoadAssetAtPath<Object>(newPath) != null)
                        {
                            var nameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(oldPath);
                            var ext = System.IO.Path.GetExtension(oldPath);
                            newPath = $"{_targetMoveFolder}/{nameWithoutExt}_{counter}{ext}";
                            counter++;
                        }

                        var error = AssetDatabase.MoveAsset(oldPath, newPath);
                        if (string.IsNullOrEmpty(error))
                        {
                            movedPaths.Add(newPath);
                            Debug.Log($"Moved: {oldPath} → {newPath}");
                        }
                        else
                        {
                            Debug.LogError($"Failed to move {oldPath}: {error}");
                            movedPaths.Add(oldPath);
                        }

                        EditorUtility.DisplayProgressBar("Moving Assets", $"Moving {i+1}/{_foundPaths.Count}", (float)(i+1)/_foundPaths.Count);
                    }
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }
            }
            else
            {
                movedPaths.AddRange(_foundPaths);
            }

            Undo.RecordObject(targetCollection, "Apply Used Assets");

            if (_replaceInsteadOfAppend)
                targetCollection.items.Clear();

            var set = new HashSet<Object>(targetCollection.items.Where(x => x));

            foreach (var p in movedPaths)
            {
                var o = AssetDatabase.LoadAssetAtPath<Object>(p);
                if (o) set.Add(o);
            }

            targetCollection.items = set.Where(x => x).ToList();
            EditorUtility.SetDirty(targetCollection);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(targetCollection);

            Debug.Log($"[UsedAssets] Applied to collection. Total: {targetCollection.items.Count}");
        }

        void CreateFolderRecursive(string path)
        {
            path = path.TrimEnd('/');
            if (AssetDatabase.IsValidFolder(path)) return;

            var parent = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
            var folderName = System.IO.Path.GetFileName(path);

            CreateFolderRecursive(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }
        
        bool IsUnderAny(string assetPath, List<string> roots)
        {
            if (roots == null || roots.Count == 0) return true; // no roots = allow all
            assetPath = assetPath.Replace("\\", "/");
            foreach (var r in roots)
            {
                if (string.IsNullOrEmpty(r)) continue;
                var norm = r.Replace("\\", "/").TrimEnd('/');
                if (assetPath.Equals(norm) || assetPath.StartsWith(norm + "/"))
                    return true;
            }
            return false;
        }
    }
}