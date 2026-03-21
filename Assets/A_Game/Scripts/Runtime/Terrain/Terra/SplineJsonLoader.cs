using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public static class SplineJsonLoader
{
    public static RuntimeSpline Load(TextAsset jsonAsset)
    {
        if (jsonAsset == null)
        {
            throw new ArgumentNullException(nameof(jsonAsset), "Spline JSON asset is required.");
        }

        object rootValue = MiniJson.Deserialize(jsonAsset.text);
        if (rootValue is not Dictionary<string, object> rootObject)
        {
            throw new InvalidOperationException($"Spline JSON '{jsonAsset.name}' must contain an object root.");
        }

        return ParseSpline(rootObject, jsonAsset.name);
    }

    public static object DeserializeRaw(string json)
    {
        return MiniJson.Deserialize(json);
    }

    private static RuntimeSpline ParseSpline(Dictionary<string, object> root, string debugPath)
    {
        SplineCoordinateKind coordinateKind = ParseCoordinate(ReadRequiredString(root, "coordinate", debugPath));
        List<object> pointsArray = ReadRequiredArray(root, "points", debugPath);
        if (pointsArray.Count == 0)
        {
            throw new InvalidOperationException($"Spline '{debugPath}' requires at least one point.");
        }

        RuntimeSplinePoint[] points = new RuntimeSplinePoint[pointsArray.Count];
        for (int i = 0; i < pointsArray.Count; i++)
        {
            if (pointsArray[i] is not Dictionary<string, object> pointObject)
            {
                throw new InvalidOperationException($"Spline '{debugPath}' point[{i}] must be an object.");
            }

            string pointPath = $"{debugPath}.points[{i}]";
            float location = ReadRequiredFloat(pointObject, "location", pointPath);
            float derivative = ReadOptionalFloat(pointObject, "derivative", pointPath, 0f);
            if (!pointObject.TryGetValue("value", out object valueRaw))
            {
                throw new InvalidOperationException($"Spline '{pointPath}' is missing value.");
            }

            RuntimeSplineValue value = ParseValue(valueRaw, $"{pointPath}.value");
            points[i] = new RuntimeSplinePoint(location, value, derivative);
        }

        return new RuntimeSpline(coordinateKind, points);
    }

    private static RuntimeSplineValue ParseValue(object rawValue, string debugPath)
    {
        if (rawValue is Dictionary<string, object> childSplineObject)
        {
            return new RuntimeSplineValue(ParseSpline(childSplineObject, debugPath));
        }

        return new RuntimeSplineValue(ConvertToFloat(rawValue, debugPath));
    }

    private static SplineCoordinateKind ParseCoordinate(string rawCoordinate)
    {
        return rawCoordinate switch
        {
            "continentalness" => SplineCoordinateKind.Continentalness,
            "erosion" => SplineCoordinateKind.Erosion,
            "peaks_and_valleys" => SplineCoordinateKind.PeaksAndValleys,
            "ridges" => SplineCoordinateKind.Ridges,
            "minecraft:overworld/continents" => SplineCoordinateKind.Continentalness,
            "minecraft:overworld/erosion" => SplineCoordinateKind.Erosion,
            "minecraft:overworld/ridges_folded" => SplineCoordinateKind.PeaksAndValleys,
            "minecraft:overworld/ridges" => SplineCoordinateKind.Ridges,
            _ => throw new InvalidOperationException($"Unsupported spline coordinate '{rawCoordinate}'."),
        };
    }

    private static string ReadRequiredString(Dictionary<string, object> root, string key, string debugPath)
    {
        if (!root.TryGetValue(key, out object value) || value is not string stringValue)
        {
            throw new InvalidOperationException($"Spline '{debugPath}' is missing string field '{key}'.");
        }

        return stringValue;
    }

    private static List<object> ReadRequiredArray(Dictionary<string, object> root, string key, string debugPath)
    {
        if (!root.TryGetValue(key, out object value) || value is not List<object> arrayValue)
        {
            throw new InvalidOperationException($"Spline '{debugPath}' is missing array field '{key}'.");
        }

        return arrayValue;
    }

    private static float ReadRequiredFloat(Dictionary<string, object> root, string key, string debugPath)
    {
        if (!root.TryGetValue(key, out object value))
        {
            throw new InvalidOperationException($"Spline '{debugPath}' is missing float field '{key}'.");
        }

        return ConvertToFloat(value, $"{debugPath}.{key}");
    }

    private static float ReadOptionalFloat(Dictionary<string, object> root, string key, string debugPath, float defaultValue)
    {
        return root.TryGetValue(key, out object value)
            ? ConvertToFloat(value, $"{debugPath}.{key}")
            : defaultValue;
    }

    private static float ConvertToFloat(object value, string debugPath)
    {
        return value switch
        {
            double doubleValue => (float)doubleValue,
            float floatValue => floatValue,
            long longValue => longValue,
            int intValue => intValue,
            _ => throw new InvalidOperationException($"Spline value '{debugPath}' must be numeric."),
        };
    }

    private static class MiniJson
    {
        public static object Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidOperationException("Spline JSON cannot be empty.");
            }

            using StringReader reader = new(json);
            Parser parser = new(reader);
            object value = parser.ParseValue();
            parser.ConsumeTrailingWhitespace();
            return value;
        }

        private sealed class Parser
        {
            private readonly StringReader _reader;

            public Parser(StringReader reader)
            {
                _reader = reader;
            }

            public object ParseValue()
            {
                ConsumeWhitespace();
                int peek = _reader.Peek();
                return peek switch
                {
                    -1 => throw new InvalidOperationException("Unexpected end of JSON."),
                    '"' => ParseString(),
                    '{' => ParseObject(),
                    '[' => ParseArray(),
                    't' => ParseTrue(),
                    'f' => ParseFalse(),
                    'n' => ParseNull(),
                    _ => ParseNumber(),
                };
            }

            public void ConsumeTrailingWhitespace()
            {
                ConsumeWhitespace();
                if (_reader.Peek() != -1)
                {
                    throw new InvalidOperationException("Unexpected trailing content after JSON root.");
                }
            }

            private Dictionary<string, object> ParseObject()
            {
                Dictionary<string, object> result = new();
                Expect('{');
                ConsumeWhitespace();
                if (TryConsume('}'))
                {
                    return result;
                }

                while (true)
                {
                    string key = ParseString();
                    ConsumeWhitespace();
                    Expect(':');
                    object value = ParseValue();
                    result[key] = value;
                    ConsumeWhitespace();
                    if (TryConsume('}'))
                    {
                        break;
                    }

                    Expect(',');
                }

                return result;
            }

            private List<object> ParseArray()
            {
                List<object> result = new();
                Expect('[');
                ConsumeWhitespace();
                if (TryConsume(']'))
                {
                    return result;
                }

                while (true)
                {
                    result.Add(ParseValue());
                    ConsumeWhitespace();
                    if (TryConsume(']'))
                    {
                        break;
                    }

                    Expect(',');
                }

                return result;
            }

            private string ParseString()
            {
                Expect('"');
                StringBuilder builder = new();
                while (true)
                {
                    int next = _reader.Read();
                    if (next < 0)
                    {
                        throw new InvalidOperationException("Unterminated JSON string.");
                    }

                    char c = (char)next;
                    if (c == '"')
                    {
                        return builder.ToString();
                    }

                    if (c == '\\')
                    {
                        int escaped = _reader.Read();
                        if (escaped < 0)
                        {
                            throw new InvalidOperationException("Invalid JSON escape sequence.");
                        }

                        builder.Append((char)escaped switch
                        {
                            '"' => '"',
                            '\\' => '\\',
                            '/' => '/',
                            'b' => '\b',
                            'f' => '\f',
                            'n' => '\n',
                            'r' => '\r',
                            't' => '\t',
                            'u' => ParseUnicodeEscape(),
                            char other => throw new InvalidOperationException($"Unsupported JSON escape '\\{other}'."),
                        });
                        continue;
                    }

                    builder.Append(c);
                }
            }

            private char ParseUnicodeEscape()
            {
                char[] hex = new char[4];
                for (int i = 0; i < 4; i++)
                {
                    int next = _reader.Read();
                    if (next < 0)
                    {
                        throw new InvalidOperationException("Invalid unicode escape in JSON string.");
                    }

                    hex[i] = (char)next;
                }

                return (char)Convert.ToInt32(new string(hex), 16);
            }

            private object ParseNumber()
            {
                StringBuilder builder = new();
                while (true)
                {
                    int peek = _reader.Peek();
                    if (peek < 0)
                    {
                        break;
                    }

                    char c = (char)peek;
                    if (char.IsDigit(c) || c is '-' or '+' or '.' or 'e' or 'E')
                    {
                        builder.Append((char)_reader.Read());
                    }
                    else
                    {
                        break;
                    }
                }

                if (builder.Length == 0)
                {
                    throw new InvalidOperationException("Invalid JSON number.");
                }

                string raw = builder.ToString();
                if (raw.IndexOf('.') >= 0 || raw.IndexOf('e') >= 0 || raw.IndexOf('E') >= 0)
                {
                    return double.Parse(raw, CultureInfo.InvariantCulture);
                }

                return long.Parse(raw, CultureInfo.InvariantCulture);
            }

            private bool ParseTrue()
            {
                Expect('t');
                Expect('r');
                Expect('u');
                Expect('e');
                return true;
            }

            private bool ParseFalse()
            {
                Expect('f');
                Expect('a');
                Expect('l');
                Expect('s');
                Expect('e');
                return false;
            }

            private object ParseNull()
            {
                Expect('n');
                Expect('u');
                Expect('l');
                Expect('l');
                return null;
            }

            private void ConsumeWhitespace()
            {
                while (_reader.Peek() >= 0 && char.IsWhiteSpace((char)_reader.Peek()))
                {
                    _reader.Read();
                }
            }

            private bool TryConsume(char expected)
            {
                ConsumeWhitespace();
                if (_reader.Peek() != expected)
                {
                    return false;
                }

                _reader.Read();
                return true;
            }

            private void Expect(char expected)
            {
                ConsumeWhitespace();
                int actual = _reader.Read();
                if (actual != expected)
                {
                    throw new InvalidOperationException($"Expected '{expected}' but found '{(actual < 0 ? "EOF" : ((char)actual).ToString())}'.");
                }
            }
        }
    }
}
