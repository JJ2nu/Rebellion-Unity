using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class GameFlowManager : MonoBehaviour
{
    private const string StageSceneName = "Stage";
    private const string DialogueSceneName = "Dialogue";
    private const string StagePathPrefix = "Stages/";
    private const string StagePathSuffix = ".json";
    private const string Stage001 = "stage_001";
    private const string Stage002 = "stage_002";
    private const string Stage003 = "stage_003";
    private const string Stage004 = "stage_004";
    private const string Stage005 = "stage_005";
    private const string Stage006 = "stage_006";
    private const string Stage007 = "stage_007";
    private const string Stage008 = "stage_008";
    private const string Stage009 = "stage_009";
    private const string AudioDramaStage1 = "1";
    private const string AudioDramaStage4 = "4";
    private const string AudioDramaStage7 = "7";
    private const string BadEndingAudioStageId = "BadEnding";
    private const string GoodEndingAudioStageId = "GoodEnding";

    private static readonly CampaignStageStep[] CampaignStages =
    {
        new(Stage001, AudioDramaStage1),
        new(Stage002, null),
        new(Stage003, null),
        new(Stage004, AudioDramaStage4),
        new(Stage005, null),
        new(Stage006, null),
        new(Stage007, AudioDramaStage7),
        new(Stage008, null),
        new(Stage009, null),
    };

    public static GameFlowManager Instance { get; private set; }
    public static bool HasInstance => Instance != null;

    private StageSceneFlowBinder currentStageBinder;
    private DialogueSceneFlowBinder currentDialogueBinder;
    private Coroutine stageSceneRoutine;
    private Coroutine endingRoutine;
    private string currentStageId;
    private string nextStageAfterDialogue;
    private string pendingDialogueLevel;
    private SimulationController.SimulationResult pendingDialogueResult;
    private bool isCampaignRunning;

    public static GameFlowManager EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        GameObject managerObject = new GameObject(nameof(GameFlowManager));
        return managerObject.AddComponent<GameFlowManager>();
    }

    public static void StartNewCampaign()
    {
        EnsureInstance().BeginCampaign();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void BeginCampaign()
    {
        isCampaignRunning = true;
        currentStageId = Stage001;
        nextStageAfterDialogue = null;
        pendingDialogueLevel = null;

        LoadScene(StageSceneName);
    }

    public void RegisterStageScene(StageSceneFlowBinder binder)
    {
        if (!isCampaignRunning || binder == null)
        {
            return;
        }

        currentStageBinder?.Unbind();
        currentStageBinder = binder;
        currentStageBinder.Bind(this);

        if (stageSceneRoutine != null)
        {
            StopCoroutine(stageSceneRoutine);
        }

        stageSceneRoutine = StartCoroutine(PrepareStageSceneRoutine(currentStageBinder));
    }

    public void RegisterDialogueScene(DialogueSceneFlowBinder binder)
    {
        if (!isCampaignRunning || binder == null)
        {
            return;
        }

        currentDialogueBinder?.Unbind();
        currentDialogueBinder = binder;
        currentDialogueBinder.Bind(this);

        if (string.IsNullOrWhiteSpace(pendingDialogueLevel))
        {
            Debug.LogWarning("[GameFlowManager] Dialogue scene loaded without pending dialogue data.", this);
            return;
        }

        currentDialogueBinder.Play(pendingDialogueLevel, pendingDialogueResult);
    }

    public void HandleStageSimulationFinished(StageSceneFlowBinder sender, SimulationController.SimulationResult result)
    {
        if (!isCampaignRunning || sender != currentStageBinder)
        {
            return;
        }

        if (result == SimulationController.SimulationResult.Lose ||
            result == SimulationController.SimulationResult.AllyDeadLose)
        {
            Debug.Log($"[GameFlowManager] Stage ended with {result}. Staying on Stage for retry.", this);
            return;
        }

        if (currentStageId == Stage008)
        {
            PlayEndingOnCurrentStage(BadEndingAudioStageId);
            return;
        }

        if (currentStageId == Stage009)
        {
            PlayEndingOnCurrentStage(HasElizaDeath(result) ? BadEndingAudioStageId : GoodEndingAudioStageId);
            return;
        }

        ResolveDialogueAndNextStage(result);
        currentStageBinder.EndLoadedStage();
        LoadScene(DialogueSceneName);
    }

    public void HandleDialogueNextStageRequested(DialogueSceneFlowBinder sender)
    {
        if (!isCampaignRunning || sender != currentDialogueBinder)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(nextStageAfterDialogue))
        {
            Debug.LogWarning("[GameFlowManager] Dialogue requested next stage, but no next stage is pending.", this);
            return;
        }

        currentStageId = nextStageAfterDialogue;
        nextStageAfterDialogue = null;
        pendingDialogueLevel = null;
        LoadScene(StageSceneName);
    }

    private IEnumerator PrepareStageSceneRoutine(StageSceneFlowBinder binder)
    {
        CampaignStageStep step = FindStageStep(currentStageId);
        if (step == null)
        {
            Debug.LogWarning($"[GameFlowManager] Unknown campaign stage: {currentStageId}", this);
            yield break;
        }

        if (!string.IsNullOrWhiteSpace(step.IntroAudioDramaStageId))
        {
            yield return binder.PlayAudioDramaAndWait(step.IntroAudioDramaStageId);
        }

        binder.LoadStage(step.StagePath);
        stageSceneRoutine = null;
    }

    private void ResolveDialogueAndNextStage(SimulationController.SimulationResult result)
    {
        pendingDialogueResult = result;

        switch (currentStageId)
        {
            case Stage001:
                pendingDialogueLevel = Stage001;
                nextStageAfterDialogue = Stage002;
                break;
            case Stage002:
                pendingDialogueLevel = Stage002;
                nextStageAfterDialogue = Stage003;
                break;
            case Stage003:
                pendingDialogueLevel = Stage003;
                nextStageAfterDialogue = Stage004;
                break;
            case Stage004:
                pendingDialogueLevel = Stage004;
                nextStageAfterDialogue = Stage005;
                break;
            case Stage005:
                pendingDialogueLevel = Stage005;
                nextStageAfterDialogue = Stage006;
                break;
            case Stage006:
                pendingDialogueLevel = Stage006;
                nextStageAfterDialogue = Stage007;
                break;
            case Stage007:
                pendingDialogueLevel = HasElizaDeath(result) ? Stage008 : Stage007;
                nextStageAfterDialogue = HasElizaDeath(result) ? Stage008 : Stage009;
                break;
            default:
                pendingDialogueLevel = currentStageId;
                nextStageAfterDialogue = null;
                break;
        }
    }

    private void PlayEndingOnCurrentStage(string endingAudioStageId)
    {
        if (endingRoutine != null)
        {
            StopCoroutine(endingRoutine);
        }

        endingRoutine = StartCoroutine(PlayEndingRoutine(endingAudioStageId));
    }

    private IEnumerator PlayEndingRoutine(string endingAudioStageId)
    {
        currentStageBinder.EndLoadedStage();
        yield return currentStageBinder.PlayAudioDramaAndWait(endingAudioStageId);
        Debug.Log($"[GameFlowManager] Campaign ending finished: {endingAudioStageId}", this);
        isCampaignRunning = false;
        currentStageId = null;
        pendingDialogueLevel = null;
        nextStageAfterDialogue = null;
        endingRoutine = null;
    }

    private static bool HasElizaDeath(SimulationController.SimulationResult result)
    {
        return result == SimulationController.SimulationResult.CivilianDeadWin ||
               result == SimulationController.SimulationResult.BothDeadWin;
    }

    private static CampaignStageStep FindStageStep(string stageId)
    {
        for (int index = 0; index < CampaignStages.Length; index++)
        {
            CampaignStageStep step = CampaignStages[index];
            if (step.StageId == stageId)
            {
                return step;
            }
        }

        return null;
    }

    private static void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    private sealed class CampaignStageStep
    {
        public string StageId { get; }
        public string StagePath { get; }
        public string IntroAudioDramaStageId { get; }

        public CampaignStageStep(string stageId, string introAudioDramaStageId)
        {
            StageId = stageId;
            StagePath = $"{StagePathPrefix}{stageId}{StagePathSuffix}";
            IntroAudioDramaStageId = introAudioDramaStageId;
        }
    }
}
