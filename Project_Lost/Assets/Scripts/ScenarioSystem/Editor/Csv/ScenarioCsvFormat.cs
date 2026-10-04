using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace ScenarioSystem.Editor.Csv
{
    [Serializable]
    public sealed class ScenarioCsvDocument
    {
        public List<ScenarioCsvRow> rows = new();
        public List<ScenarioCsvAsset> assets = new();
    }

    [Serializable]
    public sealed class ScenarioCsvRow
    {
        public string id = "", owner = "", kind = "", type = "", name = "", source = "";
        public string speaker = "", text = "", portrait = "", background = "", voice = "";
        public string position = "", speed = "", slide = "", effect = "", seconds = "";
        public string argument = "", sprite = "", color = "", next = "", loop = "", window = "", data = "";
    }

    [Serializable]
    public sealed class ScenarioCsvAsset
    {
        public string id = "", label = "", guid = "", localId = "", type = "", resourcePath = "";
    }

    /// <summary>Versioned UTF-8 CSV. Cell contents are never trimmed or normalized.</summary>
    public static class ScenarioCsvFormat
    {
        private static readonly string[] RowColumns = { "id", "owner", "kind", "type", "name", "source", "speaker", "text", "portrait", "background", "voice", "position", "speed", "slide", "effect", "seconds", "argument", "sprite", "color", "next", "loop", "window", "data" };
        private static readonly string[] AssetColumns = { "id", "label", "guid", "localId", "type", "resourcePath" };
        private const string Version = "1";

        public static string WriteRows(List<ScenarioCsvRow> rows) => Write(rows, RowColumns, "scenario-csv");
        public static List<ScenarioCsvRow> ReadRows(string csv) => Read<ScenarioCsvRow>(csv, RowColumns, "scenario-csv");
        public static string WriteAssets(List<ScenarioCsvAsset> assets) => Write(assets, AssetColumns, "scenario-assets");
        public static List<ScenarioCsvAsset> ReadAssets(string csv) => Read<ScenarioCsvAsset>(csv, AssetColumns, "scenario-assets");

        private static string Write<T>(List<T> records, string[] columns, string schema)
        {
            if (records == null) throw new ArgumentNullException(nameof(records));
            var builder = new StringBuilder("\uFEFF");
            AppendRecord(builder, new[] { schema, Version });
            AppendRecord(builder, columns);
            var fields = columns.Select(name => typeof(T).GetField(name, BindingFlags.Instance | BindingFlags.Public)).ToArray();
            foreach (var record in records)
            {
                if (record == null) throw new ArgumentException("CSVにnull行は保存できません。", nameof(records));
                AppendRecord(builder, fields.Select(field => (string)field.GetValue(record) ?? ""));
            }
            return builder.ToString();
        }

        private static List<T> Read<T>(string csv, string[] columns, string schema) where T : new()
        {
            var records = Parse(csv);
            if (records.Count < 2 || records[0].Length < 2 || records[0][0] != schema || records[0][1] != Version || records[0].Skip(2).Any(value => value.Length != 0))
                throw new FormatException($"{schema} バージョン {Version} のCSVが必要です。");
            if (!records[1].SequenceEqual(columns))
                throw new FormatException("CSVヘッダーの列名または順序が定義と一致しません。未知の列は削除せず読み込みを中止します。");
            var fields = columns.Select(name => typeof(T).GetField(name, BindingFlags.Instance | BindingFlags.Public)).ToArray();
            var result = new List<T>(records.Count - 2);
            for (int index = 2; index < records.Count; index++)
            {
                if (records[index].Length != columns.Length)
                    throw new FormatException($"CSVレコード {index + 1}: 列数が {columns.Length} と一致しません。");
                var record = new T();
                for (int column = 0; column < fields.Length; column++) fields[column].SetValue(record, records[index][column]);
                result.Add(record);
            }
            return result;
        }

        private static void AppendRecord(StringBuilder builder, IEnumerable<string> values)
        {
            bool first = true;
            foreach (string value in values)
            {
                if (!first) builder.Append(',');
                first = false;
                if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0)
                    builder.Append('"').Append(value.Replace("\"", "\"\"")).Append('"');
                else builder.Append(value);
            }
            builder.Append("\r\n");
        }

        private static List<string[]> Parse(string csv)
        {
            if (csv == null) throw new ArgumentNullException(nameof(csv));
            int index = csv.Length > 0 && csv[0] == '\uFEFF' ? 1 : 0;
            var records = new List<string[]>();
            while (index < csv.Length)
            {
                var cells = new List<string>();
                bool endRecord = false;
                while (!endRecord)
                {
                    var value = new StringBuilder();
                    if (index < csv.Length && csv[index] == '"')
                    {
                        index++;
                        bool closed = false;
                        while (index < csv.Length)
                        {
                            char c = csv[index++];
                            if (c != '"') { value.Append(c); continue; }
                            if (index < csv.Length && csv[index] == '"') { value.Append('"'); index++; continue; }
                            closed = true;
                            break;
                        }
                        if (!closed) throw new FormatException($"CSVレコード {records.Count + 1}: 引用符が閉じられていません。");
                        if (index < csv.Length && csv[index] != ',' && csv[index] != '\r' && csv[index] != '\n')
                            throw new FormatException($"CSVレコード {records.Count + 1}: 閉じ引用符の後に不正な文字があります。");
                    }
                    else
                    {
                        while (index < csv.Length && csv[index] != ',' && csv[index] != '\r' && csv[index] != '\n')
                        {
                            if (csv[index] == '"') throw new FormatException($"CSVレコード {records.Count + 1}: 非引用セル内に引用符があります。");
                            value.Append(csv[index++]);
                        }
                    }
                    cells.Add(value.ToString());
                    if (index >= csv.Length) endRecord = true;
                    else if (csv[index] == ',') index++;
                    else
                    {
                        if (csv[index++] == '\r' && (index >= csv.Length || csv[index++] != '\n'))
                            throw new FormatException($"CSVレコード {records.Count + 1}: 改行はCRLFまたはLFを使用してください。");
                        endRecord = true;
                    }
                }
                records.Add(cells.ToArray());
            }
            return records;
        }
    }
}
