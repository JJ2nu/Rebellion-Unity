using System.Collections;
using UnityEngine;

/// <summary>
/// 캠페인 진행 상태를 Scene 전환 사이에 보관하고 Stage와 Dialogue Scene의 Binder를 연결한다.
/// 각 Scene의 기존 매니저는 그대로 두고, 캠페인 순서와 분기 및 엔딩 뒤 Title 복귀만 이 객체가 담당한다.
/// </summary>
public sealed class GameFlowManager : MonoBehaviour
{
    private const string TitleSceneName = "Title";
    private const string StageSceneName = "Stage";
    private const string DialogueSceneName = "Dialogue";
#if UNITY_EDITOR || REBELLION_DEMO_BUILD
    private const string DemoTimeOverSceneName = "DemoTimeOver";
#endif
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
    private const float IntroAudioDramaStartupDelay = 0.25f;

    // 첫 Campaign 로딩은 실제로 관찰 가능한 Scene·Binder·Stage 준비 경계만 누적한다.
    private const float CampaignLoadingSceneLoadWeight = 0.45f;
    private const float CampaignLoadingSceneActivationAndBinderWeight = 0.15f;
    private const float CampaignLoadingStagePreparationWeight = 0.40f;

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
    public static bool HasActiveCampaign => Instance != null && Instance.isCampaignRunning;
    public static bool HasCompletedEndingTitleTransition =>
        Instance != null && Instance.hasCompletedEndingTitleTransition;
    public string CurrentStageId => currentStageId;

    private StageSceneFlowBinder currentStageBinder;
    private DialogueSceneFlowBinder currentDialogueBinder;
    private TitleBackgroundTransition currentTitleBackgroundTransition;
    private Coroutine stageSceneRoutine;
    private Coroutine endingRoutine;
    private Coroutine campaignSceneRoutine;
    private string currentStageId;

    // Stage Scene이 내려간 뒤 Dialogue Scene이 올라오므로 다음 대사 정보를 Scene 전환 전에 보관한다.
    private string nextStageAfterDialogue;
    private string pendingDialogueLevel;
    private SimulationController.SimulationResult pendingDialogueResult;
    private bool isCampaignRunning;
    private bool isLoadingCampaignScene;
    // SaveData가 도입되기 전까지는 DontDestroyOnLoad 매니저의 현재 실행 세션에서만 완료 상태를 보관한다.
    private bool hasCompletedEndingTitleTransition;

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

#if UNITY_EDITOR || DEVELOPMENT_BUILD || REBELLION_DEMO_BUILD
    public static bool TryStartDebugCampaign(string stageId)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        GameFlowManager manager = EnsureInstance();
        bool started = manager.TryBeginCampaignAtStage(
            stageId,
            PlaytestLogger.DebugHotkeyEntrySource,
            false);
        if (started)
        {
            Debug.Log($"[GameFlowManager] Debug campaign requested: {stageId}", manager);
        }

        return started;
#else
        return false;
#endif
    }

    public static void ReturnToTitleForDebug()
    {
        GameFlowManager manager = EnsureInstance();
        manager.BeginForcedSceneReturn(TitleSceneName, "f12DebugHotkey");
    }
#endif

#if UNITY_EDITOR || REBELLION_DEMO_BUILD
    public static void ReturnToDemoTimeOver()
    {
        GameFlowManager manager = EnsureInstance();
        manager.BeginForcedSceneReturn(DemoTimeOverSceneName, "demoTimeExpired");
    }
#endif

    public static bool TryStartFromLoadedStage(string stageId, StageSceneFlowBinder binder)
    {
        return EnsureInstance().TryBeginCampaignFromLoadedStage(stageId, binder);
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
        // Title의 Campaign 버튼만 첫 진입 준비 상태를 플레이어에게 보여 준다.
        TryBeginCampaignAtStage(
            Stage001,
            PlaytestLogger.TitleCampaignEntrySource,
            true);
    }

    private bool TryBeginCampaignAtStage(
        string stageId,
        string entrySource,
        bool showCampaignLoading)
    {
        if (FindStageStep(stageId) == null)
        {
            Debug.LogWarning($"[GameFlowManager] Cannot start campaign from unknown Stage: {stageId}", this);
            return false;
        }

        // Stage가 실제 로드될 때 같은 출처로 세션을 시작할 수 있도록 Title·디버그 진입 경로를 먼저 구분한다.
        PlaytestLogger.PrepareCampaign(entrySource);

        // 디버그 단축키로 실행 중인 캠페인을 교체할 때 이전 Scene·Stage·엔딩 코루틴이 새 흐름을 덮지 않게 정리한다.
        StopActiveCampaignRoutines();
        if (currentStageBinder != null)
        {
            // Stage끼리 바로 이동해도 기존 피스 풀과 HUD가 파괴된 이전 Scene 카메라를 보관하지 않게 정상 종료한다.
            currentStageBinder.EndLoadedStage();
            currentStageBinder.Unbind();
        }

        if (currentDialogueBinder != null)
        {
            currentDialogueBinder.Unbind();
        }

        currentStageBinder = null;
        currentDialogueBinder = null;
        isCampaignRunning = true;
        isLoadingCampaignScene = false;
        currentStageId = stageId;
        nextStageAfterDialogue = null;
        pendingDialogueLevel = null;

        LoadStageScene(showCampaignLoading);
        return true;
    }

    private bool TryBeginCampaignFromLoadedStage(string stageId, StageSceneFlowBinder binder)
    {
        if (isCampaignRunning)
        {
            return false;
        }

        if (binder == null)
        {
            Debug.LogWarning("[GameFlowManager] Cannot start from a loaded Stage without a StageSceneFlowBinder.", this);
            return false;
        }

        if (FindStageStep(stageId) == null)
        {
            Debug.LogWarning($"[GameFlowManager] Cannot start campaign from unknown Stage: {stageId}", this);
            return false;
        }

        PlaytestLogger.PrepareCampaign(PlaytestLogger.StandaloneStageEntrySource);

        // 이미 로드된 Stage를 다시 열지 않고 캠페인 위치와 Binder만 연결한다.
        isCampaignRunning = true;
        isLoadingCampaignScene = false;
        currentStageId = stageId;
        nextStageAfterDialogue = null;
        pendingDialogueLevel = null;
        currentDialogueBinder?.Unbind();
        currentDialogueBinder = null;
        BindStageScene(binder);
        PlaytestLogger.RecordStageEntered(stageId);
        stageSceneRoutine = StartCoroutine(CompleteLoadedStageStartRoutine(stageId, binder));

        Debug.Log($"[GameFlowManager] Campaign flow started from loaded Stage: {stageId}", this);
        return true;
    }

    private IEnumerator CompleteLoadedStageStartRoutine(string stageId, StageSceneFlowBinder binder)
    {
        // Stage Scene 단독 실행은 맵 오디오가 이미 재생 중이므로 다시 시작하지 않고 Stage 1만 튜토리얼을 기다린다.
        if (stageId == Stage001)
        {
            yield return binder.PlayStageTutorialAndWait();
        }

        if (binder != currentStageBinder || stageId != currentStageId)
        {
            yield break;
        }

        PlaytestLogger.RecordStageReady();
#if UNITY_EDITOR || REBELLION_DEMO_BUILD
        DemoSessionController.NotifyCampaignContentVisible();
#endif
        stageSceneRoutine = null;
    }

    private void StopActiveCampaignRoutines()
    {
        // 최신 Campaign·F12 요청이 이전 범용 로딩 화면을 덮어쓰지 않게 먼저 정리한다.
        SceneTransitionOverlay.Instance.HideLoading();

        if (stageSceneRoutine != null)
        {
            StopCoroutine(stageSceneRoutine);
            stageSceneRoutine = null;
        }

        if (endingRoutine != null)
        {
            StopCoroutine(endingRoutine);
            endingRoutine = null;
        }

        if (campaignSceneRoutine != null)
        {
            StopCoroutine(campaignSceneRoutine);
            campaignSceneRoutine = null;
        }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD || REBELLION_DEMO_BUILD
    private void BeginForcedSceneReturn(string destinationSceneName, string endTrigger)
    {
        // TimeOver 전환의 FadeIn 중 F12/0초 자동 복귀가 들어와도 최신 목적지 요청을 우선한다.
        StopActiveCampaignRoutines();
        campaignSceneRoutine = StartCoroutine(
            ReturnFromCampaignRoutine(destinationSceneName, endTrigger));
    }
#endif

    public void RegisterStageScene(StageSceneFlowBinder binder)
    {
        if (!isCampaignRunning || binder == null)
        {
            return;
        }

        BindStageScene(binder);

        if (isLoadingCampaignScene)
        {
            return;
        }

        if (stageSceneRoutine != null)
        {
            StopCoroutine(stageSceneRoutine);
        }

        stageSceneRoutine = StartCoroutine(PrepareStageSceneRoutine(currentStageBinder));
    }

    private void BindStageScene(StageSceneFlowBinder binder)
    {
        currentStageBinder?.Unbind();
        currentStageBinder = binder;
        currentStageBinder.Bind(this);
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

        if (isLoadingCampaignScene)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(pendingDialogueLevel))
        {
            Debug.LogWarning("[GameFlowManager] Dialogue scene loaded without pending dialogue data.", this);
            return;
        }

        currentDialogueBinder.Play(pendingDialogueLevel, pendingDialogueResult);
    }

    public void RegisterTitleBackgroundTransition(TitleBackgroundTransition transition)
    {
        if (transition != null)
        {
            currentTitleBackgroundTransition = transition;
        }
    }

    public void UnregisterTitleBackgroundTransition(TitleBackgroundTransition transition)
    {
        if (currentTitleBackgroundTransition == transition)
        {
            currentTitleBackgroundTransition = null;
        }
    }

    public void HandleStageSimulationFinished(StageSceneFlowBinder sender, SimulationController.SimulationResult result)
    {
        if (!isCampaignRunning || sender != currentStageBinder)
        {
            return;
        }

        // 실패 결과는 확정할 수 없으며 현재 Stage에서 Retry 선택을 기다린다.
        if (result == SimulationController.SimulationResult.Lose ||
            result == SimulationController.SimulationResult.AllyDeadLose)
        {
            Debug.Log($"[GameFlowManager] Stage ended with {result}. Staying on Stage for retry.", this);
            return;
        }

        // 성공 결과가 실제 확정된 시점만 완료로 집계해 결과 화면에서 이탈한 세션과 구분한다.
        PlaytestLogger.RecordStageCompleted(result.ToString());

        // 엔딩 Stage는 Dialogue Scene으로 나가지 않고 현재 Stage 위에서 오디오드라마를 마친다.
        if (currentStageId == Stage008)
        {
            PlaytestLogger.RecordStageExited(PlaytestLogger.EndingStartedStageExitReason);
            PlayEndingOnCurrentStage(BadEndingAudioStageId, result);
            return;
        }

        if (currentStageId == Stage009)
        {
            PlaytestLogger.RecordStageExited(PlaytestLogger.EndingStartedStageExitReason);
            PlayEndingOnCurrentStage(
                HasElizaDeath(result) ? BadEndingAudioStageId : GoodEndingAudioStageId,
                result);
            return;
        }

        PlaytestLogger.RecordStageExited(PlaytestLogger.ProgressedStageExitReason);
        PlaytestLogger.RecordDialogueEntered();
        ResolveDialogueAndNextStage(result);
        LoadDialogueSceneFromStage();
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
        LoadStageScene();
    }

    private IEnumerator PrepareStageSceneRoutine(
        StageSceneFlowBinder binder,
        bool showCampaignLoading = false)
    {
        CampaignStageStep step = FindStageStep(currentStageId);
        if (step == null)
        {
            Debug.LogWarning($"[GameFlowManager] Unknown campaign stage: {currentStageId}", this);
            yield break;
        }

        SceneTransitionOverlay overlay = SceneTransitionOverlay.Instance;
        if (!overlay.IsFullyOpaque)
        {
            yield return overlay.FadeOut();
        }

        bool hasIntroAudioDrama = !string.IsNullOrWhiteSpace(step.IntroAudioDramaStageId);
        binder.LoadStage(step.StagePath, !hasIntroAudioDrama);
        bool stageLoaded = StageManager.Instance != null &&
                           StageManager.Instance.CurrentStageId == currentStageId;
        if (stageLoaded)
        {
            PlaytestLogger.RecordStageEntered(currentStageId);
        }
        else if (showCampaignLoading)
        {
            // Binder는 있었지만 동기 Stage 로드가 실패했으므로 검정 로딩 화면과 기존 경고를 유지한다.
            Debug.LogWarning(
                "[GameFlowManager] Initial Campaign Stage did not become ready. " +
                "Keeping the loading overlay visible for diagnosis.",
                this);
            yield break;
        }

        yield return overlay.WaitForSceneSettled();

        if (showCampaignLoading)
        {
            // LoadStage 반환은 JSON·맵·피스 생성과 StageLoaded 구독자 실행이 끝난 실제 동기 경계다.
            overlay.SetLoadingProgress(
                CampaignLoadingSceneLoadWeight +
                CampaignLoadingSceneActivationAndBinderWeight +
                CampaignLoadingStagePreparationWeight);
            // 완료 값이 실제로 한 프레임 그려진 뒤에만 Prefab의 연출 홀드를 시작한다.
            yield return null;
            yield return overlay.WaitForLoadingCompletedHold();
            // 로딩 View가 살아 있는 동안에는 Stage 오디오드라마의 첫 프레임과 겹치지 않게 먼저 내린다.
            overlay.HideLoading();
        }

        if (hasIntroAudioDrama)
        {
            binder.PlayAudioDrama(step.IntroAudioDramaStageId);
            if (showCampaignLoading)
            {
                // 첫 Stage는 완료 홀드 뒤에만 인트로를 시작하고, 패널이 준비된 다음 전환막을 걷는다.
                yield return binder.WaitForAudioDramaToBecomeVisible();
            }
            else
            {
                yield return new WaitForSeconds(IntroAudioDramaStartupDelay);
            }
        }

        yield return overlay.FadeIn();

        if (stageLoaded && currentStageId != Stage001)
        {
#if UNITY_EDITOR || REBELLION_DEMO_BUILD
            // 튜토리얼이 없는 Stage는 기존처럼 화면이 보이는 순간부터 Demo 시간을 계산한다.
            DemoSessionController.NotifyCampaignContentVisible();
#endif
        }

        if (hasIntroAudioDrama)
        {
            // 오디오드라마가 끝나거나 스킵된 뒤에만 맵 BGM과 앰비언트를 시작한다.
            yield return binder.WaitForAudioDramaToFinish();
            binder.PlayCurrentMapAudio();
        }

        if (stageLoaded && currentStageId == Stage001)
        {
            // Stage 1은 맵 오디오를 먼저 시작한 뒤 튜토리얼이 닫힐 때까지 플레이 준비를 보류한다.
            yield return binder.PlayStageTutorialAndWait();

            if (binder != currentStageBinder || currentStageId != Stage001)
            {
                yield break;
            }

#if UNITY_EDITOR || REBELLION_DEMO_BUILD
            DemoSessionController.NotifyCampaignContentVisible();
#endif
        }

        if (stageLoaded)
        {
            PlaytestLogger.RecordStageReady();
        }

        stageSceneRoutine = null;
    }

    private void LoadStageScene(bool showCampaignLoading = false)
    {
        StartCampaignSceneRoutine(LoadStageSceneRoutine(showCampaignLoading));
    }

    private void LoadDialogueScene()
    {
        StartCampaignSceneRoutine(LoadDialogueSceneRoutine(true));
    }

    private void LoadDialogueSceneFromStage()
    {
        StartCampaignSceneRoutine(LoadDialogueSceneFromStageRoutine());
    }

    private void StartCampaignSceneRoutine(IEnumerator routine)
    {
        if (campaignSceneRoutine != null)
        {
            StopCoroutine(campaignSceneRoutine);
        }

        campaignSceneRoutine = StartCoroutine(routine);
    }

    private IEnumerator LoadStageSceneRoutine(bool showCampaignLoading)
    {
        isLoadingCampaignScene = true;
        currentStageBinder = null;

        SceneTransitionOverlay overlay = SceneTransitionOverlay.Instance;
        yield return overlay.FadeOut();
        if (showCampaignLoading)
        {
            // FadeOut이 끝난 검정 화면을 잠시 유지한 뒤에만 0% Prefab을 표시해 전환 연출을 분리한다.
            yield return overlay.WaitForLoadingFadeOutHold();
            overlay.ShowLoading();
            yield return overlay.LoadSceneOnly(
                StageSceneName,
                normalizedProgress => overlay.SetLoadingProgress(
                    normalizedProgress * CampaignLoadingSceneLoadWeight));
        }
        else
        {
            yield return overlay.LoadSceneOnly(StageSceneName);
        }

        yield return overlay.WaitForSceneSettled();
        yield return WaitForStageBinder();

        if (currentStageBinder != null)
        {
            if (showCampaignLoading)
            {
                // Scene 활성화와 Binder 등록이 확인된 뒤에만 다음 이정표로 진행한다.
                overlay.SetLoadingProgress(
                    CampaignLoadingSceneLoadWeight +
                    CampaignLoadingSceneActivationAndBinderWeight);
            }

            yield return PrepareStageSceneRoutine(currentStageBinder, showCampaignLoading);
        }
        else if (!showCampaignLoading)
        {
            yield return overlay.FadeIn();
        }
        else
        {
            // Binder 미등록은 이전 경고와 함께 검정 로딩 화면을 남겨 실패를 관찰할 수 있게 한다.
            Debug.LogWarning(
                "[GameFlowManager] Initial Campaign loading stopped before StageSceneFlowBinder registration. " +
                "Keeping the loading overlay visible for diagnosis.",
                this);
        }

        if (currentStageBinder != null || !showCampaignLoading)
        {
            isLoadingCampaignScene = false;
        }
        campaignSceneRoutine = null;
    }

    private IEnumerator LoadDialogueSceneFromStageRoutine()
    {
        SceneTransitionOverlay overlay = SceneTransitionOverlay.Instance;
        yield return overlay.FadeOut();
        currentStageBinder?.EndLoadedStage();
        yield return LoadDialogueSceneRoutine(false);
    }

    private IEnumerator LoadDialogueSceneRoutine(bool fadeOutFirst)
    {
        isLoadingCampaignScene = true;
        currentDialogueBinder = null;

        SceneTransitionOverlay overlay = SceneTransitionOverlay.Instance;
        if (fadeOutFirst)
        {
            yield return overlay.FadeOut();
        }

        yield return overlay.LoadSceneOnly(DialogueSceneName);
        yield return overlay.WaitForSceneSettled();
        yield return WaitForDialogueBinder();

        if (currentDialogueBinder != null && !string.IsNullOrWhiteSpace(pendingDialogueLevel))
        {
            currentDialogueBinder.Play(pendingDialogueLevel, pendingDialogueResult);
        }
        else if (string.IsNullOrWhiteSpace(pendingDialogueLevel))
        {
            Debug.LogWarning("[GameFlowManager] Dialogue scene loaded without pending dialogue data.", this);
        }

        yield return overlay.WaitForSceneSettled();
        yield return overlay.FadeIn();
        isLoadingCampaignScene = false;
        campaignSceneRoutine = null;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD || REBELLION_DEMO_BUILD
    private IEnumerator ReturnFromCampaignRoutine(
        string destinationSceneName,
        string endTrigger)
    {
        SceneTransitionOverlay overlay = SceneTransitionOverlay.Instance;
        yield return overlay.FadeOut();

        PlaytestLogger.EndActiveSession(
            PlaytestLogger.ReturnedToTitleSessionEndReason,
            endTrigger);

        // 진행 중인 Stage와 엔딩 패널은 검은 화면 아래에서만 정리해 강제 라운드 종료 중 뒤쪽 화면이 노출되지 않게 한다.
        if (currentStageBinder != null)
        {
            currentStageBinder.ReleaseHeldEndingAudioDrama();
            currentStageBinder.EndLoadedStage();
            currentStageBinder.Unbind();
        }

        currentDialogueBinder?.Unbind();
        currentStageBinder = null;
        currentDialogueBinder = null;
        currentTitleBackgroundTransition = null;
        isCampaignRunning = false;
        isLoadingCampaignScene = false;
        currentStageId = null;
        pendingDialogueLevel = null;
        nextStageAfterDialogue = null;
        hasCompletedEndingTitleTransition = false;

        yield return overlay.LoadSceneOnly(destinationSceneName);
        yield return overlay.WaitForSceneSettled();
        yield return overlay.FadeIn();
        campaignSceneRoutine = null;
    }
#endif

    private IEnumerator WaitForStageBinder()
    {
        const float TimeoutSeconds = 5f;
        float elapsed = 0f;

        while (currentStageBinder == null && elapsed < TimeoutSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (currentStageBinder == null)
        {
            Debug.LogWarning("[GameFlowManager] Stage scene loaded, but StageSceneFlowBinder was not registered.", this);
        }
    }

    private IEnumerator WaitForDialogueBinder()
    {
        const float TimeoutSeconds = 5f;
        float elapsed = 0f;

        while (currentDialogueBinder == null && elapsed < TimeoutSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (currentDialogueBinder == null)
        {
            Debug.LogWarning("[GameFlowManager] Dialogue scene loaded, but DialogueSceneFlowBinder was not registered.", this);
        }
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
                // Stage 7의 민간인 사망 결과는 Eliza 사망 분기로 해석해 Stage 8로 보낸다.
                pendingDialogueLevel = HasElizaDeath(result) ? Stage008 : Stage007;
                nextStageAfterDialogue = HasElizaDeath(result) ? Stage008 : Stage009;
                break;
            default:
                pendingDialogueLevel = currentStageId;
                nextStageAfterDialogue = null;
                break;
        }
    }

    private void PlayEndingOnCurrentStage(
        string endingAudioStageId,
        SimulationController.SimulationResult finalResult)
    {
        if (endingRoutine != null)
        {
            StopCoroutine(endingRoutine);
        }

        endingRoutine = StartCoroutine(PlayEndingRoutine(endingAudioStageId, finalResult));
    }

    private IEnumerator PlayEndingRoutine(
        string endingAudioStageId,
        SimulationController.SimulationResult finalResult)
    {
        StageSceneFlowBinder endingStageBinder = currentStageBinder;
        SceneTransitionOverlay overlay = SceneTransitionOverlay.Instance;
        string endingSourceStageId = currentStageId;

        // Stage 정리와 오디오드라마 패널 준비를 모두 검은 전환막 아래에서 처리한다.
        yield return overlay.FadeOut();
        endingStageBinder.EndLoadedStage();
        endingStageBinder.PlayEndingAudioDrama(endingAudioStageId);
        yield return endingStageBinder.WaitForAudioDramaToBecomeVisible();

        bool endingStarted = endingStageBinder.IsAudioDramaPlayingAndVisible();
        if (endingStarted)
        {
#if UNITY_EDITOR || REBELLION_DEMO_BUILD
            // 엔딩에 도달한 시연 참가자는 제한 시간이 끝나도 Title 배경 전환까지 연출을 보장한다.
            DemoSessionController.NotifyEndingStarted();
#endif

            PlaytestLogger.RecordEndingStarted(
                endingAudioStageId,
                endingSourceStageId,
                finalResult.ToString());
            yield return overlay.FadeIn();
        }

        yield return endingStageBinder.WaitForAudioDramaToFinish();
        if (endingStarted)
        {
            PlaytestLogger.RecordEndingCompleted(
                endingStageBinder.WasLastAudioDramaSkipped ? "skipped" : "natural");
        }
        else
        {
            // 리소스 누락 등으로 엔딩이 시작되지 않아도 열린 세션 상태가 다음 실행까지 남지 않게 마감한다.
            PlaytestLogger.EndActiveSession(
                PlaytestLogger.ReturnedToTitleSessionEndReason,
                "endingPlaybackUnavailable");
        }

        Debug.Log($"[GameFlowManager] Campaign ending finished: {endingAudioStageId}", this);

        if (!overlay.IsFullyOpaque)
        {
            yield return overlay.FadeOut();
        }

        // 패널은 전환막이 완전히 불투명해진 뒤에만 내려 Stage 화면이 노출되지 않게 한다.
        endingStageBinder.ReleaseHeldEndingAudioDrama();

        endingStageBinder.Unbind();
        currentStageBinder = null;
        currentDialogueBinder = null;
        currentTitleBackgroundTransition = null;
        isCampaignRunning = false;
        currentStageId = null;
        pendingDialogueLevel = null;
        nextStageAfterDialogue = null;

        // Title Scene이 검은 전환막 아래에서 기존 배경 상태를 먼저 준비한 뒤 화면에 나타나게 한다.
        yield return overlay.LoadSceneOnly(TitleSceneName);
        yield return overlay.WaitForSceneSettled();
        yield return WaitForTitleBackgroundTransition();

        bool shouldPlayTitleBackgroundTransition = !hasCompletedEndingTitleTransition;
        if (!shouldPlayTitleBackgroundTransition && currentTitleBackgroundTransition != null)
        {
            currentTitleBackgroundTransition.ShowAfterBackgroundImmediate();
        }

        yield return overlay.FadeIn();

        if (shouldPlayTitleBackgroundTransition && currentTitleBackgroundTransition != null)
        {
            yield return currentTitleBackgroundTransition.PlayAndWait();
            hasCompletedEndingTitleTransition = true;
        }

#if UNITY_EDITOR || REBELLION_DEMO_BUILD
        // 엔딩 오디오드라마와 Title 배경 전환이 모두 끝난 뒤에만 다음 시연 라운드 타이머를 초기화한다.
        DemoSessionController.NotifyEndingTitleTransitionCompleted();
#endif

        endingRoutine = null;
    }

    private IEnumerator WaitForTitleBackgroundTransition()
    {
        const float TimeoutSeconds = 5f;
        float elapsed = 0f;

        while (currentTitleBackgroundTransition == null && elapsed < TimeoutSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (currentTitleBackgroundTransition == null)
        {
            Debug.LogWarning(
                "[GameFlowManager] Title scene loaded, but TitleBackgroundTransition was not registered.",
                this);
        }
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
