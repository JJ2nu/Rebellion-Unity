using System.Collections;
using UnityEngine;

/// <summary>
/// Stage Scene의 기존 매니저를 캠페인 흐름에 연결하고, 시뮬레이션 결과를 플레이어가 확정할 때까지 보관한다.
/// Stage Scene 단독 실행에서는 GameManager가 자동 로드한 Stage ID로 캠페인 흐름을 시작한다.
/// </summary>
public sealed class StageSceneFlowBinder : MonoBehaviour
{
    [Header("Bindings")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private SimulationController simulationController;
    [SerializeField] private AudioDramaPlayer audioDramaPlayer;
    [SerializeField] private StageTutorialController stageTutorialController;

    private GameFlowManager flowManager;
    private bool isBound;

    // 결과 이벤트 직후 Scene을 바꾸지 않고 Retry, 판정 다이얼로그, 최종 확정 UI가 같은 값을 공유한다.
    private bool hasPendingSimulationResult;
    private SimulationController.SimulationResult pendingSimulationResult;

    public bool HasPendingSimulationResult => hasPendingSimulationResult;
    public bool WasLastAudioDramaSkipped =>
        audioDramaPlayer != null && audioDramaPlayer.WasLastPlaybackSkipped;

    private void OnEnable()
    {
        EnsureBindings();
        SubscribeInitialStageCampaignStart();
        SubscribeSimulationResult();
        TryRegisterWithFlowManager();
        TryStartFromAlreadyLoadedStage();
    }

    private void Start()
    {
        // GameFlowManager와 기존 Singleton의 초기화 순서가 Scene마다 달라 Start에서도 한 번 더 연결을 시도한다.
        EnsureBindings();
        SubscribeInitialStageCampaignStart();
        SubscribeSimulationResult();
        TryRegisterWithFlowManager();
    }

    private void OnDisable()
    {
        UnsubscribeInitialStageCampaignStart();
    }

    private void OnDestroy()
    {
        UnsubscribeInitialStageCampaignStart();
        Unbind();
    }

    public void Bind(GameFlowManager manager)
    {
        flowManager = manager;
        EnsureBindings();

        if (simulationController == null)
        {
            Debug.LogWarning("[StageSceneFlowBinder] SimulationController is not assigned.", this);
            return;
        }

        SubscribeSimulationResult();
        // 새 Stage Binder가 캠페인에 연결될 때 이전 실행 결과가 남지 않도록 초기화한다.
        ClearPendingSimulationResult();
        isBound = true;
    }

    public void Unbind()
    {
        if (!isBound)
        {
            return;
        }

        if (simulationController != null)
        {
            simulationController.SimulationFinished -= HandleSimulationFinished;
        }

        flowManager = null;
        isBound = false;
    }

    public void LoadStage(string stagePath, bool playMapAudioImmediately = true)
    {
        EnsureBindings();

        if (gameManager == null)
        {
            Debug.LogWarning("[StageSceneFlowBinder] GameManager is not assigned.", this);
            return;
        }

        gameManager.LoadStage(stagePath, playMapAudioImmediately);
    }

    public void PlayCurrentMapAudio()
    {
        EnsureBindings();
        gameManager?.PlayCurrentMapAudio();
    }

    public IEnumerator PlayStageTutorialAndWait()
    {
        if (stageTutorialController == null)
        {
            Debug.LogWarning("[StageSceneFlowBinder] StageTutorialController is not assigned.", this);
            yield break;
        }

        // GameFlowManager는 이 대기가 끝난 뒤에만 StageReady와 Demo 타이머 시작을 기록한다.
        yield return stageTutorialController.ShowAndWait();
    }

    public void EndLoadedStage()
    {
        EnsureBindings();
        gameManager?.EndStage();
    }

    public IEnumerator PlayAudioDramaAndWait(string stageId)
    {
        EnsureBindings();

        if (audioDramaPlayer == null)
        {
            Debug.LogWarning($"[StageSceneFlowBinder] AudioDramaPlayer is not assigned. Stage ID: {stageId}", this);
            yield break;
        }

        yield return audioDramaPlayer.PlayByStageIdAndWait(stageId);
    }

    public void PlayAudioDrama(string stageId)
    {
        EnsureBindings();

        if (audioDramaPlayer == null)
        {
            Debug.LogWarning($"[StageSceneFlowBinder] AudioDramaPlayer is not assigned. Stage ID: {stageId}", this);
            return;
        }

        audioDramaPlayer.PlayByStageId(stageId);
    }

    public void PlayEndingAudioDrama(string stageId)
    {
        EnsureBindings();

        if (audioDramaPlayer == null)
        {
            Debug.LogWarning($"[StageSceneFlowBinder] AudioDramaPlayer is not assigned. Stage ID: {stageId}", this);
            return;
        }

        audioDramaPlayer.PlayEndingByStageId(stageId);
    }

    public IEnumerator WaitForAudioDramaToBecomeVisible()
    {
        EnsureBindings();

        if (audioDramaPlayer == null)
        {
            yield break;
        }

        while (audioDramaPlayer.IsPlaying && !audioDramaPlayer.IsFullyVisible)
        {
            yield return null;
        }
    }

    public IEnumerator WaitForAudioDramaToFinish()
    {
        EnsureBindings();

        if (audioDramaPlayer == null)
        {
            yield break;
        }

        while (audioDramaPlayer.IsPlaying)
        {
            yield return null;
        }
    }

    public bool IsAudioDramaPlayingAndVisible()
    {
        EnsureBindings();
        return audioDramaPlayer != null &&
               audioDramaPlayer.IsPlaying &&
               audioDramaPlayer.IsFullyVisible;
    }

    public void ReleaseHeldEndingAudioDrama()
    {
        EnsureBindings();
        audioDramaPlayer?.ReleaseHeldEndingPanel();
    }

    public void ConfirmSimulationResult()
    {
        EnsureBindings();

        if (!hasPendingSimulationResult)
        {
            Debug.LogWarning("[StageSceneFlowBinder] Confirm requested before a simulation result is ready.", this);
            return;
        }

        SimulationController.SimulationResult confirmedResult = pendingSimulationResult;
        if (confirmedResult == SimulationController.SimulationResult.Lose ||
            confirmedResult == SimulationController.SimulationResult.AllyDeadLose)
        {
            Debug.LogWarning($"[StageSceneFlowBinder] Failure result cannot be confirmed: {confirmedResult}", this);
            return;
        }

        if (flowManager == null)
        {
            Debug.LogError(
                "[StageSceneFlowBinder] A simulation result was confirmed without campaign flow context. " +
                "Enable Auto Start Campaign Flow on GameManager or start the campaign from Title.",
                this);
            return;
        }

        // pending 결과는 실제 확정 시점에만 지운다. 패널을 여는 첫 Confirm에서는 유지되어야 한다.
        ClearPendingSimulationResult();
        simulationController?.MarkSimulationConfirmed();
        flowManager.HandleStageSimulationFinished(this, confirmedResult);
    }

    public void ClearPendingSimulationResult()
    {
        hasPendingSimulationResult = false;
        pendingSimulationResult = SimulationController.SimulationResult.Lose;
    }

    public void StoreSimulationResult(SimulationController.SimulationResult result)
    {
        pendingSimulationResult = result;
        hasPendingSimulationResult = true;
    }

    public bool TryGetPendingSimulationResult(out SimulationController.SimulationResult result)
    {
        result = pendingSimulationResult;
        return hasPendingSimulationResult;
    }

    private void HandleSimulationFinished(SimulationController.SimulationResult result)
    {
        StoreSimulationResult(result);
    }

    private void SubscribeSimulationResult()
    {
        if (simulationController == null)
        {
            return;
        }

        simulationController.SimulationFinished -= HandleSimulationFinished;
        simulationController.SimulationFinished += HandleSimulationFinished;
    }

    private void SubscribeInitialStageCampaignStart()
    {
        if (gameManager == null)
        {
            return;
        }

        gameManager.InitialStageCampaignStartRequested -= HandleInitialStageCampaignStartRequested;
        gameManager.InitialStageCampaignStartRequested += HandleInitialStageCampaignStartRequested;
    }

    private void UnsubscribeInitialStageCampaignStart()
    {
        if (gameManager != null)
        {
            gameManager.InitialStageCampaignStartRequested -= HandleInitialStageCampaignStartRequested;
        }
    }

    private void HandleInitialStageCampaignStartRequested(string stageId)
    {
        // Title에서 시작한 캠페인은 이미 올바른 Stage 상태를 갖고 있으므로 단독 실행 초기화로 덮어쓰지 않는다.
        if (GameFlowManager.HasActiveCampaign)
        {
            TryRegisterWithFlowManager();
            return;
        }

        GameFlowManager.TryStartFromLoadedStage(stageId, this);
    }

    private void TryStartFromAlreadyLoadedStage()
    {
        if (GameFlowManager.HasActiveCampaign || gameManager?.AutoStartCampaignFlow != true)
        {
            return;
        }

        // Script 실행 순서상 GameManager.Start가 먼저 끝났다면 초기 이벤트를 놓칠 수 있으므로 현재 로드 결과로 보완한다.
        string loadedStageId = StageManager.Instance?.CurrentStageId;
        if (!string.IsNullOrWhiteSpace(loadedStageId))
        {
            GameFlowManager.TryStartFromLoadedStage(loadedStageId, this);
        }
    }

    private void TryRegisterWithFlowManager()
    {
        if (!isBound && GameFlowManager.HasInstance)
        {
            GameFlowManager.Instance.RegisterStageScene(this);
        }
    }

    private void EnsureBindings()
    {
        if (gameManager == null)
        {
            gameManager = GameManager.Instance;
        }

        if (simulationController == null)
        {
            simulationController = SimulationController.Instance;
        }
    }
}
