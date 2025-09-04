#nullable enable

using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Roslynator.CSharp.Analysis.StructuralHonesty;

public static class StructuralHonesty
{
    /// <summary>
    /// Returns <c>true</c> if the node has structural honesty issues.
    /// </summary>
    public static bool Analyze(
        SyntaxNode node,
        AnalyzerConfigOptions options,
        CancellationToken cancellationToken
    )
    {
        StructuralHonestySyntaxRewriter? rewriter = CreateRewriter(node, options, cancellationToken);
        if (rewriter is null)
        {
            return false;
        }

        rewriter.DoAnalysisOnly = true;

        _ = rewriter.Visit(node);

        return rewriter.ChangesRequired;
    }

    /// <summary>
    /// Returns changed node with fixed structural honesty issues.
    /// If the returning node is null, then the node is unchanged.
    /// </summary>
    public static SyntaxNode? Fix(
        SyntaxNode node,
        AnalyzerConfigOptions options,
        CancellationToken cancellationToken
    )
    {
        StructuralHonestySyntaxRewriter? rewriter = CreateRewriter(node, options, cancellationToken);
        return rewriter?.Visit(node);
    }

    public static StructuralHonestySyntaxRewriter? CreateRewriter(
        SyntaxNode node,
        AnalyzerConfigOptions options,
        CancellationToken cancellationToken
    )
    {
        IndentationAnalysis indentationAnalysis =
            SyntaxTriviaAnalysis.AnalyzeIndentation(node, options, cancellationToken);

        string singleIndentation = indentationAnalysis.GetSingleIndentation();

        SyntaxTrivia newLine = SyntaxTriviaAnalysis.DetermineEndOfLine(node);
        return new StructuralHonestySyntaxRewriter(
            node,
            indentationAnalysis.Indentation.ToString(),
            singleIndentation,
            newLine,
            cancellationToken
        );
    }
}
