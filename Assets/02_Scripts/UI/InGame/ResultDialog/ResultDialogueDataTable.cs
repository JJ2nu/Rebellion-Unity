using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public sealed class ResultDialogueDataTable
{
    private const string AnyStageId = "*";

    private readonly List<ResultDialogueLineData> lines = new();

    public static ResultDialogueDataTable FromCsv(TextAsset csv)
    {
        ResultDialogueDataTable table = new();

        if (csv == null)
        {
            Debug.LogError("Result dialogue CSV is not assigned.");
            return table;
        }

        table.Parse(csv.text);
        return table;
    }

    public bool TryGetLine(
        string stageId,
        SimulationController.SimulationResult result,
        out ResultDialogueLineData line)
    {
        string normalizedStageId = string.IsNullOrWhiteSpace(stageId) ? string.Empty : stageId.Trim();
        List<ResultDialogueLineData> exactMatches = FindMatches(normalizedStageId, result);

        if (exactMatches.Count > 1)
        {
            Debug.LogError($"Duplicate result dialogue rows. Stage: {normalizedStageId}, Result: {result}");
            line = null;
            return false;
        }

        if (exactMatches.Count == 1)
        {
            line = exactMatches[0];
            return true;
        }

        List<ResultDialogueLineData> fallbackMatches = FindMatches(AnyStageId, result);
        if (fallbackMatches.Count > 1)
        {
            Debug.LogError($"Duplicate fallback result dialogue rows. Result: {result}");
            line = null;
            return false;
        }

        if (fallbackMatches.Count == 1)
        {
            line = fallbackMatches[0];
            return true;
        }

        Debug.LogError($"Result dialogue row was not found. Stage: {normalizedStageId}, Result: {result}");
        line = null;
        return false;
    }

    private List<ResultDialogueLineData> FindMatches(
        string stageId,
        SimulationController.SimulationResult result)
    {
        return lines
            .Where(item => item.Enabled && item.StageId == stageId && item.SimulationResult == result)
            .ToList();
    }

    private void Parse(string csvText)
    {
        List<string[]> records = ParseRecords(csvText);
        if (records.Count <= 1)
        {
            Debug.LogWarning("Result dialogue CSV has no data rows.");
            return;
        }

        Dictionary<string, int> headers = BuildHeaderMap(records[0]);

        for (int rowIndex = 1; rowIndex < records.Count; rowIndex++)
        {
            string[] row = records[rowIndex];
            string stageId = Read(row, headers, "StageId");
            string resultValue = Read(row, headers, "SimulationResult");
            string dialogueText = Read(row, headers, "DialogueText");

            if (string.IsNullOrWhiteSpace(stageId) || string.IsNullOrWhiteSpace(dialogueText))
            {
                Debug.LogError($"Invalid result dialogue row: {rowIndex + 1}");
                continue;
            }

            if (!Enum.TryParse(resultValue, true, out SimulationController.SimulationResult result))
            {
                Debug.LogError($"Invalid SimulationResult in result dialogue CSV. Row: {rowIndex + 1}, Value: {resultValue}");
                continue;
            }

            lines.Add(new ResultDialogueLineData(
                stageId,
                result,
                Read(row, headers, "SpeakerName"),
                dialogueText,
                Read(row, headers, "CharacterState"),
                ReadBool(row, headers, "Enabled")));
        }
    }

    private static Dictionary<string, int> BuildHeaderMap(string[] headerRow)
    {
        Dictionary<string, int> headers = new();

        for (int index = 0; index < headerRow.Length; index++)
        {
            string key = headerRow[index].Trim();
            if (!string.IsNullOrWhiteSpace(key))
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
            Debug.LogWarning($"Result dialogue CSV column is missing: {key}");
            return string.Empty;
        }

        return row[index].Trim();
    }

    private static bool ReadBool(string[] row, Dictionary<string, int> headers, string key)
    {
        string value = Read(row, headers, key);
        return value.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || value == "1";
    }

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

        if (fields.Any(value => !string.IsNullOrWhiteSpace(value)))
        {
            records.Add(fields.ToArray());
        }

        fields.Clear();
    }
}
