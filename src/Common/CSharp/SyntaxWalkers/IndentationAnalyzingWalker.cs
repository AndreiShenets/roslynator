#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Roslynator.CSharp.SyntaxWalkers;

/// <summary>
/// Bypass the whole child nodes, if a child node has leading trivia that starts at the beginning of a line,
/// then the walker checks if it has expected indentation.
/// </summary>
public sealed class IndentationAnalyzingWalker : CSharpSyntaxWalker
{
    /// <summary>
    /// Delegate that is called when a required change in indentation is detected.
    /// If the delegate returns <c>true</c>, then the walking process should be stopped.
    /// </summary>
    public delegate bool RequiredIndentationChangeHandler(
        IndentationAnalyzingWalker walker,
        TextChange textChange
    );

    private readonly SyntaxTree _syntaxTree;
    private readonly string _expectedIndentation;
    private readonly string _singleIndentation;
    private readonly TextLineCollection _textLines;
    private readonly RequiredIndentationChangeHandler _handler;

    private readonly Dictionary<SyntaxNode, string> _indentationCache = [];

    public List<TextChange> RequiredChanges { get; } = [];

    public IndentationAnalyzingWalker(
        SyntaxTree syntaxTree,
        string expectedIndentation,
        string singleIndentation,
        TextLineCollection textLines,
        RequiredIndentationChangeHandler handler
    )
        : base(SyntaxWalkerDepth.Token)
    {
        _syntaxTree = syntaxTree;
        _expectedIndentation = expectedIndentation;
        _singleIndentation = singleIndentation;
        _textLines = textLines;
        _handler = handler;
    }

    public override void Visit(SyntaxNode? node)
    {
        // Argument syntax is kind of a virtual wrapper over the real argument.
        // Only the real argument should be checked for indentation.
        if (node is null or ArgumentSyntax)
        {
            return;
        }

        string expectedIndentation = GetExpectedIndentation(node);

        (bool applicable, bool stop) = CheckIndentation(node, expectedIndentation);
        if (applicable && stop)
        {
            return;
        }

        if (applicable && !_indentationCache.ContainsKey(node))
        {
            _indentationCache[node] = expectedIndentation;
        }

        base.Visit(node);
    }

    private string GetExpectedIndentation(SyntaxNodeOrToken node)
    {
        string expectedIndentation = _expectedIndentation;
        SyntaxNode? parent = node.Parent;
        while (parent is not null)
        {
            if (_indentationCache.TryGetValue(parent, out string? indentation))
            {
                // Blocks and argument lists should have indentation of the parent node
                if (node.IsNode && node.AsNode() is BlockSyntax or ArgumentListSyntax)
                {
                    expectedIndentation = indentation;
                    break;
                }

                expectedIndentation = indentation + _singleIndentation;
                break;
            }

            parent = parent.Parent;
        }

        return expectedIndentation;
    }

    public override void VisitToken(SyntaxToken token)
    {
        switch (token.Kind())
        {
            case SyntaxKind.OpenParenToken or SyntaxKind.OpenBraceToken or SyntaxKind.OpenBracketToken:
                if (token.Parent?.IsMultiLine() is true)
                {
                    string expectedIndentation = GetExpectedIndentation(token);
                    (bool applicable, bool stop) = CheckIndentation(token, expectedIndentation);
                    if (applicable && stop)
                    {
                        return;
                    }
                }
                break;
            case SyntaxKind.CloseParenToken or SyntaxKind.CloseBraceToken or SyntaxKind.CloseBracketToken:
                if (token.Parent?.IsMultiLine() is true)
                {
                    string expectedIndentation = GetExpectedIndentation(token);
                    (bool applicable, bool stop) = CheckIndentation(token, expectedIndentation);
                    if (applicable && stop)
                    {
                        return;
                    }
                }
                break;
        }

        base.VisitToken(token);
    }

    private (bool Applicable, bool Stop) CheckIndentation(
        SyntaxNodeOrToken nodeOrToken,
        string expectedIndentation
    )
    {
        SyntaxTriviaList leadingTrivia = nodeOrToken.GetLeadingTrivia();

        LinePosition nodeLinePosition = _textLines.GetLinePosition(nodeOrToken.SpanStart);
        bool itemHasTheFirstCharOnLine = nodeLinePosition.Character == 0;
        bool nothingInFrontButTrivia = CheckNothingButTriviaInFront(nodeOrToken);

        bool triviaExists = leadingTrivia.Any();

        bool stop;

        if (!triviaExists && !itemHasTheFirstCharOnLine)
        {
            TextSpan span = new (nodeOrToken.SpanStart - nodeLinePosition.Character, nodeLinePosition.Character);
            IEnumerable<SyntaxTrivia> descendantTrivia = _syntaxTree.GetRoot().DescendantTrivia(span);

            SyntaxTrivia trivia =
                descendantTrivia.FirstOrDefault(
                    trivia =>
                    {
                        LinePosition linePositionStart = _textLines.GetLinePosition(trivia.SpanStart);
                        LinePosition linePositionEnd = _textLines.GetLinePosition(trivia.Span.End);
                        return linePositionStart.Line != nodeLinePosition.Line
                            && linePositionEnd.Line == nodeLinePosition.Line;
                    }
                );

            if (trivia != default && nothingInFrontButTrivia)
            {
                SyntaxTrivia newLine = SyntaxTriviaAnalysis.DetermineEndOfLine(nodeOrToken);
                stop =
                    HandleChange(
                        new TextSpan(nodeOrToken.SpanStart, 0),
                        newLine + expectedIndentation
                    );
                if (stop)
                {
                    return (Applicable: true, Stop: true);
                }
            }

            return (Applicable: false, Stop: false);
        }

        LinePosition triviaLinePosition = _textLines.GetLinePosition(leadingTrivia.Span.Start);

        // If trivia doesn't start from the first character of the line, then it is not a valid case. The trivia is probably
        // somewhere between tokens / nodes
        if (triviaLinePosition.Character > 0)
        {
            return (Applicable: false, Stop: false);
        }

        int triviaLength = leadingTrivia.Span.Length;

        // Incorrect zero-length trivia when indent expected
        if (triviaLength == 0)
        {
            // Actually, all is fine. No indentation is expected
            if (expectedIndentation.Length == 0)
            {
                return (Applicable: true, Stop: false);
            }

            // If trivia is empty, we can add expected indentation.
            // In this case the first token of the node is the first character on the line
            // and should be used as an indentation placement marker
            stop =
                HandleChange(
                    new TextSpan(nodeOrToken.SpanStart, 0),
                    expectedIndentation
                );

            return (Applicable: true, stop);
        }

        bool triviaContainsComments =
            leadingTrivia.Any(
                trivia =>
                    trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
                    || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
            );

        bool triviaContainsEndOfLine =
            leadingTrivia.Any(trivia => trivia.IsKind(SyntaxKind.EndOfLineTrivia));

        // If trivia already has expected indentation length then return true
        if (!triviaContainsComments && !triviaContainsEndOfLine && triviaLength == expectedIndentation.Length)
        {
            return (Applicable: true, Stop: false);
        }

        // If trivia is shorter or longer than the expected indentation, then we can set it to required indentation.
        // But if it contains comments or new lines, then we have to separate trivia from the code and indent both of them
        if (triviaContainsComments)
        {
            string commentIndentation = expectedIndentation;

            if (nodeOrToken.IsToken
                && nodeOrToken.Parent is BlockSyntax block
                && block.CloseBraceToken == nodeOrToken.AsToken()
            )
            {
                // This is a special case.
                // As trivia is bound to the beginning of tokens and nodes, the end trivia inside a block is bound to the closing bracket.
                // Comment in this case should have indentation of not a bracket but of the content, so to have +1 indentation
                commentIndentation += _singleIndentation;
            }

            // The structure of trivial with comment should be:
            // <?end of line trivia><indentation><comment trivia><end of line trivia>
            // <indentation><token>
            // <?end of line trivia><indentation><multi
            //      line
            //      comment
            //      trivia><end of line trivia>
            // <indentation><token>
            // there can be multiple comment trivia or multi-line comment trivia, up to one new line can be in the middle of the multi-line

            // the logic of check:
            // for each comment or multi-line comment check
            //      1. The first trivia in front is whitespace trivia of indentation length
            //      2. For multi-line comment trivia check that the indentation of the content is correct

            // When the check goes through, for each not first comment trivia we need to limit the check to the previous trivia end
            for (int triviaIndex = 0; triviaIndex < leadingTrivia.Count; triviaIndex++)
            {
                SyntaxTrivia trivia = leadingTrivia[triviaIndex];
                if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
                    || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
                )
                {
                    if (!CheckWhiteSpaceTriviaInFrontIsCorrect(leadingTrivia, triviaIndex, commentIndentation, out TextChange textChange))
                    {
                        stop = HandleChange(textChange);
                        if (stop)
                        {
                            return (Applicable: true, Stop: true);
                        }
                    }

                    if (trivia.IsKind(SyntaxKind.MultiLineCommentTrivia))
                    {
                        SyntaxTrivia newLine = SyntaxTriviaAnalysis.DetermineEndOfLine(trivia.Token);

                        LinePosition linePosition = _textLines.GetLinePosition(trivia.SpanStart);
                        SourceText? text = _textLines[linePosition.Line].Text;
                        if (text is null)
                        {
                            // I believe that it shouldn't happen. If happens, then just continue for safety
                            continue;
                        }

                        bool hasNotIndentationTriviaInFront = false;
                        for (int i = linePosition.Character - 1; i >= 0; i++)
                        {
                            if (!char.IsWhiteSpace(text[i]) || text[i] != '\t')
                            {
                                hasNotIndentationTriviaInFront = true;
                                break;
                            }
                        }

                        // For multi-line comment trivia we need to check that the indentation of the content is correct
                        // The content of the multi-line comment trivia is the text between the start and end of the trivia
                        string[] splitContent =
                            trivia.ToFullString()
                                .Split(WalkerConstants.SplitChars, StringSplitOptions.RemoveEmptyEntries);

                        int startIndex = 0;
                        if (hasNotIndentationTriviaInFront)
                        {
                            // If there is no trivia in front, then we can start from the first line
                            startIndex = 1;
                        }

                        int minimumCommentIndentation = GetMinimumCommentIndentation(splitContent, startIndex);

                        if (minimumCommentIndentation != commentIndentation.Length)
                        {
                            // the 0 index is already indented correctly with the code above
                            for (int i = 1; i < splitContent.Length; i++)
                            {
                                string line = splitContent[i];
                                if (line.Length == 0)
                                {
                                    continue;
                                }

                                int currentIndentationLength = GetIndentationLength(line);
                                int additionalIndentation = currentIndentationLength - minimumCommentIndentation;
                                int endSliceLength = line.Length - additionalIndentation;
                                ReadOnlySpan<char> restOfTheLine = line.AsSpan().Slice(line.Length - endSliceLength, endSliceLength);
                                splitContent[i] = commentIndentation + restOfTheLine.ToString();
                            }

                            stop =
                                HandleChange(
                                    trivia.Span,
                                    string.Join(newLine.ToString(), splitContent)
                                );
                            if (stop)
                            {
                                return (Applicable: true, Stop: true);
                            }
                        }

                        if (!CheckEndOfLineAfterExists(leadingTrivia, triviaIndex, newLine, expectedIndentation, out textChange))
                        {
                            stop = HandleChange(textChange);
                            if (stop)
                            {
                                return (Applicable: true, Stop: true);
                            }
                        }
                    }
                }
            }
        }

        return (Applicable: true, Stop: false);
    }

    private bool CheckNothingButTriviaInFront(SyntaxNodeOrToken nodeOrToken)
    {
        LinePosition nodeLinePosition = _textLines.GetLinePosition(nodeOrToken.SpanStart);

        SyntaxNode? parent = nodeOrToken.Parent;
        while (parent is not null)
        {
            if (parent is ArgumentSyntax)
            {
                // Argument syntax is kind of a virtual wrapper over the real argument.
                // Only the real argument should be checked for indentation.
                parent = parent.Parent;
                continue;
            }

            LinePosition linePosition = _textLines.GetLinePosition(parent.SpanStart);
            if (linePosition.Line == nodeLinePosition.Line)
            {
                return false;
            }

            parent = parent.Parent;
        }

        return true;
    }

    private static bool CheckEndOfLineAfterExists(
        SyntaxTriviaList leadingTrivia,
        int triviaIndex,
        SyntaxTrivia newLine,
        string expectedIndentation,
        out TextChange textChange
    )
    {
        textChange = default;

        SyntaxTrivia lastTrivia = leadingTrivia[triviaIndex];
        int lastTriviaIndex = triviaIndex;

        for (int i = triviaIndex + 1; i < leadingTrivia.Count; i++)
        {
            lastTrivia = leadingTrivia[i];
            lastTriviaIndex = i;

            if (lastTrivia.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                return true;
            }

            if (!lastTrivia.IsKind(SyntaxKind.WhitespaceTrivia))
            {
                break;
            }
        }

        textChange =
            new TextChange(
                new TextSpan(lastTrivia.Span.End, 0),
                lastTriviaIndex == leadingTrivia.Count - 1
                    ? newLine + expectedIndentation // indentation is required to format next token
                    : newLine.ToString()
            );

        return false;
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

    private static bool CheckWhiteSpaceTriviaInFrontIsCorrect(
        SyntaxTriviaList leadingTrivia,
        int triviaIndex,
        string expectedIndentation,
        out TextChange textChange
    )
    {
        textChange = default;

        int previousTriviaIndex = triviaIndex - 1;
        if (previousTriviaIndex < 0)
        {
            textChange =
                new TextChange(
                    new TextSpan(leadingTrivia.Span.Start, 0),
                    expectedIndentation
                );
            return false;
        }

        SyntaxTrivia previousTrivia = leadingTrivia[previousTriviaIndex];
        if (!previousTrivia.IsKind(SyntaxKind.WhitespaceTrivia))
        {
            textChange =
                new TextChange(
                    new TextSpan(leadingTrivia.Span.Start, 0),
                    expectedIndentation
                );
            return false;
        }

        if (previousTrivia.Span.Length != expectedIndentation.Length)
        {
            textChange =
                new TextChange(
                    previousTrivia.Span,
                    expectedIndentation
                );
            return false;
        }

        return true;
    }

    /// <summary>
    /// Returns <c>true</c> if the walker should stop
    /// </summary>
    private bool HandleChange(
        TextSpan span,
        string replacement
    )
    {
        TextChange textChange = new(span, replacement);
        return HandleChange(textChange);
    }

    /// <summary>
    /// Returns <c>true</c> if the walker should stop
    /// </summary>
    private bool HandleChange(TextChange textChange)
    {
        RequiredChanges.Add(textChange);
        return _handler(this, textChange);
    }
}

file static class WalkerConstants
{
    public static readonly char[] SplitChars = ['\r', '\n'];
}
