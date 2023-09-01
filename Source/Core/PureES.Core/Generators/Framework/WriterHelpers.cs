﻿using System.CodeDom.Compiler;
using System.Diagnostics;
using Microsoft.CodeAnalysis;

namespace PureES.Core.Generators.Framework;

internal static class WriterHelpers
{
    public static void WriteFileHeader(this IndentedWriter writer, bool enableNullable)
    {
        writer.WriteLine("// <auto-generated/>");
        writer.WriteLine("// This file was automatically generated by the PureES source generator.");
        writer.WriteLine("// Do not edit this file manually since it will be automatically overwritten.");
        writer.WriteLine("// ReSharper disable All");
        writer.WriteLine($"#nullable {(enableNullable ? "enable" : "disable")}");
    }
    
    public static void WriteClassAttributes(this IndentedWriter writer)
    {
        writer.WriteLine($"[global::{typeof(GeneratedCodeAttribute).FullName}(\"PureES.SourceGenerator\", \"{Version}\")]");
    }

    public static void WriteDebuggerAttributes(this IndentedWriter writer)
    {
        writer.WriteLine($"[global::{typeof(DebuggerStepThroughAttribute).FullName}()]");
        writer.WriteLine($"[global::{typeof(DebuggerNonUserCodeAttribute).FullName}()]");
    }

    private static Version Version => typeof(WriterHelpers).Assembly.GetName().Version;
    
    
    /// <summary>
    /// Writes an indented statement with braces
    /// i.e. <c>header { writeContent }</c>
    /// </summary>
    public static void WriteStatement(this IndentedWriter writer, string header, Action writeContent)
    {
        writer.WriteLine(header);
        writer.WriteLineThenPush('{');
        writeContent();
        writer.PopThenWriteLine('}');
    }
    
    /// <summary>
    /// Writes an indented statement with braces
    /// i.e. <c>header { content }</c>
    /// </summary>
    public static void WriteStatement(this IndentedWriter writer, string header, string content)
    {
        writer.WriteLine(header);
        writer.WriteLineThenPush('{');
        writer.WriteLine(content);
        writer.PopThenWriteLine('}');
    }

    public static void WritePartialTypeDefinition(this IndentedWriter writer, IType type)
    {
        //Build list of types
        var types = new List<IType>() { type};
        var parent = type.ContainingType;
        while (true)
        {
            if (parent == null)
                break;
            types.Add(parent);
            parent = parent.ContainingType;
        }

        //Write them in reverse
        types.Reverse();
        
        writer.WriteLine($"namespace {types[0].Namespace}");
        writer.WriteLineThenPush('{');
        foreach (var p in types)
        {
            writer.WriteLine($"partial class {p.Name}");
            writer.WriteLineThenPush('{');
        }
    }

    public static void PopAll(this IndentedWriter writer)
    {
        while (writer.CurrentIndentLevel > 0)
            writer.PopThenWriteLine('}');
    }
}