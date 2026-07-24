/// <summary>
/// 판정 대사 CSV 한 행을 런타임에서 사용하는 불변 데이터로 보관한다.
/// </summary>
public sealed class ResultDialogueLineData
{
    public string StageId { get; }
    public SimulationController.SimulationResult SimulationResult { get; }
    public string SpeakerName { get; }
    public string DialogueText { get; }
    public string CharacterState { get; }
    public bool Enabled { get; }

    public ResultDialogueLineData(
        string stageId,
        SimulationController.SimulationResult simulationResult,
        string speakerName,
        string dialogueText,
        string characterState,
        bool enabled)
    {
        StageId = stageId;
        SimulationResult = simulationResult;
        SpeakerName = speakerName;
        DialogueText = dialogueText;
        CharacterState = characterState;
        Enabled = enabled;
    }
}
