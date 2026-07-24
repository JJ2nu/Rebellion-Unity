using UnityEngine;
using UnityEngine.EventSystems;
using System;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// SZ_Test에서 OpeningShot 스코프 연출만 먼저 확인하기 위한 UI 프리뷰 컨트롤러다.
/// 실제 적 타겟팅과 시뮬레이션 선처리는 Stage 흐름에 연결할 때 별도 단계에서 다룬다.
/// </summary>
[DisallowMultipleComponent]
public sealed class OpeningShotScopePreview : MonoBehaviour
{
    [Header("Activation")]
    // 스코프 UI를 켜거나 실행하기 위해 사용하는 버튼이다. SZ_Test에서는 OrderSkillButton 또는 Btn_Play를 연결한다.
    [SerializeField] private Button activationButton;

    // true면 버튼을 누를 때마다 켜기/끄기를 반복하고, false면 버튼을 눌렀을 때 켜기만 한다.
    [SerializeField] private bool toggleOnClick = true;

    [Header("Scene References")]

    // 원래 화면을 렌더링하는 기준 카메라다. 이 카메라의 위치와 방향을 기준으로 스코프 확대 카메라를 맞춘다.
    [SerializeField] private Camera sourceCamera;

    // 스코프 안쪽 확대 화면만 따로 렌더링하는 전용 카메라다. 출력은 화면이 아니라 RenderTexture로 보낸다.
    [SerializeField] private Camera scopeCamera;

    // 마우스를 따라 움직이는 스코프 UI 전체의 루트 RectTransform이다.
    [SerializeField] private RectTransform scopeRoot;

    // 확대 화면이 원형 영역 안에서만 보이도록 잘라내는 마스크 RectTransform이다.
    [SerializeField] private RectTransform viewportMask;

    // scopeCamera가 그린 RenderTexture를 화면에 표시하는 RawImage다.
    [SerializeField] private RawImage magnifiedView;

    // OpeningShotScope.png 프레임 이미지를 표시하는 Image다. 확대 화면보다 위에 놓인다.
    [SerializeField] private Image scopeFrame;

    // 스코프 UI가 올라가는 Canvas다. 화면 좌표를 Canvas 로컬 좌표로 바꿀 때 사용한다.
    [SerializeField] private Canvas targetCanvas;

    // 스코프 프레임에 사용할 Sprite다. Assets/04_Images/UI/OpeningShotScope.png를 연결한다.
    [SerializeField] private Sprite scopeSprite;

    [Header("Scope View")]

    // 화면에 표시되는 스코프 프레임 전체 크기다. 이미지 원본 크기와 맞추면 찌그러짐이 적다.
    [SerializeField] private Vector2 scopeSize = new(394f, 391f);

    // 스코프 안에서 확대 화면이 실제로 보이는 원형 영역의 지름이다.
    [SerializeField] private float viewportDiameter = 264f;

    // 확대 배율이다. 값이 클수록 scopeCamera의 시야각이 좁아져 더 크게 보인다.
    [SerializeField, Min(1f)] private float magnification = 2f;

    // 확대 화면용 RenderTexture의 한 변 해상도다. 값이 클수록 선명하지만 렌더링 비용이 증가한다.
    [SerializeField, Min(64)] private int renderTextureSize = 512;

    // scopeCamera가 그린 화면을 magnifiedView에 전달하기 위한 런타임 RenderTexture다.
    private RenderTexture scopeTexture;

    // viewportMask에 사용할 원형 마스크 Sprite다. 별도 에셋 없이 런타임에 생성한다.
    private Sprite runtimeMaskSprite;

    // 현재 스코프가 표시 중인지 저장하는 상태값이다.
    private bool isScopeVisible;

    // 커서를 숨겼다가 복구할 때 이전 커서 표시 상태를 기억하기 위한 값이다.
    private bool previousCursorVisible = true;

    // 외부 검증 코드나 다른 컴포넌트가 스코프 표시 상태를 읽을 수 있게 제공하는 읽기 전용 프로퍼티다.
    public bool IsScopeVisible => isScopeVisible;

    // 우클릭으로 스코프를 닫았을 때 실제 타겟팅 상태도 함께 취소할 수 있도록 외부에 알린다.
    public event Action CancelRequested;

    // OrderSkillButton처럼 런타임에 생성되는 버튼을 나중에 연결할 수 있게 제공하는 바인딩 함수다.
    public void BindActivationButton(Button button)
    {
        UnregisterActivationButton(activationButton);
        activationButton = button;
        RegisterActivationButton(activationButton);
    }

    // 자기 참조와 UI 기본 상태를 준비한다. RenderTexture를 만들고 시작 시에는 스코프를 숨긴다.
    private void Awake()
    {
        if (sourceCamera == null)
        {
            sourceCamera = Camera.main;
        }

        ConfigureStaticUi();
        EnsureRenderTexture();
        HideScope();
    }

    // 컴포넌트가 켜질 때 버튼 클릭 이벤트를 등록한다.
    private void OnEnable()
    {
        RegisterActivationButton(activationButton);
    }

    // 컴포넌트가 꺼질 때 버튼 클릭 이벤트를 해제하고 스코프 표시도 정리한다.
    private void OnDisable()
    {
        UnregisterActivationButton(activationButton);

        HideScope();
    }

    // 런타임에 만든 RenderTexture와 원형 마스크 Texture/Sprite를 해제한다.
    private void OnDestroy()
    {
        ReleaseRenderTexture();

        if (runtimeMaskSprite != null)
        {
            Destroy(runtimeMaskSprite.texture);
            Destroy(runtimeMaskSprite);
        }
    }

    // 스코프가 켜져 있는 동안 매 프레임 마우스 위치와 확대 카메라 방향을 갱신한다.
    private void LateUpdate()
    {
        if (!isScopeVisible)
        {
            return;
        }

        if (WasRightMousePressedThisFrame())
        {
            HideScope();
            CancelRequested?.Invoke();
            return;
        }

        if (sourceCamera == null)
        {
            sourceCamera = Camera.main;
        }

        if (sourceCamera == null || scopeCamera == null || scopeRoot == null)
        {
            return;
        }

        Vector2 pointerPosition = ReadPointerPosition();
        UpdateScopeUiPosition(pointerPosition);
        UpdateScopeCamera(pointerPosition);
    }

    // 스코프 UI와 확대 카메라를 켜고, 켜기 전 커서 표시 상태를 기억한 뒤 기본 커서를 숨긴다.
    public void ShowScope()
    {
        if (isScopeVisible)
        {
            return;
        }

        isScopeVisible = true;
        previousCursorVisible = Cursor.visible;
        Cursor.visible = false;


        if (scopeRoot != null)
        {
            scopeRoot.gameObject.SetActive(true);
        }

        if (scopeCamera != null)
        {
            scopeCamera.gameObject.SetActive(true);
        }
    }

    // 스코프 UI와 확대 카메라를 끄고, ShowScope에서 기억해 둔 커서 표시 상태로 되돌린다.
    public void HideScope()
    {
        if (!isScopeVisible && scopeRoot != null && !scopeRoot.gameObject.activeSelf)
        {
            return;
        }

        isScopeVisible = false;
        Cursor.visible = previousCursorVisible;

        if (scopeRoot != null)
        {
            scopeRoot.gameObject.SetActive(false);
        }

        if (scopeCamera != null)
        {
            scopeCamera.gameObject.SetActive(false);
        }
    }

    // 현재 상태에 따라 ShowScope 또는 HideScope를 호출해 스코프 표시를 전환한다.
    public void ToggleScope()
    {
        if (isScopeVisible)
        {
            HideScope();
            return;
        }

        ShowScope();
    }

    // 연결된 버튼을 눌렀을 때 호출된다. toggleOnClick 설정에 따라 토글하거나 켜기만 한다.
    private void HandleActivationClicked()
    {
        if (toggleOnClick)
        {
            ToggleScope();
            return;
        }

        ShowScope();
    }

    // 버튼 onClick에 스코프 활성화 콜백을 등록한다. 중복 등록을 막기 위해 먼저 제거한 뒤 다시 추가한다.
    private void RegisterActivationButton(Button button)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(HandleActivationClicked);
        button.onClick.AddListener(HandleActivationClicked);
    }

    // 버튼 onClick에서 스코프 활성화 콜백을 제거한다.
    private void UnregisterActivationButton(Button button)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(HandleActivationClicked);
    }

    // Inspector 값 기준으로 스코프 UI 크기, 원형 마스크, 프레임 이미지를 초기 설정한다.
    private void ConfigureStaticUi()
    {
        if (scopeRoot != null)
        {
            scopeRoot.sizeDelta = scopeSize;
        }

        if (viewportMask != null)
        {
            viewportMask.sizeDelta = new Vector2(viewportDiameter, viewportDiameter);
            Image maskImage = viewportMask.GetComponent<Image>();

            if (maskImage != null && maskImage.sprite == null)
            {
                maskImage.sprite = CreateCircleMaskSprite();
                maskImage.color = Color.white;
                maskImage.raycastTarget = false;
            }

            Mask mask = viewportMask.GetComponent<Mask>();

            if (mask != null)
            {
                mask.showMaskGraphic = false;
            }
        }

        if (magnifiedView != null)
        {
            magnifiedView.raycastTarget = false;
        }

        if (scopeFrame != null)
        {
            scopeFrame.sprite = scopeSprite != null ? scopeSprite : scopeFrame.sprite;
            scopeFrame.preserveAspect = true;
            scopeFrame.raycastTarget = false;
        }
    }

    // 확대 카메라가 그릴 RenderTexture를 만들고 scopeCamera와 magnifiedView에 연결한다.
    private void EnsureRenderTexture()
    {
        if (renderTextureSize <= 0 || scopeCamera == null || magnifiedView == null)
        {
            return;
        }

        if (scopeTexture != null && scopeTexture.width == renderTextureSize)
        {
            return;
        }

        ReleaseRenderTexture();

        scopeTexture = new RenderTexture(renderTextureSize, renderTextureSize, 16, RenderTextureFormat.ARGB32)
        {
            name = "OpeningShotScopePreview_RT"
        };
        scopeTexture.Create();

        scopeCamera.targetTexture = scopeTexture;
        magnifiedView.texture = scopeTexture;
    }

    // RenderTexture 참조를 카메라와 UI에서 분리한 뒤 메모리를 해제한다.
    private void ReleaseRenderTexture()
    {
        if (scopeCamera != null && scopeCamera.targetTexture == scopeTexture)
        {
            scopeCamera.targetTexture = null;
        }

        if (magnifiedView != null && magnifiedView.texture == scopeTexture)
        {
            magnifiedView.texture = null;
        }

        if (scopeTexture == null)
        {
            return;
        }

        scopeTexture.Release();
        Destroy(scopeTexture);
        scopeTexture = null;
    }

    // 마우스의 화면 좌표를 Canvas 좌표로 바꿔 스코프 UI 루트를 마우스 위치에 배치한다.
    private void UpdateScopeUiPosition(Vector2 pointerPosition)
    {
        if (targetCanvas == null)
        {
            scopeRoot.position = pointerPosition;
            return;
        }

        RectTransform canvasRect = targetCanvas.transform as RectTransform;

        if (canvasRect == null)
        {
            scopeRoot.position = pointerPosition;
            return;
        }

        Camera uiCamera = targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : targetCanvas.worldCamera;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, pointerPosition, uiCamera, out Vector2 localPoint))
        {
            scopeRoot.anchoredPosition = localPoint;
        }
    }

    // sourceCamera를 기준으로 scopeCamera를 마우스 방향에 맞추고, magnification 값만큼 시야각을 줄여 확대 효과를 만든다.
    private void UpdateScopeCamera(Vector2 pointerPosition)
    {
        scopeCamera.CopyFrom(sourceCamera);
        scopeCamera.enabled = true;
        scopeCamera.targetTexture = scopeTexture;
        scopeCamera.fieldOfView = Mathf.Max(1f, sourceCamera.fieldOfView / magnification);
        scopeCamera.transform.SetPositionAndRotation(sourceCamera.transform.position,
                                                     Quaternion.LookRotation(sourceCamera.ScreenPointToRay(pointerPosition).direction,
                                                                             sourceCamera.transform.up));
    }

    // Unity Input System의 현재 마우스 화면 좌표를 읽는다. 마우스가 없으면 안전하게 0 좌표를 반환한다.
    private Vector2 ReadPointerPosition()
    {
        if (Mouse.current != null)
        {
            return Mouse.current.position.ReadValue();
        }

        return Vector2.zero;
    }

    // 스코프가 켜진 상태에서 우클릭으로 조준 모드를 취소하기 위해 현재 프레임의 우클릭 입력을 확인한다.
    private bool WasRightMousePressedThisFrame()
    {
        return Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
    }

    // 원형 마스크용 흰색 Sprite를 런타임에 만든다. 원 밖 픽셀은 투명해서 확대 화면이 원 밖으로 보이지 않는다.
    private Sprite CreateCircleMaskSprite()
    {
        const int textureSize = 128;
        Texture2D texture = new(textureSize, textureSize, TextureFormat.RGBA32, false)
        {
            name = "OpeningShotScopeCircleMask"
        };

        float radius = (textureSize - 2f) * 0.5f;
        Vector2 center = new((textureSize - 1f) * 0.5f, (textureSize - 1f) * 0.5f);

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                byte alpha = distance <= radius ? byte.MaxValue : byte.MinValue;
                texture.SetPixel(x, y, new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, alpha));
            }
        }

        texture.Apply(false, true);
        runtimeMaskSprite = Sprite.Create(texture,
                                          new Rect(0f, 0f, textureSize, textureSize),
                                          new Vector2(0.5f, 0.5f),
                                          textureSize);
        runtimeMaskSprite.name = "OpeningShotScopeCircleMask";
        return runtimeMaskSprite;
    }
}
