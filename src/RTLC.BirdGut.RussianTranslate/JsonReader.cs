using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RTLC.BirdGut.RussianTranslate
{
    /// <summary>
    /// Minimal RFC 8259 JSON parser compatible with Unity 2017 / Mono .NET 3.5 without JsonUtility visibility quirks.
    /// </summary>
    internal sealed class JsonReader
    {
        private readonly string _json;
        private int _pos;

        private JsonReader(string json)
        {
            _json = json ?? string.Empty;
            if (_json.Length > 0 && _json[0] == '\uFEFF')
            {
                _pos = 1;
            }
        }

        public static object Parse(string json)
        {
            var reader = new JsonReader(json);
            return reader.ReadValue();
        }

        public static string GetString(Dictionary<string, object> dict, string key, string fallback = "")
        {
            if (dict != null && dict.TryGetValue(key, out object val) && val != null)
            {
                return val as string ?? Convert.ToString(val, CultureInfo.InvariantCulture);
            }
            return fallback;
        }

        public static float GetFloat(Dictionary<string, object> dict, string key, float fallback = 0f)
        {
            if (dict != null && dict.TryGetValue(key, out object val) && val != null)
            {
                return Convert.ToSingle(val, CultureInfo.InvariantCulture);
            }
            return fallback;
        }

        private void SkipWhitespace()
        {
            while (_pos < _json.Length)
            {
                char c = _json[_pos];
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
                {
                    _pos++;
                }
                else
                {
                    break;
                }
            }
        }

        private object ReadValue()
        {
            SkipWhitespace();
            if (_pos >= _json.Length)
            {
                throw new FormatException("Unexpected end of JSON input.");
            }

            char c = _json[_pos];
            switch (c)
            {
                case '{':
                    return ReadObject();
                case '[':
                    return ReadArray();
                case '"':
                    return ReadString();
                case 't':
                    ExpectLiteral("true");
                    return true;
                case 'f':
                    ExpectLiteral("false");
                    return false;
                case 'n':
                    ExpectLiteral("null");
                    return null;
                default:
                    if (c == '-' || (c >= '0' && c <= '9'))
                    {
                        return ReadNumber();
                    }
                    throw new FormatException($"Unexpected character '{c}' at position {_pos}.");
            }
        }

        private Dictionary<string, object> ReadObject()
        {
            var dict = new Dictionary<string, object>(StringComparer.Ordinal);
            _pos++; // '{'
            SkipWhitespace();

            if (_pos < _json.Length && _json[_pos] == '}')
            {
                _pos++;
                return dict;
            }

            while (true)
            {
                SkipWhitespace();
                if (_pos >= _json.Length || _json[_pos] != '"')
                {
                    throw new FormatException($"Expected string key at position {_pos}.");
                }

                string key = ReadString();
                SkipWhitespace();
                if (_pos >= _json.Length || _json[_pos] != ':')
                {
                    throw new FormatException($"Expected ':' after key '{key}' at position {_pos}.");
                }

                _pos++;
                dict[key] = ReadValue();
                SkipWhitespace();

                if (_pos >= _json.Length)
                {
                    throw new FormatException("Unterminated JSON object.");
                }

                char next = _json[_pos++];
                if (next == '}')
                {
                    return dict;
                }

                if (next != ',')
                {
                    throw new FormatException($"Expected ',' or '}}' at position {_pos - 1}.");
                }
            }
        }

        private List<object> ReadArray()
        {
            var list = new List<object>();
            _pos++; // '['
            SkipWhitespace();

            if (_pos < _json.Length && _json[_pos] == ']')
            {
                _pos++;
                return list;
            }

            while (true)
            {
                list.Add(ReadValue());
                SkipWhitespace();

                if (_pos >= _json.Length)
                {
                    throw new FormatException("Unterminated JSON array.");
                }

                char next = _json[_pos++];
                if (next == ']')
                {
                    return list;
                }

                if (next != ',')
                {
                    throw new FormatException($"Expected ',' or ']' at position {_pos - 1}.");
                }
            }
        }

        private string ReadString()
        {
            _pos++; // '"'
            var sb = new StringBuilder();

            while (_pos < _json.Length)
            {
                char c = _json[_pos++];
                if (c == '"')
                {
                    return sb.ToString();
                }

                if (c == '\\')
                {
                    if (_pos >= _json.Length)
                    {
                        throw new FormatException("Unterminated string escape.");
                    }

                    char esc = _json[_pos++];
                    switch (esc)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (_pos + 4 > _json.Length)
                            {
                                throw new FormatException("Incomplete \\u escape in JSON string.");
                            }
                            string hex = _json.Substring(_pos, 4);
                            _pos += 4;
                            sb.Append((char)ushort.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            break;
                        default:
                            throw new FormatException($"Invalid escape '\\{esc}' in JSON string.");
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }

            throw new FormatException("Unterminated JSON string.");
        }

        private object ReadNumber()
        {
            int start = _pos;
            bool isFloat = false;
            if (_json[_pos] == '-')
            {
                _pos++;
            }

            while (_pos < _json.Length)
            {
                char c = _json[_pos];
                if (c >= '0' && c <= '9')
                {
                    _pos++;
                }
                else if (c == '.' || c == 'e' || c == 'E' || c == '+' || c == '-')
                {
                    isFloat = true;
                    _pos++;
                }
                else
                {
                    break;
                }
            }

            string token = _json.Substring(start, _pos - start);
            if (!isFloat && long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out long lval))
            {
                return lval;
            }
            return double.Parse(token, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private void ExpectLiteral(string literal)
        {
            if (_pos + literal.Length > _json.Length || string.CompareOrdinal(_json, _pos, literal, 0, literal.Length) != 0)
            {
                throw new FormatException($"Expected '{literal}' at position {_pos}.");
            }
            _pos += literal.Length;
        }
    }
}
