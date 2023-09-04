using System.Text;

namespace PureES.Core.Generators.Framework;

/// <summary>
/// Provides an indented wrapper of <see cref="StringBuilder"/>
/// that always uses <c>LF</c> line endings
/// </summary>
internal class IndentedWriter
{
    private readonly int _indentSize;
    
    public IndentedWriter(int indentSize = 4) => _indentSize = indentSize;

    private readonly StringBuilder _builder = new();

    /// <summary>
    /// The string written so far
    /// </summary>
    public string Value => _builder.ToString();

    public int CurrentIndentLevel { get; private set; } = 0;

    /// <summary>
    /// Gets indent, possibly offset from the current value
    /// </summary>
    public string GetIndent(int offset = 0) => new (' ', (CurrentIndentLevel + offset) * _indentSize);

    private void WriteIndent()
    {
        _builder.Append(GetIndent());
    }

    private void AppendLine(char value)
    {
        _builder.Append(value);
        _builder.Append('\n');
    }
    
    private void AppendLine(string value)
    {
        _builder.Append(value);
        _builder.Append('\n');
    }
    
    private void AppendLine()
    {
        _builder.Append('\n');
    }
    
    /// <summary>
    /// Increases the indent level by 1
    /// </summary>
    public void Push()
    {
        CurrentIndentLevel++;
    }
    
    /// <summary>
    /// reduces indent level by 1
    /// </summary>
    public void Pop()
    {
        if (CurrentIndentLevel == 0)
            throw new InvalidOperationException("Indent level is already at 0");
        CurrentIndentLevel--;
    }

    /// <summary>
    /// Writes an indented line and increases the indent level by 1
    /// </summary>
    public void WriteLineThenPush(string value)
    {
        WriteIndent();
        AppendLine(value);
        CurrentIndentLevel++;
    }
    
    /// <summary>
    /// Writes an opening brace and increases indent by 1
    /// </summary>
    public void PushBrace()
    {
        WriteIndent();
        AppendLine('{');
        CurrentIndentLevel++;
    }

    /// <summary>
    /// Writes a closing brace and reduces the indent level by 1
    /// </summary>
    public void PopBrace()
    {
        if (CurrentIndentLevel == 0)
            throw new InvalidOperationException("Indent level is already at 0");
        CurrentIndentLevel--;
        WriteIndent();
        AppendLine('}');
    }

    /// <summary>
    /// Writes an indented line
    /// </summary>
    public void WriteLine(char c)
    {
        WriteIndent();
        _builder.Append(c);
        AppendLine();
    }
    
    /// <summary>
    /// Writes an indented line
    /// </summary>
    public void WriteLine(string value)
    {
        WriteIndent();
        AppendLine(value);
    }
    
    /// <summary>
    /// Writes an empty
    /// </summary>
    public void WriteLine()
    {
        //Empty line, we don't need indent
        AppendLine();
    }
    
    /// <summary>
    /// Writes an indented value (without a trailing newline)
    /// </summary>
    public void Write(string value)
    {
        WriteIndent();
        _builder.Append(value);
    }
    
    /// <summary>
    /// Writes value without any indent applied
    /// </summary>
    public void WriteRaw(string value)
    {
        _builder.Append(value);
    }
    
    /// <summary>
    /// Writes value without any indent applied
    /// </summary>
    public void WriteRawLine(char c)
    {
        _builder.Append(c);
        AppendLine();
    }
    
    /// <summary>
    /// Writes value without any indent applied
    /// </summary>
    public void WriteRawLine(string value)
    {
        AppendLine(value);
    }

    public void TrimEnd(int numChars)
    {
        if (numChars < 0)
            throw new ArgumentOutOfRangeException(nameof(numChars), numChars,
                "Value must be greater than or equal to 0");
        _builder.Length -= numChars;
    }

    public override string ToString() => new
    {
        Lines = _builder.ToString().Count(c => c == '\n') + 1,
        CurrentIndentLevel
    }.ToString();
}