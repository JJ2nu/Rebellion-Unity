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
