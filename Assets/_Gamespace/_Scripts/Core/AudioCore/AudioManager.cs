using System;
using System.Collections;
using System.Collections.Generic;
using _Workspace._Scripts.Core.Audio;
using UnityEngine;
using UnityEngine.Audio;
using Random = UnityEngine.Random;

namespace _Workspace._Scripts.Core.AudioCore
{
    [DefaultExecutionOrder(-100)]
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }
        
        [Header("Scriptable Objects")]
        [SerializeField] private AudioSetting settings;
        [SerializeField] private AudioBank audioBank;
        
        [Header("Audio Source")]
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioSource ambientSource;
        [SerializeField] private AudioMixer masterMixer;
        
        [Header("One-shot 3D pool")]
        [SerializeField] private AudioSource oneShot3DPrefab;
        [SerializeField] private Transform audioPoolRoot;
        [SerializeField] private int poolSize = 10;
        
        private const string PARAM_BGM = "BGM_Volume";
        private const string PARAM_SFX = "SFX_Volume";
        private const string PARAM_AMB = "Ambient_Volume";

        private readonly Dictionary<string, float> cooldownUntil = new();
        private readonly Queue<AudioSource> audioPool = new();
        
        public event Action<float> OnBGMVolumeChanged;
        public event Action<float> OnSfxVolumeChanged;
        
        public bool IsAmbientPlaying { get; private set; }
        public bool IsBgmPlaying { get; private set; }
        
        public float BgmVolume     => settings ? settings.bgm     : 1f;
        public float SfxVolume     => settings ? settings.sfx     : 1f;
        public float AmbientVolume => settings ? settings.ambient : 1f;
        
        public bool BgmMuted => settings && settings.bgmMuted;
        public bool SfxMuted => settings && settings.sfxMuted;
        public bool AmbientMuted => settings && settings.ambientMuted;

        private void Awake()
        {
            if(Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        
        private void Start()
        {
            if(settings) settings.Load();
            PreWarm3DPool();
            ApplyVolume();
        }

        private void PreWarm3DPool()
        {
            if(!oneShot3DPrefab) return;
            for (int i = 0; i < poolSize; i++)
            {
                AudioSource s =  Instantiate(oneShot3DPrefab, audioPoolRoot);
                s.gameObject.SetActive(false);
                audioPool.Enqueue(s);
            }
        }
        
        public void PlayBGM(AudioCue cue)
        {
            if(!cue) return;
            AudioClip clip = cue.Pick(); if (!clip || !bgmSource) return;

            SetupSource(bgmSource, cue, isBgm: true);
            bgmSource.clip = clip;
            bgmSource.loop = true;
            bgmSource.spatialBlend = 0f;
            bgmSource.Play();
            
            IsBgmPlaying = true;
        }

        public void PlayBGM(string key)
        {
            AudioCue cue = audioBank.GetClip(key);
            if(cue != null)
                PlayBGM(cue);
            else
                Debug.LogWarning($"[AudioManager]Audio clip not found for key: {key}");
        }

        public void CrossFade(AudioCue cue, float fadeDuration = 1)
        {
            if(!cue || !bgmSource) return;
            StartCoroutine(CoCrossFade(cue, Mathf.Max(0.01f, fadeDuration)));
        }

        private IEnumerator CoCrossFade(AudioCue cue, float duration)
        {
            float half = duration * 0.5f;
            float startVolume = bgmSource.volume;
            float t = 0f;
            while(t < half) { t += Time.unscaledDeltaTime; bgmSource.volume = Mathf.Lerp(startVolume, 0f, t / half); yield return null; }
            PlayBGM(cue);
            float target = settings && !settings.bgmMuted ? settings.bgm * cue.volume : 0f;
            t = 0f;
            while (t < half)
            {
                t += Time.unscaledDeltaTime; 
                bgmSource.volume = Mathf.Lerp(0, target, t / half);
                yield return null;
            }
        }

        public void StopBGM() { if(bgmSource) bgmSource.Stop(); IsBgmPlaying = false; }
        public void PauseBGM() { if(bgmSource) bgmSource.Pause(); IsBgmPlaying = false; }
        public void ResumeBGM() { if(bgmSource && !IsBgmPlaying && bgmSource.clip != null) 
            bgmSource.Play(); IsBgmPlaying = true; }
        
        public void PlayAmbient(AudioCue cue)
        {
            if(!cue) return;
            AudioClip clip = cue.Pick(); if(!clip || !ambientSource) return;
            
            SetupSource(ambientSource, cue, isAmbient: true);
            ambientSource.clip = clip;
            ambientSource.loop = cue.loop;
            ambientSource.spatialBlend = 0f;
            ambientSource.Play();
            
            IsAmbientPlaying = true;
        }
        
        public void PlayAmbient(string key)
        {
            AudioCue cue = audioBank.GetClip(key);
            if(cue != null)
                PlayAmbient(cue);
            else
                Debug.LogWarning($"[AudioManager]Audio clip not found for key: {key}");
        }

        public void StopAmbient() { if(ambientSource) ambientSource.Stop(); IsAmbientPlaying = false; }
        public void PauseAmbient() { if(ambientSource) ambientSource.Pause(); IsAmbientPlaying = false; }
        public void ResumeAmbient() { if(ambientSource && !IsAmbientPlaying && ambientSource.clip != null) 
            ambientSource.Play(); IsAmbientPlaying = true; }
        
        public void PlaySfx(AudioCue cue)
        {
            if(!cue || !sfxSource) return;
            if(BlockedByCooldown(cue)) return;
            
            AudioClip clip = cue.Pick(); if(!clip) return;
            
            SetupSource(sfxSource, cue);
            sfxSource.spatialBlend = 0f;
            sfxSource.PlayOneShot(clip, sfxSource.volume);
            ArmCooldown(cue);
        }

        public void PlaySfx(AudioCue cue, Vector3 wordPos)
        {
            if(!cue) {PlaySfx(cue); return;} 
            if(!cue.spatial) {PlaySfx(cue); return;}
            if(BlockedByCooldown(cue)) return;

            AudioClip clip = cue.Pick(); if(!clip) return;
            AudioSource src = Get3DSource(); if(!src) {PlaySfx(cue); return;} 

            src.transform.position = wordPos;
            SetupSource(src, cue);
            src.loop = false;
            src.spatialBlend = 1f;
            src.clip = clip;
            src.gameObject.SetActive(true);
            src.Play();
            StartCoroutine(ReturnWhenDone(src));
            ArmCooldown(cue);
        }
        
        public void PlaySfx(string key)
        {
            AudioCue clip = audioBank.GetClip(key);
            if(clip != null)
                PlaySfx(clip);
            else
                Debug.LogWarning($"[AudioManager]Audio clip not found for key: {key}");
        }
        
        public void StopSfx() { if(sfxSource) sfxSource.Stop(); }

        private AudioSource Get3DSource()
        {
            if(audioPool.Count > 0) return audioPool.Dequeue();
            if(!oneShot3DPrefab) return null;
            AudioSource s = Instantiate(oneShot3DPrefab, transform);
            s.gameObject.SetActive(false);
            return s;
        }

        private IEnumerator ReturnWhenDone(AudioSource src)
        {
            yield return new WaitWhile(() => src && src.isPlaying);
            if(!src) yield break;
            src.gameObject.SetActive(false);
            audioPool.Enqueue(src);
        }

        public void SetBGMVolume(float volume)
        {
            if (!settings) return;
            settings.bgm = volume;
            if(masterMixer) masterMixer.SetFloat(PARAM_BGM, Mathf.Log10(Mathf.Clamp(volume, 0.0001f, 1f)) * 20f);
            settings.Save();
            ApplyBGMVolume();
            OnBGMVolumeChanged?.Invoke(settings.bgm);
        }

        public void SetSfxVolume(float volume)
        {
            if (!settings) return;
            settings.sfx = volume;
            if(masterMixer) masterMixer.SetFloat(PARAM_SFX, Mathf.Log10(Mathf.Clamp(volume, 0.0001f, 1f)) * 20f);
            ApplySfxVolume();
            settings.Save();
            OnSfxVolumeChanged?.Invoke(settings.sfx);
        }

        public void SetAmbientVolume(float volume)
        {
            if (!settings) return;
            settings.ambient = volume;
            if(masterMixer) masterMixer.SetFloat(PARAM_AMB, Mathf.Log10(Mathf.Clamp(volume, 0.0001f, 1f)) * 20f);
            ApplyAmbientVolume();
            settings.Save();
        }

        public void ToggleBgmMute() { if(!settings) return; settings.bgmMuted = !settings.bgmMuted; ApplyBGMVolume(); settings.Save(); }

        public void ToggleSfxMute() { if(!settings) return; settings.sfxMuted = !settings.sfxMuted;  ApplySfxVolume(); settings.Save(); }

        public void ToggleAmbientMute() { if(!settings) return; settings.ambientMuted = !settings.ambientMuted; ApplyAmbientVolume(); settings.Save(); }
        
        private void ApplyVolume() { ApplyBGMVolume(); ApplySfxVolume(); ApplyAmbientVolume(); }
        private void ApplyBGMVolume() {if(bgmSource) bgmSource.volume = settings && !settings.bgmMuted ? settings.bgm : 0f;}
        private void ApplySfxVolume() { if(sfxSource) sfxSource.volume = settings && !settings.sfxMuted ? settings.sfx : 0f;}
        private void ApplyAmbientVolume() { if(ambientSource) ambientSource.volume = settings && !settings.ambientMuted ? settings.ambient : 0f;}

        private void SetupSource(AudioSource src, AudioCue cue, bool isBgm = false, bool isAmbient = false)
        {
            if(!src || !cue) return;
            if (cue.mixerGroups) src.outputAudioMixerGroup = cue.mixerGroups;
            
            float baseVol = 
                isBgm ? (settings && !settings.bgmMuted ? settings.bgm             : 0f) : 
                isAmbient ? (settings && !settings.ambientMuted ? settings.ambient : 0f) :
                            (settings && !settings.sfxMuted ? settings.sfx         : 0f);
            
            src.volume = baseVol * Mathf.Clamp01(cue.volume);
            src.pitch  = Random.Range(Mathf.Min(cue.pitchMin, cue.pitchMax), Mathf.Max(cue.pitchMin, cue.pitchMax));
        }

        private bool BlockedByCooldown(AudioCue cue)
        {
            if(cue.cooldown <= 0f) return false;
            if(cooldownUntil.TryGetValue(cue.name, out var until) && Time.unscaledTime < until)
                return true;
            return false;
        }

        private void ArmCooldown(AudioCue cue)
        {
            if(cue.cooldown <= 0f) return;
            cooldownUntil[cue.name] = Time.unscaledTime + cue.cooldown;
        }
    }
}