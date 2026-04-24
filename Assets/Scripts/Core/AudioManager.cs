using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rebellion.Core
{
    /// <summary>
    /// Manages background music and sound effect playback.
    /// Supports BGM crossfade and pooled SFX sources.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Audio Sources")]
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioSource sfxSource;

        [Header("Volume Settings")]
        [Range(0f, 1f)] [SerializeField] private float bgmVolume = 0.7f;
        [Range(0f, 1f)] [SerializeField] private float sfxVolume = 1f;

        public float BGMVolume
        {
            get => bgmVolume;
            set
            {
                bgmVolume = Mathf.Clamp01(value);
                bgmSource.volume = bgmVolume;
            }
        }

        public float SFXVolume
        {
            get => sfxVolume;
            set
            {
                sfxVolume = Mathf.Clamp01(value);
                sfxSource.volume = sfxVolume;
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (bgmSource == null) bgmSource = gameObject.AddComponent<AudioSource>();
            if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();

            bgmSource.loop = true;
            bgmSource.volume = bgmVolume;
            sfxSource.volume = sfxVolume;
        }

        public void PlayBGM(AudioClip clip, float fadeTime = 0f)
        {
            if (clip == null) return;

            if (fadeTime > 0f)
                StartCoroutine(CrossFadeBGM(clip, fadeTime));
            else
            {
                bgmSource.clip = clip;
                bgmSource.Play();
            }
        }

        public void StopBGM(float fadeTime = 0f)
        {
            if (fadeTime > 0f)
                StartCoroutine(FadeOutBGM(fadeTime));
            else
                bgmSource.Stop();
        }

        public void PlaySFX(AudioClip clip)
        {
            if (clip == null) return;
            sfxSource.PlayOneShot(clip, sfxVolume);
        }

        private IEnumerator CrossFadeBGM(AudioClip newClip, float fadeTime)
        {
            float halfFade = fadeTime * 0.5f;

            float startVolume = bgmSource.volume;
            float elapsed = 0f;

            while (elapsed < halfFade)
            {
                elapsed += Time.unscaledDeltaTime;
                bgmSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / halfFade);
                yield return null;
            }

            bgmSource.Stop();
            bgmSource.clip = newClip;
            bgmSource.Play();

            elapsed = 0f;
            while (elapsed < halfFade)
            {
                elapsed += Time.unscaledDeltaTime;
                bgmSource.volume = Mathf.Lerp(0f, bgmVolume, elapsed / halfFade);
                yield return null;
            }

            bgmSource.volume = bgmVolume;
        }

        private IEnumerator FadeOutBGM(float fadeTime)
        {
            float startVolume = bgmSource.volume;
            float elapsed = 0f;

            while (elapsed < fadeTime)
            {
                elapsed += Time.unscaledDeltaTime;
                bgmSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / fadeTime);
                yield return null;
            }

            bgmSource.Stop();
            bgmSource.volume = bgmVolume;
        }
    }
}
