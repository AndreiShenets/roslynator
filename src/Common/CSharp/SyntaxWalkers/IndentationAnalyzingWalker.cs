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

    private static readonly char[] SplitChars = ['\r', '\n'];

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
            base.Visit(node);
            return;
        }

        string expectedIndentation;

        // If there is trivia in front of then indentation correction might be required
        bool nothingButTriviaInFront = CheckNothingButTriviaInFront(node);

        if (!node.IsMultiLine() && nothingButTriviaInFront)
        {
            expectedIndentation = GetExpectedIndentation(node);
            CheckIndentation(node, expectedIndentation);

            return;
        }

        if (!CheckApplicableForIndentation(node) && !nothingButTriviaInFront)
        {
            base.Visit(node);
            return;
        }

        // The correction was already applied
        if (_indentationCache.ContainsKey(node))
        {
            base.Visit(node);
            return;
        }

        expectedIndentation = GetExpectedIndentation(node);

        (bool applicable, bool stop) = CheckIndentation(node, expectedIndentation);
        if (applicable && stop)
        {
            return;
        }

        if (applicable)
        {
            _indentationCache[node] = expectedIndentation;
        }

        base.Visit(node);
    }

    /// <summary>
    /// Returns <c>false</c> if node is not applicable for indentation and <c>true</c> otherwise.
    /// </summary>
    private static bool CheckApplicableForIndentation(SyntaxNodeOrToken nodeOrToken)
    {
        SyntaxKind syntaxKind = nodeOrToken.Kind();

        if (
            syntaxKind is SyntaxKind.AnonymousObjectCreationExpression
                or SyntaxKind.ArrayCreationExpression
                or SyntaxKind.ObjectCreationExpression
                or SyntaxKind.ImplicitArrayCreationExpression
                or SyntaxKind.StackAllocArrayCreationExpression
                or SyntaxKind.ImplicitObjectCreationExpression
                or SyntaxKind.ImplicitStackAllocArrayCreationExpression
        )
        {
            return true;
        }

        if (syntaxKind is SyntaxKind.ParenthesizedLambdaExpression
                or SyntaxKind.SimpleLambdaExpression
                or SyntaxKind.InvocationExpression
            && nodeOrToken.Parent?.Kind() is not SyntaxKind.AwaitExpression
        )
        {
            // The case with AwaitExpression should be handled on its level
            return true;
        }

        if (syntaxKind == SyntaxKind.AwaitExpression
            && nodeOrToken.IsNode
            && nodeOrToken.AsNode()!.ChildNodes()
                .Any(
                    c =>
                        c.Kind() is SyntaxKind.ParenthesizedLambdaExpression
                            or SyntaxKind.SimpleLambdaExpression
                            or SyntaxKind.InvocationExpression
                )
        )
        {
            return true;
        }

        if (syntaxKind is SyntaxKind.OpenBraceToken
            or SyntaxKind.CloseBracketToken
            or SyntaxKind.CloseParenToken
            or SyntaxKind.CloseBraceToken
        )
        {
            return true;
        }

        if (syntaxKind is SyntaxKind.WithInitializerExpression or SyntaxKind.WithExpression)
        {
            return true;
        }

        return false;
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

        if (token.Parent != null && _indentationCache.ContainsKey(token.Parent))
        {
            // Token belongs to a node that already has indentation applied
            LinePosition parentLinePosition = _textLines.GetLinePosition(token.Parent.SpanStart);
            LinePosition tokenLinePosition = _textLines.GetLinePosition(token.SpanStart);
            // But only if they are on the same line.
            if (parentLinePosition.Line == tokenLinePosition.Line)
            {
                base.VisitToken(token);
                return;
            }
        }

        if (CheckNothingButTriviaInFront(token))
        {
            string expectedIndentation = GetExpectedIndentation(token);
            (bool applicable, bool stop) = CheckIndentation(token, expectedIndentation);
            if (applicable && stop)
            {
                return;
            }
        }

        base.VisitToken(token);
    }

    private string GetExpectedIndentation(SyntaxNodeOrToken nodeOrToken)
    {
        string expectedIndentation = _expectedIndentation;
        SyntaxNode? parent = nodeOrToken.Parent;
        while (parent is not null)
        {
            if (_indentationCache.TryGetValue(parent, out string? indentation))
            {
                // Blocks of different kinds that are part of their parents should have indentation of the parent node
                // RCS0055: The formatting of the boolean expression is correct and readable
#pragma warning disable RCS0055
                if (
                    (
                        nodeOrToken.IsNode
                        && nodeOrToken.AsNode() is SyntaxNode syntaxNode
                        && (
                            syntaxNode is BlockSyntax
                                or InitializerExpressionSyntax
                                or WithExpressionSyntax
                            || syntaxNode.Parent is WithExpressionSyntax
                        )
                    )
                    || nodeOrToken is { IsToken: true, Parent: InitializerExpressionSyntax }
                )
                {
                    expectedIndentation = indentation;
                    break;
                }
#pragma warning restore RCS0055

                if (nodeOrToken.IsToken
                    && nodeOrToken.AsToken().Kind()
                        is SyntaxKind.CloseParenToken
                        or SyntaxKind.OpenBraceToken // Part of a block
                        or SyntaxKind.CloseBraceToken // Part of a block
                        or SyntaxKind.CloseBracketToken
                )
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

    private (bool Applicable, bool Stop) CheckIndentation(
        SyntaxNodeOrToken nodeOrToken,
        string expectedIndentation
    )
    {
        SyntaxTriviaList leadingTrivia = nodeOrToken.GetLeadingTrivia();

        LinePosition nodeLinePosition = _textLines.GetLinePosition(nodeOrToken.SpanStart);
        bool itemHasTheFirstCharOnLine = nodeLinePosition.Character == 0;
        bool leadingTriviaExists = leadingTrivia.Any();
        SyntaxTrivia newLine = SyntaxTriviaAnalysis.DetermineEndOfLine(nodeOrToken);

        bool stop;

        if (!leadingTriviaExists && !itemHasTheFirstCharOnLine)
        {
            // It might the case when there is a multi-line comment bound as a trailing trivia to the previous node
            if (!CheckNothingButMultilineCommentFromParentTrailingTrivia(nodeOrToken, out SyntaxTrivia trivia))
            {
                if ((nodeOrToken.IsNode && nodeOrToken.AsNode()!.IsMultiLine() && CheckApplicableForIndentation(nodeOrToken))
                    || (nodeOrToken.IsToken
                        && nodeOrToken.Kind()
                            is SyntaxKind.CloseParenToken
                            or SyntaxKind.OpenBraceToken
                            or SyntaxKind.CloseBraceToken
                            or SyntaxKind.CloseBracketToken
                        && !CheckNothingButWhitespacesInFront(nodeOrToken.SpanStart)
                    )
                )
                {
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

            // This trivia should be formatted as leading trivia for the current node
            string commentIndentation = GetTriviaIndentation(nodeOrToken, expectedIndentation, TriviaType.Leading);
            if (CheckTriviaIndentation(commentIndentation, trivia.GetContainingList(), newLine))
            {
                return (Applicable: true, Stop: true);
            }

            stop =
                HandleChange(
                    new TextSpan(nodeOrToken.SpanStart, 0),
                    newLine + expectedIndentation
                );

            // No trivia to do correction, so we are done here
            return (Applicable: true, Stop: stop);
        }

        if (leadingTriviaExists)
        {
            string commentIndentation = GetTriviaIndentation(nodeOrToken, expectedIndentation, TriviaType.Leading);
            if (CheckTriviaIndentation(commentIndentation, leadingTrivia, newLine))
            {
                return (Applicable: true, Stop: true);
            }

            bool nothingButWhitespaceTriviaInFront = CheckNothingButWhitespacesInFront(nodeOrToken.SpanStart);
            if (nothingButWhitespaceTriviaInFront)
            {
                if (nodeLinePosition.Character != expectedIndentation.Length)
                {
                    int lineStart = nodeOrToken.SpanStart - nodeLinePosition.Character;
                    stop =
                        HandleChange(
                            new TextSpan(lineStart, nodeLinePosition.Character),
                            expectedIndentation
                        );
                    if (stop)
                    {
                        return (Applicable: true, Stop: true);
                    }
                }
            }
            else if (CheckApplicableForIndentation(nodeOrToken))
            {
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
        }
        else
        {
            // No trivia, but it should be there to indent the node
            if (nodeLinePosition.Character != expectedIndentation.Length)
            {
                stop =
                    HandleChange(
                        new TextSpan(nodeOrToken.Span.Start, 0),
                        nodeLinePosition.Character == 0
                            ? expectedIndentation
                            : newLine + expectedIndentation
                    );
                if (stop)
                {
                    return (Applicable: true, Stop: true);
                }
            }
        }

        SyntaxTriviaList trailingTrivia = nodeOrToken.GetTrailingTrivia();
        if (trailingTrivia.Any())
        {
            string commentIndentation = GetTriviaIndentation(nodeOrToken, expectedIndentation, TriviaType.Trailing);
            if (CheckTriviaIndentation(commentIndentation, trailingTrivia, newLine))
            {
                return (Applicable: true, Stop: true);
            }
        }

        return (Applicable: true, Stop: false);
    }

    /// <summary>
    /// Returns if processing should be stopped
    /// </summary>
    private bool CheckTriviaIndentation(
        string expectedIndentation,
        SyntaxTriviaList leadingTrivia,
        SyntaxTrivia newLine)
    {
        // The structure of trivial with comment might be one of or combination of:
        // <?end of line trivia><indentation><comment trivia><end of line trivia>
        // <indentation><token>
        // <?end of line trivia><indentation><multi
        //      line
        //      comment
        //      trivia><end of line trivia>
        // <indentation><token>

        // the logic of check:
        // for each comment or multi-line comment check
        //      1. The first trivia in front is whitespace trivia of indentation length
        //      2. For multi-line comment trivia check that the indentation of the content is correct

        bool stop;

        // When the check goes through, for each not first comment trivia we need to limit the check to the previous trivia end
        for (int triviaIndex = 0; triviaIndex < leadingTrivia.Count; triviaIndex++)
        {
            SyntaxTrivia trivia = leadingTrivia[triviaIndex];
            if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
                || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
            )
            {
                bool triviaInFrontIsValid =
                    CheckWhiteSpaceTriviaInFrontMultilineComment(
                        leadingTrivia,
                        triviaIndex,
                        expectedIndentation,
                        newLine,
                        out TextChange textChange
                    );

                if (!triviaInFrontIsValid)
                {
                    stop = HandleChange(textChange);
                    if (stop)
                    {
                        return true;
                    }
                }

                if (trivia.IsKind(SyntaxKind.MultiLineCommentTrivia))
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

                            stop =
                                HandleChange(
                                    trivia.Span,
                                    string.Join(newLine.ToString(), splitContent)
                                );
                            if (stop)
                            {
                                return true;
                            }
                        }

                        // if (!CheckEndOfLineAfterExists(leadingTrivia, triviaIndex, newLine, expectedIndentation, out textChange))
                        // {
                        //     stop = HandleChange(textChange);
                        //     if (stop)
                        //     {
                        //         return true;
                        //     }
                        // }
                    }
                }
            }
        }

        return false;
    }

    private enum TriviaType
    {
        Leading,
        Trailing
    }

    private string GetTriviaIndentation(SyntaxNodeOrToken nodeOrToken, string expectedIndentation, TriviaType triviaType)
    {
        string commentIndentation = expectedIndentation;

        if (nodeOrToken.IsToken)
        {
            // There are special cases.
            // As trivia is bound to the beginning of tokens and nodes, the end trivia inside a block or argument list
            // or similar is bound to the closing bracket or paren.
            // Comment in this case should have indentation of not a bracket but of the content, so to have +1 indentation
            SyntaxToken syntaxToken = nodeOrToken.AsToken();
            switch (nodeOrToken.Parent)
            {
                case BlockSyntax block:
                {
                    if (triviaType == TriviaType.Leading && block.CloseBraceToken == syntaxToken)
                    {
                        commentIndentation += _singleIndentation;
                    }
                    if (triviaType == TriviaType.Trailing && block.OpenBraceToken == syntaxToken)
                    {
                        commentIndentation += _singleIndentation;
                    }

                    break;
                }
                case InitializerExpressionSyntax initializerExpressionSyntax:
                {
                    if (triviaType == TriviaType.Leading && initializerExpressionSyntax.CloseBraceToken == syntaxToken)
                    {
                        commentIndentation += _singleIndentation;
                    }
                    if (triviaType == TriviaType.Trailing && initializerExpressionSyntax.OpenBraceToken == syntaxToken)
                    {
                        commentIndentation += _singleIndentation;
                    }

                    break;
                }
                case ArgumentListSyntax argumentListSyntax:
                {
                    if (triviaType == TriviaType.Leading && argumentListSyntax.CloseParenToken == syntaxToken)
                    {
                        commentIndentation += _singleIndentation;
                    }
                    if (triviaType == TriviaType.Trailing && argumentListSyntax.OpenParenToken == syntaxToken)
                    {
                        commentIndentation += _singleIndentation;
                    }

                    break;
                }
            }
        }

        return commentIndentation;
    }

    private bool CheckNothingButTriviaInFront(SyntaxNodeOrToken nodeOrToken)
    {
        LinePosition nodeLinePosition = _textLines.GetLinePosition(nodeOrToken.SpanStart);
        if (nodeLinePosition.Character == 0)
        {
            return true;
        }

        if (CheckParentOnTheSameLine(nodeOrToken))
        {
            return false;
        }

        bool nothingButWhitespacesInFront = CheckNothingButWhitespacesInFront(nodeOrToken.SpanStart);
        if (nothingButWhitespacesInFront)
        {
            return true;
        }

        if (CheckNothingButMultilineCommentFromParentTrailingTrivia(nodeOrToken, out _))
        {
            return true;
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

    private bool CheckNothingButMultilineCommentFromParentTrailingTrivia(
        SyntaxNodeOrToken nodeOrToken,
        out SyntaxTrivia parentTrivia
    )
    {
        parentTrivia = default;

        if (CheckParentOnTheSameLine(nodeOrToken))
        {
            return false;
        }

        LinePosition nodeLinePosition = _textLines.GetLinePosition(nodeOrToken.SpanStart);

        TextSpan span = new(nodeOrToken.SpanStart - nodeLinePosition.Character, nodeLinePosition.Character);
        IEnumerable<SyntaxTrivia> descendantTrivia = _syntaxTree.GetRoot().DescendantTrivia(span);

        parentTrivia =
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

    private bool CheckParentOnTheSameLine(SyntaxNodeOrToken nodeOrToken)
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

    private bool CheckWhiteSpaceTriviaInFrontMultilineComment(
        SyntaxTriviaList leadingTrivia,
        int triviaIndex,
        string expectedIndentation,
        SyntaxTrivia newLine,
        out TextChange textChange
    )
    {
        textChange = default;

        int previousTriviaIndex = triviaIndex - 1;
        if (previousTriviaIndex < 0)
        {
            bool nothingButWhitespaceTriviaInFront = CheckNothingButWhitespacesInFront(leadingTrivia.Span.Start);

            textChange =
                new TextChange(
                    new TextSpan(leadingTrivia.Span.Start, 0),
                    nothingButWhitespaceTriviaInFront
                        ? expectedIndentation
                        : newLine + expectedIndentation
                );
            return false;
        }

        SyntaxTrivia previousTrivia = leadingTrivia[previousTriviaIndex];
        if (!previousTrivia.IsKind(SyntaxKind.WhitespaceTrivia))
        {
            textChange =
                new TextChange(
                    new TextSpan(leadingTrivia.Span.Start, 0),
                    previousTrivia.IsKind(SyntaxKind.EndOfLineTrivia)
                        ? expectedIndentation
                        : newLine + expectedIndentation
                );
            return false;
        }

        if (previousTrivia.Span.Length != expectedIndentation.Length)
        {
            textChange = new TextChange(previousTrivia.Span, expectedIndentation);
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
        if (RequiredChanges.Any(c => c.Span == textChange.Span))
        {
            // Because algo is kind of recursive and the special case with parent trailing trivia is checked,
            // it might happen that when we process nodes, some trivial is processed twice, which produces duplicate changes.
            // I assume that only this part of logic produces duplicates, so it is safe to just check for them.
            // If the assumption is not correct, then there will be problems in tests or in formatting,
            // which should be addressed later.
            return false;
        }

        RequiredChanges.Add(textChange);
        return _handler(this, textChange);
    }
}
