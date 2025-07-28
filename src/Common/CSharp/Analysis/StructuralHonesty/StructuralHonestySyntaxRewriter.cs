#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Roslynator.CSharp.Analysis.StructuralHonesty;

public sealed class StructuralHonestySyntaxRewriter : CSharpSyntaxRewriter
{
    private static readonly char[] SplitChars = ['\r', '\n'];

    private readonly SyntaxTree _syntaxTree;
    private readonly TextLineCollection _textLines;
    private readonly string _parentIndentation;
    private readonly string _singleIndentation;
    private readonly CancellationToken _cancellationToken;

    private readonly Dictionary<SyntaxNode, string> _indentationCache = [];

    public bool DoAnalysisOnly { get; set; }
    public bool ChangesApplied { get; set; }

    public StructuralHonestySyntaxRewriter(
        SyntaxTree syntaxTree,
        TextLineCollection textLines,
        string parentIndentation,
        string singleIndentation,
        CancellationToken cancellationToken
    )
    {
        _syntaxTree = syntaxTree;
        _textLines = textLines;
        _parentIndentation = parentIndentation;
        _singleIndentation = singleIndentation;
        _cancellationToken = cancellationToken;
    }

    public override SyntaxNode? Visit(SyntaxNode? node)
    {
        if (node is null)
        {
            return null;
        }

        // We shouldn't neither format trivia nor add something to the indentation cache if it is not the first node in the line.
        // Otherwise, each deeper level will have an additional unexpected indentation.
        if (!CheckNothingButTriviaInFront(node))
        {
            return base.Visit(node);
        }

        // If we are here, then it is a node that is the first on the line
        // Doesn't matter it is a single-lined or multi-lined node - it must be properly indented

        string expectedIndentation = GetParentIndentation(node) + _singleIndentation;
        SyntaxNodeOrToken? newNodeOrToken = ReformatLeadingTrivia(node, expectedIndentation);

        // Are there changes?
        if (newNodeOrToken is not null)
        {
            ChangesApplied = true;
            node = newNodeOrToken.Value.AsNode()!;

            // Immediate stop if at least one change was applied
            if (DoAnalysisOnly)
            {
                return node;
            }
        }

        newNodeOrToken = ReformatTrailingTrivia(node, expectedIndentation);
        if (newNodeOrToken is not null)
        {
            ChangesApplied = true;
            node = newNodeOrToken.Value.AsNode()!;

            // Immediate stop if at least one change was applied
            if (DoAnalysisOnly)
            {
                return node;
            }
        }

        if (node.IsMultiLine())
        {
            _indentationCache[node] = expectedIndentation;
        }

        return base.Visit(node);
    }

    public override SyntaxNode? VisitEqualsValueClause(EqualsValueClauseSyntax node)
    {
        if (node.Value.IsSingleLine(cancellationToken: _cancellationToken)
            || node.EqualsToken.TrailingTrivia.LastOrDefault().IsKind(SyntaxKind.EndOfLineTrivia)
            || node.Value.GetLeadingTrivia().FirstOrDefault().IsKind(SyntaxKind.EndOfLineTrivia)
        )
        {
            return base.VisitEqualsValueClause(node);
        }

        node =
            node.WithEqualsToken(
                node.EqualsToken.AppendToTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed)
            );

        ChangesApplied = true;

        // Immediate stop if at least one change was applied
        if (DoAnalysisOnly)
        {
            return node;
        }

        return base.VisitEqualsValueClause(node);
    }

    // public override SyntaxNode? VisitAssignmentExpression(AssignmentExpressionSyntax node)
    // {
    //     if (node.OperatorToken.TrailingTrivia.LastOrDefault().IsKind(SyntaxKind.EndOfLineTrivia)
    //         || node.Right.GetLeadingTrivia().FirstOrDefault().IsKind(SyntaxKind.EndOfLineTrivia)
    //         || node.Right.IsSingleLine(cancellationToken: _cancellationToken)
    //     )
    //     {
    //         return base.VisitAssignmentExpression(node);
    //     }
    //
    //     node =
    //         node.WithOperatorToken(
    //             node.OperatorToken.AppendToTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed)
    //         );
    //
    //     if (DoAnalysisOnly)
    //     {
    //         HasStructuralHonestyIssues = true;
    //         return node;
    //     }
    //
    //     return base.VisitAssignmentExpression(node);
    // }

    public override SyntaxNode? VisitArgumentList(ArgumentListSyntax node)
    {
        string parentIndentation = GetParentIndentation(node);
        string increasedParentIndentation = parentIndentation + _singleIndentation;

        bool openParenOnTheNewLine = false;

        // A pervert case when someone placed the open paren to the new line
        if (CheckNothingButTriviaInFront(node.OpenParenToken))
        {
            openParenOnTheNewLine = true;

            SyntaxNodeOrToken? changedOpenParenToken = ReformatLeadingTrivia(node.OpenParenToken, parentIndentation);

            if (changedOpenParenToken is not null)
            {
                node = node.WithOpenParenToken(changedOpenParenToken.Value.AsToken());
                ChangesApplied = true;
                // Immediate stop if at least one change was applied
                if (DoAnalysisOnly)
                {
                    return node;
                }
            }

            changedOpenParenToken = ReformatTrailingTrivia(node.OpenParenToken, increasedParentIndentation);

            if (changedOpenParenToken is not null)
            {
                node = node.WithOpenParenToken(changedOpenParenToken.Value.AsToken());
                ChangesApplied = true;
                // Immediate stop if at least one change was applied
                if (DoAnalysisOnly)
                {
                    return node;
                }
            }
        }

        if (CheckNothingButTriviaInFront(node.CloseParenToken))
        {
            string indentation =
                openParenOnTheNewLine
                    ? increasedParentIndentation
                    : parentIndentation;

            SyntaxNodeOrToken? changedCloseParenToken = ReformatLeadingTrivia(node.CloseParenToken, indentation);

            if (changedCloseParenToken is not null)
            {
                node = node.WithCloseParenToken(changedCloseParenToken.Value.AsToken());
                ChangesApplied = true;
                // Immediate stop if at least one change was applied
                if (DoAnalysisOnly)
                {
                    return node;
                }
            }

            changedCloseParenToken = ReformatTrailingTrivia(node.CloseParenToken, indentation);

            if (changedCloseParenToken is not null)
            {
                node = node.WithCloseParenToken(changedCloseParenToken.Value.AsToken());
                ChangesApplied = true;
                // Immediate stop if at least one change was applied
                if (DoAnalysisOnly)
                {
                    return node;
                }
            }
        }

        return base.VisitArgumentList(node);
    }

    public override SyntaxNode? VisitArgument(ArgumentSyntax node)
    {
        if (node.Parent is ArgumentListSyntax argumentListSyntax)
        {
            LinePosition openParenLinePosition =
                _textLines.GetLinePosition(argumentListSyntax.OpenParenToken.SpanStart);
            LinePosition closeParenLinePosition =
                _textLines.GetLinePosition(argumentListSyntax.CloseParenToken.SpanStart);

            if (openParenLinePosition.Line != closeParenLinePosition.Line // Multi-lined
                && argumentListSyntax.Arguments.Last() == node
                && !node.GetTrailingTrivia().LastOrDefault().IsKind(SyntaxKind.EndOfLineTrivia))
            {
                // Moving closing paren to the next line
                node = node.AppendToTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);
                ChangesApplied = true;
                if (DoAnalysisOnly)
                {
                    return node;
                }
            }
        }

        return base.VisitArgument(node);
    }

    // public override SyntaxNode? VisitAwaitExpression(AwaitExpressionSyntax node)
    // {
    //     if (!CheckNothingButTriviaInFront(node))
    //     {
    //         return base.VisitAwaitExpression(node);
    //     }
    //
    //     // The leading trivia should be already formatted in the Visit method
    //
    //     // The logic in the Visit + CheckNothingButTriviaInFront above should eliminate the possibility
    //     // to have a single-lined node here
    //
    //     // Now we need to process different special cases for the await expression that require additional formatting
    //     AwaitExpressionSyntax? newNode = null;
    //
    //     switch (node.Expression)
    //     {
    //         case InvocationExpressionSyntax invocationExpressionSyntax:
    //             break;
    //         case ParenthesizedExpressionSyntax parenthesizedExpressionSyntax:
    //             break;
    //     }
    //
    //     if (newNode is not null)
    //     {
    //         node = newNode;
    //         ChangesApplied = true;
    //
    //         // Immediate stop if at least one change was applied
    //         if (DoAnalysisOnly)
    //         {
    //             return node;
    //         }
    //     }
    //     // node =
    //     //     node.WithEqualsToken(
    //     //         node.EqualsToken.AppendToTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed)
    //     //     );
    //     //
    //     // newNodeOrToken = ReformatTrailingTrivia(node, expectedIndentation);
    //     // if (newNodeOrToken is not null)
    //     // {
    //     //     if (DoAnalysisOnly)
    //     //     {
    //     //         HasStructuralHonestyIssues = true;
    //     //         return newNodeOrToken.Value.AsNode();
    //     //     }
    //     //
    //     //     node = newNodeOrToken.Value.AsNode();
    //     // }
    //
    //     return base.VisitAwaitExpression(node);
    // }

    private SyntaxNodeOrToken? ReformatLeadingTrivia(
        SyntaxNodeOrToken node,
        string expectedIndentation
    )
    {
        if (!CheckNothingButTriviaInFront(node))
        {
            return null;
        }

        SyntaxTriviaList leadingTrivia = node.GetLeadingTrivia();
        if (leadingTrivia.Count == 0)
        {
            return node.WithLeadingTrivia(SyntaxFactory.Whitespace(expectedIndentation));
        }

        (bool changesExist, List<SyntaxTrivia> newLeadingTrivia) = ReformatTrivia(node, expectedIndentation, leadingTrivia);

        if (changesExist)
        {
            return node.WithLeadingTrivia(newLeadingTrivia);
        }

        return null;
    }

    private SyntaxNodeOrToken? ReformatTrailingTrivia(
        SyntaxNodeOrToken node,
        string expectedIndentation
    )
    {
        SyntaxTriviaList leadingTrivia = node.GetLeadingTrivia();
        if (leadingTrivia.Count == 0)
        {
            return null;
        }

        (bool changesExist, List<SyntaxTrivia> newLeadingTrivia) = ReformatTrivia(node, expectedIndentation, leadingTrivia);

        if (changesExist)
        {
            return node.WithTrailingTrivia(newLeadingTrivia);
        }

        return null;
    }

    private (bool changesExist, List<SyntaxTrivia> newLeadingTrivia) ReformatTrivia(
        SyntaxNodeOrToken node,
        string expectedIndentation,
        SyntaxTriviaList leadingTrivia
    )
    {
        bool changesExist = false;

        List<SyntaxTrivia> newLeadingTrivia =
            leadingTrivia
                .Select(
                    trivia =>
                    {
                        if (trivia.IsKind(SyntaxKind.WhitespaceTrivia)
                            && trivia.Span.Length != expectedIndentation.Length
                        )
                        {
                            changesExist = true;
                            return SyntaxFactory.Whitespace(expectedIndentation);
                        }

                        if (trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
                            && node.SyntaxTree is not null
                            && trivia.Span.IsMultiLine(node.SyntaxTree, _cancellationToken)
                        )
                        {
                            // For multi-line comment trivia we need to check that the indentation of the content is correct
                            // The content of the multi-line comment trivia is the text between the start and end of the trivia
                            string[] splitContent =
                                trivia.ToFullString()
                                    .Split(SplitChars, StringSplitOptions.RemoveEmptyEntries);

                            if (splitContent.Length > 1)
                            {
                                // The trivia on index 0 is already corrected above. Whatever is there is not relevant
                                int minimumCommentIndentation = GetMinimumCommentIndentation(splitContent, startIndex: 1);

                                if (minimumCommentIndentation != expectedIndentation.Length)
                                {
                                    int splitContentLastIndex = splitContent.Length - 1;
                                    for (int i = 1; i <= splitContentLastIndex; i++)
                                    {
                                        string line = splitContent[i];
                                        if (line.Length == 0)
                                        {
                                            continue;
                                        }

                                        int currentIndentationLength = GetIndentationLength(line);
                                        int additionalIndentation = currentIndentationLength - minimumCommentIndentation;
                                        int endSliceLength = line.Length - minimumCommentIndentation - additionalIndentation;
                                        ReadOnlySpan<char> restOfTheLine = line.AsSpan().Slice(line.Length - endSliceLength, endSliceLength);
                                        splitContent[i] = expectedIndentation + restOfTheLine.ToString();
                                    }

                                    SyntaxTrivia newLine = SyntaxTriviaAnalysis.DetermineEndOfLine(node);

                                    return SyntaxFactory.Comment(string.Join(newLine.ToString(), splitContent));
                                }
                            }
                        }

                        return trivia;
                    }
                )
                .ToList();

        return (changesExist, newLeadingTrivia);
    }

    private static int GetMinimumCommentIndentation(string[] splitContent, int startIndex)
    {
        int minimumIndentation = 0;

        for (int index = startIndex; index < splitContent.Length; index++)
        {
            string line = splitContent[index];
            if (line.Length == 0)
            {
                continue;
            }

            int currentIndentationLength = GetIndentationLength(line);

            if (minimumIndentation == 0
                || currentIndentationLength < minimumIndentation
            )
            {
                minimumIndentation = currentIndentationLength;
            }
        }

        return minimumIndentation;
    }

    private static int GetIndentationLength(string line)
    {
        int currentIndentationLength = 0;
        while (
            currentIndentationLength < line.Length
            && (
                // Let's count both cases as 1
                char.IsWhiteSpace(line[currentIndentationLength])
                || line[currentIndentationLength] == '\t'
            )
        )
        {
            currentIndentationLength++;
        }

        return currentIndentationLength;
    }

    private bool CheckNothingButTriviaInFront(SyntaxNodeOrToken nodeOrToken)
    {
        if (CheckParentOnTheSameLine(nodeOrToken))
        {
            return false;
        }

        LinePosition nodeLinePosition = _textLines.GetLinePosition(nodeOrToken.SpanStart);
        if (nodeLinePosition.Character == 0)
        {
            return true;
        }

        bool nothingButWhitespacesInFront = CheckNothingButWhitespacesInFront(nodeOrToken.SpanStart);
        if (nothingButWhitespacesInFront)
        {
            return true;
        }

        if (CheckNothingButMultilineCommentFromParentTrailingTrivia(nodeOrToken))
        {
            return true;
        }

        return false;
    }

    private bool CheckParentOnTheSameLine(SyntaxNodeOrToken nodeOrToken)
    {
        LinePosition nodeLinePosition = _textLines.GetLinePosition(nodeOrToken.SpanStart);

        SyntaxNode? parent = nodeOrToken.Parent;
        while (parent is not null)
        {
            if (parent.Kind() is SyntaxKind.Argument)
            {
                // Argument syntax is kind of a virtual wrapper over the real argument.
                // Only the real argument should be checked for indentation.
                parent = parent.Parent;
                continue;
            }

            LinePosition linePosition = _textLines.GetLinePosition(parent.SpanStart);
            if (linePosition.Line == nodeLinePosition.Line)
            {
                return true;
            }

            if (linePosition.Line < nodeLinePosition.Line)
            {
                break;
            }

            parent = parent.Parent;
        }

        return false;
    }

    private bool CheckNothingButWhitespacesInFront(int spanStart)
    {
        TextLine lineText = _textLines.GetLineFromPosition(spanStart);

        SourceText? sourceText = lineText.Text;
        if (sourceText == null)
        {
            // I believe that it shouldn't happen. But just for safety
            return true;
        }

        int charIndex = spanStart - 1;
        while (charIndex >= lineText.Start)
        {
            char c = sourceText[charIndex];

            if (!char.IsWhiteSpace(c) && c != '\t')
            {
                return false;
            }

            --charIndex;
        }

        return true;
    }

    private bool CheckNothingButMultilineCommentFromParentTrailingTrivia(SyntaxNodeOrToken nodeOrToken)
    {
        if (CheckParentOnTheSameLine(nodeOrToken))
        {
            return false;
        }

        LinePosition nodeLinePosition = _textLines.GetLinePosition(nodeOrToken.SpanStart);

        TextSpan span = new(nodeOrToken.SpanStart - nodeLinePosition.Character, nodeLinePosition.Character);
        IEnumerable<SyntaxTrivia> descendantTrivia = _syntaxTree.GetRoot().DescendantTrivia(span);

        SyntaxTrivia parentTrivia =
            descendantTrivia.FirstOrDefault(
                trivia =>
                {
                    LinePosition linePositionStart = _textLines.GetLinePosition(trivia.SpanStart);
                    LinePosition linePositionEnd = _textLines.GetLinePosition(trivia.Span.End);
                    return linePositionStart.Line != nodeLinePosition.Line
                        && linePositionEnd.Line == nodeLinePosition.Line;
                }
            );

        return parentTrivia != default;
    }

    private string GetParentIndentation(SyntaxNodeOrToken nodeOrToken)
    {
        SyntaxNode? parent = nodeOrToken.Parent;

        while (parent is not null)
        {
            if (_indentationCache.TryGetValue(parent, out string? indentation))
            {
                return indentation;
            }

            parent = parent.Parent;
        }

        return _parentIndentation;
    }
}
