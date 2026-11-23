using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// StreamingAssets/Stages의 tutorialSteps를 실행 전에 점검한다.
/// 알 수 없는 단계 종류, 없는 gotoId, 보드 밖 칸, 가이드 기물과 맞지 않는 강제 배치를 콘솔에 보고한다.
/// </summary>
public static class TutorialStepValidator
{
    private static readonly HashSet<string> KnownTypes = new()
    {
        "line", "place", "placeAll", "start", "result", "waitRetry", "enemyLines",
    };

    private static readonly HashSet<string> KnownConditions = new() { "", "win", "fail", "perfect" };

    [MenuItem("Rebellion/Tutorial/Validate Tutorial Steps")]
    public static void ValidateAll()
    {
        string folder = Path.Combine(Application.streamingAssetsPath, "Stages");
        int checkedStages = 0;
        int problems = 0;

        foreach (string path in Directory.GetFiles(folder, "*.json"))
        {
            StageData data = JsonUtility.FromJson<StageData>(File.ReadAllText(path));
            if (data == null || !data.HasGuidedTutorial)
            {
                continue;
            }

            checkedStages++;
            problems += Validate(Path.GetFileNameWithoutExtension(path), data);
        }

        Debug.Log($"[TutorialStepValidator] Checked {checkedStages} tutorial stage(s), {problems} problem(s).");
    }

    private static int Validate(string stageId, StageData data)
    {
        int problems = 0;
        HashSet<string> ids = new();
        foreach (TutorialStepData step in data.tutorialSteps)
        {
            if (step != null && !string.IsNullOrWhiteSpace(step.id))
            {
                ids.Add(step.id);
            }
        }

        int cellCount = data.boardSize * data.boardSize;
        for (int index = 0; index < data.tutorialSteps.Length; index++)
        {
            TutorialStepData step = data.tutorialSteps[index];
            string label = $"{stageId} step {index}";
            if (step == null)
            {
                problems += Report($"{label}: null step.");
                continue;
            }

            if (!KnownTypes.Contains(step.type))
            {
                problems += Report($"{label}: unknown type '{step.type}'.");
            }

            if (!KnownConditions.Contains(step.condition ?? string.Empty))
            {
                problems += Report($"{label}: unknown condition '{step.condition}'.");
            }

            if (step.type == "waitRetry" && !string.IsNullOrWhiteSpace(step.gotoId) && !ids.Contains(step.gotoId))
            {
                problems += Report($"{label}: gotoId '{step.gotoId}' does not exist.");
            }

            if (step.type != "place")
            {
                continue;
            }

            if (step.cellIndex < 0 || step.cellIndex >= cellCount)
            {
                problems += Report($"{label}: cellIndex {step.cellIndex} is outside the board.");
            }

            if (data.GetAllyCount((PieceType)step.pieceType) <= 0)
            {
                problems += Report($"{label}: no ally slot for pieceType {step.pieceType}.");
            }

            bool hasMatchingGhost = false;
            foreach (TutorialGhostPieceData ghost in data.tutorialGhostPieces)
            {
                if (ghost != null && ghost.cellIndex == step.cellIndex)
                {
                    hasMatchingGhost = ghost.ghostType == step.pieceType && (step.facing < 0 || ghost.facing == step.facing);
                }
            }

            if (!hasMatchingGhost)
            {
                problems += Report($"{label}: no tutorial ghost with the same type and facing at cell {step.cellIndex}.");
            }

            foreach (StageEntityData entity in data.entities)
            {
                if (entity != null && entity.cellIndex == step.cellIndex)
                {
                    problems += Report($"{label}: cell {step.cellIndex} is occupied by an entity.");
                }
            }
        }

        return problems;
    }

    private static int Report(string message)
    {
        Debug.LogWarning($"[TutorialStepValidator] {message}");
        return 1;
    }
}
