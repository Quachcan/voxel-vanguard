using System.Collections.Generic;
using UnityEngine;

namespace _Workspace.Dev.EditorTools
{
    [CreateAssetMenu(menuName = "Tools/Asset Collection", fileName = "AssetCollection")]
    public class AssetCollection : ScriptableObject
    {
        public List<Object> items = new List<Object>();
    }
}