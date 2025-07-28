#nullable enable

using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Roslynator.CSharp.Analysis.StructuralHonesty.SyntaxRewriters;

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
            // If there is no rewriter, then it is either an unsupported case or the node is single-line.
            return false;
        }
        rewriter.DoAnalysisOnly = true;

        _ = rewriter.Visit(node);

        return rewriter.HasStructuralHonestyIssues;
    }

    /// <summary>
    /// Returns changed node with fixed structural honesty issues.
    /// If the returned node is null, then the node is unchanged.
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
        if (node.IsSingleLine(cancellationToken: cancellationToken))
        {
            // No fix is required for single-line nodes.
            return null;
        }

        // Indentation analysis should be done on the parent of the node.
        // Weather the indentation is correct, we can say only relatively to its parent.
        SyntaxNode? parent = node.Parent;
        // Argument syntax is kind of a virtual wrapper over the real argument.
        // Only the real argument should be checked for indentation.
        if (parent is ArgumentSyntax)
        {
            parent = parent.Parent;
        }

        if (parent is null)
        {
            // If the node has no parent, then it is a root node, so we cannot analyze its indentation.
            // Is it even possible to come here?
            return null;
        }

        IndentationAnalysis indentationAnalysis =
            SyntaxTriviaAnalysis.AnalyzeIndentation(parent, options, cancellationToken);

        bool rootExpression =
            node.Parent is GlobalStatementSyntax
            || (node.Parent is ExpressionStatementSyntax expression && expression.Parent is GlobalStatementSyntax);

        string expectedIndentation =
            rootExpression
                ? string.Empty
                : indentationAnalysis.GetIncreasedIndentation();

        string singleIndentation = indentationAnalysis.GetSingleIndentation();

        StructuralHonestySyntaxRewriter syntaxRewriter =
            node.Kind() switch
            {
                _ => new IndentingStructuralHonestySyntaxRewriter(expectedIndentation, singleIndentation)
            };

        return syntaxRewriter;
    }
}
