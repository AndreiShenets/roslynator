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

        if (nothingButTriviaInFront
            && node.Kind() is not (SyntaxKind.Block or SyntaxKind.ObjectInitializerExpression)
        )
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

        return base.Visit(node);
    }

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
            // Logic of moving close parens, bracket and braces to the next line and align them with the parent start
            string expectedIndentation = parentIndentation;

            if (token.Kind() is SyntaxKind.CloseParenToken or SyntaxKind.CloseBracketToken or SyntaxKind.CloseBraceToken)
            {
                // Comments in front of the closing parent, bracket or brace should be additionally indented,
                // When the closing parent, bracket or brace should be on the parent level
                expectedIndentation = parentIndentation + _singleIndentation;
            }

            string expectedLastIndentation = parentIndentation;
            if (token.Kind()
                is not (
                    SyntaxKind.CloseParenToken
                    or SyntaxKind.CloseBracketToken
                    or SyntaxKind.CloseBraceToken
                    // if OpenParenToken has nothing in front, then it is the weird case of placing the open paren to the new line.
                    // in this case the ArgumentList has been added to the indentation cache,
                    // and as a result, the parent indentation is already increased
                    or SyntaxKind.OpenParenToken
                    // { should be placed on the parent level
                    or SyntaxKind.OpenBraceToken
                )
            )
            {
                // If the token is not a closing parent, bracket or brace, then it should be indented
                expectedLastIndentation += _singleIndentation;
            }

            SyntaxNodeOrToken? newNodeOrToken = ReformatLeadingTrivia(token, expectedIndentation, expectedLastIndentation);
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
                    token = token.WithTrailingTrivia(trailingTrivia.AppendNewLine(_newLine));

                    ChangesApplied = true;
                    // Immediate stop if at least one change was applied
                    if (DoAnalysisOnly)
                    {
                        return token;
                    }
                }
            }
        }

        // Logic to add a new line if the next token is multilined. The logic should be applied after special tokens only
#pragma warning disable RCS0055
        SyntaxTree? syntaxTree = token.SyntaxTree;
        if (syntaxTree is not null)
        {
            SyntaxToken nextToken = token.GetNextToken();
            if (
                CheckOnTheSameLine(syntaxTree, token.Span, nextToken.Span)
                && (
                    // The next token is inside the parent of the token
                    GetNextNode(token, nextToken)?.IsMultiLine() is true
                    // the token is inside a block, and the next token is the block close brace
                    || (
                        nextToken.Parent?.IsMultiLine() is true
                        && nextToken.Parent.ChildNodesAndTokens()
                            .Any(cnt => cnt.IsNode && ReferenceEquals(cnt.AsNode(), token.Parent))
                    )
                )
                && (
                    token.Kind() is SyntaxKind.OpenParenToken or SyntaxKind.OpenBracketToken or SyntaxKind.OpenBraceToken
                    || nextToken.Kind()
                        is SyntaxKind.OpenBracketToken
                        or SyntaxKind.OpenBraceToken
                        or SyntaxKind.CloseBracketToken
                        or SyntaxKind.CloseBraceToken
                )
            )
            {
                token = token.WithTrailingTrivia(token.TrailingTrivia.AppendNewLine(_newLine));

                ChangesApplied = true;
                // Immediate stop if at least one change was applied
                if (DoAnalysisOnly)
                {
                    return token;
                }
            }
        }
#pragma warning restore RCS0055

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
                node.EqualsToken.TrailingTrivia.AppendNewLine(_newLine)
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

    public override SyntaxNode? VisitAssignmentExpression(AssignmentExpressionSyntax node)
    {
        bool equalsTokenAndValueOnDifferentLines = !CheckOnTheSameLine(node.SyntaxTree, node.OperatorToken.Span, node.Right.Span);

        // Either the equals token and value are on the different lines
        if (equalsTokenAndValueOnDifferentLines
            // Or they are on the same line, but the value is single-lined
            || node.Right.IsSingleLine(cancellationToken: _cancellationToken)
        )
        {
            return base.VisitAssignmentExpression(node);
        }

        SyntaxToken newOperatorToken =
            node.OperatorToken.WithTrailingTrivia(
                node.OperatorToken.TrailingTrivia.AppendNewLine(_newLine)
            );
        node = node.WithOperatorToken(newOperatorToken);

        ChangesApplied = true;
        // Immediate stop if at least one change was applied
        if (DoAnalysisOnly)
        {
            return node;
        }

        return base.VisitAssignmentExpression(node);
    }

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
                        lastArgument.GetTrailingTrivia().AppendNewLine(_newLine)
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
                        node.OpenParenToken.TrailingTrivia.AppendNewLine(_newLine)
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

    private SyntaxNodeOrToken? ReformatLeadingTrivia(
        SyntaxNodeOrToken node,
        string expectedIndentation
    )
    {
        return ReformatLeadingTrivia(node, expectedIndentation, expectedIndentation);
    }

    private SyntaxNodeOrToken? ReformatLeadingTrivia(
        SyntaxNodeOrToken node,
        string expectedIndentation,
        string expectedLastIndentation
    )
    {
        if (!CheckNothingButTriviaInFront(node))
        {
            return null;
        }

        SyntaxTriviaList leadingTrivia = node.GetLeadingTrivia();
        if (leadingTrivia.Count == 0)
        {
            if (expectedLastIndentation.Length == 0)
            {
                // No leading trivia and no expected indentation, so nothing to do
                return null;
            }
            return node.WithLeadingTrivia(SyntaxFactory.Whitespace(expectedLastIndentation));
        }

        (bool changesExist, List<SyntaxTrivia> newLeadingTrivia) =
            ReformatTrivia(node, expectedIndentation, expectedLastIndentation, leadingTrivia);

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

            if (expectedLastIndentation.Length > 0)
            {
                newLeadingTrivia.Add(SyntaxFactory.Whitespace(expectedLastIndentation));
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
        string expectedLastIndentation,
        SyntaxTriviaList triviaList
    )
    {
        bool changesExist = false;

        int lastTriviaIndex = triviaList.Count - 1;

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
                string indentation =
                    index == lastTriviaIndex
                        ? expectedLastIndentation
                        : expectedIndentation;

                if (trivia.Span.Length != indentation.Length)
                {
                    changesExist = true;
                    yield return SyntaxFactory.Whitespace(indentation);
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
        if (nodeLinePosition != default && nodeLinePosition.Character == 0)
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

        // If the span starts with 0, then it likely means that the nodeOrToken was already changed before,
        // therefore CheckNothingButWhitespacesInFront will always return true. So we cannot use this check.
        if (nodeOrToken.SpanStart > 0)
        {
            bool nothingButWhitespacesInFront = CheckNothingButWhitespacesInFront(textLines, nodeOrToken.SpanStart);
            if (nothingButWhitespacesInFront)
            {
                return true;
            }
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

    private static SyntaxNode? GetNextNode(SyntaxToken token, SyntaxToken nextToken)
    {
        SyntaxNode? tokenParent = token.Parent;
        if (tokenParent is null)
        {
            return null;
        }

        SyntaxNode? parent = nextToken.Parent;
        while (parent is not null)
        {
            if (CheckNodeInTheParentTree(token, parent))
            {
                return parent;
            }

            parent = parent.Parent;
        }

        return null;

        static bool CheckNodeInTheParentTree(SyntaxToken token, SyntaxNode node)
        {
            SyntaxNode? tokenParent = token.Parent;
            while (tokenParent is not null)
            {
                ChildSyntaxList tokenParentChildren = tokenParent.ChildNodesAndTokens();

                if (ReferenceEquals(node, tokenParent)
                    || tokenParentChildren.Any(tpc => tpc.IsNode && ReferenceEquals(tpc.AsNode(), node))
                )
                {
                    return true;
                }

                tokenParent = tokenParent.Parent;
            }

            return false;
        }
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

    public static SyntaxTriviaList AppendNewLine(this SyntaxTriviaList trailingTrivia, SyntaxTrivia newLine)
    {
        if (trailingTrivia.Any())
        {
            SyntaxTrivia lastSyntaxTrivia = trailingTrivia.Last();

            if (lastSyntaxTrivia.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                // If the last trivia is already an end of line, then we don't need to add a new line
                return trailingTrivia;
            }

            if (lastSyntaxTrivia.IsKind(SyntaxKind.WhitespaceTrivia))
            {
                // If the last trivia is whitespace, then we can replace it with a new line
                return trailingTrivia.Replace(lastSyntaxTrivia, newLine);
            }
        }

        return SyntaxFactory.TriviaList(trailingTrivia.Append(newLine));
    }
}
