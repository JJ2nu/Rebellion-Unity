using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
