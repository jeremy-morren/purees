﻿using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace PureES.SourceGenerators.Framework;

internal static class WriterHelpers
{
    public static void WriteFileHeader(this IndentedWriter writer, bool enableNullable)
    {
        writer.WriteLine("// <auto-generated/>");
        writer.WriteLine();
        
        writer.WriteLine("// This file was automatically generated by the PureES source generator.");
        writer.WriteLine("// Do not edit this file manually since it will be automatically overwritten.");
        writer.WriteLine("// ReSharper disable All");
        writer.WriteLine();
        
        writer.WriteLine($"#nullable {(enableNullable ? "enable" : "disable")}");
        writer.WriteLine("#pragma warning disable CS0162 //Unreachable code detected");
        writer.WriteLine();
        
        //Common using
        writer.WriteLine("#pragma warning disable CS8019 //Unnecessary using directive");
        writer.WriteLine("using System;");
        writer.WriteLine("using System.Threading;");
        writer.WriteLine("using System.Threading.Tasks;");
        writer.WriteLine("using System.Linq;");
        writer.WriteLine($"using {ExternalTypes.LoggingNamespace};");
        writer.WriteLine($"using {ExternalTypes.DINamespace};");

        writer.WriteLine();
    }
    
    private static Version Version => typeof(WriterHelpers).Assembly.GetName().Version;

    public static void WriteBrowsableState(this IndentedWriter writer)
    {
        writer.WriteLine($"[global::{typeof(EditorBrowsableAttribute).FullName}(global::{typeof(EditorBrowsableState).FullName}.{EditorBrowsableState.Never})]");
    }
    
    public static void WriteClassAttributes(this IndentedWriter writer)
    {
        writer.WriteBrowsableState();
        writer.WriteLine($"[global::{typeof(ExcludeFromCodeCoverageAttribute).FullName}]");
        writer.WriteLine($"[global::{typeof(GeneratedCodeAttribute).FullName}(\"PureES.SourceGenerator\", \"{Version}\")]");
    }

    public static void WriteMethodAttributes(this IndentedWriter writer, MethodImplOptions? methodImplOptions = null)
    {
        writer.WriteLine();
        writer.WriteBrowsableState();
        writer.WriteLine($"[global::{typeof(DebuggerStepThroughAttribute).FullName}()]");
        writer.WriteLine($"[global::{typeof(DebuggerNonUserCodeAttribute).FullName}()]");
        if (methodImplOptions.HasValue)
            writer.WriteLine($"[global::{typeof(MethodImplAttribute).FullName}(global::{typeof(MethodImplOptions).FullName}.{methodImplOptions.Value})]");
    }

    /// <summary>
    /// Writes an indented statement with braces
    /// i.e. <c>header { writeContent }</c>
    /// </summary>
    public static void WriteStatement(this IndentedWriter writer, string header, Action writeContent)
    {
        writer.WriteLine(header);
        writer.PushBrace();
        writeContent();
        writer.PopBrace();
    }
    
    /// <summary>
    /// Writes an indented statement with braces
    /// i.e. <c>header { content }</c>
    /// </summary>
    public static void WriteStatement(this IndentedWriter writer, string header, string content)
    {
        writer.WriteLine(header);
        writer.PushBrace();
        writer.WriteLine(content);
        writer.PopBrace();
    }

    public static void PopAllBraces(this IndentedWriter writer)
    {
        while (writer.CurrentIndentLevel > 0)
            writer.PopBrace();
    }
    
    
    public static void WriteParameters(this IndentedWriter writer, params IEnumerable<string>[] argLists)
    {
        writer.WriteParameters(argLists.SelectMany(l => l).ToArray());
    }
    
    public static void WriteParameters(this IndentedWriter writer, params string[] args)
    {
        var str = string.Join(", ", args);
        if (str.Length > (80 - writer.GetIndent().Length))
        {
            //Preceding newline + newline after comma
            str = $"\n{writer.GetIndent(1)}" + string.Join($",\n{writer.GetIndent(1)}", args);
        }
        writer.WriteRaw(str);
    }
    
    public static void WriteLogMessage(this IndentedWriter writer,
        string level,
        string exception, 
        [StructuredMessageTemplate] string message, 
        params string[] args)
    {
        writer.Write("this._logger.Log(");
        if (!level.Contains("LogLevel"))
            level = $"{ExternalTypes.LogLevel}.{level}";
        //ILogger.Log(LogLevel logLevel, Exception? exception, string? message, params object?[] args)
        writer.WriteParameters(new[]
        {
            $"logLevel: {level}",
            $"exception: {exception}",
            $"message: {message.ToStringLiteral()}",
        }, args);

        writer.WriteRawLine(");");
    }
}