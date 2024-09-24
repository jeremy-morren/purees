using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PureES.SourceGenerators.Framework;

internal static class SyntaxHelpers
{
    public static bool IsPartial(this TypeDeclarationSyntax type) =>
        type.Modifiers.Any(t => t.IsKind(SyntaxKind.PartialKeyword));
    
    /// <summary>
    /// Converts a string to a string literal (including quotes)
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public static string ToStringLiteral(this string input)
    {
        return SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(input)).ToFullString();
    }
}