using System;
using UnityEngine;

/// <summary>
/// StageManager가 확정한 맵 인덱스를 BGM과 반복 앰비언트 조합으로 변환해 재생한다.
/// Stage JSON에는 오디오 정보를 중복 저장하지 않고 기존 mapIndex를 단일 기준으로 사용한다.
/// </summary>
public sealed class MapAudioController : MonoBehaviour
{
    [Serializable]
    private sealed class MapAudioBinding
    {
        [SerializeField] private AudioClip bgmClip;
        [SerializeField] private AudioClip ambientClip;

        public AudioClip BgmClip => bgmClip;
        public AudioClip AmbientClip => ambientClip;
    }

    [SerializeField] private AudioSource bgmAudioSource;
    [SerializeField] private AudioSource ambientAudioSource;
    [SerializeField] private MapAudioBinding[] mapAudioBindings = Array.Empty<MapAudioBinding>();

    public int CurrentMapIndex { get; private set; } = -1;
    public AudioClip CurrentBgmClip => bgmAudioSource != null ? bgmAudioSource.clip : null;
    public AudioClip CurrentAmbientClip => ambientAudioSource != null ? ambientAudioSource.clip : null;
    public bool IsBgmPlaying => bgmAudioSource != null && bgmAudioSource.isPlaying;
    public bool IsAmbientPlaying => ambientAudioSource != null && ambientAudioSource.isPlaying;

    private void Awake()
    {
        ConfigureLooping2DSource(bgmAudioSource);
        ConfigureLooping2DSource(ambientAudioSource);
    }

    private void OnDisable()
    {
        Stop();
    }

    /// <summary>
    /// 새 스테이지 진입 시 이전 맵 오디오를 정리하고 지정된 맵 조합을 처음부터 재생한다.
    /// 한 채널의 참조가 누락되어도 다른 채널과 Stage 로드 흐름은 계속 유지한다.
    /// </summary>
    public void PlayForMap(int mapIndex)
    {
        PrepareForMap(mapIndex);
        PlayPrepared();
    }

    /// <summary>
    /// 오디오드라마가 먼저 재생되는 Stage를 위해 클립만 선택하고 실제 재생은 보류한다.
    /// </summary>
    public void PrepareForMap(int mapIndex)
    {
        Stop();

        if (mapAudioBindings == null || mapIndex < 0 || mapIndex >= mapAudioBindings.Length)
        {
            Debug.LogWarning($"Map audio binding is missing for map index {mapIndex}.", this);
            return;
        }

        MapAudioBinding binding = mapAudioBindings[mapIndex];
        if (binding == null)
        {
            Debug.LogWarning($"Map audio binding is null for map index {mapIndex}.", this);
            return;
        }

        CurrentMapIndex = mapIndex;
        AssignChannel(bgmAudioSource, binding.BgmClip, "BGM", mapIndex);
        AssignChannel(ambientAudioSource, binding.AmbientClip, "ambient", mapIndex);
    }

    /// <summary>
    /// 준비된 맵 오디오를 처음부터 재생한다. 오디오드라마 완료 또는 스킵 뒤 같은 경로를 사용한다.
    /// </summary>
    public void PlayPrepared()
    {
        if (CurrentMapIndex < 0)
        {
            return;
        }

        PlayAssignedChannel(bgmAudioSource);
        PlayAssignedChannel(ambientAudioSource);
    }

    /// <summary>
    /// Stage 종료나 맵 전환 전에 두 채널을 모두 정리해 영속 StageManager에 소리가 남지 않게 한다.
    /// </summary>
    public void Stop()
    {
        StopChannel(bgmAudioSource);
        StopChannel(ambientAudioSource);
        CurrentMapIndex = -1;
    }

    private static void ConfigureLooping2DSource(AudioSource audioSource)
    {
        if (audioSource == null)
        {
            return;
        }

        audioSource.playOnAwake = false;
        audioSource.loop = true;
        audioSource.spatialBlend = 0f;
    }

    private void AssignChannel(AudioSource audioSource, AudioClip clip, string channelName, int mapIndex)
    {
        if (audioSource == null)
        {
            Debug.LogWarning($"Map {channelName} AudioSource is not assigned.", this);
            return;
        }

        if (clip == null)
        {
            Debug.LogWarning($"Map {channelName} clip is not assigned for map index {mapIndex}.", this);
            return;
        }

        audioSource.clip = clip;
    }

    private static void PlayAssignedChannel(AudioSource audioSource)
    {
        if (audioSource == null || audioSource.clip == null)
        {
            return;
        }

        audioSource.time = 0f;
        audioSource.Play();
    }

    private static void StopChannel(AudioSource audioSource)
    {
        if (audioSource == null)
        {
            return;
        }

        audioSource.Stop();
        audioSource.clip = null;
    }
}
