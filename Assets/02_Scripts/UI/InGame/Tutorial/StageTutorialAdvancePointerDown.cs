using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 튜토리얼 페이지 이동 버튼(다음·닫기·이전)의 왼쪽 PointerDown만 Passive View에 전달한다.
/// Button 자체는 SpriteSwap pressed/hover 표현을 계속 담당한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class StageTutorialAdvancePointerDown : MonoBehaviour, IPointerDownHandler
{
    [SerializeField] private Button targetButton;

    public event Action Pressed;

    private void Awake()
    {
        if (targetButton == null)
        {
            targetButton = GetComponent<Button>();
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData == null ||
            eventData.button != PointerEventData.InputButton.Left ||
            targetButton == null ||
            !targetButton.IsInteractable())
        {
            return;
        }

        Pressed?.Invoke();
    }
}
