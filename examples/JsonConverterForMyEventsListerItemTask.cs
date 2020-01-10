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
    public sealed class JsonConverterForMyEventsListerItemTask : JsonConverter<MyEventsListerItemTask>
    {
        private JsonConverterForMyEventsListerItemTask() {}
        
        public static readonly JsonConverterForMyEventsListerItemTask Instance = new JsonConverterForMyEventsListerItemTask();
        
        private static ulong NameKey = 288230377853378894;
        private static JsonEncodedText NameText = JsonEncodedText.Encode("Name", encoder: null);
        
        private static ulong StartDateKey = 18402064819338441811;
        private static JsonEncodedText StartDateText = JsonEncodedText.Encode("StartDate", encoder: null);
        
        private static ulong EndDateKey = 532960092021354053;
        private static JsonEncodedText EndDateText = JsonEncodedText.Encode("EndDate", encoder: null);
        
        private static JsonEncodedText FormattedDateText = JsonEncodedText.Encode("FormattedDate", encoder: null);
        
        
        public override MyEventsListerItemTask Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // Validate that the reader's cursor is at a start object token.
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException();
            }
            
            // Create returned object. This assumes type has public parameterless ctor.
            MyEventsListerItemTask value = new MyEventsListerItemTask();
            
            // Read all properties.
            while (true)
            {
                reader.Read();
                
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    break;
                }
                
                // Only unescaped property names are allowed.
                ReadOnlySpan<byte> propertyName = reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan;
                
                // Move reader cursor to property value.
                reader.Read();
                
                // Try to match property name with object properties (case sensitive).
                
                // Determine if JSON property matches 'Name'.
                if (Helpers.GetKey(propertyName) == NameKey)
                {
                    value.Name = reader.GetString();
                }
                // Determine if JSON property matches 'StartDate'.
                else if (Helpers.GetKey(propertyName) == StartDateKey)
                {
                    value.StartDate = reader.GetDateTimeOffset();
                }
                // Determine if JSON property matches 'EndDate'.
                else if (Helpers.GetKey(propertyName) == EndDateKey)
                {
                    value.EndDate = reader.GetDateTimeOffset();
                }
            }
            
            return value;
        }
        
        public override void Write(Utf8JsonWriter writer, MyEventsListerItemTask value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }
            
            writer.WriteStartObject();
            
            writer.WriteString(NameText, value.Name);
            
            writer.WriteString(StartDateText, value.StartDate);
            
            writer.WriteString(EndDateText, value.EndDate);
            
            writer.WriteString(FormattedDateText, value.FormattedDate);
            
            writer.WriteEndObject();
        }
    }
}
