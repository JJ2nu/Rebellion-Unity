using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Stage의 Play, Retry, Confirm 버튼을 시뮬레이션 실행 상태와 보관된 결과에 맞춰 전환한다.
/// 결과 확정 전까지 배치 모드를 잠그고, 완전 승리 외 결과는 판정 다이얼로그를 거쳐 기존 확정/Retry 흐름으로 보낸다.
/// </summary>
public sealed class StageSimulationControls : MonoBehaviour
{
    [Header("Bindings")]
    [SerializeField] private SimulationController simulationController;
    [SerializeField] private StageSceneFlowBinder stageSceneFlowBinder;
    [SerializeField] private Button playButton;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button confirmButton;
    [SerializeField] private ResultDialogController resultDialogController;

    [Header("Confirm Override")]
    [SerializeField] private bool useConfirmOverride;
    [SerializeField] private UnityEvent confirmOverride;

    private Image playButtonImage;
    private Sprite playActiveSprite;
    private Sprite playInactiveSprite;

    private void Awake()
    {
        EnsureBindings();
        CachePlayButtonSprites();
        MatchConfirmButtonToPlayButton();
        ApplyState();
    }

    private void OnEnable()
    {
        EnsureBindings();

        if (simulationController != null)
        {
            simulationController.RunningStateChanged -= HandleRunningStateChanged;
            simulationController.RunningStateChanged += HandleRunningStateChanged;
            simulationController.SimulationFinished -= HandleSimulationFinished;
            simulationController.SimulationFinished += HandleSimulationFinished;
        }

        playButton?.onClick.AddListener(StartSimulation);
        retryButton?.onClick.AddListener(RetrySimulation);
        confirmButton?.onClick.AddListener(ConfirmSimulation);
        ApplyState();
    }

    private void OnDisable()
    {
        if (simulationController != null)
        {
            simulationController.RunningStateChanged -= HandleRunningStateChanged;
            simulationController.SimulationFinished -= HandleSimulationFinished;
        }

        playButton?.onClick.RemoveListener(StartSimulation);
        retryButton?.onClick.RemoveListener(RetrySimulation);
        confirmButton?.onClick.RemoveListener(ConfirmSimulation);
    }

    public void StartSimulation()
    {
        EnsureBindings();

        if (IsSimulationRunningOrPendingResult())
        {
            ApplyState();
            return;
        }

        simulationController?.StartSimulation();
        ApplyState();
    }

    public void RetrySimulation()
    {
        EnsureBindings();
        stageSceneFlowBinder?.ClearPendingSimulationResult();
        simulationController?.RetrySimulation();
        ApplyState();
    }

    public void ConfirmSimulation()
    {
        EnsureBindings();

        if (useConfirmOverride)
        {
            confirmOverride?.Invoke();
            return;
        }

        if (stageSceneFlowBinder == null ||
            !stageSceneFlowBinder.TryGetPendingSimulationResult(out SimulationController.SimulationResult result))
        {
            Debug.LogWarning("[StageSimulationControls] Confirm requested before a simulation result is ready.", this);
            ApplyState();
            return;
        }

        // 완전 승리는 판정 패널을 생략하고, 나머지 결과만 플레이어 선택을 위해 패널에 전달한다.
        if (result == SimulationController.SimulationResult.PerfectWin)
        {
            stageSceneFlowBinder.ConfirmSimulationResult();
            ApplyState();
            return;
        }

        if (resultDialogController == null)
        {
            Debug.LogWarning("[StageSimulationControls] ResultDialogController is not assigned.", this);
            return;
        }

        if (!resultDialogController.IsVisible)
        {
            string stageId = StageManager.Instance != null ? StageManager.Instance.CurrentStageId : string.Empty;
            resultDialogController.Show(stageId, result);
        }

        ApplyState();
    }

    public void MatchConfirmButtonToPlayButton()
    {
        if (playButton == null || confirmButton == null)
        {
            return;
        }

        RectTransform playRect = playButton.transform as RectTransform;
        RectTransform confirmRect = confirmButton.transform as RectTransform;
        if (playRect == null || confirmRect == null)
        {
            return;
        }

        confirmRect.anchorMin = playRect.anchorMin;
        confirmRect.anchorMax = playRect.anchorMax;
        confirmRect.pivot = playRect.pivot;
        confirmRect.anchoredPosition = playRect.anchoredPosition;
        confirmRect.sizeDelta = playRect.sizeDelta;
        confirmRect.localRotation = playRect.localRotation;
        confirmRect.localScale = playRect.localScale;
    }

    private void HandleRunningStateChanged(bool _)
    {
        ApplyState();
    }

    private void HandleSimulationFinished(SimulationController.SimulationResult _)
    {
        EnsureBindings();

        // Binder의 이벤트 구독 순서와 관계없이 버튼 갱신 전에 pending 결과가 존재하도록 보장한다.
        stageSceneFlowBinder?.StoreSimulationResult(_);
        ApplyState();
    }

    private void ApplyState()
    {
        EnsureBindings();
        CachePlayButtonSprites();

        // 실행 중에는 Play를 비활성 스프라이트로 남기고, 결과가 생긴 뒤에만 Retry/Confirm으로 교체한다.
        bool isSimulationMode = simulationController != null && simulationController._isRunning;
        bool hasSimulationResult = stageSceneFlowBinder != null && stageSceneFlowBinder.HasPendingSimulationResult;

        if (playButton != null)
        {
            playButton.gameObject.SetActive(!hasSimulationResult);
            playButton.interactable = !isSimulationMode && !hasSimulationResult;
            SetPlayButtonSprite(isSimulationMode ? playInactiveSprite : playActiveSprite);
        }

        if (retryButton != null)
        {
            retryButton.gameObject.SetActive(hasSimulationResult);
            retryButton.interactable = hasSimulationResult;
        }

        if (confirmButton != null)
        {
            confirmButton.gameObject.SetActive(hasSimulationResult);
            confirmButton.interactable = hasSimulationResult;
        }
    }

    private void EnsureBindings()
    {
        if (simulationController == null)
        {
            simulationController = SimulationController.Instance;
        }

        if (stageSceneFlowBinder == null)
        {
            stageSceneFlowBinder = FindAnyObjectByType<StageSceneFlowBinder>();
        }
    }

    private bool IsSimulationRunningOrPendingResult()
    {
        bool isSimulationMode = simulationController != null && simulationController._isRunning;
        bool hasSimulationResult = stageSceneFlowBinder != null && stageSceneFlowBinder.HasPendingSimulationResult;
        return isSimulationMode || hasSimulationResult;
    }

    private void CachePlayButtonSprites()
    {
        if (playButton == null)
        {
            return;
        }

        if (playButtonImage == null)
        {
            playButtonImage = playButton.targetGraphic as Image;
        }

        if (playActiveSprite == null && playButtonImage != null)
        {
            playActiveSprite = playButtonImage.sprite;
        }

        SpriteState spriteState = playButton.spriteState;
        if (playInactiveSprite == null)
        {
            playInactiveSprite = spriteState.selectedSprite != null
                ? spriteState.selectedSprite
                : spriteState.pressedSprite;
        }

        if (playInactiveSprite != null && spriteState.disabledSprite == null)
        {
            spriteState.disabledSprite = playInactiveSprite;
            playButton.spriteState = spriteState;
        }

        ColorBlock colors = playButton.colors;
        if (colors.disabledColor.a < 1f || colors.disabledColor.r < 1f || colors.disabledColor.g < 1f || colors.disabledColor.b < 1f)
        {
            colors.disabledColor = Color.white;
            playButton.colors = colors;
        }
    }

    private void SetPlayButtonSprite(Sprite sprite)
    {
        if (playButtonImage == null || sprite == null)
        {
            return;
        }

        playButtonImage.sprite = sprite;
    }
}
