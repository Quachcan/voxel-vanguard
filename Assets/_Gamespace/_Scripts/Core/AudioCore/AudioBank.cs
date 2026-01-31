using System.Collections.Generic;
using _Workspace._Scripts.Core.Audio;
using UnityEngine;

namespace _Workspace._Scripts.Core.AudioCore
{
    [CreateAssetMenu(menuName = "Core/Audio/AudioBank", fileName = "AudioBank")]
    
    public class AudioBank : ScriptableObject
    {
        [SerializeField] private AudioCueEntry[] audioCues;
        
        private Dictionary<string, AudioCueEntry> _map;

        private void OnEnable()
        {
            BuildMap();
        }
        
        private void BuildMap()
        {
            _map = new Dictionary<string, AudioCueEntry>();
            foreach (var e in audioCues)
            {
                if (string.IsNullOrEmpty(e.key)) continue;
                if (_map.ContainsKey(e.key)) Debug.LogWarning($"Duplicate audio key: {e.key}");
                _map[e.key] = e;
            }
        }
        
        public AudioCue GetClip(string key)
        {
           if(_map == null) BuildMap();
           return _map != null && _map.TryGetValue(key, out var e) ? e.GetRandomClip() : null;
        }
    }
}