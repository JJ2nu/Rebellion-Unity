// 오디오드라마 CSV를 읽고 Stage ID로 조회 가능한 테이블을 만든다.

using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

public sealed class AudioDramaDataTable
{
    #region Constants

    private const int HeaderRowIndex = 1;
    private const int DataStartRowIndex = 2;

    #endregion

    #region Fields

    private readonly Dictionary<string, AudioDramaData> rowsByStageId = new();

    #endregion

    #region Public Methods

    public static AudioDramaDataTable FromCsv(TextAsset csv)
    {
        AudioDramaDataTable table = new();

        if (csv == null)
        {
            Debug.LogError("AudioDrama CSV is not assigned.");
            return table;
        }

        table.Parse(csv.text);
        return table;
    }

    public bool TryGetByStageId(string stageId, out AudioDramaData data)
    {
        if (string.IsNullOrWhiteSpace(stageId))
        {
            data = null;
            return false;
        }

        return rowsByStageId.TryGetValue(stageId.Trim(), out data);
    }

    #endregion

    #region Parsing

    private void Parse(string csvText)
    {
        List<string[]> records = ParseRecords(csvText);

        if (records.Count <= DataStartRowIndex)
        {
            Debug.LogWarning("AudioDrama CSV has no data rows.");
            return;
        }

        Dictionary<string, int> headers = BuildHeaderMap(records[HeaderRowIndex]);

        for (int rowIndex = DataStartRowIndex; rowIndex < records.Count; rowIndex++)
        {
            string[] row = records[rowIndex];
            string stageId = Read(row, headers, "Stage ID");
            string audioId = Read(row, headers, "Audio");
            string dialogueId = Read(row, headers, "DialogueID");
            string dialogueText = Read(row, headers, "DialogueText");

            if (string.IsNullOrWhiteSpace(stageId) ||
                string.IsNullOrWhiteSpace(audioId) ||
                string.IsNullOrWhiteSpace(dialogueId))
            {
                continue;
            }

            if (!rowsByStageId.TryGetValue(stageId, out AudioDramaData data))
            {
                data = new AudioDramaData(stageId, audioId);
                rowsByStageId.Add(stageId, data);
            }

            data.AddLine(new AudioDramaLineData(
                dialogueId,
                dialogueText,
                ReadFloat(row, headers, "Start Time"),
                ReadFloat(row, headers, "End Time")));
        }

        foreach (AudioDramaData data in rowsByStageId.Values)
        {
            data.SortLinesByTime();
        }
    }

    #endregion

    #region Column Readers

    private static Dictionary<string, int> BuildHeaderMap(string[] headerRow)
    {
        Dictionary<string, int> headers = new();

        for (int index = 0; index < headerRow.Length; index++)
        {
            string key = headerRow[index].Trim();
            if (!string.IsNullOrEmpty(key))
            {
                headers[key] = index;
            }
        }

        return headers;
    }

    private static string Read(string[] row, Dictionary<string, int> headers, string key)
    {
        if (!headers.TryGetValue(key, out int index) || index < 0 || index >= row.Length)
        {
            Debug.LogWarning($"AudioDrama CSV column is missing: {key}");
            return string.Empty;
        }

        return row[index].Trim();
    }

    private static float ReadFloat(string[] row, Dictionary<string, int> headers, string key)
    {
        string value = Read(row, headers, key);

        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
        {
            return result;
        }

        Debug.LogWarning($"Invalid float in AudioDrama CSV. Column: {key}, Value: {value}");
        return 0f;
    }

    #endregion

    #region CSV Parser

    private static List<string[]> ParseRecords(string csvText)
    {
        List<string[]> records = new();
        List<string> fields = new();
        StringBuilder field = new();
        bool inQuotes = false;

        for (int index = 0; index < csvText.Length; index++)
        {
            char c = csvText[index];

            if (c == '"')
            {
                if (inQuotes && index + 1 < csvText.Length && csvText[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(field.ToString());
                field.Length = 0;
            }
            else if ((c == '\n' || c == '\r') && !inQuotes)
            {
                if (c == '\r' && index + 1 < csvText.Length && csvText[index + 1] == '\n')
                {
                    index++;
                }

                AddRecord(records, fields, field);
            }
            else
            {
                field.Append(c);
            }
        }

        AddRecord(records, fields, field);
        return records;
    }

    private static void AddRecord(List<string[]> records, List<string> fields, StringBuilder field)
    {
        fields.Add(field.ToString());
        field.Length = 0;

        if (!IsEmptyRecord(fields))
        {
            records.Add(fields.ToArray());
        }

        fields.Clear();
    }

    private static bool IsEmptyRecord(List<string> fields)
    {
        for (int index = 0; index < fields.Count; index++)
        {
            if (!string.IsNullOrWhiteSpace(fields[index]))
            {
                return false;
            }
        }

        return true;
    }

    #endregion
}
