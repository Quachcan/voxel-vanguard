using UnityEngine;

namespace _Workspace._Scripts.Core.Audio
{
    [CreateAssetMenu(fileName = "AudioSetting", menuName = "Core/Audio/AudioSetting", order = -1000)]
    public class AudioSetting : ScriptableObject
    {
        [Range(0, 1)] public float bgm = 0.5f;
        [Range(0, 1)] public float sfx = 0.5f;
        [Range(0, 1)] public float ambient = 0.5f;

        public bool bgmMuted;
        public bool sfxMuted;
        public bool ambientMuted;
        
        private const string K_BGM = "BGMVolume";
        private const string K_SFX = "SFXVolume";
        private const string K_AMB = "AmbientVolume";
        private const string K_BGM_MUTE = "BGMMuted";
        private const string K_SFX_MUTE = "SFXMuted";
        private const string K_AMB_MUTE = "AmbientMuted";

        public void Load()
        {
            bgm = PlayerPrefs.GetFloat(K_BGM, 0.5f);
            sfx = PlayerPrefs.GetFloat(K_SFX, 0.5f);
            ambient = PlayerPrefs.GetFloat(K_AMB, 0.5f);
            bgmMuted = PlayerPrefs.GetInt(K_BGM_MUTE, 0) == 1;
            sfxMuted = PlayerPrefs.GetInt(K_SFX_MUTE, 0) == 1;
            ambientMuted = PlayerPrefs.GetInt(K_AMB_MUTE, 0) == 1;
        }

        public void Save()
        {
            PlayerPrefs.SetFloat(K_BGM, bgm);
            PlayerPrefs.SetFloat(K_SFX, sfx);
            PlayerPrefs.SetFloat(K_AMB, ambient);
            PlayerPrefs.SetInt(K_BGM_MUTE, bgmMuted ? 1 : 0);
            PlayerPrefs.SetInt(K_SFX_MUTE, sfxMuted ? 1 : 0);
            PlayerPrefs.SetInt(K_AMB_MUTE, ambientMuted ? 1 : 0);
        }
    }
}