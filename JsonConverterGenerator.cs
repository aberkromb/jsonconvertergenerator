﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

namespace JsonConverterGenerator
{
    public class GenerationClassFrame
    {
        public Type Type;
        public int Indent;
        public StringBuilder SourceBuilder;

        public PropertyInfo[] Properties;

        public string TypeName;
        public string ConverterBaseName;

        public GenerationClassFrame(Type type)
        {
            Type = type;
            Indent = 0;
            SourceBuilder = new StringBuilder();
            Properties = Type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            TypeName = Type.Name;
            ConverterBaseName = $"JsonConverterFor{CodeGenerator.GetReadableTypeName(Type)}";
        }
    }

    public class CodeGenerator
    {
        private Stack<GenerationClassFrame> _frameStack = new Stack<GenerationClassFrame>();

        private GenerationClassFrame _currentFrame => _frameStack.Peek();
        private StringBuilder _sourceBuilder => _currentFrame.SourceBuilder;
        private Type _type => _currentFrame.Type;
        private string _converterBaseName => _currentFrame.ConverterBaseName;
        private string _typeName => _currentFrame.TypeName;
        private PropertyInfo[] _properties => _currentFrame.Properties;
        private int _indent
        {
            get
            {
                return _currentFrame.Indent;
            }
            set
            {
                _currentFrame.Indent = value;
            }
        }

        private readonly string _outputNamespace;

        private static readonly HashSet<Type> s_simpleTypes = new HashSet<Type>
        {
            typeof(bool),
            typeof(int),
            typeof(string),
            typeof(char),
            typeof(DateTime),
            typeof(DateTimeOffset),
        };

        private HashSet<Type> generatedTypes = new HashSet<Type>();
        private Dictionary<Type, string> _generatedCode = new Dictionary<Type, string>();

        public CodeGenerator(string outputNamespace)
        {
            if (string.IsNullOrWhiteSpace(outputNamespace))
            {
                throw new ArgumentException(string.Format("{0} is null, empty, or is whitespace", outputNamespace), "outputNamespace");
            }

            _outputNamespace = outputNamespace;
        }

        public Dictionary<Type, string> Generate(Type[] types)
        {
            if (types == null || types.Length < 1)
            {
                throw new ArgumentException(string.Format("{0} is null or empty", types), "types");
            }

            foreach (Type type in types)
            {
                WriteJsonConverterForTypeIfAbsent(type);
            }

            return _generatedCode;
        }

        private void WriteJsonConverterForTypeIfAbsent(Type type)
        {
            if (!generatedTypes.Contains(type))
            {
                generatedTypes.Add(type);

                _frameStack.Push(new GenerationClassFrame(type));

                WriteJsonConverterWorker();
                
                _generatedCode[type] = _frameStack.Pop().SourceBuilder.ToString();
            }
        }

        private void WriteJsonConverterWorker()
        {
            WriteAutoGenerationDisclaimer();

            WriteBlankLine();

            // TODO: Dynamically generate this with input types as a factor.
            WriteLine("using System;");
            WriteLine("using System.Buffers;");
            WriteLine("using System.Collections.Generic;");
            WriteLine("using System.Runtime.InteropServices;");
            WriteLine("using System.Text.Json;");
            WriteLine("using System.Text.Json.Serialization;");

            WriteBlankLine();

            BeginNewControlBlock($"namespace {_outputNamespace}");

            WriteConverterDeclaration();
            
            WriteProtectedConstructor();
            WriteStaticConverterInstanceField();
            
            if (_type.IsArray)
            {
                WriteConverterReadMethodForArray();
                WriteConverterWriteMethodForArray();
            }
            else if (_type.IsGenericType)
            {
                if (_type.GetGenericTypeDefinition() == typeof(List<>))
                {
                    WriteConverterReadMethodForListOfT();
                    WriteConverterWriteMethodForListOfT();
                }
                if (_type.GetGenericTypeDefinition() == typeof(Dictionary<,>)
                    && _type.GetGenericArguments()[0] == typeof(string))
                {
                    WriteConverterReadMethodForDictionaryOfStringToTValue();
                    WriteConverterWriteMethodForDictionaryOfStringToTValue();
                }
            }
            else if (!typeof(IEnumerable).IsAssignableFrom(_type))
            {
                WritePropertyNameConstants();
                WriteConverterReadMethodForObject();
                WriteConverterWriteMethodForObject();
            }
            
            WriteControlBlockEnd();

            WriteControlBlockEnd();
        }

        private void WriteConverterDeclaration()
        {
            // Apply indentation.
            _sourceBuilder.Append(new string(' ', _indent * 4));

            _sourceBuilder.Append($"public sealed class {_converterBaseName}");
            _sourceBuilder.Append(" : JsonConverter<");
            _sourceBuilder.Append(GetCompilableTypeName(_type));
            _sourceBuilder.Append(">");
            MoveToNewLine();

            WriteControlBlockStart();
        }

        private void WriteProtectedConstructor()
        {
            // Apply indentation.
            _sourceBuilder.Append(new string(' ', _indent * 4));
            _sourceBuilder.Append($"private {_converterBaseName}() {{}}");
            MoveToNewLine();
            WriteBlankLine();
        }

        private void WriteStaticConverterInstanceField()
        {
            // Apply indentation.
            _sourceBuilder.Append(new string(' ', _indent * 4));
            _sourceBuilder.Append($"public static readonly {_converterBaseName} Instance = new {_converterBaseName}();");
            MoveToNewLine();
            WriteBlankLine();
        }

        private void WritePropertyNameConstants()
        {
            foreach (PropertyInfo property in _properties)
            {
                string objectPropertyName = property.Name;
                int objectPropertyNameLength = objectPropertyName.Length;

                StringBuilder sb = new StringBuilder();

                sb.Append($"private static ReadOnlySpan<byte> {objectPropertyName}Bytes => new byte[{objectPropertyNameLength}] {{ (byte)'{objectPropertyName[0]}'");

                for (int i = 1; i < objectPropertyNameLength; i++)
                {
                    sb.Append($", (byte)'{objectPropertyName[i]}'");
                }

                sb.Append(" };");

                WriteLine(sb.ToString());
            }

            WriteBlankLine();
        }

        private void WriteConverterReadMethodForObject()
        {
            WriteMethodStart(
                level: AccessibilityLevel.Public,
                isOverride: true,
                returnTypeName: $"{GetCompilableTypeName(_type)}",
                methodName: "Read",
                parameterListValue: "ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options");

            // Validate that the reader's cursor is at a start object token.
            WriteSingleLineComment("Validate that the reader's cursor is at a start object token");
            WriteLine("if (reader.TokenType != JsonTokenType.StartObject)");
            WriteControlBlockStart();
            WriteThrowJsonException();
            WriteControlBlockEnd();

            WriteBlankLine();

            // Create returned object. This assumes type has public parameterless ctor.
            WriteSingleLineComment("Create returned object. This assumes type has public parameterless ctor");
            WriteLine($"{GetCompilableTypeName(_type)} value = new {GetCompilableTypeName(_type)}();");

            WriteBlankLine();

            if (_properties.Length > 0)
            {
                // Read all properties.
                WriteSingleLineComment("Read all properties");
                WriteLine("while (true)");
                WriteControlBlockStart();

                WriteLine("reader.Read();");
                WriteBlankLine();

                WriteLine("if (reader.TokenType == JsonTokenType.EndObject)");
                WriteControlBlockStart();
                WriteLine("break;");
                WriteControlBlockEnd();

                WriteBlankLine();

                // Note that we don't check for escaping: only unescaped property names are accounted for.
                WriteSingleLineComment("Only unescaped property names are allowed");
                WriteLine("ReadOnlySpan<byte> propertyName = reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan;");

                WriteBlankLine();

                WriteSingleLineComment("Move reader cursor to property value");
                WriteLine("reader.Read();");

                WriteBlankLine();

                // Try to match property name with object properties (case sensitive).
                WriteSingleLineComment("Try to match property name with object properties (case sensitive)");
                WriteBlankLine();

                for (int i = 0; i < _properties.Length; i++)
                {
                    PropertyInfo property = _properties[i];

                    // Ignore readonly properties.
                    if (!property.CanWrite)
                    {
                        continue;
                    }

                    Type propertyType = property.PropertyType;
                    string objectPropertyName = property.Name;

                    string compilableTypeName = GetCompilableTypeName(propertyType);
                    string readableTypeName = GetReadableTypeName(compilableTypeName);

                    string elsePrefix = i > 0 ? "else " : "";

                    WriteSingleLineComment($"Determine if JSON property matches '{objectPropertyName}'");
                    WriteLine(@$"{elsePrefix}if ({objectPropertyName}Bytes.SequenceEqual(propertyName))");
                    WriteControlBlockStart();

                    if (propertyType == typeof(char))
                    {
                        WriteLine("string tmp = reader.GetString();");
                        WriteLine("if (string.IsNullOrEmpty(tmp))");
                        WriteControlBlockStart();
                        WriteThrowJsonException();
                        WriteControlBlockEnd();

                        WriteBlankLine();

                        WriteLine($"value.{objectPropertyName} = tmp[0];");
                    }
                    else if (propertyType == typeof(byte[]))
                    {
                        WriteLine($@"reader.GetBytesFromBase64();");
                    }
                    else if (s_simpleTypes.Contains(propertyType))
                    {
                        WriteLine($"value.{objectPropertyName} = reader.Get{readableTypeName}();");
                    }
                    else
                    {
                        WriteJsonConverterForTypeIfAbsent(propertyType);
                        WriteLine($"value.{objectPropertyName} = JsonConverterFor{readableTypeName}.Instance.Read(ref reader, typeToConvert, options);");
                    }

                    WriteControlBlockEnd();
                }
            };

            WriteControlBlockEnd();

            WriteBlankLine();

            WriteLine($"return value;");

            WriteControlBlockEnd();

            WriteBlankLine();
        }

        private void WriteConverterReadMethodForArray()
        {
            WriteMethodStart(
                level: AccessibilityLevel.Public,
                isOverride: true,
                returnTypeName: $"{GetCompilableTypeName(_type)}",
                methodName: "Read",
                parameterListValue: "ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options");

            // Validate that the reader's cursor is at a start array token.
            WriteSingleLineComment("Validate that the reader's cursor is at a start array token");
            WriteLine("if (reader.TokenType != JsonTokenType.StartArray)");
            WriteControlBlockStart();
            WriteThrowJsonException();
            WriteControlBlockEnd();

            WriteBlankLine();

            Debug.Assert(_type.IsArray);
            
            Type elementType = _type.GetElementType();
            string tempListTypeName = $"List<{GetCompilableTypeName(elementType)}>";

            // Create returned object. This assumes type has public parameterless ctor.
            WriteSingleLineComment("Create temporary list of array elements.");
            WriteLine($"{tempListTypeName} elements = new {tempListTypeName}();");

            WriteBlankLine();

            // Read all properties.
            WriteSingleLineComment("Read all elements");
            WriteLine("while (true)");
            WriteControlBlockStart();

            WriteLine(@$"reader.Read();");
            WriteBlankLine();

            WriteLine("if (reader.TokenType == JsonTokenType.EndArray)");
            WriteControlBlockStart();
            WriteLine("break;");
            WriteControlBlockEnd();

            WriteBlankLine();

            string elementReadableTypeName = GetReadableTypeName(elementType);
            if (s_simpleTypes.Contains(elementType))
            {
                WriteLine($"elements.Add(reader.Get{elementReadableTypeName}());");
            }
            else
            {
                WriteJsonConverterForTypeIfAbsent(elementType);
                WriteLine($"elements.Add(JsonConverterFor{elementReadableTypeName}.Instance.Read(ref reader, typeToConvert, options));");
            }

            // End while true loop.
            WriteControlBlockEnd();

            WriteBlankLine();

            WriteLine("return elements.ToArray();");
            
            WriteControlBlockEnd();

            WriteBlankLine();
        }

        private void WriteConverterReadMethodForListOfT()
        {
            WriteMethodStart(
                level: AccessibilityLevel.Public,
                isOverride: true,
                returnTypeName: $"{GetCompilableTypeName(_type)}",
                methodName: "Read",
                parameterListValue: "ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options");

            // Validate that the reader's cursor is at a start array token.
            WriteSingleLineComment("Validate that the reader's cursor is at a start array token");
            WriteLine("if (reader.TokenType != JsonTokenType.StartArray)");
            WriteControlBlockStart();
            WriteThrowJsonException();
            WriteControlBlockEnd();

            WriteBlankLine();

            Debug.Assert(_type.IsGenericType && _type.GetGenericTypeDefinition() == typeof(List<>));
            
            Type elementType = _type.GetGenericArguments()[0];
            string tempListTypeName = $"List<{GetCompilableTypeName(elementType)}>";

            WriteLine($"{tempListTypeName} elements = new {tempListTypeName}();");

            WriteBlankLine();

            // Read all properties.
            WriteSingleLineComment("Read all elements");
            WriteLine("while (true)");
            WriteControlBlockStart();

            WriteLine(@$"reader.Read();");
            WriteBlankLine();

            WriteLine("if (reader.TokenType == JsonTokenType.EndArray)");
            WriteControlBlockStart();
            WriteLine("break;");
            WriteControlBlockEnd();

            WriteBlankLine();

            string elementReadableTypeName = GetReadableTypeName(elementType);
            if (s_simpleTypes.Contains(elementType))
            {
                // Validate that the reader's cursor is at a string token.
                WriteSingleLineComment("Validate that the reader's cursor is at a string token");
                WriteLine("if (reader.TokenType != JsonTokenType.String)");
                WriteControlBlockStart();
                WriteThrowJsonException();
                WriteControlBlockEnd();

                WriteBlankLine();

                WriteLine($"elements.Add(reader.Get{elementReadableTypeName}());");
            }
            if (s_simpleTypes.Contains(elementType))
            {
                WriteLine($"elements.Add(reader.Get{elementReadableTypeName}());");
            }
            else
            {
                WriteJsonConverterForTypeIfAbsent(elementType);
                WriteLine($"elements.Add(JsonConverterFor{elementReadableTypeName}.Instance.Read(ref reader, typeToConvert, options));");
            }

            // End while true loop.
            WriteControlBlockEnd();

            WriteBlankLine();

            WriteLine("return elements;");
            
            WriteControlBlockEnd();

            WriteBlankLine();
        }

        private void WriteConverterReadMethodForDictionaryOfStringToTValue()
        {
            WriteMethodStart(
                level: AccessibilityLevel.Public,
                isOverride: true,
                returnTypeName: $"{GetCompilableTypeName(_type)}",
                methodName: "Read",
                parameterListValue: "ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options");

            Debug.Assert(_type.IsGenericType
                && _type.GetGenericTypeDefinition() == typeof(Dictionary<,>)
                && _type.GetGenericArguments()[0] == typeof(string));
        
            Type elementType = _type.GetGenericArguments()[0];
            string tempListTypeName = $"List<{GetCompilableTypeName(elementType)}>";

            // Validate that the reader's cursor is at a start object token.
            WriteSingleLineComment("Validate that the reader's cursor is at a start object token");
            WriteLine("if (reader.TokenType != JsonTokenType.StartObject)");
            WriteControlBlockStart();
            WriteThrowJsonException();
            WriteControlBlockEnd();

            WriteBlankLine();

            // Create returned object. This assumes type has public parameterless ctor.
            WriteSingleLineComment("Create returned object. This assumes type has public parameterless ctor");
            WriteLine($"{GetCompilableTypeName(_type)} value = new {GetCompilableTypeName(_type)}();");

            WriteBlankLine();

            // Read all properties.
            WriteSingleLineComment("Read all properties");
            WriteLine("while (true)");
            WriteControlBlockStart();

            WriteLine("reader.Read();");
            WriteBlankLine();

            WriteLine("if (reader.TokenType == JsonTokenType.EndObject)");
            WriteControlBlockStart();
            WriteLine("break;");
            WriteControlBlockEnd();

            WriteBlankLine();

            // Note that we don't check for escaping: only unescaped property names are accounted for.
            WriteLine("string key = reader.GetString();");

            WriteBlankLine();

            WriteSingleLineComment("Move reader cursor to property value");
            WriteLine("reader.Read();");

            WriteBlankLine();

            string elementReadableTypeName = GetReadableTypeName(elementType);

            if (elementType == typeof(char))
            {
                WriteLine("string tmp = reader.GetString();");
                WriteLine("if (string.IsNullOrEmpty(tmp))");
                WriteControlBlockStart();
                WriteThrowJsonException();
                WriteControlBlockEnd();

                WriteBlankLine();

                WriteLine($"value[key] = tmp[0];");
            }
            else if (elementType == typeof(byte[]))
            {
                WriteLine($@"reader.GetBytesFromBase64();");
            }
            else if (s_simpleTypes.Contains(elementType))
            {
                WriteLine($"value[key] = reader.Get{elementReadableTypeName}();");
            }
            else
            {
                WriteJsonConverterForTypeIfAbsent(elementType);
                WriteLine($"value[key] = JsonConverterFor{elementReadableTypeName}.Instance.Read(ref reader, typeToConvert, options);");
            }

            WriteControlBlockEnd();

            WriteBlankLine();

            WriteLine($"return value;");

            WriteControlBlockEnd();

            WriteBlankLine();
        }

        private void WriteConverterWriteMethodForObject()
        {
            WriteMethodStart(
                level: AccessibilityLevel.Public,
                isOverride: true,
                returnTypeName: "void",
                methodName: "Write",
                parameterListValue: $"Utf8JsonWriter writer, {GetCompilableTypeName(_type)} value, JsonSerializerOptions options");

            // Write null and return if value is null.
            WriteLine("if (value == null)");
            WriteControlBlockStart();
            WriteLine("writer.WriteNullValue();");
            WriteLine("return;");
            WriteControlBlockEnd();

            WriteBlankLine();

            WriteLine("writer.WriteStartObject();");

            WriteBlankLine();

            foreach (PropertyInfo property in _properties)
            {
                Type propertyType = property.PropertyType;
                string objectPropertyName = property.Name;

                string compilableTypeName = GetCompilableTypeName(propertyType);
                string readableTypeName = GetReadableTypeName(compilableTypeName);

                string jsonPropertyBytesVarName = $"{objectPropertyName}Bytes";

                string currentValueName = $"value.{objectPropertyName}";

                if (propertyType == typeof(int))
                {
                    WriteLine($@"writer.WriteNumber({jsonPropertyBytesVarName}, {currentValueName});");
                }
                else if (propertyType == typeof(char))
                {
                    WriteLine($"char charValue = {currentValueName};");
                    WriteSingleLineComment("Assume we are running NetCore app");
                    WriteLine($@"writer.WriteString({jsonPropertyBytesVarName}, MemoryMarshal.CreateSpan(ref charValue, 1));");
                }
                else if (propertyType == typeof(bool))
                {
                    WriteLine($@"writer.WriteBoolean({jsonPropertyBytesVarName}, {currentValueName});");
                }
                else if (propertyType == typeof(byte[]))
                {
                    WriteLine($@"writer.WriteBase64String({jsonPropertyBytesVarName}, {currentValueName});");
                }
                else if (s_simpleTypes.Contains(propertyType))
                {
                    WriteLine($@"writer.WriteString({jsonPropertyBytesVarName}, {currentValueName});");
                }
                else
                {
                    WriteLine($@"writer.WritePropertyName({jsonPropertyBytesVarName});");
                    WriteLine($"JsonConverterFor{readableTypeName}.Instance.Write(writer, {currentValueName}, options);");
                }

                WriteBlankLine();
            }

            WriteLine("writer.WriteEndObject();");

            WriteControlBlockEnd();
        }

        private void WriteConverterWriteMethodForArray()
        {
            WriteMethodStart(
                level: AccessibilityLevel.Public,
                isOverride: true,
                returnTypeName: "void",
                methodName: "Write",
                parameterListValue: $"Utf8JsonWriter writer, {GetCompilableTypeName(_type)} value, JsonSerializerOptions options");

            Debug.Assert(_type.IsArray);

            Type elementType = _type.GetElementType();
            string elementReadableTypeName = GetReadableTypeName(elementType);

            // Write null and return if value is null.
            WriteSingleLineComment("TODO: account for value-type collections (e.g. ImmutableArray)");
            WriteLine("if (value == null)");
            WriteControlBlockStart();
            WriteLine("writer.WriteNullValue();");
            WriteLine("return;");
            WriteControlBlockEnd();

            WriteBlankLine();

            WriteLine("writer.WriteStartArray();");

            WriteBlankLine();

            WriteLine($"foreach ({elementReadableTypeName} element in value)");
            WriteControlBlockStart();

            if (elementType == typeof(int))
            {
                WriteLine($@"writer.WriteNumberValue(element);");
            }
            else if (elementType == typeof(char))
            {
                WriteLine($"char charValue = element;");
                WriteSingleLineComment("Assume we are running NetCore app");
                WriteLine($@"writer.WriteStringValue(MemoryMarshal.CreateSpan(ref charValue, 1));");
            }
            else if (elementType == typeof(bool))
            {
                WriteLine($@"writer.WriteBooleanValue(element);");
            }
            else if (elementType == typeof(byte[]))
            {
                WriteLine($@"writer.WriteBase64StringValue(element);");
            }
            else if (s_simpleTypes.Contains(elementType))
            {
                WriteLine($@"writer.WriteStringValue(element);");
            }
            else
            {
                WriteLine($"JsonConverterFor{elementReadableTypeName}.Instance.Write(writer, element, options);");
            }

            WriteControlBlockEnd();

            WriteBlankLine();

            WriteLine("writer.WriteEndArray();");

            WriteControlBlockEnd();
        }

        private void WriteConverterWriteMethodForListOfT()
        {
            WriteMethodStart(
                level: AccessibilityLevel.Public,
                isOverride: true,
                returnTypeName: "void",
                methodName: "Write",
                parameterListValue: $"Utf8JsonWriter writer, {GetCompilableTypeName(_type)} value, JsonSerializerOptions options");

            Debug.Assert(_type.IsGenericType && _type.GetGenericTypeDefinition() == typeof(List<>));

            Type elementType = _type.GetGenericArguments()[0];
            string elementReadableTypeName = GetReadableTypeName(elementType);

            // Write null and return if value is null.
            WriteSingleLineComment("TODO: account for value-type collections (e.g. ImmutableArray)");
            WriteLine("if (value == null)");
            WriteControlBlockStart();
            WriteLine("writer.WriteNullValue();");
            WriteLine("return;");
            WriteControlBlockEnd();

            WriteBlankLine();

            WriteLine("writer.WriteStartArray();");

            WriteBlankLine();

            WriteLine($"foreach ({elementReadableTypeName} element in value)");
            WriteControlBlockStart();

            if (elementType == typeof(int))
            {
                WriteLine($@"writer.WriteNumberValue(element);");
            }
            else if (elementType == typeof(char))
            {
                WriteLine($"char charValue = element;");
                WriteSingleLineComment("Assume we are running NetCore app");
                WriteLine($@"writer.WriteStringValue(MemoryMarshal.CreateSpan(ref charValue, 1));");
            }
            else if (elementType == typeof(bool))
            {
                WriteLine($@"writer.WriteBooleanValue(element);");
            }
            else if (elementType == typeof(byte[]))
            {
                WriteLine($@"writer.WriteBase64StringValue(element);");
            }
            else if (s_simpleTypes.Contains(elementType))
            {
                WriteLine($@"writer.WriteStringValue(element);");
            }
            else
            {
                WriteLine($"JsonConverterFor{elementReadableTypeName}.Instance.Write(writer, element, options);");
            }

            WriteControlBlockEnd();

            WriteBlankLine();

            WriteLine("writer.WriteEndArray();");

            WriteControlBlockEnd();
        }

        private void WriteConverterWriteMethodForDictionaryOfStringToTValue()
        {
            WriteMethodStart(
                level: AccessibilityLevel.Public,
                isOverride: true,
                returnTypeName: "void",
                methodName: "Write",
                parameterListValue: $"Utf8JsonWriter writer, {GetCompilableTypeName(_type)} value, JsonSerializerOptions options");

            Debug.Assert(_type.IsGenericType
                && _type.GetGenericTypeDefinition() == typeof(Dictionary<,>)
                && _type.GetGenericArguments()[0] == typeof(string));

            Type elementType = _type.GetGenericArguments()[0];
            string elementReadableTypeName = GetReadableTypeName(elementType);

            // Write null and return if value is null.
            WriteSingleLineComment("TODO: account for value-type collections");
            WriteLine("if (value == null)");
            WriteControlBlockStart();
            WriteLine("writer.WriteNullValue();");
            WriteLine("return;");
            WriteControlBlockEnd();

            WriteBlankLine();

            WriteLine("writer.WriteStartObject();");

            WriteBlankLine();

            WriteLine($"foreach (KeyValuePair<string, {elementReadableTypeName}> pair in value)");
            WriteControlBlockStart();

            if (elementType == typeof(int))
            {
                WriteLine($@"writer.WriteNumber(pair.Key, pair.Value);");
            }
            else if (elementType == typeof(char))
            {
                WriteLine($"char charValue = element;");
                WriteSingleLineComment("Assume we are running NetCore app");
                WriteLine($@"writer.WriteStringValue(MemoryMarshal.CreateSpan(ref charValue, 1));");
            }
            else if (elementType == typeof(bool))
            {
                WriteLine($@"writer.WriteBoolean(pair.Key, pair.Value);");
            }
            else if (elementType == typeof(byte[]))
            {
                WriteLine($@"writer.WriteBase64String(pair.Key, pair.Value);");
            }
            else if (s_simpleTypes.Contains(elementType))
            {
                WriteLine($@"writer.WriteString(pair.Key, pair.Value);");
            }
            else
            {
                WriteLine($@"writer.WritePropertyName(pair.Key);");
                WriteLine($"JsonConverterFor{elementReadableTypeName}.Instance.Write(writer, pair.Value, options);");
            }

            WriteControlBlockEnd();

            WriteBlankLine();

            WriteLine("writer.WriteEndObject();");

            WriteControlBlockEnd();
        }

        private void WriteThrowJsonException()
        {
            WriteLine("throw new JsonException();");
        }

        private void WriteAutoGenerationDisclaimer()
        {
            WriteLine($@"//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:{Environment.Version}
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------");
        }

        private void WriteBlankLine()
        {
            WriteLine("");
        }

        private void MoveToNewLine()
        {
            _sourceBuilder.Append("\n");
        }

        private void WriteLine(string value)
        {
            if (_indent > 0)
            {
                _sourceBuilder.Append(new string(' ', _indent * 4));
            }
            _sourceBuilder.AppendLine(value);
        }

        private void WriteMethodStart(AccessibilityLevel level, bool isOverride, string returnTypeName, string methodName, string parameterListValue)
        {
            // Apply indentation.
            _sourceBuilder.Append(new string(' ', _indent * 4));

            if (level == AccessibilityLevel.Public)
            {
                _sourceBuilder.Append("public");
            }

            _sourceBuilder.Append(" ");

            if (isOverride)
            {
                _sourceBuilder.Append("override");
                _sourceBuilder.Append(" ");
            }

            _sourceBuilder.Append(returnTypeName);

            _sourceBuilder.Append(" ");

            _sourceBuilder.Append(methodName);
            _sourceBuilder.Append("(");
            _sourceBuilder.Append(parameterListValue);
            _sourceBuilder.Append(")");
            MoveToNewLine();

            WriteControlBlockStart();
        }

        private void BeginNewControlBlock(string value)
        {
            if (_indent > 0)
            {
                _sourceBuilder.Append(new string(' ', _indent * 4));
            }
            _sourceBuilder.AppendLine(value);

            WriteControlBlockStart();
        }

        private void WriteControlBlockStart()
        {
            WriteLine("{");
            Indent();
        }

        private void WriteControlBlockEnd()
        {
            Unindent();
            WriteLine("}");
        }

        private void Indent()
        {
            _indent++;
        }

        private void Unindent()
        {
            _indent--;
        }

        private void WriteSingleLineComment(string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                WriteLine($"// {value}.");
            }
        }

        private static string GetCompilableTypeName(Type type)
        {
            string typeName = type.Name;

            if (!type.IsGenericType)
            {
                return typeName;
            }

            // TODO: Guard against open generics?
            Debug.Assert(!type.ContainsGenericParameters);

            int backTickIndex = typeName.IndexOf('`');
            string baseName = typeName.Substring(0, backTickIndex);

            return $"{baseName}<{string.Join(',', type.GetGenericArguments().Select(arg => GetCompilableTypeName(arg)))}>";
        }

        public static string GetReadableTypeName(Type type)
        {
            return GetReadableTypeName(GetCompilableTypeName(type));
        }

        private static string GetReadableTypeName(string compilableName)
        {
            return compilableName.Replace("<", "").Replace(">", "").Replace(",", "").Replace("[]", "Array");
        }
    }

    internal enum AccessibilityLevel
    {
        Public,
    }
}
