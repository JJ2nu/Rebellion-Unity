using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>
/// 시연 캠페인의 Stage 진입, 시도, 결과, 이탈과 엔딩을 세션별 JSON Lines 파일로 즉시 저장한다.
/// Scene 전환과 무관하게 같은 세션을 유지하고, 다음 실행에서는 남아 있는 상태 파일로 비정상 종료 지점을 복구한다.
/// </summary>
public sealed class PlaytestLogger : MonoBehaviour
{
    public const string TitleCampaignEntrySource = "titleCampaign";
    public const string DebugHotkeyEntrySource = "debugHotkey";
    public const string StandaloneStageEntrySource = "standaloneStage";

    public const string ProgressedStageExitReason = "progressed";
    public const string ReturnedToTitleStageExitReason = "returnedToTitle";
    public const string ApplicationQuitStageExitReason = "applicationQuit";
    public const string EndingStartedStageExitReason = "endingStarted";

    public const string ReturnedToTitleSessionEndReason = "returnedToTitle";
    public const string ApplicationQuitSessionEndReason = "applicationQuit";
    public const string EndingCompletedSessionEndReason = "endingCompleted";

    private const int SchemaVersion = 1;
    private const string DefaultStationId = "demo-pc-01";
    private const string StageIntroPhase = "stageIntro";
    private const string StagePlayPhase = "stagePlay";
    private const string StageResultPhase = "stageResult";
    private const string DialoguePhase = "dialogue";
    private const string EndingPhase = "ending";
    private const string TitlePhase = "title";
    private const string UnexpectedTerminationReason = "unexpectedTermination";
    private const string NextLaunchRecoveryTrigger = "nextLaunchRecovery";
    private const string ActiveSessionFileName = "active_session.json";
    private const string StationIdFileName = "station_id.txt";

    private static readonly Dictionary<string, int> StageVisitCounts = new();

    public static PlaytestLogger Instance { get; private set; }
    public static bool HasActiveSession => Instance != null && Instance.sessionState.isActive;
    public static string PlayLogsRootDirectoryPath
    {
        get
        {
            if (Application.platform == RuntimePlatform.WindowsPlayer)
            {
                string buildDirectoryPath = Path.GetDirectoryName(Application.dataPath);
                if (!string.IsNullOrWhiteSpace(buildDirectoryPath))
                {
                    return Path.Combine(buildDirectoryPath, "PlayLogs");
                }
            }

            return Path.Combine(Application.persistentDataPath, "PlayLogs");
        }
    }

    public static string LogDirectoryPath
    {
        get
        {
            if (Application.isEditor)
            {
                return Path.Combine(PlayLogsRootDirectoryPath, "EditorTests");
            }

            if (Application.platform == RuntimePlatform.WindowsPlayer)
            {
                return Path.Combine(PlayLogsRootDirectoryPath, "BuildLogs");
            }

            return PlayLogsRootDirectoryPath;
        }
    }

    private PlaytestSessionState sessionState = new();
    private string pendingEntrySource = StandaloneStageEntrySource;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        StageVisitCounts.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateBeforeFirstScene()
    {
        EnsureInstance();
    }

    public static void PrepareCampaign(string entrySource)
    {
        PlaytestLogger logger = EnsureInstance();
        if (logger.sessionState.isActive)
        {
            logger.EndSessionInternal(ReturnedToTitleSessionEndReason, "campaignReplaced");
        }

        logger.pendingEntrySource = string.IsNullOrWhiteSpace(entrySource)
            ? StandaloneStageEntrySource
            : entrySource;
    }

    public static void RecordStageEntered(string stageId)
    {
        EnsureInstance().RecordStageEnteredInternal(stageId);
    }

    public static void RecordStageReady()
    {
        Instance?.RecordStageReadyInternal();
    }

    public static void RecordSimulationStarted()
    {
        Instance?.RecordSimulationStartedInternal();
    }

    public static void RecordSimulationFinished(string simulationResult)
    {
        Instance?.RecordSimulationFinishedInternal(simulationResult);
    }

    public static void RecordStageCompleted(string clearResult)
    {
        Instance?.RecordStageCompletedInternal(clearResult);
    }

    public static void RecordStageExited(string stageExitReason)
    {
        Instance?.RecordStageExitedInternal(stageExitReason);
    }

    public static void RecordDialogueEntered()
    {
        Instance?.ChangeFlowPhase(DialoguePhase);
    }

    public static void RecordEndingStarted(
        string endingType,
        string sourceStageId,
        string finalSimulationResult)
    {
        Instance?.RecordEndingStartedInternal(endingType, sourceStageId, finalSimulationResult);
    }

    public static void RecordEndingCompleted(string endingCompletionMethod)
    {
        Instance?.RecordEndingCompletedInternal(endingCompletionMethod);
    }

    public static void EndActiveSession(string sessionEndReason, string endTrigger)
    {
        Instance?.EndSessionInternal(sessionEndReason, endTrigger);
    }

    private static PlaytestLogger EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        GameObject loggerObject = new(nameof(PlaytestLogger));
        return loggerObject.AddComponent<PlaytestLogger>();
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
        EnsureLogDirectory();
        RecoverInterruptedSession();
    }

    private void OnApplicationQuit()
    {
        EndSessionInternal(ApplicationQuitSessionEndReason, "windowClose");
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            // Scene 전환 파괴와 앱 종료를 혼동하지 않도록 종료 기록은 OnApplicationQuit과 명시적 종료 경로만 담당한다.
            Instance = null;
        }
    }

    private void RecordStageEnteredInternal(string stageId)
    {
        if (string.IsNullOrWhiteSpace(stageId))
        {
            return;
        }

        if (!sessionState.isActive)
        {
            StartSession(stageId);
        }

        if (sessionState.stageOpen)
        {
            if (sessionState.currentStageId == stageId)
            {
                return;
            }

            RecordStageExitedInternal(ProgressedStageExitReason);
        }

        DateTime nowUtc = DateTime.UtcNow;
        sessionState.currentStageId = stageId;
        sessionState.furthestStageId = GetFurthestStageId(sessionState.furthestStageId, stageId);
        sessionState.flowPhase = StageIntroPhase;
        sessionState.stageOpen = true;
        sessionState.stageEnteredAtUtc = FormatUtc(nowUtc);
        sessionState.stageReadyAtUtc = string.Empty;
        sessionState.attemptStartedAtUtc = string.Empty;
        sessionState.attemptCount = 0;
        sessionState.lastSimulationResult = string.Empty;
        sessionState.stageCompleted = false;

        int stageVisitNumber = GetNextStageVisitNumber(stageId);
        PlaytestLogEvent logEvent = CreateBaseEvent("StageEntered", nowUtc);
        logEvent.stageVisitNumber = stageVisitNumber;
        WriteEvent(logEvent, nowUtc);
    }

    private void RecordStageReadyInternal()
    {
        if (!sessionState.isActive || !sessionState.stageOpen)
        {
            return;
        }

        DateTime nowUtc = DateTime.UtcNow;
        if (string.IsNullOrWhiteSpace(sessionState.stageReadyAtUtc))
        {
            sessionState.stageReadyAtUtc = FormatUtc(nowUtc);
        }

        ChangeFlowPhase(StagePlayPhase, nowUtc);

        PlaytestLogEvent logEvent = CreateBaseEvent("StageReady", nowUtc);
        logEvent.stageIntroSeconds = ElapsedSeconds(sessionState.stageEnteredAtUtc, nowUtc);
        WriteEvent(logEvent, nowUtc);
    }

    private void RecordSimulationStartedInternal()
    {
        if (!sessionState.isActive || !sessionState.stageOpen)
        {
            return;
        }

        DateTime nowUtc = DateTime.UtcNow;
        ChangeFlowPhase(StagePlayPhase, nowUtc);
        sessionState.attemptCount++;
        sessionState.totalAttemptCount++;
        sessionState.attemptStartedAtUtc = FormatUtc(nowUtc);
        sessionState.lastSimulationResult = string.Empty;

        PlaytestLogEvent logEvent = CreateBaseEvent("SimulationStarted", nowUtc);
        logEvent.attemptNumber = sessionState.attemptCount;
        logEvent.stageElapsedSecondsAtStart =
            ElapsedSeconds(sessionState.stageEnteredAtUtc, nowUtc);
        WriteEvent(logEvent, nowUtc);
    }

    private void RecordSimulationFinishedInternal(string simulationResult)
    {
        if (!sessionState.isActive || !sessionState.stageOpen || sessionState.attemptCount <= 0)
        {
            return;
        }

        DateTime nowUtc = DateTime.UtcNow;
        sessionState.lastSimulationResult = simulationResult ?? string.Empty;
        ChangeFlowPhase(StageResultPhase, nowUtc);

        PlaytestLogEvent logEvent = CreateBaseEvent("SimulationFinished", nowUtc);
        logEvent.attemptNumber = sessionState.attemptCount;
        logEvent.simulationResult = sessionState.lastSimulationResult;
        logEvent.attemptDurationSeconds =
            ElapsedSeconds(sessionState.attemptStartedAtUtc, nowUtc);
        logEvent.stageElapsedSecondsAtFinish =
            ElapsedSeconds(sessionState.stageEnteredAtUtc, nowUtc);
        WriteEvent(logEvent, nowUtc);
    }

    private void RecordStageCompletedInternal(string clearResult)
    {
        if (!sessionState.isActive || !sessionState.stageOpen || sessionState.stageCompleted)
        {
            return;
        }

        DateTime nowUtc = DateTime.UtcNow;
        sessionState.stageCompleted = true;
        sessionState.completedStageCount++;

        PlaytestLogEvent logEvent = CreateBaseEvent("StageCompleted", nowUtc);
        logEvent.clearResult = clearResult ?? string.Empty;
        logEvent.attemptCount = sessionState.attemptCount;
        PopulateStageDurations(logEvent, nowUtc);
        WriteEvent(logEvent, nowUtc);
    }

    private void RecordStageExitedInternal(string stageExitReason)
    {
        if (!sessionState.isActive || !sessionState.stageOpen)
        {
            return;
        }

        DateTime nowUtc = DateTime.UtcNow;
        PlaytestLogEvent logEvent = CreateBaseEvent("StageExited", nowUtc);
        logEvent.stageExitReason = stageExitReason ?? string.Empty;
        logEvent.attemptCount = sessionState.attemptCount;
        logEvent.lastSimulationResult = sessionState.lastSimulationResult;
        logEvent.wasCompleted = sessionState.stageCompleted;
        PopulateStageDurations(logEvent, nowUtc);
        WriteEvent(logEvent, nowUtc);

        sessionState.stageOpen = false;
        sessionState.attemptStartedAtUtc = string.Empty;
        SaveActiveSessionState();
    }

    private void RecordEndingStartedInternal(
        string endingType,
        string sourceStageId,
        string finalSimulationResult)
    {
        if (!sessionState.isActive)
        {
            return;
        }

        DateTime nowUtc = DateTime.UtcNow;
        sessionState.endingType = endingType ?? string.Empty;
        sessionState.endingCompleted = false;
        sessionState.endingStartedAtUtc = FormatUtc(nowUtc);
        ChangeFlowPhase(EndingPhase, nowUtc);

        PlaytestLogEvent logEvent = CreateBaseEvent("EndingStarted", nowUtc);
        logEvent.endingType = sessionState.endingType;
        logEvent.sourceStageId = sourceStageId ?? sessionState.currentStageId;
        logEvent.finalSimulationResult = finalSimulationResult ?? string.Empty;
        WriteEvent(logEvent, nowUtc);
    }

    private void RecordEndingCompletedInternal(string endingCompletionMethod)
    {
        if (!sessionState.isActive || string.IsNullOrWhiteSpace(sessionState.endingType))
        {
            return;
        }

        DateTime nowUtc = DateTime.UtcNow;
        sessionState.endingCompleted = true;

        PlaytestLogEvent logEvent = CreateBaseEvent("EndingCompleted", nowUtc);
        logEvent.endingType = sessionState.endingType;
        logEvent.endingCompletionMethod = endingCompletionMethod ?? string.Empty;
        logEvent.endingPlaybackSeconds =
            ElapsedSeconds(sessionState.endingStartedAtUtc, nowUtc);
        WriteEvent(logEvent, nowUtc);

        EndSessionInternal(EndingCompletedSessionEndReason, "endingAutomaticReturn");
    }

    private void StartSession(string initialStageId)
    {
        EnsureLogDirectory();
        StageVisitCounts.Clear();

        DateTime nowUtc = DateTime.UtcNow;
        string sessionId = Guid.NewGuid().ToString();
        sessionState = new PlaytestSessionState
        {
            isActive = true,
            sessionId = sessionId,
            logFilePath = Path.Combine(
                LogDirectoryPath,
                $"playtest_{nowUtc:yyyyMMdd_HHmmss}_{sessionId}.jsonl"),
            entrySource = pendingEntrySource,
            stationId = ReadStationId(),
            appVersion = Application.version,
            buildType = GetBuildType(),
            startedAtUtc = FormatUtc(nowUtc),
            lastEventAtUtc = FormatUtc(nowUtc),
            currentStageId = initialStageId,
            furthestStageId = initialStageId,
            flowPhase = StageIntroPhase,
        };

        PlaytestLogEvent logEvent = CreateBaseEvent("CampaignSessionStarted", nowUtc);
        logEvent.startedAtUtc = sessionState.startedAtUtc;
        WriteEvent(logEvent, nowUtc);
    }

    private void EndSessionInternal(string sessionEndReason, string endTrigger)
    {
        if (!sessionState.isActive)
        {
            return;
        }

        string lastFlowPhase = sessionState.flowPhase;
        string stageExitReason = sessionEndReason switch
        {
            ApplicationQuitSessionEndReason => ApplicationQuitStageExitReason,
            UnexpectedTerminationReason => UnexpectedTerminationReason,
            _ => ReturnedToTitleStageExitReason,
        };

        RecordStageExitedInternal(stageExitReason);

        DateTime nowUtc = DateTime.UtcNow;
        ChangeFlowPhase(TitlePhase, nowUtc);

        PlaytestLogEvent logEvent = CreateBaseEvent("CampaignSessionEnded", nowUtc);
        logEvent.sessionEndReason = sessionEndReason ?? string.Empty;
        logEvent.endTrigger = endTrigger ?? string.Empty;
        logEvent.lastStageId = sessionState.currentStageId;
        logEvent.furthestStageId = sessionState.furthestStageId;
        logEvent.lastFlowPhase = lastFlowPhase;
        logEvent.sessionPlaySeconds = ElapsedSeconds(sessionState.startedAtUtc, nowUtc);
        logEvent.completedStageCount = sessionState.completedStageCount;
        logEvent.totalAttemptCount = sessionState.totalAttemptCount;
        logEvent.endingType = sessionState.endingType;
        logEvent.endingCompleted = sessionState.endingCompleted;
        WriteEvent(logEvent, nowUtc);

        sessionState.isActive = false;
        DeleteActiveSessionState();
        pendingEntrySource = StandaloneStageEntrySource;
    }

    private void ChangeFlowPhase(string nextFlowPhase)
    {
        ChangeFlowPhase(nextFlowPhase, DateTime.UtcNow);
    }

    private void ChangeFlowPhase(string nextFlowPhase, DateTime nowUtc)
    {
        if (!sessionState.isActive ||
            string.IsNullOrWhiteSpace(nextFlowPhase) ||
            sessionState.flowPhase == nextFlowPhase)
        {
            return;
        }

        string previousFlowPhase = sessionState.flowPhase;
        sessionState.flowPhase = nextFlowPhase;

        PlaytestLogEvent logEvent = CreateBaseEvent("FlowPhaseChanged", nowUtc);
        logEvent.previousFlowPhase = previousFlowPhase;
        WriteEvent(logEvent, nowUtc);
    }

    private PlaytestLogEvent CreateBaseEvent(string eventName, DateTime occurredAtUtc)
    {
        return new PlaytestLogEvent
        {
            schemaVersion = SchemaVersion,
            eventName = eventName,
            occurredAtUtc = FormatUtc(occurredAtUtc),
            sessionId = sessionState.sessionId,
            stationId = sessionState.stationId,
            appVersion = sessionState.appVersion,
            buildType = sessionState.buildType,
            entrySource = sessionState.entrySource,
            stageId = sessionState.currentStageId,
            flowPhase = sessionState.flowPhase,
        };
    }

    private void WriteEvent(PlaytestLogEvent logEvent, DateTime occurredAtUtc)
    {
        if (logEvent == null || string.IsNullOrWhiteSpace(sessionState.logFilePath))
        {
            return;
        }

        sessionState.eventSequence++;
        sessionState.lastEventAtUtc = FormatUtc(occurredAtUtc);
        logEvent.eventSequence = sessionState.eventSequence;

        try
        {
            File.AppendAllText(
                sessionState.logFilePath,
                JsonUtility.ToJson(logEvent) + Environment.NewLine);
            SaveActiveSessionState();
        }
        catch (Exception exception)
        {
            Debug.LogError($"[PlaytestLogger] Failed to write playtest log: {exception.Message}", this);
        }
    }

    private void SaveActiveSessionState()
    {
        if (!sessionState.isActive)
        {
            return;
        }

        try
        {
            File.WriteAllText(GetActiveSessionPath(), JsonUtility.ToJson(sessionState));
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"[PlaytestLogger] Failed to save active session state: {exception.Message}",
                this);
        }
    }

    private void RecoverInterruptedSession()
    {
        string activeSessionPath = GetActiveSessionPath();
        if (!File.Exists(activeSessionPath))
        {
            return;
        }

        try
        {
            PlaytestSessionState interruptedState =
                JsonUtility.FromJson<PlaytestSessionState>(File.ReadAllText(activeSessionPath));
            if (interruptedState == null ||
                !interruptedState.isActive ||
                string.IsNullOrWhiteSpace(interruptedState.logFilePath))
            {
                DeleteActiveSessionState();
                return;
            }

            DateTime recoveredAtUtc = ParseUtc(
                interruptedState.lastEventAtUtc,
                DateTime.UtcNow);
            string lastFlowPhase = interruptedState.flowPhase;

            if (interruptedState.stageOpen)
            {
                PlaytestLogEvent stageExited = CreateRecoveredBaseEvent(
                    interruptedState,
                    "StageExited",
                    recoveredAtUtc);
                stageExited.stageExitReason = UnexpectedTerminationReason;
                stageExited.attemptCount = interruptedState.attemptCount;
                stageExited.lastSimulationResult = interruptedState.lastSimulationResult;
                stageExited.wasCompleted = interruptedState.stageCompleted;
                PopulateRecoveredStageDurations(stageExited, interruptedState, recoveredAtUtc);
                AppendRecoveredEvent(interruptedState, stageExited);
            }

            interruptedState.flowPhase = TitlePhase;
            PlaytestLogEvent sessionEnded = CreateRecoveredBaseEvent(
                interruptedState,
                "CampaignSessionEnded",
                recoveredAtUtc);
            sessionEnded.sessionEndReason = UnexpectedTerminationReason;
            sessionEnded.endTrigger = NextLaunchRecoveryTrigger;
            sessionEnded.lastStageId = interruptedState.currentStageId;
            sessionEnded.furthestStageId = interruptedState.furthestStageId;
            sessionEnded.lastFlowPhase = lastFlowPhase;
            sessionEnded.sessionPlaySeconds =
                ElapsedSeconds(interruptedState.startedAtUtc, recoveredAtUtc);
            sessionEnded.completedStageCount = interruptedState.completedStageCount;
            sessionEnded.totalAttemptCount = interruptedState.totalAttemptCount;
            sessionEnded.endingType = interruptedState.endingType;
            sessionEnded.endingCompleted = interruptedState.endingCompleted;
            AppendRecoveredEvent(interruptedState, sessionEnded);
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"[PlaytestLogger] Failed to recover interrupted session: {exception.Message}",
                this);
        }
        finally
        {
            DeleteActiveSessionState();
        }
    }

    private static PlaytestLogEvent CreateRecoveredBaseEvent(
        PlaytestSessionState interruptedState,
        string eventName,
        DateTime occurredAtUtc)
    {
        return new PlaytestLogEvent
        {
            schemaVersion = SchemaVersion,
            eventName = eventName,
            occurredAtUtc = FormatUtc(occurredAtUtc),
            sessionId = interruptedState.sessionId,
            stationId = interruptedState.stationId,
            appVersion = interruptedState.appVersion,
            buildType = interruptedState.buildType,
            entrySource = interruptedState.entrySource,
            stageId = interruptedState.currentStageId,
            flowPhase = interruptedState.flowPhase,
        };
    }

    private static void AppendRecoveredEvent(
        PlaytestSessionState interruptedState,
        PlaytestLogEvent logEvent)
    {
        interruptedState.eventSequence++;
        logEvent.eventSequence = interruptedState.eventSequence;
        File.AppendAllText(
            interruptedState.logFilePath,
            JsonUtility.ToJson(logEvent) + Environment.NewLine);
    }

    private void PopulateStageDurations(PlaytestLogEvent logEvent, DateTime endedAtUtc)
    {
        logEvent.stageTotalSeconds =
            ElapsedSeconds(sessionState.stageEnteredAtUtc, endedAtUtc);
        logEvent.stagePlayableSeconds =
            ElapsedSeconds(sessionState.stageReadyAtUtc, endedAtUtc);
    }

    private static void PopulateRecoveredStageDurations(
        PlaytestLogEvent logEvent,
        PlaytestSessionState interruptedState,
        DateTime endedAtUtc)
    {
        logEvent.stageTotalSeconds =
            ElapsedSeconds(interruptedState.stageEnteredAtUtc, endedAtUtc);
        logEvent.stagePlayableSeconds =
            ElapsedSeconds(interruptedState.stageReadyAtUtc, endedAtUtc);
    }

    private static float ElapsedSeconds(string startedAtUtc, DateTime endedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(startedAtUtc))
        {
            return 0f;
        }

        DateTime startUtc = ParseUtc(startedAtUtc, endedAtUtc);
        return (float)Math.Max(0d, Math.Round((endedAtUtc - startUtc).TotalSeconds, 3));
    }

    private static string GetFurthestStageId(string currentFurthestStageId, string candidateStageId)
    {
        int currentIndex = ParseStageIndex(currentFurthestStageId);
        int candidateIndex = ParseStageIndex(candidateStageId);
        return candidateIndex > currentIndex ? candidateStageId : currentFurthestStageId;
    }

    private static int ParseStageIndex(string stageId)
    {
        if (string.IsNullOrWhiteSpace(stageId))
        {
            return -1;
        }

        int separatorIndex = stageId.LastIndexOf('_');
        return separatorIndex >= 0 &&
               int.TryParse(stageId[(separatorIndex + 1)..], out int stageIndex)
            ? stageIndex
            : -1;
    }

    private static int GetNextStageVisitNumber(string stageId)
    {
        StageVisitCounts.TryGetValue(stageId, out int visitCount);
        visitCount++;
        StageVisitCounts[stageId] = visitCount;
        return visitCount;
    }

    private static string ReadStationId()
    {
        string stationIdPath = Path.Combine(
            PlayLogsRootDirectoryPath,
            StationIdFileName);
        try
        {
            if (File.Exists(stationIdPath))
            {
                string configuredStationId = File.ReadAllText(stationIdPath).Trim();
                if (!string.IsNullOrWhiteSpace(configuredStationId))
                {
                    return configuredStationId;
                }
            }

            File.WriteAllText(stationIdPath, DefaultStationId);
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"[PlaytestLogger] Failed to read station ID. Using default: {exception.Message}");
        }

        return DefaultStationId;
    }

    private static string GetBuildType()
    {
#if UNITY_EDITOR
        return "Editor";
#elif DEVELOPMENT_BUILD
        return "Development";
#else
        return "Release";
#endif
    }

    private static DateTime ParseUtc(string value, DateTime fallbackUtc)
    {
        return DateTime.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out DateTime parsed)
            ? parsed.ToUniversalTime()
            : fallbackUtc;
    }

    private static string FormatUtc(DateTime value)
    {
        return value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    }

    private static void EnsureLogDirectory()
    {
        Directory.CreateDirectory(LogDirectoryPath);
    }

    private static string GetActiveSessionPath()
    {
        return Path.Combine(LogDirectoryPath, ActiveSessionFileName);
    }

    private static void DeleteActiveSessionState()
    {
        string path = GetActiveSessionPath();
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    [Serializable]
    private sealed class PlaytestSessionState
    {
        public bool isActive;
        public string sessionId;
        public string logFilePath;
        public string entrySource;
        public string stationId;
        public string appVersion;
        public string buildType;
        public string startedAtUtc;
        public string lastEventAtUtc;
        public int eventSequence;
        public string currentStageId;
        public string furthestStageId;
        public string flowPhase;
        public bool stageOpen;
        public string stageEnteredAtUtc;
        public string stageReadyAtUtc;
        public string attemptStartedAtUtc;
        public int attemptCount;
        public int totalAttemptCount;
        public string lastSimulationResult;
        public bool stageCompleted;
        public int completedStageCount;
        public string endingType;
        public bool endingCompleted;
        public string endingStartedAtUtc;
    }

    [Serializable]
    private sealed class PlaytestLogEvent
    {
        public int schemaVersion;
        public string eventName;
        public int eventSequence;
        public string occurredAtUtc;
        public string sessionId;
        public string stationId;
        public string appVersion;
        public string buildType;
        public string entrySource;
        public string stageId;
        public string flowPhase;

        public string startedAtUtc;
        public int stageVisitNumber;
        public float stageIntroSeconds;
        public string previousFlowPhase;
        public int attemptNumber;
        public float stageElapsedSecondsAtStart;
        public string simulationResult;
        public float attemptDurationSeconds;
        public float stageElapsedSecondsAtFinish;
        public string clearResult;
        public int attemptCount;
        public float stageTotalSeconds;
        public float stagePlayableSeconds;
        public string lastSimulationResult;
        public string stageExitReason;
        public bool wasCompleted;
        public string endingType;
        public string sourceStageId;
        public string finalSimulationResult;
        public string endingCompletionMethod;
        public float endingPlaybackSeconds;
        public string sessionEndReason;
        public string endTrigger;
        public string lastStageId;
        public string furthestStageId;
        public string lastFlowPhase;
        public float sessionPlaySeconds;
        public int completedStageCount;
        public int totalAttemptCount;
        public bool endingCompleted;
    }
}
