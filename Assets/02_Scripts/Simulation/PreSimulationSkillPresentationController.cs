using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 선처리 스킬의 게임 효과와 선택적인 Presentation을 목록 순서대로 실행하는 Controller다.
/// 특정 스킬, 카메라, UI를 알지 않고 각 Presentation의 완료만 기다린다.
/// </summary>
public sealed class PreSimulationSkillPresentationController : MonoBehaviour
{
    [SerializeField] private List<PreSimulationSkillPresentation> presentations = new();

    private PreSimulationSkillPresentation activePresentation;
    private int sequenceVersion;

    public bool IsRunning { get; private set; }

    private void OnDisable()
    {
        CancelCurrentSequence();
    }

    /// <summary>
    /// 호출자가 전달한 순서를 그대로 사용해 실행 가능한 선처리 스킬을 하나씩 처리한다.
    /// 복수 스킬의 우선순위 정책은 이 Controller가 결정하지 않는다.
    /// </summary>
    public IEnumerator PlayInOrder(
        IReadOnlyList<SkillBase> skills,
        SimulationController simulationController,
        Func<IReadOnlyList<PieceBase>> getActivePieces)
    {
        CancelCurrentSequence();

        int currentVersion = ++sequenceVersion;
        IsRunning = true;

        if (skills == null)
        {
            IsRunning = false;
            yield break;
        }

        for (int index = 0; index < skills.Count; index++)
        {
            if (currentVersion != sequenceVersion)
            {
                yield break;
            }

            SkillBase skill = skills[index];
            if (skill == null || skill.Timing != SkillTiming.PreSimulation)
            {
                continue;
            }

            IReadOnlyList<PieceBase> activePieces = GetActivePiecesSafely(getActivePieces);
            if (!CanExecuteSafely(skill, activePieces))
            {
                continue;
            }

            Debug.Log($"[Simulation] Executing Pre-Simulation Skill: {skill.SkillName}", this);

            PreSimulationPresentationContext context = new(
                skill,
                simulationController,
                activePieces);
            activePresentation = FindPresentation(skill);

            if (activePresentation != null)
            {
                IEnumerator presentationRoutine = CreatePresentationRoutine(activePresentation, context);
                if (presentationRoutine != null)
                {
                    PresentationExecutionState executionState = new();
                    yield return RunSafely(presentationRoutine, executionState, currentVersion);

                    if (executionState.Exception != null)
                    {
                        Debug.LogException(executionState.Exception, activePresentation);
                        CancelPresentationSafely(activePresentation);
                    }
                }
            }

            if (currentVersion != sequenceVersion)
            {
                yield break;
            }

            // Presentation이 효과 시점을 지정하지 않았거나 연결되지 않은 경우 기존 즉시 효과를 보장한다.
            ApplyEffectSafely(context);
            activePresentation = null;
        }

        if (currentVersion == sequenceVersion)
        {
            IsRunning = false;
        }
    }

    /// <summary>
    /// 현재 Presentation을 즉시 정리하고 실행 중인 순차 흐름을 무효화한다.
    /// </summary>
    public void CancelCurrentSequence()
    {
        sequenceVersion++;

        if (activePresentation != null)
        {
            CancelPresentationSafely(activePresentation);
            activePresentation = null;
        }

        IsRunning = false;
    }

    private PreSimulationSkillPresentation FindPresentation(SkillBase skill)
    {
        if (presentations == null)
        {
            return null;
        }

        for (int index = 0; index < presentations.Count; index++)
        {
            PreSimulationSkillPresentation presentation = presentations[index];
            if (presentation == null)
            {
                continue;
            }

            try
            {
                if (presentation.CanPresent(skill))
                {
                    return presentation;
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, presentation);
            }
        }

        return null;
    }

    private IReadOnlyList<PieceBase> GetActivePiecesSafely(
        Func<IReadOnlyList<PieceBase>> getActivePieces)
    {
        try
        {
            return getActivePieces?.Invoke() ?? Array.Empty<PieceBase>();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            return Array.Empty<PieceBase>();
        }
    }

    private bool CanExecuteSafely(
        SkillBase skill,
        IReadOnlyList<PieceBase> activePieces)
    {
        try
        {
            return skill.CanExecute(activePieces);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            return false;
        }
    }

    private static void CancelPresentationSafely(PreSimulationSkillPresentation presentation)
    {
        if (presentation == null)
        {
            return;
        }

        try
        {
            presentation.CancelPresentation();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, presentation);
        }
    }

    private void ApplyEffectSafely(PreSimulationPresentationContext context)
    {
        try
        {
            context.TryApplyEffect();
        }
        catch (Exception exception)
        {
            // 한 스킬 효과의 예외가 공용 순차 흐름을 영구 대기 상태로 남기지 않게 기록 후 다음 항목으로 진행한다.
            Debug.LogException(exception, this);
        }
    }

    private static IEnumerator CreatePresentationRoutine(
        PreSimulationSkillPresentation presentation,
        PreSimulationPresentationContext context)
    {
        try
        {
            return presentation.Play(context);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, presentation);
            return null;
        }
    }

    /// <summary>
    /// 중첩 IEnumerator까지 직접 순회해 Presentation 예외가 전체 Simulation 코루틴을 멈추지 않게 한다.
    /// </summary>
    private IEnumerator RunSafely(
        IEnumerator rootRoutine,
        PresentationExecutionState executionState,
        int currentVersion)
    {
        Stack<IEnumerator> routines = new();
        routines.Push(rootRoutine);

        while (routines.Count > 0 && currentVersion == sequenceVersion)
        {
            IEnumerator routine = routines.Peek();
            bool hasNext;
            object yieldedValue = null;

            try
            {
                hasNext = routine.MoveNext();
                if (hasNext)
                {
                    yieldedValue = routine.Current;
                }
            }
            catch (Exception exception)
            {
                executionState.Exception = exception;
                break;
            }

            if (!hasNext)
            {
                DisposeRoutine(routines.Pop());
                continue;
            }

            if (yieldedValue is IEnumerator nestedRoutine)
            {
                routines.Push(nestedRoutine);
                continue;
            }

            yield return yieldedValue;
        }

        while (routines.Count > 0)
        {
            DisposeRoutine(routines.Pop());
        }
    }

    private static void DisposeRoutine(IEnumerator routine)
    {
        if (routine is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private sealed class PresentationExecutionState
    {
        public Exception Exception { get; set; }
    }
}
