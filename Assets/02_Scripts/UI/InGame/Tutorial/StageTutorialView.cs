using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Stage 1 튜토리얼의 이미지와 진행 버튼 표시만 담당하는 Passive View다.
/// 페이지 순서와 완료 판단은 StageTutorialController가 소유한다.
/// </summary>
public sealed class StageTutorialView : MonoBehaviour
{
    [Header("Bindings")]
    [SerializeField] private GameObject presentationRoot;
    [SerializeField] private Image pageImage;
    [SerializeField] private Button advanceButton;
    [SerializeField] private RectTransform advanceButtonRectTransform;
    [SerializeField] private Image advanceButtonImage;
    [SerializeField] private StageTutorialAdvancePointerDown advancePointerDown;
    [SerializeField] private Button previousButton;
    [SerializeField] private Image previousButtonImage;
    [SerializeField] private StageTutorialAdvancePointerDown previousPointerDown;

    [Header("Page Sprites")]
    [SerializeField] private Sprite[] pageSprites = Array.Empty<Sprite>();

    [Header("Next Button")]
    [SerializeField] private Sprite nextDefaultSprite;
    [SerializeField] private Sprite nextHoverSprite;

    [Header("Previous Button")]
    [SerializeField] private Sprite previousDefaultSprite;
    [SerializeField] private Sprite previousHoverSprite;
    [SerializeField] private Vector2 singleAdvancePosition = new Vector2(0f, 80f);
    [SerializeField] private Vector2 pairedAdvancePosition = new Vector2(110f, 80f);

    [Header("Close Button")]
    [SerializeField] private Sprite closeDefaultSprite;
    [SerializeField] private Sprite closeHoverSprite;

    public int PageCount => pageSprites?.Length ?? 0;

    public event Action AdvanceRequested;
    public event Action PreviousRequested;

    private void Awake()
    {
        SetVisible(false);
    }

    private void OnEnable()
    {
        if (advancePointerDown != null)
        {
            advancePointerDown.Pressed += HandleAdvancePressed;
        }

        if (previousPointerDown != null)
        {
            previousPointerDown.Pressed += HandlePreviousPressed;
        }
    }

    private void OnDisable()
    {
        if (advancePointerDown != null)
        {
            advancePointerDown.Pressed -= HandleAdvancePressed;
        }

        if (previousPointerDown != null)
        {
            previousPointerDown.Pressed -= HandlePreviousPressed;
        }
    }

    public void ShowPage(int pageIndex)
    {
        if (pageImage == null ||
            advanceButton == null ||
            advanceButtonRectTransform == null ||
            advanceButtonImage == null ||
            advancePointerDown == null ||
            previousButton == null ||
            previousButtonImage == null ||
            previousPointerDown == null)
        {
            Debug.LogError("[StageTutorialView] Required UI bindings are missing.", this);
            return;
        }

        if (pageSprites == null || pageIndex < 0 || pageIndex >= pageSprites.Length)
        {
            Debug.LogError($"[StageTutorialView] Invalid page index: {pageIndex}", this);
            return;
        }

        bool isLastPage = pageIndex == pageSprites.Length - 1;
        bool canGoPrevious = pageIndex > 0;
        Sprite defaultSprite = isLastPage ? closeDefaultSprite : nextDefaultSprite;
        Sprite hoverSprite = isLastPage ? closeHoverSprite : nextHoverSprite;

        pageImage.sprite = pageSprites[pageIndex];
        advanceButtonImage.sprite = defaultSprite;

        SpriteState spriteState = advanceButton.spriteState;
        spriteState.highlightedSprite = hoverSprite;
        spriteState.pressedSprite = hoverSprite;
        spriteState.selectedSprite = defaultSprite;
        advanceButton.spriteState = spriteState;
        advanceButton.interactable = true;

        // 첫 페이지는 다음 버튼을 중앙에 두고, 이후 페이지는 이전/다음 버튼을 좌우로 배치한다.
        previousButton.gameObject.SetActive(canGoPrevious);
        advanceButtonRectTransform.anchoredPosition = canGoPrevious
            ? pairedAdvancePosition
            : singleAdvancePosition;

        if (canGoPrevious)
        {
            previousButtonImage.sprite = previousDefaultSprite;

            SpriteState previousSpriteState = previousButton.spriteState;
            previousSpriteState.highlightedSprite = previousHoverSprite;
            previousSpriteState.pressedSprite = previousHoverSprite;
            previousSpriteState.selectedSprite = previousDefaultSprite;
            previousButton.spriteState = previousSpriteState;
            previousButton.interactable = true;
        }

        SetVisible(true);
    }

    public void SetVisible(bool visible)
    {
        if (presentationRoot != null)
        {
            presentationRoot.SetActive(visible);
        }
    }

    private void HandleAdvancePressed()
    {
        AdvanceRequested?.Invoke();
    }

    private void HandlePreviousPressed()
    {
        PreviousRequested?.Invoke();
    }
}
