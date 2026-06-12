using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 버튼 Prefab 자체의 AudioSource로 hover와 click SFX를 재생한다.
/// additionalClickClip은 기본 클릭음과 함께 재생해야 하는 버튼 전용 효과음에 사용한다.
/// </summary>
[RequireComponent(typeof(Button))]
[RequireComponent(typeof(AudioSource))]
public sealed class UIButtonSfx : MonoBehaviour, IPointerEnterHandler, IPointerDownHandler
{
    [SerializeField] private AudioClip hoverClip;
    [SerializeField] private AudioClip clickClip;
    [SerializeField] private AudioClip additionalClickClip;

    private AudioSource audioSource;
    private Button button;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        button = GetComponent<Button>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!button.interactable || hoverClip == null)
        {
            return;
        }

        audioSource.PlayOneShot(hoverClip);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || !button.interactable)
        {
            return;
        }

        if (clickClip != null)
        {
            audioSource.PlayOneShot(clickClip);
        }

        if (additionalClickClip != null)
        {
            audioSource.PlayOneShot(additionalClickClip);
        }
    }
}
