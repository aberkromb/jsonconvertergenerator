//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:3.1.0
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JsonConverterGenerator
{
    public sealed class JsonConverterForListMyEventsListerItemTask : JsonConverter<List<MyEventsListerItemTask>>
    {
        private JsonConverterForListMyEventsListerItemTask() {}
        
        public static readonly JsonConverterForListMyEventsListerItemTask Instance = new JsonConverterForListMyEventsListerItemTask();
        
        public override List<MyEventsListerItemTask> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // Validate that the reader's cursor is at a start array token.
            if (reader.TokenType != JsonTokenType.StartArray)
            {
                throw new JsonException();
            }
            
            List<MyEventsListerItemTask> elements = new List<MyEventsListerItemTask>();
            
            // Read all elements.
            while (true)
            {
                reader.Read();
                
                if (reader.TokenType == JsonTokenType.EndArray)
                {
                    break;
                }
                
                elements.Add(JsonConverterForMyEventsListerItemTask.Instance.Read(ref reader, typeToConvert, options));
            }
            
            return elements;
        }
        
        public override void Write(Utf8JsonWriter writer, List<MyEventsListerItemTask> value, JsonSerializerOptions options)
        {
            // TODO: account for value-type collections (e.g. ImmutableArray).
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }
            
            writer.WriteStartArray();
            
            foreach (MyEventsListerItemTask element in value)
            {
                JsonConverterForMyEventsListerItemTask.Instance.Write(writer, element, options);
            }
            
            writer.WriteEndArray();
        }
    }
}
