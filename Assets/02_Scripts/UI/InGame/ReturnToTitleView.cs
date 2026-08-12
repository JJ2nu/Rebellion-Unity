using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 타이틀 복귀 버튼과 확인 팝업의 표시·입력 전달만 담당하는 Passive View.
/// 복귀 실행 여부와 버튼 활성 판단은 ReturnToTitleController가 담당한다.
/// </summary>
public sealed class ReturnToTitleView : MonoBehaviour
{
    [Header("Bindings")]
    // Inspector 연결: 좌하단 타이틀 복귀 버튼(Btn_ReturnToTitle)을 넣는다.
    [SerializeField] private Button returnButton;
    // Inspector 연결: 확인 팝업 루트(ReturnConfirmPopup). 기본 비활성 상태로 둔다.
    [SerializeField] private GameObject confirmPopupRoot;
    // Inspector 연결: 팝업 안의 돌아가기 확정 버튼과 취소 버튼을 넣는다.
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    public event Action ReturnRequested;
    public event Action ConfirmRequested;
    public event Action CancelRequested;

    public bool IsPopupVisible => confirmPopupRoot != null && confirmPopupRoot.activeSelf;

    private void OnEnable()
    {
        // View는 버튼 클릭을 이벤트로만 올리고 실제 처리는 Controller 구독자가 결정한다.
        if (returnButton != null)
        {
            returnButton.onClick.AddListener(HandleReturnClicked);
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(HandleConfirmClicked);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.AddListener(HandleCancelClicked);
        }
    }

    private void OnDisable()
    {
        if (returnButton != null)
        {
            returnButton.onClick.RemoveListener(HandleReturnClicked);
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(HandleConfirmClicked);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveListener(HandleCancelClicked);
        }
    }

    public void SetReturnButtonInteractable(bool isInteractable)
    {
        if (returnButton != null)
        {
            // SpriteSwap 버튼이므로 비활성화 시 Inspector에 지정된 disabled 스프라이트로 표시된다.
            returnButton.interactable = isInteractable;
        }
    }

    public void SetPopupVisible(bool isVisible)
    {
        if (confirmPopupRoot != null)
        {
            confirmPopupRoot.SetActive(isVisible);
        }
    }

    private void HandleReturnClicked()
    {
        ReturnRequested?.Invoke();
    }

    private void HandleConfirmClicked()
    {
        ConfirmRequested?.Invoke();
    }

    private void HandleCancelClicked()
    {
        CancelRequested?.Invoke();
    }
}
