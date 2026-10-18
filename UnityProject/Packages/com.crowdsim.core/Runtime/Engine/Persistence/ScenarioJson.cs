#nullable enable
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace CrowdSim.Core.Persistence
{
    /// <summary>Versioned authored JSON codec with strict primitive types and reference upgrade paths.</summary>
    public static class ScenarioJson
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            DateParseHandling = DateParseHandling.None,
            Culture = CultureInfo.InvariantCulture,
            MaxDepth = 64
        };

        public static string Serialize(ScenarioDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            definition.ValidateBasic();
            return JsonConvert.SerializeObject(definition, Settings);
        }

        public static ScenarioDefinition Deserialize(string json)
        {
            ValidateLexicalForm(json);
            JToken token;
            using (StringReader source = new StringReader(json))
            using (JsonTextReader reader = new JsonTextReader(source) { DateParseHandling = DateParseHandling.None, MaxDepth = 64 })
            {
                token = JToken.ReadFrom(reader);
                if (reader.Read()) throw new InvalidDataException("Additional JSON content is not allowed.");
            }
            ValidateTypes(token, typeof(ScenarioDefinition));
            ScenarioDefinition? definition = token.ToObject<ScenarioDefinition>(JsonSerializer.Create(Settings));
            if (definition == null) throw new InvalidDataException("Scenario JSON did not contain a valid scenario object.");
            if (definition.Version == 1) definition = ScenarioDefinition.UpgradeLegacyV1(definition);
            if (definition.Version == 2) definition = ScenarioDefinition.UpgradeLegacyV2(definition);
            if (definition.Version == 3) definition = ScenarioDefinition.UpgradeLegacyV3(definition);
            definition.ValidateBasic();
            return definition;
        }

        private static void ValidateTypes(JToken token, Type expected)
        {
            if (token.Type == JTokenType.Null)
            {
                if (expected.IsValueType) throw new InvalidDataException("Null is not valid for " + expected.Name);
                return;
            }
            if (expected.IsEnum || expected == typeof(int))
            {
                if (token.Type != JTokenType.Integer) throw new InvalidDataException("An integer is required for " + expected.Name);
                long integer = token.Value<long>();
                if (integer < int.MinValue || integer > int.MaxValue)
                    throw new InvalidDataException("The integer is outside the scenario's 32-bit range.");
                return;
            }
            if (expected == typeof(float))
            {
                if (token.Type != JTokenType.Float && token.Type != JTokenType.Integer)
                    throw new InvalidDataException("A finite number is required.");
                float value = token.Value<float>();
                if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidDataException("Non-finite numbers are not allowed.");
                return;
            }
            if (expected == typeof(bool) || expected == typeof(string))
            {
                JTokenType required = expected == typeof(bool) ? JTokenType.Boolean : JTokenType.String;
                if (token.Type != required) throw new InvalidDataException("Incorrect JSON primitive type.");
                return;
            }
            if (typeof(IList).IsAssignableFrom(expected))
            {
                if (!(token is JArray array)) throw new InvalidDataException("An array is required.");
                foreach (JToken child in array) ValidateTypes(child, expected.GetGenericArguments()[0]);
                return;
            }
            if (!(token is JObject obj)) throw new InvalidDataException("A scenario object is required.");
            foreach (JProperty property in obj.Properties())
            {
                PropertyInfo? member = expected.GetProperties().FirstOrDefault(p =>
                    string.Equals(p.Name, property.Name, StringComparison.OrdinalIgnoreCase));
                if (member != null) ValidateTypes(property.Value, member.PropertyType);
            }
        }

        private static void ValidateLexicalForm(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            bool quoted = false;
            for (int i = 0; i < text.Length; i++)
            {
                char value = text[i];
                if (quoted)
                {
                    if (value < ' ') throw new InvalidDataException("Unescaped control characters are not valid JSON strings.");
                    if (value == '\\') i++;
                    else if (value == '"') quoted = false;
                    continue;
                }
                if (value == '"') { quoted = true; continue; }
                if (value == '/' || value == '\'') throw new InvalidDataException("Comments and single-quoted strings are not valid scenario JSON.");
                if (value == '+') throw new InvalidDataException("Leading plus signs are not valid JSON numbers.");
                if (char.IsLetter(value) || value == '_')
                {
                    int end = i + 1;
                    while (end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] == '_')) end++;
                    string literal = text.Substring(i, end - i);
                    int next = end;
                    while (next < text.Length && char.IsWhiteSpace(text[next])) next++;
                    if ((literal != "true" && literal != "false" && literal != "null") ||
                        (next < text.Length && text[next] == ':'))
                        throw new InvalidDataException("Property names must be quoted and JSON literals must be standard.");
                    i = end - 1;
                    continue;
                }
                if (value == ',')
                {
                    int next = i + 1;
                    while (next < text.Length && char.IsWhiteSpace(text[next])) next++;
                    if (next < text.Length && (text[next] == ']' || text[next] == '}'))
                        throw new InvalidDataException("Trailing commas are not valid scenario JSON.");
                }
                if (value == '-' || char.IsDigit(value))
                {
                    int end = i + 1;
                    while (end < text.Length && !char.IsWhiteSpace(text[end]) && ",}:]".IndexOf(text[end]) < 0) end++;
                    string number = text.Substring(i, end - i);
                    if (!Regex.IsMatch(number, @"\A-?(0|[1-9][0-9]*)(\.[0-9]+)?([eE][+-]?[0-9]+)?\z"))
                        throw new InvalidDataException("Invalid JSON number.");
                    i = end - 1;
                }
            }
        }
    }
}
