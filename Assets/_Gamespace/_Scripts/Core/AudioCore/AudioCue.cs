using UnityEngine;
using UnityEngine.Audio;

namespace _Workspace._Scripts.Core.Audio
{
    [CreateAssetMenu(fileName = "AudioCue_New", menuName = "Core/Audio/AudioCue")]
    public class AudioCue : ScriptableObject
    {
        [Header("Clips")]
        public AudioClip[] clips;
        
        [Header("Mixer & Modes")]
        public AudioMixerGroup mixerGroups;
        public bool loop;
        public bool spatial;
        
        [Header("Levels")]
        [Range(0,1)] public float volume = 1f;
        [Min(0)] public float cooldown = 1f;
        [Range(0.5f, 2f)] public float pitchMin = 1f;
        [Range(0.5f, 2f)] public float pitchMax = 1f;

        public AudioClip Pick()
        {
            if(clips == null || clips.Length == 0) return null;
            return clips[Random.Range(0, clips.Length)];
        }
    }
}