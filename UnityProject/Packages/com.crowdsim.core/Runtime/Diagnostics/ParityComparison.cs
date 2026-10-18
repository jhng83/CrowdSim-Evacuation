#nullable enable
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace CrowdSim.Diagnostics
{
    /// <summary>Exact discrete data, bounded float comparisons for the Mono diagnostic checkpoints.</summary>
    public static class ParityComparison
    {
        public const double AbsoluteTolerance = 0.0001;
        public const double RelativeTolerance = 0.00001;

        public static void Compare(JToken expected, JToken actual, IList<string> failures, string path = "root")
        {
            if (failures.Count >= 12) return;
            if (expected is JObject expectedObject && actual is JObject actualObject)
            {
                foreach (JProperty property in expectedObject.Properties())
                {
                    JToken? value = actualObject[property.Name];
                    if (value == null) failures.Add(path + "." + property.Name + ": missing");
                    else Compare(property.Value, value, failures, path + "." + property.Name);
                }
                return;
            }
            if (expected is JArray expectedArray && actual is JArray actualArray)
            {
                if (expectedArray.Count != actualArray.Count)
                {
                    failures.Add(path + ": array count differs");
                    return;
                }
                for (int i = 0; i < expectedArray.Count; i++) Compare(expectedArray[i], actualArray[i], failures, path + "[" + i + "]");
                return;
            }
            if (IsNumeric(expected) && IsNumeric(actual))
            {
                double e = expected.Value<double>(), a = actual.Value<double>();
                double tolerance = expected.Type == JTokenType.Integer && actual.Type == JTokenType.Integer
                    ? 0d : AbsoluteTolerance + RelativeTolerance * Math.Abs(e);
                if (double.IsNaN(a) || double.IsInfinity(a) || Math.Abs(e - a) > tolerance)
                    failures.Add(path + ": expected " + expected + ", actual " + actual);
                return;
            }
            if (!JToken.DeepEquals(expected, actual)) failures.Add(path + ": expected " + expected + ", actual " + actual);
        }

        private static bool IsNumeric(JToken value) => value.Type == JTokenType.Integer || value.Type == JTokenType.Float;
    }
}
