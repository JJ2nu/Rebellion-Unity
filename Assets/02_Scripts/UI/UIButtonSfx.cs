using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 버튼 Prefab 자체의 AudioSource로 hover와 click SFX를 재생한다.
/// additionalClickClip은 기본 클릭음과 함께 재생해야 하는 버튼 전용 효과음에 사용한다.
/// </summary>
[RequireComponent(typeof(Button))]
public sealed class UIButtonSfx : MonoBehaviour, IPointerEnterHandler, IPointerDownHandler
{
    [SerializeField] private AudioClip hoverClip;
    [SerializeField] private AudioClip clickClip;
    [SerializeField] private AudioClip additionalClickClip;
    // Campaign처럼 클릭음은 영속 출력으로 끝까지 유지하면서, 별도 Scene BGM만 즉시 멈춰야 하는 경우에만 연결한다.
    [SerializeField] private AudioSource audioSourceToStopOnClick;

    private AudioSource audioSource;
    private Button button;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        button = GetComponent<Button>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!button.interactable)
        {
            return;
        }

        PlayClip(hoverClip);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || !button.interactable)
        {
            return;
        }

        PlayClickSfxSet();
    }

    // 키보드 하이라이트가 마우스 hover와 같은 소리를 내도록 외부(키보드 입력 처리)에서 호출하는 진입점이다.
    public void PlayHoverSfxForKeyboard()
    {
        if (button != null && !button.interactable)
        {
            return;
        }

        PlayClip(hoverClip);
    }

    // 키보드 확정이 마우스 클릭(PointerDown)과 같은 소리를 내도록 외부에서 호출하는 진입점이다.
    public void PlayClickSfxForKeyboard()
    {
        if (button != null && !button.interactable)
        {
            return;
        }

        PlayClickSfxSet();
    }

    // 마우스 클릭과 키보드 확정이 같은 클릭음 조합을 공유한다.
    private void PlayClickSfxSet()
    {
        if (clickClip != null)
        {
            PlayClip(clickClip);
        }

        if (additionalClickClip != null)
        {
            PlayClip(additionalClickClip);
        }

        StopConfiguredAudioSource();
    }

    private void StopConfiguredAudioSource()
    {
        // 재생 출력과 같은 AudioSource를 잘못 연결해도 방금 시작한 클릭음을 끊지 않는다.
        if (audioSourceToStopOnClick == null || audioSourceToStopOnClick == audioSource)
        {
            return;
        }

        audioSourceToStopOnClick.Stop();
    }

    private void PlayClip(AudioClip clip)
    {
        if (clip == null)
        {
            return;
        }

        if (audioSource == null && GameSceneManager.Instance != null)
        {
            // Title 재진입 시 삭제되는 새 Managers 참조 대신 영속 싱글톤의 출력을 런타임에 사용한다.
            audioSource = GameSceneManager.Instance.GetComponent<AudioSource>();
        }

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }

        audioSource.PlayOneShot(clip);
    }
}
