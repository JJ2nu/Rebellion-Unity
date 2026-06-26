using System.Collections;
using UnityEngine;

/// <summary>
/// Stage Scene의 기존 매니저를 캠페인 흐름에 연결하고, 시뮬레이션 결과를 플레이어가 확정할 때까지 보관한다.
/// 캠페인 밖에서 Stage Scene을 단독 실행할 때는 기존 Dialogue Scene 직접 이동을 대체 경로로 사용한다.
/// </summary>
public sealed class StageSceneFlowBinder : MonoBehaviour
{
    private const string DialogueSceneName = "Dialogue";

    [Header("Bindings")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private SimulationController simulationController;
    [SerializeField] private AudioDramaPlayer audioDramaPlayer;

    private GameFlowManager flowManager;
    private bool isBound;

    // 결과 이벤트 직후 Scene을 바꾸지 않고 Retry, 판정 다이얼로그, 최종 확정 UI가 같은 값을 공유한다.
    private bool hasPendingSimulationResult;
    private SimulationController.SimulationResult pendingSimulationResult;

    public bool HasPendingSimulationResult => hasPendingSimulationResult;

    private void OnEnable()
    {
        EnsureBindings();
        SubscribeSimulationResult();
        TryRegisterWithFlowManager();
    }

    private void Start()
    {
        // GameFlowManager와 기존 Singleton의 초기화 순서가 Scene마다 달라 Start에서도 한 번 더 연결을 시도한다.
        EnsureBindings();
        SubscribeSimulationResult();
        TryRegisterWithFlowManager();
    }

    private void OnDestroy()
    {
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

    public void LoadStage(string stagePath)
    {
        EnsureBindings();

        if (gameManager == null)
        {
            Debug.LogWarning("[StageSceneFlowBinder] GameManager is not assigned.", this);
            return;
        }

        gameManager.LoadStage(stagePath);
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

        // pending 결과는 실제 확정 시점에만 지운다. 패널을 여는 첫 Confirm에서는 유지되어야 한다.
        ClearPendingSimulationResult();
        simulationController?.MarkSimulationConfirmed();
        if (flowManager != null)
        {
            flowManager.HandleStageSimulationFinished(this, confirmedResult);
            return;
        }

        SceneTransitionOverlay.Instance.LoadScene(DialogueSceneName, EndLoadedStage);
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
