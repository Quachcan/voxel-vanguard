using UnityEngine;

namespace _Workspace._Scripts.Core.Audio
{
    [System.Serializable]
    public class AudioCueEntry
    {
        public string key;
        public AudioCue[] cues;
    
        public AudioCue GetRandomClip()
        {
            if(cues == null || cues.Length == 0) return null;
            return cues.Length == 1 ? cues[0] : cues[Random.Range(0, cues.Length)];
        }
    }
}