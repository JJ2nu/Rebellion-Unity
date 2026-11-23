using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// StageData.tutorialSteps를 순서대로 실행하는 대사형 튜토리얼 진행자.
/// 대사는 TutorialDialogueView로, 조작 강조는 TutorialSpotlightOverlay로, 입력 제한은 StageTutorialGate로 처리한다.
/// Stage Scene에 속한 오브젝트라 Scene이 내려가면 함께 파괴되며, 파괴 시 게이트와 모달 잠금을 모두 해제한다.
/// </summary>
public sealed class GuidedTutorialDirector : MonoBehaviour
{
    private const string PanelResourcePath = "UI/TutorialDialoguePanel";

    private const string ConditionWin = "win";
    private const string ConditionFail = "fail";
    private const string ConditionPerfect = "perfect";

    private StageSceneFlowBinder binder;
    private SimulationController simulation;
    private PlacementController placement;
    private ResultDialogController resultDialog;
    private TutorialStepData[] steps;

    private TutorialDialogueView view;
    private RectTransform uiParent;
    // 대원 슬롯 컨테이너(Storages). 슬롯을 눌러야 하는 단계에서만 대사창을 이 뒤로 내린다.
    private Transform storageRoot;
    private TutorialSpotlightOverlay spotlight;
    private AttackLinePresenter attackLines;
    private readonly List<InGameUnitStorageSlotUI> slots = new();

    private InputAction advanceAction;
    private IDisposable modalLease;
    private bool advanceRequested;
    private int lastAdvanceFrame = -1;
    private bool isSuppressed;

    private bool hasResult;
    private SimulationController.SimulationResult lastResult;
    private bool retryRequested;
    // 결과가 나온 뒤 Retry를 기다리는 구간(결과 대사 ~ waitRetry)인지. 이 구간에서는 언제 Retry를 눌러도 되돌아간다.
    private bool isAfterResult;
    private bool friendlyFireSeen;
    private int pendingJumpIndex = -1;

    /// <summary>첫 조작 단계에 도달했거나 튜토리얼이 끝났다. 캠페인 흐름은 이 시점부터 플레이 준비 완료로 본다.</summary>
    public bool IsIntroComplete { get; private set; }

    public bool IsFinished { get; private set; }

    public static GuidedTutorialDirector Create(
        StageSceneFlowBinder owner,
        RectTransform uiParent,
        StageData stageData,
        SimulationController simulationController)
    {
        if (stageData == null || !stageData.HasGuidedTutorial)
        {
            return null;
        }

        GameObject panelPrefab = Resources.Load<GameObject>(PanelResourcePath);
        if (panelPrefab == null || uiParent == null)
        {
            Debug.LogError(
                $"[GuidedTutorialDirector] Cannot start the tutorial. Panel prefab: {(panelPrefab != null)}, UI parent: {(uiParent != null)}. " +
                $"Run 'Rebellion/Tutorial/Build Tutorial Dialogue Panel' to create Resources/{PanelResourcePath}.prefab.");
            return null;
        }

        GameObject directorObject = new("GuidedTutorialDirector");
        directorObject.transform.SetParent(owner.transform, false);
        GuidedTutorialDirector director = directorObject.AddComponent<GuidedTutorialDirector>();
        director.Initialize(owner, uiParent, stageData, simulationController, panelPrefab);
        return director;
    }

    private void Initialize(
        StageSceneFlowBinder owner,
        RectTransform uiParent,
        StageData stageData,
        SimulationController simulationController,
        GameObject panelPrefab)
    {
        binder = owner;
        this.uiParent = uiParent;
        simulation = simulationController != null ? simulationController : SimulationController.Instance;
        steps = stageData.tutorialSteps;
        placement = FindFirstObjectByType<PlacementController>();
        resultDialog = FindFirstObjectByType<ResultDialogController>(FindObjectsInactive.Include);

        spotlight = TutorialSpotlightOverlay.Create(uiParent);
        spotlight.transform.SetAsLastSibling();

        GameObject panel = Instantiate(panelPrefab, uiParent, false);
        panel.name = panelPrefab.name;
        panel.SetActive(true);
        panel.transform.SetAsLastSibling();
        view = panel.GetComponent<TutorialDialogueView>();
        view.AdvanceRequested += HandleAdvanceRequested;
        storageRoot = FindStorageRoot(uiParent);

        attackLines = AttackLinePresenter.Create(transform);

        advanceAction = new InputAction("AdvanceGuidedTutorial", InputActionType.Button, "<Keyboard>/space");
        advanceAction.performed += HandleSpacePerformed;
        advanceAction.Enable();

        if (simulation != null)
        {
            simulation.HitConfirmed += HandleHitConfirmed;
            simulation.SimulationReset += HandleSimulationReset;
        }

        StartCoroutine(Run());
    }

    private void OnDestroy()
    {
        if (view != null)
        {
            view.AdvanceRequested -= HandleAdvanceRequested;
            Destroy(view.gameObject);
        }

        if (spotlight != null)
        {
            Destroy(spotlight.gameObject);
        }

        if (advanceAction != null)
        {
            advanceAction.performed -= HandleSpacePerformed;
            advanceAction.Dispose();
        }

        if (simulation != null)
        {
            simulation.HitConfirmed -= HandleHitConfirmed;
            simulation.SimulationReset -= HandleSimulationReset;
        }

        SetModal(false);
        StageTutorialGate.End();
    }

    private void Update()
    {
        // 결과 판정 패널이 열리면 그 패널이 대사 연출을 맡으므로 튜토리얼 대사창과 강조를 잠시 내린다.
        bool suppress = resultDialog != null && resultDialog.IsVisible;
        if (suppress == isSuppressed)
        {
            return;
        }

        isSuppressed = suppress;
        view?.SetSuppressed(suppress);
        spotlight?.gameObject.SetActive(!suppress);
    }

    private IEnumerator Run()
    {
        StageTutorialGate.Begin();

        int index = 0;
        while (index < steps.Length)
        {
            TutorialStepData step = steps[index];
            pendingJumpIndex = -1;

            if (step == null || !MatchesCondition(step.condition))
            {
                index++;
                continue;
            }

            switch (step.type)
            {
                case "line":
                    yield return RunLine(step);
                    break;
                case "place":
                    yield return RunPlace(step);
                    break;
                case "placeAll":
                    yield return RunPlaceAll(step);
                    break;
                case "start":
                    yield return RunStart(step);
                    break;
                case "result":
                    yield return RunResult(step);
                    break;
                case "waitRetry":
                    yield return RunWaitRetry(step);
                    break;
                case "enemyLines":
                    attackLines.ShowEnemyLines = true;
                    break;
                default:
                    Debug.LogWarning($"[GuidedTutorialDirector] Unknown tutorial step type: {step.type}", this);
                    break;
            }

            // 결과 대사 도중 Retry를 누르면 남은 결과 대사를 건너뛰고 해당 결과의 waitRetry 목적지로 바로 돌아간다.
            if (pendingJumpIndex < 0 && isAfterResult && retryRequested)
            {
                pendingJumpIndex = FindRetryJumpIndex(index);
            }

            index = pendingJumpIndex >= 0 ? pendingJumpIndex : index + 1;
        }

        Finish();
    }

    private void Finish()
    {
        SetModal(false);
        spotlight.Clear();
        view.SetVisible(false);
        StageTutorialGate.End();
        IsIntroComplete = true;
        IsFinished = true;
    }

    #region Steps

    private IEnumerator RunLine(TutorialStepData step)
    {
        SetPanelBehindStorage(false);
        // 연속된 대사 동안 모달 잠금을 유지해 배치·시작 입력이 대사 사이로 새지 않게 한다.
        // 대원 슬롯은 월드 입력 모달과 별개인 UI 버튼이라 게이트로 함께 잠근다.
        SetModal(true);
        StageTutorialGate.SetAllSlotsLocked(true);
        spotlight.Clear();
        view.ShowLine(step.speaker, step.text, step.character, true);
        advanceRequested = false;

        while (true)
        {
            if (isAfterResult && retryRequested)
            {
                break;
            }

            if (advanceRequested)
            {
                advanceRequested = false;
                if (view.IsTyping)
                {
                    view.CompleteTyping();
                }
                else
                {
                    view.PlayAdvanceSound();
                    break;
                }
            }

            yield return null;
        }

        EventSystem.current?.SetSelectedGameObject(null);
    }

    private IEnumerator RunPlace(TutorialStepData step)
    {
        BeginInteractiveStep();
        SetPanelBehindStorage(true);

        PieceType pieceType = (PieceType)step.pieceType;
        Direction? facing = step.facing >= 0 ? (Direction)step.facing : null;
        StageTutorialGate.SetAllowedSlotType(pieceType);
        StageTutorialGate.SetSimulationStartLocked(true);
        StageTutorialGate.SetPieceRemovalLocked(true);
        StageTutorialGate.SetRequiredPlacement(pieceType, step.cellIndex, facing);
        CacheSlots();

        string shownText = null;
        while (!IsPlaced(pieceType, step.cellIndex, facing))
        {
            bool isPlacingThis = placement != null && placement.PendingUnitType == pieceType;
            string text = isPlacingThis || string.IsNullOrWhiteSpace(step.selectText) ? step.text : step.selectText;
            if (text != shownText)
            {
                view.ShowLine(step.speaker, text, step.character, false);
                shownText = text;
            }

            if (isPlacingThis && GameManager.Instance != null)
            {
                spotlight.FocusWorldCell(GameManager.Instance.GetCellPosition(step.cellIndex), GetCellSize(), 6f, 0.35f);
            }
            else
            {
                spotlight.FocusRect(FindSlotRect(pieceType), 12f, 0.5f);
            }

            yield return null;
        }

        spotlight.Clear();
        // 이어지는 설명 대사 동안 방금 놓은 기물의 사선을 고정해 보여준다.
        attackLines.SetPinned(FindPlacedPiece(pieceType, step.cellIndex));
        StageTutorialGate.ClearRequiredPlacement();
        StageTutorialGate.LockCell(step.cellIndex);
        StageTutorialGate.SetPieceRemovalLocked(false);
        StageTutorialGate.SetAllowedSlotType(null);
    }

    private IEnumerator RunPlaceAll(TutorialStepData step)
    {
        BeginInteractiveStep();
        SetPanelBehindStorage(true);
        StageTutorialGate.SetAllowedSlotType(null);
        StageTutorialGate.SetSimulationStartLocked(true);
        StageTutorialGate.SetPieceRemovalLocked(false);
        CacheSlots();

        if (AreAllSlotsPlaced())
        {
            yield break;
        }

        view.ShowLine(step.speaker, step.text, step.character, false);
        spotlight.Clear();

        while (!AreAllSlotsPlaced())
        {
            yield return null;
        }
    }

    private IEnumerator RunStart(TutorialStepData step)
    {
        BeginInteractiveStep();
        SetPanelBehindStorage(false);
        StageTutorialGate.SetAllowedSlotType(null);
        StageTutorialGate.SetSimulationStartLocked(false);

        view.ShowLine(step.speaker, step.text, step.character, false);
        StageSimulationControlsView controls = FindFirstObjectByType<StageSimulationControlsView>();

        while (simulation != null && !simulation._isRunning)
        {
            spotlight.FocusRect(controls != null ? controls.PlayButtonRect : null, 10f, 0.25f);
            yield return null;
        }

        spotlight.Clear();
        StageTutorialGate.SetSimulationStartLocked(true);
    }

    private IEnumerator RunResult(TutorialStepData step)
    {
        SetModal(false);
        SetPanelBehindStorage(false);
        spotlight.Clear();
        hasResult = false;
        isAfterResult = false;
        retryRequested = false;
        friendlyFireSeen = false;

        if (string.IsNullOrWhiteSpace(step.text))
        {
            view.SetVisible(false);
        }
        else
        {
            view.ShowLine(step.speaker, step.text, step.character, false);
        }

        // 같은 시뮬레이션에서는 오인사격 안내를 한 번만 띄운다.
        bool calloutShown = string.IsNullOrWhiteSpace(step.calloutText);
        SimulationController.SimulationResult result = SimulationController.SimulationResult.Lose;
        while (binder == null || !binder.TryGetPendingSimulationResult(out result))
        {
            if (friendlyFireSeen && !calloutShown)
            {
                calloutShown = true;
                view.ShowLine(step.speaker, step.calloutText, step.character, false);
            }

            yield return null;
        }

        hasResult = true;
        isAfterResult = true;
        lastResult = result;
        view.SetVisible(false);
    }

    private IEnumerator RunWaitRetry(TutorialStepData step)
    {
        BeginInteractiveStep();
        SetPanelBehindStorage(false);

        if (!string.IsNullOrWhiteSpace(step.text))
        {
            view.ShowLine(step.speaker, step.text, step.character, false);
        }

        StageSimulationControlsView controls = FindFirstObjectByType<StageSimulationControlsView>();
        bool focusRetry = IsFailure(lastResult);

        // 결과 대사 중에 이미 Retry를 눌렀다면 바로 통과한다.
        while (!retryRequested)
        {
            if (focusRetry)
            {
                spotlight.FocusRect(controls != null ? controls.RetryButtonRect : null, 10f, 0.25f);
            }

            yield return null;
        }

        spotlight.Clear();
        ClearRetryState();
        pendingJumpIndex = FindStepIndex(step.gotoId);
    }

    #endregion

    #region Helpers

    private void BeginInteractiveStep()
    {
        SetModal(false);
        StageTutorialGate.SetAllSlotsLocked(false);
        attackLines.SetPinned(null);
        IsIntroComplete = true;
    }

    private bool MatchesCondition(string condition)
    {
        if (string.IsNullOrWhiteSpace(condition))
        {
            return true;
        }

        if (!hasResult)
        {
            return false;
        }

        return condition switch
        {
            ConditionWin => !IsFailure(lastResult),
            ConditionFail => IsFailure(lastResult),
            ConditionPerfect => lastResult == SimulationController.SimulationResult.PerfectWin,
            _ => true,
        };
    }

    private static bool IsFailure(SimulationController.SimulationResult result)
    {
        return result == SimulationController.SimulationResult.Lose ||
               result == SimulationController.SimulationResult.AllyDeadLose;
    }

    /// <summary>
    /// 현재 단계 뒤에서 이번 결과에 해당하는 waitRetry를 찾아 그 gotoId 단계 인덱스를 돌려준다.
    /// </summary>
    private int FindRetryJumpIndex(int currentIndex)
    {
        for (int index = currentIndex + 1; index < steps.Length; index++)
        {
            TutorialStepData step = steps[index];
            if (step != null && step.type == "waitRetry" && MatchesCondition(step.condition))
            {
                view.SetVisible(false);
                spotlight.Clear();
                ClearRetryState();
                return FindStepIndex(step.gotoId);
            }
        }

        return -1;
    }

    private void ClearRetryState()
    {
        hasResult = false;
        isAfterResult = false;
        retryRequested = false;
    }

    private int FindStepIndex(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return -1;
        }

        for (int index = 0; index < steps.Length; index++)
        {
            if (steps[index] != null && steps[index].id == id)
            {
                return index;
            }
        }

        Debug.LogWarning($"[GuidedTutorialDirector] gotoId not found: {id}", this);
        return -1;
    }

    private static bool IsPlaced(PieceType pieceType, int cellIndex, Direction? facing)
    {
        PieceBase piece = FindPlacedPiece(pieceType, cellIndex);
        return piece != null && (facing == null || piece.FacingDirection == facing.Value);
    }

    private static PieceBase FindPlacedPiece(PieceType pieceType, int cellIndex)
    {
        StageManager stageManager = StageManager.Instance;
        if (stageManager == null)
        {
            return null;
        }

        int boardSize = stageManager.CurrentStageData?.boardSize ?? 6;
        IReadOnlyList<PieceBase> pieces = stageManager.GetAllActivePieces();
        for (int index = 0; index < pieces.Count; index++)
        {
            PieceBase piece = pieces[index];
            if (piece == null || piece.Faction != Faction.Ally || piece.PieceType != pieceType)
            {
                continue;
            }

            // Retry 되감기 중에도 배치 상태를 맞게 읽도록 현재 좌표 대신 배치 시점 좌표를 쓴다.
            int pieceCell = StageGridIndexUtility.ToCellIndex(boardSize, piece.SpawnGridX, piece.SpawnGridY);
            if (pieceCell == cellIndex)
            {
                return piece;
            }
        }

        return null;
    }

    private void CacheSlots()
    {
        slots.Clear();
        slots.AddRange(FindObjectsByType<InGameUnitStorageSlotUI>(FindObjectsSortMode.None));
    }

    private RectTransform FindSlotRect(PieceType pieceType)
    {
        for (int index = 0; index < slots.Count; index++)
        {
            if (slots[index] != null && slots[index].UnitType == pieceType)
            {
                return slots[index].transform as RectTransform;
            }
        }

        return null;
    }

    private bool AreAllSlotsPlaced()
    {
        if (placement != null && placement.IsPlacing)
        {
            return false;
        }

        for (int index = 0; index < slots.Count; index++)
        {
            if (slots[index] != null && slots[index].RemainingDeployableCount > 0)
            {
                return false;
            }
        }

        return true;
    }

    private static float GetCellSize()
    {
        GameManager gameManager = GameManager.Instance;
        if (gameManager == null)
        {
            return 1.3f;
        }

        // 인접한 두 칸 중심 거리를 칸 크기로 사용한다. 인덱스 0과 1은 같은 줄의 이웃 칸이다.
        float size = Vector3.Distance(gameManager.GetCellPosition(0), gameManager.GetCellPosition(1));
        return size > 0.01f ? size : 1.3f;
    }

    /// <summary>
    /// 슬롯을 눌러야 하는 단계에서는 강조 오버레이 → 대사창 → Storages 순으로 두어 슬롯이 대사창 위에 보이게 하고,
    /// 글자 폭도 슬롯을 피해 줄인다. 그 외에는 강조 오버레이 → 대사창을 맨 앞으로 올려 전체 너비를 쓴다.
    /// 어느 쪽이든 대사창이 강조 오버레이보다 앞이라 안내 문구는 어두워지지 않는다.
    /// </summary>
    private void SetPanelBehindStorage(bool behind)
    {
        if (view == null)
        {
            return;
        }

        Transform panel = view.transform;
        bool canGoBehind = behind && storageRoot != null;
        if (canGoBehind)
        {
            MoveJustBefore(panel, storageRoot);
            if (spotlight != null)
            {
                MoveJustBefore(spotlight.transform, panel);
            }
        }
        else
        {
            spotlight?.transform.SetAsLastSibling();
            panel.SetAsLastSibling();
        }

        view.SetCompact(canGoBehind);
    }

    private static void MoveJustBefore(Transform target, Transform anchor)
    {
        int anchorIndex = anchor.GetSiblingIndex();
        int targetIndex = target.GetSiblingIndex();
        // 앞에서 빼 오면 anchor 인덱스가 하나 당겨지므로 그만큼 보정한다.
        target.SetSiblingIndex(targetIndex < anchorIndex ? anchorIndex - 1 : anchorIndex);
    }

    private static Transform FindStorageRoot(RectTransform parent)
    {
        InGameStorageView storageView = FindFirstObjectByType<InGameStorageView>();
        Transform current = storageView != null ? storageView.transform : null;
        while (current != null && current.parent != parent)
        {
            current = current.parent;
        }

        return current;
    }

    private void SetModal(bool isModal)
    {
        if (isModal && modalLease == null)
        {
            modalLease = StageInputModalGate.Acquire();
        }
        else if (!isModal && modalLease != null)
        {
            modalLease.Dispose();
            modalLease = null;
        }
    }

    #endregion

    #region Input

    private void HandleSpacePerformed(InputAction.CallbackContext _)
    {
        HandleAdvanceRequested();
    }

    private void HandleAdvanceRequested()
    {
        // 클릭과 Space가 같은 프레임에 들어와도 한 번만 처리하고, 숨겨진 동안에는 받지 않는다.
        if (isSuppressed || lastAdvanceFrame == Time.frameCount)
        {
            return;
        }

        lastAdvanceFrame = Time.frameCount;
        advanceRequested = true;
    }

    private void HandleHitConfirmed(CombatHitContext context)
    {
        // 같은 진영끼리 맞은 순간을 오인사격으로 본다.
        if (context.Attacker.Piece != null &&
            context.Victim.Piece != null &&
            context.Attacker.Faction == context.Victim.Faction)
        {
            friendlyFireSeen = true;
        }
    }

    private void HandleSimulationReset()
    {
        retryRequested = true;
    }

    #endregion
}
