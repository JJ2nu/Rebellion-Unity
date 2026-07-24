// 오디오드라마 한 편과 시간별 대사 데이터를 보관한다.

using System.Collections.Generic;

public sealed class AudioDramaData
{
    #region Properties

    public string StageId { get; }
    public string AudioId { get; }
    public IReadOnlyList<AudioDramaLineData> Lines => lines;

    #endregion

    #region Fields

    private readonly List<AudioDramaLineData> lines = new();

    #endregion

    #region Constructors

    public AudioDramaData(string stageId, string audioId)
    {
        StageId = stageId;
        AudioId = audioId;
    }

    #endregion

    #region Public Methods

    public void AddLine(AudioDramaLineData line)
    {
        if (line != null)
        {
            lines.Add(line);
        }
    }

    public void SortLinesByTime()
    {
        lines.Sort((left, right) => left.StartTime.CompareTo(right.StartTime));
    }

    #endregion
}

public sealed class AudioDramaLineData
{
    #region Properties

    public string DialogueId { get; }
    public string Text { get; }
    public float StartTime { get; }
    public float EndTime { get; }

    #endregion

    #region Constructors

    public AudioDramaLineData(string dialogueId, string text, float startTime, float endTime)
    {
        DialogueId = dialogueId;
        Text = text;
        StartTime = startTime;
        EndTime = endTime;
    }

    #endregion
}
