using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ScenarioSystem.Editor.Csv
{
    /// <summary>Portable Unity serialized fields; references always use the document's asset catalog.</summary>
    public static class ScenarioCsvSerialization
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        public static string Capture(Object value, ScenarioCsvDocument document, params string[] excludedFields)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (document == null) throw new ArgumentNullException(nameof(document));
            var excluded = new HashSet<string>(excludedFields ?? System.Array.Empty<string>(), StringComparer.Ordinal);
            var fields = SerializableFields(value.GetType());
            var records = new JArray();
            using var serialized = new SerializedObject(value);
            var property = serialized.GetIterator();
            bool enterChildren = true;
            while (property.Next(enterChildren))
            {
                // Unity exposes native children of strings and object references too.
                // Only actual serialized containers are traversed; leaf values are already complete.
                enterChildren = false;
                string root = property.propertyPath.Split('.')[0];
                if (!fields.Contains(root) || excluded.Any(path => property.propertyPath == path || property.propertyPath.StartsWith(path + ".", StringComparison.Ordinal))) continue;
                if (property.propertyType == SerializedPropertyType.Generic)
                {
                    enterChildren = true;
                    if (!property.isArray || property.propertyPath.EndsWith(".Array", StringComparison.Ordinal)) continue;
                }
                if (property.propertyType == SerializedPropertyType.ArraySize) continue;
                string kind = property.isArray && property.propertyType != SerializedPropertyType.String ? "Array" : property.propertyType.ToString();
                records.Add(new JObject { ["path"] = property.propertyPath, ["kind"] = kind, ["value"] = ReadValue(property, kind, document) });
            }
            return new JObject { ["version"] = 1, ["type"] = value.GetType().FullName, ["properties"] = records }.ToString(Formatting.None);
        }

        public static void Apply(string data, Object target, ScenarioCsvDocument document, Func<string, Object> resolve = null)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (string.IsNullOrEmpty(data)) return;
            var payload = JObject.Parse(data, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            RequireKeys(payload, "version", "type", "properties");
            if ((int?)payload["version"] != 1 || (string)payload["type"] != target.GetType().FullName || payload["properties"] is not JArray records)
                throw new FormatException("CSV詳細データの版または型が対象と一致しません。");
            var fields = SerializableFields(target.GetType());
            var seen = new HashSet<string>(StringComparer.Ordinal);
            using var serialized = new SerializedObject(target);
            foreach (JToken token in records)
            {
                if (token is not JObject record) throw new FormatException("CSV詳細データのプロパティ形式が不正です。");
                RequireKeys(record, "path", "kind", "value");
                string path = (string)record["path"];
                string kind = (string)record["kind"];
                if (string.IsNullOrEmpty(path) || !seen.Add(path) || !fields.Contains(path.Split('.')[0]))
                    throw new FormatException("CSV詳細データに不正または重複したフィールドがあります: " + path);
                FieldType(target.GetType(), path);
                var property = serialized.FindProperty(path);
                if (property == null) throw new FormatException("CSV詳細データのフィールドが現在の型に存在しません: " + path);
                string actualKind = property.isArray && property.propertyType != SerializedPropertyType.String ? "Array" : property.propertyType.ToString();
                if (kind != actualKind) throw new FormatException("CSV詳細データのフィールド型が一致しません: " + path);
                WriteValue(property, kind, record["value"], target.GetType(), document, resolve);
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static string AddAsset(Object value, ScenarioCsvDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (value == null) return "";
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out string guid, out long localId) || string.IsNullOrEmpty(guid))
                throw new InvalidOperationException("保存されていないオブジェクトはCSVから参照できません: " + value.name);
            string local = localId.ToString(Invariant);
            foreach (var existing in document.assets)
                if (existing != null && existing.guid == guid && existing.localId == local)
                {
                    if (string.IsNullOrEmpty(existing.id)) throw new FormatException("素材カタログに空IDがあります。");
                    return existing.id;
                }
            string baseId = guid + ":" + local;
            string id = baseId;
            for (int suffix = 2; document.assets.Any(asset => asset != null && asset.id == id); suffix++) id = baseId + "~" + suffix;
            string path = AssetDatabase.GetAssetPath(value).Replace('\\', '/');
            int resourceIndex = path.LastIndexOf("/Resources/", StringComparison.Ordinal);
            string resourcePath = resourceIndex < 0 ? "" : path.Substring(resourceIndex + "/Resources/".Length);
            int extension = resourcePath.LastIndexOf('.');
            if (extension >= 0) resourcePath = resourcePath.Substring(0, extension);
            document.assets.Add(new ScenarioCsvAsset { id = id, label = value.name, guid = guid, localId = local, type = value.GetType().AssemblyQualifiedName, resourcePath = resourcePath });
            return id;
        }

        public static Object ResolveAsset(string id, ScenarioCsvDocument document)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (document == null) throw new ArgumentNullException(nameof(document));
            var matches = document.assets.Where(asset => asset != null && asset.id == id).ToArray();
            if (matches.Length != 1) throw new FormatException("素材IDが未登録または重複しています: " + id);
            var entry = matches[0];
            if (string.IsNullOrEmpty(entry.guid) || !long.TryParse(entry.localId, NumberStyles.Integer, Invariant, out long localId))
                throw new FormatException("素材のGUIDまたはlocalFileIDが不正です: " + id);
            var type = Type.GetType(entry.type ?? "", false);
            if (type == null || !typeof(Object).IsAssignableFrom(type)) throw new FormatException("素材の型が不正です: " + id);
            string path = AssetDatabase.GUIDToAssetPath(entry.guid);
            if (!string.IsNullOrEmpty(path))
                foreach (var value in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (value != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out string guid, out long candidate) && guid == entry.guid && candidate == localId)
                    {
                        if (!type.IsInstanceOfType(value)) throw new FormatException("素材の型が一致しません: " + id);
                        return value;
                    }
            throw new FormatException("素材が見つかりません: " + id + " (" + entry.label + ")");
        }

        private static HashSet<string> SerializableFields(Type type)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (; type != null && type != typeof(ScriptableObject) && type != typeof(Object); type = type.BaseType)
                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    if (!field.IsStatic && !field.IsInitOnly && !field.IsNotSerialized && (field.IsPublic || field.IsDefined(typeof(SerializeField), true) || field.IsDefined(typeof(SerializeReference), true))) names.Add(field.Name);
            return names;
        }

        private static Type FieldType(Type type, string path)
        {
            string[] parts = path.Split('.');
            for (int index = 0; index < parts.Length; index++)
            {
                string part = parts[index];
                bool collection = type.IsArray || type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>);
                if (collection)
                {
                    if (part != "Array" || ++index >= parts.Length)
                        throw new FormatException("配列のフィールドパスが不正です: " + path);
                    string element = parts[index];
                    if (!element.StartsWith("data[", StringComparison.Ordinal) || !element.EndsWith("]", StringComparison.Ordinal) ||
                        !int.TryParse(element.Substring(5, element.Length - 6), NumberStyles.None, Invariant, out int elementIndex) || elementIndex < 0)
                        throw new FormatException("配列要素のフィールドパスが不正です: " + path);
                    type = type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0];
                    continue;
                }
                if (index > 0 && IsLeafType(type))
                    throw new FormatException("Unity内部のフィールドはCSVから復元できません: " + path);
                FieldInfo field = null;
                for (Type current = type; current != null && field == null; current = current.BaseType)
                    field = current.GetField(part, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field == null || field.IsStatic || field.IsInitOnly || field.IsNotSerialized ||
                    !(field.IsPublic || field.IsDefined(typeof(SerializeField), true) || field.IsDefined(typeof(SerializeReference), true)))
                    throw new FormatException("直列化対象のフィールドが存在しません: " + path);
                type = field.FieldType;
            }
            return type;
        }

        private static bool IsLeafType(Type type)
            => type.IsPrimitive || type.IsEnum || type == typeof(string) || typeof(Object).IsAssignableFrom(type)
                || type == typeof(Color) || type == typeof(Vector2) || type == typeof(Vector3) || type == typeof(Vector4)
                || type == typeof(Quaternion) || type == typeof(Rect) || type == typeof(Bounds)
                || type == typeof(Vector2Int) || type == typeof(Vector3Int) || type == typeof(RectInt)
                || type == typeof(BoundsInt) || type == typeof(Hash128) || type == typeof(AnimationCurve) || type == typeof(Gradient);

        private static JToken ReadValue(SerializedProperty property, string kind, ScenarioCsvDocument document)
        {
            if (kind == "Array") return new JValue(property.arraySize);
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer: return new JValue(property.longValue);
                case SerializedPropertyType.Boolean: return new JValue(property.boolValue);
                case SerializedPropertyType.Float: return new JValue(property.doubleValue);
                case SerializedPropertyType.String: return new JValue(property.stringValue);
                case SerializedPropertyType.Enum:
                case SerializedPropertyType.Character:
                case SerializedPropertyType.LayerMask: return new JValue(property.intValue);
                case SerializedPropertyType.ObjectReference:
                    if (property.objectReferenceValue == null && property.objectReferenceInstanceIDValue != 0)
                        throw new InvalidOperationException("CSVへ保存する参照が欠落しています: " + property.propertyPath);
                    return new JValue(AddAsset(property.objectReferenceValue, document));
                case SerializedPropertyType.Color: var color = property.colorValue; return Array(color.r, color.g, color.b, color.a);
                case SerializedPropertyType.Vector2: var v2 = property.vector2Value; return Array(v2.x, v2.y);
                case SerializedPropertyType.Vector3: var v3 = property.vector3Value; return Array(v3.x, v3.y, v3.z);
                case SerializedPropertyType.Vector4: var v4 = property.vector4Value; return Array(v4.x, v4.y, v4.z, v4.w);
                case SerializedPropertyType.Quaternion: var q = property.quaternionValue; return Array(q.x, q.y, q.z, q.w);
                case SerializedPropertyType.Rect: var rect = property.rectValue; return Array(rect.x, rect.y, rect.width, rect.height);
                case SerializedPropertyType.Bounds: var bounds = property.boundsValue; return Array(bounds.center.x, bounds.center.y, bounds.center.z, bounds.size.x, bounds.size.y, bounds.size.z);
                case SerializedPropertyType.Vector2Int: var i2 = property.vector2IntValue; return new JArray(i2.x, i2.y);
                case SerializedPropertyType.Vector3Int: var i3 = property.vector3IntValue; return new JArray(i3.x, i3.y, i3.z);
                case SerializedPropertyType.RectInt: var ri = property.rectIntValue; return new JArray(ri.x, ri.y, ri.width, ri.height);
                case SerializedPropertyType.BoundsInt: var bi = property.boundsIntValue; return new JArray(bi.position.x, bi.position.y, bi.position.z, bi.size.x, bi.size.y, bi.size.z);
                case SerializedPropertyType.Hash128: return new JValue(property.hash128Value.ToString());
                default: throw new NotSupportedException("CSV詳細データで未対応のUnityフィールドです: " + property.propertyPath + " (" + kind + ")");
            }
        }

        private static void WriteValue(SerializedProperty property, string kind, JToken value, Type targetType, ScenarioCsvDocument document, Func<string, Object> resolve)
        {
            if (kind == "Array")
            {
                int size = Required<int>(value, JTokenType.Integer);
                if (size < 0) throw new FormatException("CSV配列長は0以上が必要です: " + property.propertyPath);
                property.arraySize = size;
                return;
            }
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer: property.longValue = Required<long>(value, JTokenType.Integer); break;
                case SerializedPropertyType.Boolean: property.boolValue = Required<bool>(value, JTokenType.Boolean); break;
                case SerializedPropertyType.Float: property.doubleValue = Number(value); break;
                case SerializedPropertyType.String: property.stringValue = Required<string>(value, JTokenType.String); break;
                case SerializedPropertyType.Enum:
                case SerializedPropertyType.Character:
                case SerializedPropertyType.LayerMask: property.intValue = Required<int>(value, JTokenType.Integer); break;
                case SerializedPropertyType.ObjectReference:
                    string id = Required<string>(value, JTokenType.String);
                    Object reference = string.IsNullOrEmpty(id) ? null : resolve?.Invoke(id) ?? ResolveAsset(id, document);
                    if (reference != null && !FieldType(targetType, property.propertyPath).IsInstanceOfType(reference))
                        throw new FormatException("素材の型が参照フィールドと一致しません: " + property.propertyPath + " / " + id);
                    property.objectReferenceValue = reference;
                    break;
                case SerializedPropertyType.Color: var c = Numbers(value, 4); property.colorValue = new Color(c[0], c[1], c[2], c[3]); break;
                case SerializedPropertyType.Vector2: var v2 = Numbers(value, 2); property.vector2Value = new Vector2(v2[0], v2[1]); break;
                case SerializedPropertyType.Vector3: var v3 = Numbers(value, 3); property.vector3Value = new Vector3(v3[0], v3[1], v3[2]); break;
                case SerializedPropertyType.Vector4: var v4 = Numbers(value, 4); property.vector4Value = new Vector4(v4[0], v4[1], v4[2], v4[3]); break;
                case SerializedPropertyType.Quaternion: var q = Numbers(value, 4); property.quaternionValue = new Quaternion(q[0], q[1], q[2], q[3]); break;
                case SerializedPropertyType.Rect: var r = Numbers(value, 4); property.rectValue = new Rect(r[0], r[1], r[2], r[3]); break;
                case SerializedPropertyType.Bounds: var b = Numbers(value, 6); property.boundsValue = new Bounds(new Vector3(b[0], b[1], b[2]), new Vector3(b[3], b[4], b[5])); break;
                case SerializedPropertyType.Vector2Int: var i2 = Integers(value, 2); property.vector2IntValue = new Vector2Int(i2[0], i2[1]); break;
                case SerializedPropertyType.Vector3Int: var i3 = Integers(value, 3); property.vector3IntValue = new Vector3Int(i3[0], i3[1], i3[2]); break;
                case SerializedPropertyType.RectInt: var ri = Integers(value, 4); property.rectIntValue = new RectInt(ri[0], ri[1], ri[2], ri[3]); break;
                case SerializedPropertyType.BoundsInt: var bi = Integers(value, 6); property.boundsIntValue = new BoundsInt(new Vector3Int(bi[0], bi[1], bi[2]), new Vector3Int(bi[3], bi[4], bi[5])); break;
                case SerializedPropertyType.Hash128: property.hash128Value = Hash128.Parse(Required<string>(value, JTokenType.String)); break;
                default: throw new NotSupportedException("CSV詳細データで未対応のUnityフィールドです: " + property.propertyPath + " (" + kind + ")");
            }
        }

        private static JArray Array(params float[] values) => new(values.Select(value => new JValue(value)));
        private static T Required<T>(JToken token, JTokenType type)
        {
            if (token == null || token.Type != type) throw new FormatException("CSV詳細データの値の型が不正です。");
            return token.Value<T>();
        }
        private static double Number(JToken token)
        {
            if (token == null || (token.Type != JTokenType.Integer && token.Type != JTokenType.Float)) throw new FormatException("CSV詳細データに数値が必要です。");
            return token.Value<double>();
        }
        private static float[] Numbers(JToken value, int count)
        {
            if (value is not JArray array || array.Count != count) throw new FormatException("CSV詳細データの数値配列長が不正です。");
            return array.Select(item => (float)Number(item)).ToArray();
        }
        private static int[] Integers(JToken value, int count)
        {
            if (value is not JArray array || array.Count != count) throw new FormatException("CSV詳細データの整数配列長が不正です。");
            return array.Select(item => Required<int>(item, JTokenType.Integer)).ToArray();
        }
        private static void RequireKeys(JObject value, params string[] names)
        {
            if (!value.Properties().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal).SequenceEqual(names.OrderBy(name => name, StringComparer.Ordinal)))
                throw new FormatException("CSV詳細データに不足または未知の項目があります。");
        }
    }
}
