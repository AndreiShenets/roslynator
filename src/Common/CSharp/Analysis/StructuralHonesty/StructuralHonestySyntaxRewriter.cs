#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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

    private readonly string _parentIndentation;
    private readonly string _singleIndentation;
    private readonly SyntaxTrivia _newLine;
    private readonly CancellationToken _cancellationToken;

    private readonly Dictionary<SyntaxNode, string> _indentationCache = [];

    public bool DoAnalysisOnly { get; set; }
    public bool ChangesApplied { get; set; }

    public StructuralHonestySyntaxRewriter(
        string parentIndentation,
        string singleIndentation,
        SyntaxTrivia newLine,
        CancellationToken cancellationToken
    )
    {
        _parentIndentation = parentIndentation;
        _singleIndentation = singleIndentation;
        _newLine = newLine;
        _cancellationToken = cancellationToken;
    }

    public override SyntaxNode? Visit(SyntaxNode? node)
    {
        if (node is null)
        {
            return null;
        }

        if (ChangesApplied && DoAnalysisOnly)
        {
            // If at least one change was applied, then we should stop the analysis
            // and return the node as is.
            return node;
        }

        // There are some elements that are kind of virtual wrappers over the real nodes and tokens.
        // They should be excluded from the check as they are on the same line as the real node.
        if (node.IsWrapper())
        {
            return base.Visit(node);
        }

        // There are two actions that make code structurally honest: inserting new line and indenting.
        // The inserting new line should be done in the Type specific methods like VisitEqualsValueClause, VisitArgumentList, etc.
        // as it is much easier to do it there.
        // The indentation should be done in the Visit and VisitToken methods because even if the node or token doesn't require
        // a new line somewhere, its content or surrounding trivia may require indentation.

        // We shouldn't add nodes to the indentation cache if it is not the first node in the line.
        // Otherwise, each deeper level will have an additional unexpected indentation.
        // We still have to try to format trivia as the single line breakdown to multiple lines
        // and comment reformatting are still possible.

        bool nothingButTriviaInFront = CheckNothingButTriviaInFront(node);
        string expectedIndentation = GetParentIndentation(node);

        if (nothingButTriviaInFront)
        {
            expectedIndentation += _singleIndentation;
        }

        // Immediately add the current node expected indentation to the cache
        // After reformatting a parent node might be lost
        _indentationCache[node] = expectedIndentation;

        SyntaxNodeOrToken? newNodeOrToken = ReformatLeadingTrivia(node, expectedIndentation);
        // Are there changes?
        if (newNodeOrToken is not null)
        {
            node = newNodeOrToken.Value.AsNode()!;
            _indentationCache[node] = expectedIndentation;

            ChangesApplied = true;
            // Immediate stop if at least one change was applied
            if (DoAnalysisOnly)
            {
                return node;
            }
        }

        // SyntaxNode? newNode = EnsureMultilineChildrenSeparatedWithNewLine(node);
        // if (newNode is not null)
        // {
        //     node = newNode;
        //     _indentationCache[node] = expectedIndentation;
        //
        //     ChangesApplied = true;
        //     // Immediate stop if at least one change was applied
        //     if (DoAnalysisOnly)
        //     {
        //         return node;
        //     }
        // }

        return base.Visit(node);
    }

    // private SyntaxNode? EnsureMultilineChildrenSeparatedWithNewLine(SyntaxNode node)
    // {
    //     ChildSyntaxList children = node.ChildNodesAndTokens();
    //     if (children.Count < 2)
    //     {
    //         // Nothing to change
    //         return null;
    //     }
    //
    //     bool changesExist = false;
    //
    //     for (int i = 0; i < children.Count - 1; i++)
    //     {
    //         SyntaxNodeOrToken child = children[i];
    //
    //         SyntaxTriviaList trailingTrivia = child.GetTrailingTrivia();
    //         if (!trailingTrivia.Any())
    //         {
    //             // No trailing trivia, so nothing to do
    //             continue;
    //         }
    //
    //         SyntaxNodeOrToken nextChild = children[i + 1];
    //
    //         if (CheckOnTheSameLine(node.SyntaxTree, trailingTrivia.Span, nextChild.Span))
    //         {
    //             if (child.IsNode)
    //             {
    //                 SyntaxNode childAsNode = child.AsNode()!;
    //
    //                 node =
    //                     node.ReplaceNode(
    //                         childAsNode,
    //                         childAsNode.WithTrailingTrivia(
    //                             child.GetTrailingTrivia().Append(_newLine)
    //                         )
    //                     );
    //             }
    //             else
    //             {
    //                 SyntaxToken childAsToken = child.AsToken();
    //
    //                 node =
    //                     node.ReplaceToken(
    //                         childAsToken,
    //                         childAsToken.WithTrailingTrivia(
    //                             child.GetTrailingTrivia().Append(_newLine)
    //                         )
    //                     );
    //             }
    //
    //             changesExist = true;
    //             // Immediate stop if at least one change was applied
    //             if (DoAnalysisOnly)
    //             {
    //                 return node;
    //             }
    //         }
    //     }
    //
    //     if (changesExist)
    //     {
    //         return node;
    //     }
    //
    //     return null;
    // }

    public override SyntaxToken VisitToken(SyntaxToken token)
    {
        if (ChangesApplied && DoAnalysisOnly)
        {
            // If at least one change was applied, then we should stop the analysis
            // and return the token as is.
            return token;
        }

        bool nothingButTriviaInFront = CheckNothingButTriviaInFront(token);

        string parentIndentation = GetParentIndentation(token);

        if (nothingButTriviaInFront)
        {
            SyntaxNodeOrToken? newNodeOrToken = ReformatLeadingTrivia(token, parentIndentation);
            // Are there changes?
            if (newNodeOrToken is not null)
            {
                token = newNodeOrToken.Value.AsToken();

                ChangesApplied = true;
                // Immediate stop if at least one change was applied
                if (DoAnalysisOnly)
                {
                    return token;
                }
            }
        }

        // Logic to move tokens to the next line if multiline trailing trivia of a previous token is in front
        SyntaxTree? tokenSyntaxTree = token.SyntaxTree;
        if (tokenSyntaxTree is not null)
        {
            SyntaxTriviaList trailingTrivia = token.TrailingTrivia;
            if (trailingTrivia.Any() && !trailingTrivia.Last().IsKind(SyntaxKind.EndOfLineTrivia))
            {
                TextSpan tokenFullSpan = trailingTrivia.FullSpan;

                SyntaxToken nextToken = token.GetNextToken();

                if (tokenFullSpan.IsMultiLine(tokenSyntaxTree, _cancellationToken)
                    && CheckOnTheSameLine(tokenSyntaxTree, tokenFullSpan, nextToken.Span)
                )
                {
                    token = token.WithTrailingTrivia(trailingTrivia.Append(_newLine));

                    ChangesApplied = true;
                    // Immediate stop if at least one change was applied
                    if (DoAnalysisOnly)
                    {
                        return token;
                    }
                }
            }
        }

        return base.VisitToken(token);
    }

    public override SyntaxNode? VisitEqualsValueClause(EqualsValueClauseSyntax node)
    {
        bool equalsTokenAndValueOnDifferentLines = !CheckOnTheSameLine(node.SyntaxTree, node.EqualsToken.Span, node.Value.Span);

        // Either the equals token and value are on the different lines
        if (equalsTokenAndValueOnDifferentLines
            // Or they are on the same line, but the value is single-lined
            || node.Value.IsSingleLine(cancellationToken: _cancellationToken)
        )
        {
            return base.VisitEqualsValueClause(node);
        }

        SyntaxToken newEqualsToken =
            node.EqualsToken.WithTrailingTrivia(
                node.EqualsToken.TrailingTrivia.Append(_newLine)
            );
        node = node.WithEqualsToken(newEqualsToken);

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
        // Indentation reference must be adjusted
        if (CheckNothingButTriviaInFront(node.OpenParenToken))
        {
            openParenOnTheNewLine = true;
            // Wrapper node becomes the parent node, although it has the wrapper kind
            _indentationCache[node] = increasedParentIndentation;
        }

        if (CheckOnTheSameLine(node.SyntaxTree, node.OpenParenToken.Span, node.CloseParenToken.Span))
        {
            return base.VisitArgumentList(node);
        }

        // Parents are on different lines, but the closing paren is not on its own line, so move it to the next line
        if (!CheckNothingButTriviaInFront(node.CloseParenToken))
        {
            if (node.Arguments.Count > 0)
            {
                ArgumentSyntax lastArgument = node.Arguments.Last();

                ArgumentSyntax newLastArgument =
                    lastArgument.WithTrailingTrivia(
                        lastArgument.GetTrailingTrivia().Append(_newLine)
                    );

                node =
                    node.Update(
                        node.OpenParenToken,
                        node.Arguments.Replace(lastArgument, newLastArgument),
                        node.CloseParenToken
                    );

                ChangesApplied = true;
                // Immediate stop if at least one change was applied
                if (DoAnalysisOnly)
                {
                    return node;
                }
            }
            else
            {
                // If parens are on the different lines but there are no arguments,
                // then nothing should be in front apart from multiline comments attached to the open paren
                SyntaxToken newOpenToken =
                    node.OpenParenToken.WithTrailingTrivia(
                        node.OpenParenToken.TrailingTrivia.Append(_newLine)
                    );
                node = node.Update(newOpenToken, node.Arguments, node.CloseParenToken);

                ChangesApplied = true;
                // Immediate stop if at least one change was applied
                if (DoAnalysisOnly)
                {
                    return node;
                }
            }

            // Updating indentation reference again as node has been changed
            if (openParenOnTheNewLine)
            {
                _indentationCache[node] = increasedParentIndentation;
            }
        }

        return base.VisitArgumentList(node);
    }

    // public override SyntaxNode? VisitArgument(ArgumentSyntax node)
    // {
    //     if (node.Parent is ArgumentListSyntax argumentListSyntax)
    //     {
    //         LinePosition openParenLinePosition =
    //             _textLines.GetLinePosition(argumentListSyntax.OpenParenToken.SpanStart);
    //         LinePosition closeParenLinePosition =
    //             _textLines.GetLinePosition(argumentListSyntax.CloseParenToken.SpanStart);
    //
    //         if (openParenLinePosition.Line != closeParenLinePosition.Line // Multi-lined
    //             && argumentListSyntax.Arguments.Last() == node
    //             && !node.GetTrailingTrivia().LastOrDefault().IsKind(SyntaxKind.EndOfLineTrivia))
    //         {
    //             SyntaxTrivia newLine =(node);
    //             // Moving closing paren to the next line
    //             node = node.AppendToTrailingTrivia(newLine);
    //
    //             ChangesApplied = true;
    //             // Immediate stop if at least one change was applied
    //             if (DoAnalysisOnly)
    //             {
    //                 return node;
    //             }
    //         }
    //     }
    //
    //     return base.VisitArgument(node);
    // }

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
            if (expectedIndentation.Length == 0)
            {
                // No leading trivia and no expected indentation, so nothing to do
                return null;
            }
            return node.WithLeadingTrivia(SyntaxFactory.Whitespace(expectedIndentation));
        }

        (bool changesExist, List<SyntaxTrivia> newLeadingTrivia) =
            ReformatTrivia(node, expectedIndentation, leadingTrivia);

        if (!changesExist)
        {
            return null;
        }

        SyntaxTrivia lastTrivia = newLeadingTrivia.Last();

        // It is expected to have the end of line + indent at the end
        if (lastTrivia.IsKind(SyntaxKind.WhitespaceTrivia))
        {
            int preLastTriviaIndex = newLeadingTrivia.Count - 2;
            if (preLastTriviaIndex >= 0
                && !newLeadingTrivia[preLastTriviaIndex].IsKind(SyntaxKind.EndOfLineTrivia)
            )
            {
                newLeadingTrivia.Insert(newLeadingTrivia.Count - 1, _newLine);
            }
        }
        else
        {
            if (!lastTrivia.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                newLeadingTrivia.Add(_newLine);
            }

            if (expectedIndentation.Length > 0)
            {
                newLeadingTrivia.Add(SyntaxFactory.Whitespace(expectedIndentation));
            }
        }

        if (changesExist)
        {
            return node.WithLeadingTrivia(newLeadingTrivia);
        }

        return null;
    }

    private (bool ChangesExist, List<SyntaxTrivia> NewTrivia) ReformatTrivia(
        SyntaxNodeOrToken node,
        string expectedIndentation,
        SyntaxTriviaList triviaList
    )
    {
        bool changesExist = false;

        List<SyntaxTrivia> newTrivia =
            triviaList
                .SelectMany((trivia, index) => CorrectTrivia(trivia, index))
                .ToList();

        return (changesExist, newTrivia);

        IEnumerable<SyntaxTrivia> CorrectTrivia(SyntaxTrivia trivia, int index)
        {
            if (trivia.IsKind(SyntaxKind.WhitespaceTrivia)
                && (index == 0
                    || triviaList[index - 1].IsKind(SyntaxKind.EndOfLineTrivia)
                )
            )
            {
                if (trivia.Span.Length != expectedIndentation.Length)
                {
                    changesExist = true;
                    yield return SyntaxFactory.Whitespace(expectedIndentation);
                    yield break;
                }

                yield return trivia;
                yield break;
            }

            // Check if the trivia comments are properly indented
            if (expectedIndentation.Length > 0
                && trivia.Kind() is SyntaxKind.SingleLineCommentTrivia or SyntaxKind.MultiLineCommentTrivia
                && (index == 0 || !triviaList[index - 1].IsKind(SyntaxKind.WhitespaceTrivia))
            )
            {
                if (index > 0
                    && !triviaList[index - 1].IsKind(SyntaxKind.EndOfLineTrivia)
                )
                {
                    yield return _newLine;
                }

                changesExist = true;
                yield return SyntaxFactory.Whitespace(expectedIndentation);
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

                            int endSliceLength = line.Length - minimumCommentIndentation;
                            ReadOnlySpan<char> restOfTheLine =
                                line.AsSpan().Slice(line.Length - endSliceLength, endSliceLength);
                            splitContent[i] = expectedIndentation + restOfTheLine.ToString();
                        }

                        changesExist = true;
                        yield return SyntaxFactory.Comment(string.Join(_newLine.ToString(), splitContent));
                        yield break;
                    }
                }
            }

            yield return trivia;
        }
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
        SyntaxTree? syntaxTree = nodeOrToken.SyntaxTree;
        if (syntaxTree is null)
        {
            // Cannot analyze the node without a syntax tree.
            // The case can happen when the parent node was reformatted
            return false;
        }

        TextLineCollection textLines = syntaxTree.GetText(_cancellationToken).Lines;

        if (CheckParentOnTheSameLine(textLines, nodeOrToken))
        {
            return false;
        }

        LinePosition nodeLinePosition = textLines.GetLinePosition(nodeOrToken.SpanStart);
        if (nodeLinePosition.Character == 0)
        {
            return true;
        }

        SyntaxTriviaList leadingTrivia = nodeOrToken.GetLeadingTrivia();
        LinePosition trivialLinePosition = textLines.GetLinePosition(leadingTrivia.Span.Start);
        // Trivia strats at the beginning of the line
        if (trivialLinePosition != default && trivialLinePosition.Character == 0)
        {
            return true;
        }

        bool nothingButWhitespacesInFront = CheckNothingButWhitespacesInFront(textLines, nodeOrToken.SpanStart);
        if (nothingButWhitespacesInFront)
        {
            return true;
        }

        if (nodeOrToken.IsToken)
        {
            SyntaxToken previousToken = nodeOrToken.AsToken().GetPreviousToken();
            if (!previousToken.IsKind(SyntaxKind.None)
                && CheckOnTheSameLine(syntaxTree, previousToken.Span, nodeOrToken.Span)
            )
            {
                return false;
            }
        }

        if (CheckNothingButMultilineCommentFromParentTrailingTrivia(syntaxTree, textLines, nodeOrToken))
        {
            return true;
        }

        return false;
    }

    private static bool CheckParentOnTheSameLine(TextLineCollection textLines, SyntaxNodeOrToken nodeOrToken)
    {
        LinePosition nodeLinePosition = textLines.GetLinePosition(nodeOrToken.SpanStart);

        SyntaxNode? parent = nodeOrToken.Parent;
        while (parent is not null)
        {
            // There are some classes that are kind of virtual wrappers over the real nodes and tokens.
            // They should be excluded from the check as they are on the same line as the real node.
            if (parent.Kind() is SyntaxKind.Argument or SyntaxKind.ArgumentList)
            {
                parent = parent.Parent;
                continue;
            }

            LinePosition linePosition = textLines.GetLinePosition(parent.SpanStart);
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

    private static bool CheckNothingButWhitespacesInFront(TextLineCollection textLines, int spanStart)
    {
        TextLine lineText = textLines.GetLineFromPosition(spanStart);

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

    private static bool CheckNothingButMultilineCommentFromParentTrailingTrivia(
        SyntaxTree syntaxTree,
        TextLineCollection textLines,
        SyntaxNodeOrToken nodeOrToken
    )
    {
        if (CheckParentOnTheSameLine(textLines, nodeOrToken))
        {
            return false;
        }

        LinePosition nodeLinePosition = textLines.GetLinePosition(nodeOrToken.SpanStart);

        TextSpan span = new(nodeOrToken.SpanStart - nodeLinePosition.Character, nodeLinePosition.Character);
        IEnumerable<SyntaxTrivia> descendantTrivia = syntaxTree.GetRoot().DescendantTrivia(span);

        SyntaxTrivia parentTrivia =
            descendantTrivia.FirstOrDefault(
                trivia =>
                {
                    LinePosition linePositionStart = textLines.GetLinePosition(trivia.SpanStart);
                    LinePosition linePositionEnd = textLines.GetLinePosition(trivia.Span.End);
                    return linePositionStart.Line != nodeLinePosition.Line
                        && linePositionEnd.Line == nodeLinePosition.Line;
                }
            );

        return parentTrivia != default;
    }

    private bool CheckOnTheSameLine(SyntaxTree syntaxTree, TextSpan left, TextSpan right)
        => CheckOnTheSameLine(syntaxTree, left.End, right.Start);

    private bool CheckOnTheSameLine(SyntaxTree syntaxTree, int leftPosition, int rightPosition)
    {
        TextLineCollection textLines = syntaxTree.GetText(_cancellationToken).Lines;

        LinePosition openParenLinePosition =
            textLines.GetLinePosition(leftPosition);
        LinePosition closeParenLinePosition =
            textLines.GetLinePosition(rightPosition);

        return openParenLinePosition.Line == closeParenLinePosition.Line;
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

/// <summary>
/// There are some elements that are kind of virtual wrappers over the real nodes and tokens.
/// They should be excluded from the check as they are on the same line as the real node.
/// </summary>
[SuppressMessage(
    "Style",
    "RCS1060",
    Justification =
        "The class should be here. It is a part of the analysis logic but extensions can be declared only on file level."
)]
file static class SyntaxNodeOrTokenExtensions
{
    /// <summary>
    /// There are some elements that are kind of virtual wrappers over the real nodes and tokens.
    /// They should be excluded from the check as they are on the same line as the real node.
    /// </summary>
    public static bool IsWrapper(this SyntaxNode nodeOrToken) => nodeOrToken.Kind().IsWrapperKind();

    [SuppressMessage(
        "Style",
        "RCS1016",
        Justification = "It's handier to have this as it is"
    )]
    private static bool IsWrapperKind(this SyntaxKind kind)
        => kind
            is SyntaxKind.Argument
            or SyntaxKind.ArgumentList;
}
