using UnityEngine;

/// <summary>
/// 여러 Stage에서 재사용하는 미션 문구와 판정 메타데이터를 한 에셋에 보관한다.
/// Stage JSON은 이 에셋의 missionId만 저장한다.
/// </summary>
[CreateAssetMenu(fileName = "MissionDefinition", menuName = "Rebellion/Missions/Mission Definition")]
public sealed class MissionDefinition : ScriptableObject
{
    [SerializeField] private string missionId;
    [SerializeField] private string displayText;
    [SerializeField] private MissionType missionType;
    [SerializeField] private MissionEvaluationTiming evaluationTiming;

    public string MissionId => missionId;
    public string DisplayText => displayText;
    public MissionType MissionType => missionType;
    public MissionEvaluationTiming EvaluationTiming => evaluationTiming;
}
