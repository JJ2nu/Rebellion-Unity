using System;

public sealed class DialogueLineData
{
    #region Properties

    public string DialogueId { get; }
    public string Level { get; }
    public int SequenceNo { get; }
    public string Phase { get; }
    public string LineType { get; }
    public string SimulationResult { get; }
    public string SpeakerId { get; }
    public string SpeakerName { get; }
    public string CharacterImage { get; }
    public string DialogueText { get; }
    public DialogueNextAction NextAction { get; }
    public bool Enabled { get; }
    public bool IsDummy { get; }

    #endregion

    #region Constructors

    public DialogueLineData(
        string dialogueId,
        string level,
        int sequenceNo,
        string phase,
        string lineType,
        string simulationResult,
        string speakerId,
        string speakerName,
        string characterImage,
        string dialogueText,
        DialogueNextAction nextAction,
        bool enabled)
    {
        DialogueId = dialogueId;
        Level = level;
        SequenceNo = sequenceNo;
        Phase = phase;
        LineType = lineType;
        SimulationResult = simulationResult;
        SpeakerId = speakerId;
        SpeakerName = speakerName;
        CharacterImage = characterImage;
        DialogueText = dialogueText;
        NextAction = nextAction;
        Enabled = enabled;
        IsDummy = ContainsDummyText(dialogueText);
    }

    #endregion

    #region Private Methods

    private static bool ContainsDummyText(string text)
    {
        return !string.IsNullOrEmpty(text) &&
               (text.Contains("더미데이터", StringComparison.Ordinal) ||
                text.Contains("더미 데이터", StringComparison.Ordinal));
    }

    #endregion
}
