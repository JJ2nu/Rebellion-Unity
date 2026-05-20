using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

public sealed class DialogueDataTable
{
    #region Constants

    private const string AnyResultKey = "Any";
    private const string ResultPhaseKey = "Result";
    private const string CivilianDeadWinKey = "CivilianDeadWin";
    private const string BothDeadWinKey = "BothDeadWin";

    #endregion

    #region Fields

    private readonly List<DialogueLineData> lines = new();

    #endregion

    #region Public Methods

    public static DialogueDataTable FromCsv(TextAsset csv)
    {
        DialogueDataTable table = new();

        if (csv == null)
        {
            Debug.LogError("Dialogue CSV is not assigned.");
            return table;
        }

        table.Parse(csv.text);
        return table;
    }

    public List<DialogueLineData> GetLines(string level, SimulationController.SimulationResult result)
    {
        if (string.IsNullOrWhiteSpace(level))
        {
            Debug.LogError("Dialogue Level이 비어 있습니다.");
            return new List<DialogueLineData>();
        }

        if (result == SimulationController.SimulationResult.Lose)
        {
            Debug.LogError("Lose 결과는 다이얼로그 씬으로 전달되면 안 됩니다.");
            return new List<DialogueLineData>();
        }

        string normalizedLevel = level.Trim();

        if (IsImpossibleCivilianResult(normalizedLevel, result))
        {
            Debug.LogWarning($"{normalizedLevel}에서는 {result}이 나올 수 없는 결과입니다.");
            return new List<DialogueLineData>();
        }

        string resultKey = ResolveResultKey(normalizedLevel, result);

        return lines
            .Where(line =>
                line.Enabled &&
                !line.IsDummy &&
                line.Level == normalizedLevel &&
                (line.SimulationResult == resultKey || line.SimulationResult == AnyResultKey))
            .OrderBy(line => line.SequenceNo)
            .ThenBy(line => line.DialogueId)
            .ToList();
    }

    #endregion

    #region Result Rules

    private string ResolveResultKey(string level, SimulationController.SimulationResult result)
    {
        if (result != SimulationController.SimulationResult.BothDeadWin)
        {
            return result.ToString();
        }

        if (HasSpecificResultLines(level, BothDeadWinKey))
        {
            return BothDeadWinKey;
        }

        return CivilianDeadWinKey;
    }

    private bool IsImpossibleCivilianResult(string level, SimulationController.SimulationResult result)
    {
        if (result != SimulationController.SimulationResult.CivilianDeadWin &&
            result != SimulationController.SimulationResult.BothDeadWin)
        {
            return false;
        }

        return !HasSpecificResultLines(level, CivilianDeadWinKey);
    }

    private bool HasSpecificResultLines(string level, string resultKey)
    {
        return lines.Any(line =>
            line.Enabled &&
            !line.IsDummy &&
            line.Level == level &&
            line.SimulationResult == resultKey &&
            line.Phase == ResultPhaseKey);
    }

    #endregion

    #region Parsing

    private void Parse(string csvText)
    {
        List<string[]> records = ParseRecords(csvText);

        if (records.Count <= 1)
        {
            Debug.LogWarning("Dialogue CSV has no data rows.");
            return;
        }

        Dictionary<string, int> headers = BuildHeaderMap(records[0]);

        for (int rowIndex = 1; rowIndex < records.Count; rowIndex++)
        {
            string[] row = records[rowIndex];

            string dialogueText = Read(row, headers, "DialogueText");
            bool enabled = ReadBool(row, headers, "Enabled");

            DialogueLineData line = new(
                Read(row, headers, "DialogueID"),
                Read(row, headers, "Level"),
                ReadInt(row, headers, "SequenceNo"),
                Read(row, headers, "Phase"),
                Read(row, headers, "LineType"),
                Read(row, headers, "SimulationResult"),
                Read(row, headers, "SpeakerId"),
                Read(row, headers, "SpeakerName"),
                Read(row, headers, "CharacterImage"),
                dialogueText,
                ReadNextAction(row, headers, "NextAction"),
                enabled);

            if (!string.IsNullOrWhiteSpace(line.Level) &&
                !string.IsNullOrWhiteSpace(line.DialogueId))
            {
                lines.Add(line);
            }
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
            Debug.LogWarning($"Dialogue CSV column is missing: {key}");
            return string.Empty;
        }

        return row[index].Trim();
    }

    private static int ReadInt(string[] row, Dictionary<string, int> headers, string key)
    {
        string value = Read(row, headers, key);

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
        {
            return result;
        }

        Debug.LogWarning($"Invalid int in Dialogue CSV. Column: {key}, Value: {value}");
        return 0;
    }

    private static bool ReadBool(string[] row, Dictionary<string, int> headers, string key)
    {
        string value = Read(row, headers, key);

        return value.Equals("TRUE", System.StringComparison.OrdinalIgnoreCase) ||
               value.Equals("True", System.StringComparison.OrdinalIgnoreCase) ||
               value == "1";
    }

    private static DialogueNextAction ReadNextAction(string[] row, Dictionary<string, int> headers, string key)
    {
        string value = Read(row, headers, key);

        if (value == nameof(DialogueNextAction.NextStage))
        {
            return DialogueNextAction.NextStage;
        }

        return DialogueNextAction.Next;
    }

    #endregion

    #region CSV Parser

    private static List<string[]> ParseRecords(string csvText)
    {
        List<string[]> records = new();
        List<string> fields = new();
        StringBuilder field = new();
        bool inQuotes = false;

        if (!string.IsNullOrEmpty(csvText) && csvText[0] == '\ufeff')
        {
            csvText = csvText[1..];
        }

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
