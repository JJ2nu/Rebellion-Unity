using UnityEngine;

/// <summary>
/// Dialogue Scene의 DialoguePlayer를 캠페인 진행 상태와 연결한다.
/// DialoguePlayer는 대사 표시만 담당하고 다음 Stage 결정은 GameFlowManager에 위임한다.
/// </summary>
public sealed class DialogueSceneFlowBinder : MonoBehaviour
{
    [Header("Bindings")]
    [SerializeField] private DialoguePlayer dialoguePlayer;

    private GameFlowManager flowManager;
    private bool isBound;

    private void OnEnable()
    {
        TryRegisterWithFlowManager();
    }

    private void Start()
    {
        // GameFlowManager가 OnEnable 이후 생성되는 Scene 진입 순서도 지원한다.
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

        if (dialoguePlayer == null)
        {
            Debug.LogWarning("[DialogueSceneFlowBinder] DialoguePlayer is not assigned.", this);
            return;
        }

        dialoguePlayer.NextStageRequested -= HandleNextStageRequested;
        dialoguePlayer.NextStageRequested += HandleNextStageRequested;
        isBound = true;
    }

    public void Unbind()
    {
        if (!isBound)
        {
            return;
        }

        if (dialoguePlayer != null)
        {
            dialoguePlayer.NextStageRequested -= HandleNextStageRequested;
        }

        flowManager = null;
        isBound = false;
    }

    public void Play(string level, SimulationController.SimulationResult result)
    {
        EnsureBindings();

        if (dialoguePlayer == null)
        {
            Debug.LogWarning($"[DialogueSceneFlowBinder] Cannot play dialogue. Level: {level}, Result: {result}", this);
            return;
        }

        dialoguePlayer.Play(level, result);
    }

    private void HandleNextStageRequested()
    {
        flowManager?.HandleDialogueNextStageRequested(this);
    }

    private void TryRegisterWithFlowManager()
    {
        if (!isBound && GameFlowManager.HasInstance)
        {
            GameFlowManager.Instance.RegisterDialogueScene(this);
        }
    }

    private void EnsureBindings()
    {
        if (dialoguePlayer == null)
        {
            dialoguePlayer = GetComponentInChildren<DialoguePlayer>(true);
        }
    }
}
