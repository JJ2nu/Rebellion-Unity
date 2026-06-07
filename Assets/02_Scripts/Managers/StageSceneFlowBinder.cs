using System.Collections;
using UnityEngine;

public sealed class StageSceneFlowBinder : MonoBehaviour
{
    [Header("Bindings")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private SimulationController simulationController;
    [SerializeField] private AudioDramaPlayer audioDramaPlayer;

    private GameFlowManager flowManager;
    private bool isBound;

    private void OnEnable()
    {
        TryRegisterWithFlowManager();
    }

    private void Start()
    {
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

        simulationController.SimulationFinished -= HandleSimulationFinished;
        simulationController.SimulationFinished += HandleSimulationFinished;
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

    private void HandleSimulationFinished(SimulationController.SimulationResult result)
    {
        flowManager?.HandleStageSimulationFinished(this, result);
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
