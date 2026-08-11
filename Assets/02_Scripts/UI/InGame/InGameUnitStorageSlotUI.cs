// 대원 배치 버튼의 남은 배치 수를 표시하고 클릭에 따라 수량과 이미지를 갱신한다.

using System.Collections;
using System;
using Rebellion;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class InGameUnitStorageSlotUI : MonoBehaviour, IPointerUpHandler
{
    #region Fields

    [SerializeField] private Button button;
    [SerializeField] private Image storageImage;
    [SerializeField] private Sprite activeSprite;
    [SerializeField] private Sprite deactiveSprite;
    [SerializeField] private Sprite lockedSprite;
    [SerializeField] private TMP_Text countText;
    [SerializeField] private UIButtonLockView buttonLockView;

    [Header("Guide Highlight")]
    // 튜토리얼 가이드가 이 슬롯 종류의 배치를 안내할 때 쓰는 크기 펄스 설정이다.
    // 주기는 가이드 기물 펄스(TutorialGuidePresentation)와 같은 기본값을 써서 화면 전체 안내가 같은 박자로 보이게 한다.
    [SerializeField, Min(0.1f)] private float guideHighlightCycleSeconds = 1.8f;
    [SerializeField, Range(0f, 0.3f)] private float guideHighlightScaleAmount = 0.07f;

    public PieceType UnitType { get; private set; }
    public int MaxDeployableCount { get; private set; }
    public int RemainingDeployableCount { get; private set; }

    public event Action<InGameUnitStorageSlotUI> Clicked;
    private bool interactionLocked;
    private Coroutine guideHighlightRoutine;
    private Vector3 guideHighlightBaseScale = Vector3.one;

    #endregion

    #region Public Methods

    public void Bind(PieceType unitType, int deployableCount)
    {
        UnitType = unitType;
        MaxDeployableCount = Mathf.Max(0, deployableCount);
        RemainingDeployableCount = MaxDeployableCount;

        if (button != null)
        {
            button.targetGraphic = storageImage;
            button.onClick.RemoveListener(HandleClick);
            button.onClick.AddListener(HandleClick);
            ApplyButtonSpriteState();
        }

        EnsureButtonLockView();
        UpdateView();
        ApplyInteractionLockState();
    }

    public bool TryConsumeOne()
    {
        if (RemainingDeployableCount <= 0)
        {
            return false;
        }

        RemainingDeployableCount--;
        UpdateView();
        ApplyInteractionLockState();
        return true;
    }

    public bool TryRestoreOne()
    {
        if (RemainingDeployableCount >= MaxDeployableCount)
        {
            return false;
        }

        RemainingDeployableCount++;
        UpdateView();
        ApplyInteractionLockState();
        return true;
    }

    public void SetInteractionLocked(bool isLocked)
    {
        interactionLocked = isLocked;

        // 실행 중 잠금은 공용 버튼 잠금 컴포넌트에 위임하되, 수량별 이미지는 UpdateView가 계속 관리한다.
        UpdateView();
        ApplyInteractionLockState();
    }

    /// <summary>
    /// 튜토리얼 가이드가 이 슬롯 종류의 배치를 안내 중인지에 따라 크기 펄스 하이라이트를 켜고 끈다.
    /// 가이드 기물이 화면에서 사라지면 InGameStorageUIController가 false로 다시 호출한다.
    /// </summary>
    public void SetGuideHighlight(bool isActive)
    {
        if (isActive)
        {
            if (guideHighlightRoutine == null && gameObject.activeInHierarchy)
            {
                guideHighlightBaseScale = transform.localScale;
                guideHighlightRoutine = StartCoroutine(PulseGuideHighlight());
            }

            return;
        }

        if (guideHighlightRoutine != null)
        {
            StopCoroutine(guideHighlightRoutine);
            guideHighlightRoutine = null;
            transform.localScale = guideHighlightBaseScale;
        }
    }

    #endregion

    #region Button Events

    private void HandleClick()
    {
        if (interactionLocked)
        {
            return;
        }

        if (RemainingDeployableCount <= 0)
        {
            return;
        }

        Clicked?.Invoke(this);

        if (RemainingDeployableCount > 0)
        {
            StartCoroutine(ResetActiveSpriteAfterClick());
        }
    }

    #endregion

    #region Pointer Events

    public void OnPointerUp(PointerEventData eventData)
    {
        if (interactionLocked)
        {
            return;
        }

        if (RemainingDeployableCount > 0)
        {
            StartCoroutine(ResetActiveSpriteAfterClick());
        }
    }

    #endregion

    #region View

    private void UpdateView()
    {
        bool hasRemainingCount = RemainingDeployableCount > 0;

        if (countText == null)
        {
            Debug.LogWarning($"{nameof(InGameUnitStorageSlotUI)} has no count text assigned.", this);
        }
        else
        {
            countText.text = RemainingDeployableCount.ToString();
        }

        if (button != null)
        {
            button.interactable = hasRemainingCount;
        }

        if (storageImage != null)
        {
            SetStorageSprite(hasRemainingCount ? activeSprite : deactiveSprite);
        }
    }

    private void SetStorageSprite(Sprite sprite)
    {
        if (storageImage != null && sprite != null)
        {
            storageImage.sprite = sprite;
        }
    }

    #endregion

    #region Button State

    private void ApplyButtonSpriteState()
    {
        if (button == null)
        {
            return;
        }

        SpriteState spriteState = button.spriteState;
        spriteState.disabledSprite = deactiveSprite;
        button.spriteState = spriteState;
    }

    private void EnsureButtonLockView()
    {
        if (buttonLockView == null)
        {
            buttonLockView = GetComponent<UIButtonLockView>();
        }

        if (buttonLockView == null)
        {
            Debug.LogWarning($"{nameof(InGameUnitStorageSlotUI)} has no button lock view assigned.", this);
            return;
        }

        buttonLockView.Configure(
            button,
            storageImage,
            activeSprite,
            null,
            deactiveSprite,
            lockedSprite
        );
    }

    private void ApplyInteractionLockState()
    {
        EnsureButtonLockView();

        UIButtonLockMode lockMode = interactionLocked
            ? UIButtonLockMode.VisualDisabled
            : UIButtonLockMode.None;

        if (buttonLockView != null)
        {
            buttonLockView.SetAvailableVisual(RemainingDeployableCount > 0);
            buttonLockView.SetLockMode(lockMode);
        }
    }

    private IEnumerator ResetActiveSpriteAfterClick()
    {
        yield return null;

        if (interactionLocked || RemainingDeployableCount <= 0)
        {
            yield break;
        }

        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        SetStorageSprite(activeSprite);
    }

    #endregion

    #region Guide Highlight

    private void OnDisable()
    {
        // 오브젝트 비활성화로 코루틴이 함께 중단되므로, 크기와 핸들을 초기화해 재활성 시 새로 요청받을 수 있게 한다.
        if (guideHighlightRoutine != null)
        {
            guideHighlightRoutine = null;
            transform.localScale = guideHighlightBaseScale;
        }
    }

    private IEnumerator PulseGuideHighlight()
    {
        while (true)
        {
            // 실행 잠금 중이거나 남은 수량이 없으면 원래 크기를 유지한 채 강조만 쉰다.
            bool canHighlight = !interactionLocked && RemainingDeployableCount > 0;
            float wave = canHighlight
                ? (Mathf.Sin(Time.time * (Mathf.PI * 2f) / guideHighlightCycleSeconds) + 1f) * 0.5f
                : 0f;

            transform.localScale = guideHighlightBaseScale * (1f + guideHighlightScaleAmount * wave);
            yield return null;
        }
    }

    #endregion
}
