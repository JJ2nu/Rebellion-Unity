// 인게임 UI CSV를 파싱하고 Level 이름으로 데이터를 찾을 수 있게 관리한다.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

public sealed class InGameUIDataTable
{
    #region Fields

    private readonly Dictionary<string, InGameUIData> rows = new();

    #endregion

    #region Public Methods

    public static InGameUIDataTable FromCsv(TextAsset csv)
    {
        InGameUIDataTable table = new();

        if (csv == null)
        {
            Debug.LogError("InGame UI CSV is not assigned.");
            return table;
        }

        table.Parse(csv.text);
        return table;
    }

    public bool TryGet(string level, out InGameUIData data)
    {
        if (string.IsNullOrWhiteSpace(level))
        {
            data = null;
            return false;
        }

        return rows.TryGetValue(level.Trim(), out data);
    }

    #endregion

    #region Parsing

    private void Parse(string csvText)
    {
        List<string[]> records = ParseRecords(csvText);

        if (records.Count <= 1)
        {
            Debug.LogWarning("InGame UI CSV has no data rows.");
            return;
        }

        Dictionary<string, int> headers = BuildHeaderMap(records[0]);

        for (int rowIndex = 1; rowIndex < records.Count; rowIndex++)
        {
            string[] row = records[rowIndex];
            string level = Read(row, headers, "Level");

            if (string.IsNullOrWhiteSpace(level))
            {
                continue;
            }

            InGameUIData data = new(
                level.Trim(),
                Read(row, headers, "MainMission"),
                Read(row, headers, "SubMission_1"),
                Read(row, headers, "SubMission_2"),
                ReadInt(row, headers, "Brawler"),
                ReadInt(row, headers, "Slasher"),
                ReadInt(row, headers, "Gunman"),
                ReadBool(row, headers, "Order"));

            rows[data.Level] = data;
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
            Debug.LogWarning($"InGame UI CSV column is missing: {key}");
            return string.Empty;
        }

        return row[index].Trim();
    }

    private static int ReadInt(string[] row, Dictionary<string, int> headers, string key)
    {
        string value = Read(row, headers, key);
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
        {
            return result;
        }

        Debug.LogWarning($"Invalid int in InGame UI CSV. Column: {key}, Value: {value}");
        return 0;
    }

    private static bool ReadBool(string[] row, Dictionary<string, int> headers, string key)
    {
        string value = Read(row, headers, key).Trim().ToLowerInvariant();
        return value == "1" || value == "true" || value == "yes" || value == "y";
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
