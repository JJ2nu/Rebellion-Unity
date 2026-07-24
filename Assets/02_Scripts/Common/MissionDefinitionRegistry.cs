using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stage JSON의 missionId를 실제 미션 정의 에셋으로 변환하는 단일 목록이다.
/// </summary>
[CreateAssetMenu(fileName = "MissionDefinitionRegistry", menuName = "Rebellion/Missions/Mission Definition Registry")]
public sealed class MissionDefinitionRegistry : ScriptableObject
{
    [SerializeField] private MissionDefinition[] definitions = Array.Empty<MissionDefinition>();

    private readonly Dictionary<string, MissionDefinition> definitionsById =
        new(StringComparer.Ordinal);
    private bool isCacheBuilt;

    public IReadOnlyList<MissionDefinition> Definitions => definitions;

    public bool TryGetDefinition(string missionId, out MissionDefinition definition)
    {
        EnsureCache();
        definition = null;
        return !string.IsNullOrWhiteSpace(missionId) &&
            definitionsById.TryGetValue(missionId, out definition);
    }

    public bool TryValidate(out string error)
    {
        definitionsById.Clear();
        isCacheBuilt = true;

        if (definitions == null)
        {
            definitionsById.Clear();
            error = "Definitions array is null.";
            return false;
        }

        for (int index = 0; index < definitions.Length; index++)
        {
            MissionDefinition definition = definitions[index];
            if (definition == null)
            {
                definitionsById.Clear();
                error = $"Definition element {index} is null.";
                return false;
            }

            string missionId = definition.MissionId;
            if (string.IsNullOrWhiteSpace(missionId))
            {
                definitionsById.Clear();
                error = $"Definition '{definition.name}' has an empty missionId.";
                return false;
            }

            if (!definitionsById.TryAdd(missionId, definition))
            {
                definitionsById.Clear();
                error = $"Duplicate missionId '{missionId}'.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private void OnEnable()
    {
        isCacheBuilt = false;
    }

    private void OnValidate()
    {
        isCacheBuilt = false;
    }

    private void EnsureCache()
    {
        if (isCacheBuilt)
        {
            return;
        }

        TryValidate(out _);
    }
}
