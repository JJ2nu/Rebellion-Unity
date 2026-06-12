using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class StageSceneFlowBinder : MonoBehaviour
{
    private const string DialogueSceneName = "Dialogue";

    [Header("Bindings")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private SimulationController simulationController;
    [SerializeField] private AudioDramaPlayer audioDramaPlayer;

    private GameFlowManager flowManager;
    private bool isBound;
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

        ClearPendingSimulationResult();
        simulationController?.MarkSimulationConfirmed();
        if (flowManager != null)
        {
            flowManager.HandleStageSimulationFinished(this, confirmedResult);
            return;
        }

        SceneManager.LoadScene(DialogueSceneName);
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
