using System;
using System.Text;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class DictionaryConverter : JsonConverter
{
    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) { this.WriteValue(writer, value); }

    private void WriteValue(JsonWriter writer, object value)
    {
        var t = JToken.FromObject(value);
        switch (t.Type)
        {
            case JTokenType.Object:
                this.WriteObject(writer, value);
                break;
            case JTokenType.Array:
                this.WriteArray(writer, value);
                break;
            default:
                writer.WriteValue(value);
                break;
        }
    }

    private void WriteObject(JsonWriter writer, object value)
    {
        writer.WriteStartObject();
        var obj = value as IDictionary<string, object>;
        foreach (var kvp in obj)
        {
            writer.WritePropertyName(kvp.Key);
            this.WriteValue(writer, kvp.Value);
        }
        writer.WriteEndObject();
    }

    private void WriteArray(JsonWriter writer, object value)
    {
        writer.WriteStartArray();
        var array = value as IEnumerable<object>;
        foreach (var o in array)
        {
            this.WriteValue(writer, o);
        }
        writer.WriteEndArray();
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        return ReadValue(reader);
    }

    private object ReadValue(JsonReader reader)
    {
        while (reader.TokenType == JsonToken.Comment)
        {
            if (!reader.Read()) throw new JsonSerializationException("Unexpected Token when converting IDictionary<string, object>");
        }

        switch (reader.TokenType)
        {
            case JsonToken.StartObject:
                return ReadObject(reader);
            case JsonToken.StartArray:
                return this.ReadArray(reader);
            case JsonToken.Integer:
                return Convert.ToInt32(reader.Value);
            case JsonToken.Float:
                return Convert.ToSingle(reader.Value);
            case JsonToken.String:
            case JsonToken.Boolean:
            case JsonToken.Undefined:
            case JsonToken.Null:
            case JsonToken.Date:
            case JsonToken.Bytes:
                return reader.Value;
            default:
                throw new JsonSerializationException
                    (string.Format("Unexpected token when converting IDictionary<string, object>: {0}", reader.TokenType));
        }
    }

    private object ReadArray(JsonReader reader)
    {
        IList<object> list = new List<object>();

        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonToken.Comment:
                    break;
                default:
                    var v = ReadValue(reader);

                    list.Add(v);
                    break;
                case JsonToken.EndArray:
                    return list;
            }
        }

        throw new JsonSerializationException("Unexpected end when reading IDictionary<string, object>");
    }

    private object ReadObject(JsonReader reader)
    {
        var obj = new Dictionary<string, object>();

        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonToken.PropertyName:
                    var propertyName = reader.Value.ToString();

                    if (!reader.Read())
                    {
                        throw new JsonSerializationException("Unexpected end when reading IDictionary<string, object>");
                    }

                    var v = ReadValue(reader);

                    obj[propertyName] = v;
                    break;
                case JsonToken.Comment:
                    break;
                case JsonToken.EndObject:
                    return obj;
            }
        }

        throw new JsonSerializationException("Unexpected end when reading IDictionary<string, object>");
    }

    public override bool CanConvert(Type objectType) { return typeof(IDictionary<string, object>).IsAssignableFrom(objectType); }
}

namespace Ink.Runtime
{
    /// <summary>
    /// Simple custom JSON serialisation implementation that takes JSON-able System.Collections that
    /// are produced by the ink engine and converts to and from JSON text.
    /// </summary>
    public static class SimpleJson
    {
        public static Dictionary<string, object> TextToDictionary (string text)
        {
            return JsonConvert.DeserializeObject<Dictionary<string, object>>(text, new DictionaryConverter());
            //return new Reader (text).ToDictionary ();
        }

        public static string Serialize(Dictionary<string, object> dict)
        {
            return JsonConvert.SerializeObject(dict);
        }

        private class Writer
        {
            public Writer()
            {
                _writer = new StringWriter();
            }

            public Writer(Stream stream)
            {
                _writer = new System.IO.StreamWriter(stream, Encoding.UTF8);
            }

            public void WriteObject(Action<Writer> inner)
            {
                WriteObjectStart();
                inner(this);
                WriteObjectEnd();
            }

            public void WriteObjectStart()
            {
                StartNewObject(container: true);
                _stateStack.Push(new StateElement { type = State.Object });
                _writer.Write("{");
            }

            public void WriteObjectEnd()
            {
                Assert(state == State.Object);
                _writer.Write("}");
                _stateStack.Pop();
            }

            public void WriteProperty(string name, Action<Writer> inner)
            {
                WriteProperty<string>(name, inner);
            }

            public void WriteProperty(int id, Action<Writer> inner)
            {
                WriteProperty<int>(id, inner);
            }

            public void WriteProperty(string name, string content)
            {
                WritePropertyStart(name);
                Write(content);
                WritePropertyEnd();
            }

            public void WriteProperty(string name, int content)
            {
                WritePropertyStart(name);
                Write(content);
                WritePropertyEnd();
            }

            public void WriteProperty(string name, bool content)
            {
                WritePropertyStart(name);
                Write(content);
                WritePropertyEnd();
            }

            public void WritePropertyStart(string name)
            {
                WritePropertyStart<string>(name);
            }

            public void WritePropertyStart(int id)
            {
                WritePropertyStart<int>(id);
            }

            public void WritePropertyEnd()
            {
                Assert(state == State.Property);
                Assert(childCount == 1);
                _stateStack.Pop();
            }

            public void WritePropertyNameStart()
            {
                Assert(state == State.Object);

                if (childCount > 0)
                    _writer.Write(",");

                _writer.Write("\"");

                IncrementChildCount();

                _stateStack.Push(new StateElement { type = State.Property });
                _stateStack.Push(new StateElement { type = State.PropertyName });
            }

            public void WritePropertyNameEnd()
            {
                Assert(state == State.PropertyName);

                _writer.Write("\":");

                // Pop PropertyName, leaving Property state
                _stateStack.Pop();
            }

            public void WritePropertyNameInner(string str)
            {
                Assert(state == State.PropertyName);
                _writer.Write(str);
            }

            void WritePropertyStart<T>(T name)
            {
                Assert(state == State.Object);

                if (childCount > 0)
                    _writer.Write(",");

                _writer.Write("\"");
                _writer.Write(name);
                _writer.Write("\":");

                IncrementChildCount();

                _stateStack.Push(new StateElement { type = State.Property });
            }


            // allow name to be string or int
            void WriteProperty<T>(T name, Action<Writer> inner)
            {
                WritePropertyStart(name);

                inner(this);

                WritePropertyEnd();
            }

            public void WriteArrayStart()
            {
                StartNewObject(container: true);
                _stateStack.Push(new StateElement { type = State.Array });
                _writer.Write("[");
            }

            public void WriteArrayEnd()
            {
                Assert(state == State.Array);
                _writer.Write("]");
                _stateStack.Pop();
            }

            public void Write(int i)
            {
                StartNewObject(container: false);
                _writer.Write(i);
            }

            public void Write(float f)
            {
                StartNewObject(container: false);

                // TODO: Find an heap-allocation-free way to do this please!
                // _writer.Write(formatStr, obj (the float)) requires boxing
                // Following implementation seems to work ok but requires creating temporary garbage string.
                string floatStr = f.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if( floatStr == "Infinity" ) {
                    _writer.Write("3.4E+38"); // JSON doesn't support, do our best alternative
                } else if (floatStr == "-Infinity") {
                    _writer.Write("-3.4E+38"); // JSON doesn't support, do our best alternative
                } else if ( floatStr == "NaN" ) {
                    _writer.Write("0.0"); // JSON doesn't support, not much we can do
                } else {
                    _writer.Write(floatStr);
                    if (!floatStr.Contains(".") && !floatStr.Contains("E")) 
                        _writer.Write(".0"); // ensure it gets read back in as a floating point value
                }
            }

            public void Write(string str, bool escape = true)
            {
                StartNewObject(container: false);

                _writer.Write("\"");
                if (escape)
                    WriteEscapedString(str);
                else
                    _writer.Write(str);
                _writer.Write("\"");
            }

            public void Write(bool b)
            {
                StartNewObject(container: false);
                _writer.Write(b ? "true" : "false");
            }

            public void WriteNull()
            {
                StartNewObject(container: false);
                _writer.Write("null");
            }

            public void WriteStringStart()
            {
                StartNewObject(container: false);
                _stateStack.Push(new StateElement { type = State.String });
                _writer.Write("\"");
            }

            public void WriteStringEnd()
            {
                Assert(state == State.String);
                _writer.Write("\"");
                _stateStack.Pop();
            }

            public void WriteStringInner(string str, bool escape = true)
            {
                Assert(state == State.String);
                if (escape)
                    WriteEscapedString(str);
                else
                    _writer.Write(str);
            }

            void WriteEscapedString(string str)
            {
                foreach (var c in str)
                {
                    if (c < ' ')
                    {
                        // Don't write any control characters except \n and \t
                        switch (c)
                        {
                            case '\n':
                                _writer.Write("\\n");
                                break;
                            case '\t':
                                _writer.Write("\\t");
                                break;
                        }
                    }
                    else
                    {
                        switch (c)
                        {
                            case '\\':
                            case '"':
                                _writer.Write("\\");
                                _writer.Write(c);
                                break;
                            default:
                                _writer.Write(c);
                                break;
                        }
                    }
                }
            }

            void StartNewObject(bool container)
            {

                if (container)
                    Assert(state == State.None || state == State.Property || state == State.Array);
                else
                    Assert(state == State.Property || state == State.Array);

                if (state == State.Array && childCount > 0)
                    _writer.Write(",");

                if (state == State.Property)
                    Assert(childCount == 0);

                if (state == State.Array || state == State.Property)
                    IncrementChildCount();
            }

            State state
            {
                get
                {
                    if (_stateStack.Count > 0) return _stateStack.Peek().type;
                    else return State.None;
                }
            }

            int childCount
            {
                get
                {
                    if (_stateStack.Count > 0) return _stateStack.Peek().childCount;
                    else return 0;
                }
            }

            void IncrementChildCount()
            {
                Assert(_stateStack.Count > 0);
                var currEl = _stateStack.Pop();
                currEl.childCount++;
                _stateStack.Push(currEl);
            }

            // Shouldn't hit this assert outside of initial JSON development,
            // so it's save to make it debug-only.
            [System.Diagnostics.Conditional("DEBUG")]
            void Assert(bool condition)
            {
                if (!condition)
                    throw new System.Exception("Assert failed while writing JSON");
            }

            public override string ToString()
            {
                return _writer.ToString();
            }

            enum State
            {
                None,
                Object,
                Array,
                Property,
                PropertyName,
                String
            };

            struct StateElement
            {
                public State type;
                public int childCount;
            }

            Stack<StateElement> _stateStack = new Stack<StateElement>();
            TextWriter _writer;
        }


    }
}

