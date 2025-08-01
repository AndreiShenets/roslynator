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
        string expectedIndentation = GetParentIndentation(node) + _singleIndentation;

        // Immediately add the current node expected indentation to the cache
        // After reformatting a parent node might be lost
        if (nothingButTriviaInFront)
        {
            _indentationCache[node] = expectedIndentation;
        }

        // Trailing trivia is considered until the end of a line.
        // Whatever is after should become the leading of the next token trivia and be moved to a new line.
        SyntaxNode? newNode = MoveChildrenTrailingCommentsToLeadingComments(node);
        // Are there changes?
        if (newNode is not null)
        {
            node = newNode;
            _indentationCache[node] = expectedIndentation;

            ChangesApplied = true;
            // Immediate stop if at least one change was applied
            if (DoAnalysisOnly)
            {
                return node;
            }
        }

        SyntaxNodeOrToken? newNodeOrToken;

        // Although ReformatLeadingTrivia also checks for nothing but trivia in front, the trivia moving logic above can
        // erase the parent, which might prevent from doing a proper check inside the method.
        if (nothingButTriviaInFront)
        {
            newNodeOrToken = ReformatLeadingTrivia(node, expectedIndentation);
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
        }

        newNodeOrToken = ReformatTrailingTrivia(node, expectedIndentation);
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

        return base.VisitToken(token);
    }

    public override SyntaxNode? VisitEqualsValueClause(EqualsValueClauseSyntax node)
    {
        bool equalsTokenAndValueOnDifferentLines = !CheckOnTheSameLine(node.SyntaxTree, node.EqualsToken, node.Value);

        // Either the equals token and value are on the different lines
        if (equalsTokenAndValueOnDifferentLines
            // Or they are on the same line, but the value is single-lined
            || node.Value.IsSingleLine(cancellationToken: _cancellationToken)
        )
        {
            return base.VisitEqualsValueClause(node);
        }

        // By this time trailing trivia shouldn't have comments. The only possible cases are whitespaces as
        // the check above also guarantees that the value is on the same line that the equals token.
        SyntaxToken newEqualsToken = node.EqualsToken.WithTrailingTrivia(_newLine);
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
        if (CheckNothingButTriviaInFront(node.OpenParenToken))
        {
            // Wrapper node becomes the parent node, although it has the wrapper kind
            _indentationCache[node] = increasedParentIndentation;

            openParenOnTheNewLine = true;

            SyntaxNodeOrToken? changedOpenParenToken = ReformatLeadingTrivia(node.OpenParenToken, increasedParentIndentation);

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

            string doubleIncreasedParentIndentation =
                increasedParentIndentation + _singleIndentation;

            changedOpenParenToken = ReformatTrailingTrivia(node.OpenParenToken, doubleIncreasedParentIndentation);

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

        if (CheckOnTheSameLine(node.SyntaxTree, node.OpenParenToken, node.CloseParenToken))
        {
            return base.VisitArgumentList(node);
        }

        if (CheckNothingButTriviaInFront(node.CloseParenToken))
        {
            string closeParenIndentation = parentIndentation;
            if (openParenOnTheNewLine)
            {
                closeParenIndentation = increasedParentIndentation;
            }

            SyntaxNodeOrToken? changedCloseParenToken = ReformatLeadingTrivia(node.CloseParenToken, closeParenIndentation);

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

            changedCloseParenToken = ReformatTrailingTrivia(node.CloseParenToken, closeParenIndentation);

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
        // The parens are expected to be on the different lines because of the check above
        else
        {
            // The closing paren is expected to be on a new line
            if (node.Arguments.Count > 0)
            {
                ArgumentSyntax lastArgument = node.Arguments.Last();
                ArgumentSyntax newLastArgument = lastArgument.WithTrailingTrivia(_newLine);
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
                SyntaxToken newOpenToken = node.OpenParenToken.WithTrailingTrivia(_newLine);
                node = node.Update(newOpenToken, node.Arguments, node.CloseParenToken);

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

    private readonly struct TriviaMoveResult(SyntaxNodeOrToken newLeftPart, SyntaxNodeOrToken newRightPart)
    {
        public readonly SyntaxNodeOrToken NewLeftPart = newLeftPart;
        public readonly SyntaxNodeOrToken NewRightPart = newRightPart;
    }

    private SyntaxNode? MoveChildrenTrailingCommentsToLeadingComments(SyntaxNode node)
    {
        ChildSyntaxList children = node.ChildNodesAndTokens();
        if (children.All(c => !c.HasTrailingTrivia))
        {
            return null;
        }

        bool hasChanges = false;
        for (int i = 0; i < children.Count - 1; i++)
        {
            SyntaxNodeOrToken child = children[i];
            SyntaxNodeOrToken nextChild = children[i + 1];

            if (nextChild.AsNode()?.IsSingleLine() is true
                && CheckOnTheSameLine(node.SyntaxTree, child, nextChild)
            )
            {
                continue;
            }

            TriviaMoveResult? moveResult = MoveTrailingTriviaToLeadingTrivia(child, nextChild);
            if (moveResult is not null)
            {
                hasChanges = true;

                if (child.IsNode)
                {
                    node =
                        node.ReplaceNode(
                            child.AsNode()!,
                            moveResult.Value.NewLeftPart.AsNode()!
                        );
                }
                else if (child.IsToken)
                {
                    node =
                        node.ReplaceToken(
                            child.AsToken(),
                            moveResult.Value.NewLeftPart.AsToken()
                        );
                }

                // After node replacement all positions are changed and therefore the next child replacement doesn't work.
                // Updating the link to the next child to fix it
                children = node.ChildNodesAndTokens();
                nextChild = children[i + 1];

                if (nextChild.IsNode)
                {
                    node =
                        node.ReplaceNode(
                            nextChild.AsNode()!,
                            moveResult.Value.NewRightPart.AsNode()!
                        );
                }
                else if (nextChild.IsToken)
                {
                    node =
                        node.ReplaceToken(
                            nextChild.AsToken(),
                            moveResult.Value.NewRightPart.AsToken()
                        );
                }

                children = node.ChildNodesAndTokens();

                ChangesApplied = true;
                // Immediate stop if at least one change was applied
                if (DoAnalysisOnly)
                {
                    return node;
                }
            }
        }

        return hasChanges ? node : null;
    }

    private TriviaMoveResult? MoveTrailingTriviaToLeadingTrivia(SyntaxNodeOrToken leftPart, SyntaxNodeOrToken rightPart)
    {
        SyntaxTriviaList trailingTriviaList = leftPart.GetTrailingTrivia();

        if (trailingTriviaList.Count == 0
            || (trailingTriviaList.Count == 1
                && trailingTriviaList[0].Kind() is SyntaxKind.EndOfLineTrivia or SyntaxKind.WhitespaceTrivia
            )
        )
        {
            // If the left part doesn't have trivia or has just a new line or whitespaces, then no changes required
            return null;
        }

        IEnumerable<SyntaxTrivia> trailingTrivia =
            trailingTriviaList.SkipWhile(
                t =>
                    t.Kind()
                        is SyntaxKind.WhitespaceTrivia
                        or SyntaxKind.EndOfLineTrivia
            );

        leftPart = leftPart.WithTrailingTrivia(_newLine);
        rightPart = rightPart.WithLeadingTrivia(trailingTrivia.Concat(rightPart.GetLeadingTrivia()));

        return new TriviaMoveResult(leftPart, rightPart);
    }

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

    private SyntaxNodeOrToken? ReformatTrailingTrivia(
        SyntaxNodeOrToken node,
        string expectedIndentation
    )
    {
        SyntaxTriviaList trailingTrivia = node.GetTrailingTrivia();
        if (trailingTrivia.Count == 0)
        {
            return null;
        }

        // By the time of the calling this method, the MoveChildrenTrailingCommentsToLeadingComments(...) in the Visit method
        // should have already moved all comments to the leading trivia.
        // If there is a comment in the trailing trivia, then it should be the end of an expression comment, for example
        // between expression and semicolon.

        int commentIndex =
            trailingTrivia
                .IndexOf(
                    static t =>
                        t.Kind()
                            is SyntaxKind.SingleLineCommentTrivia
                            or SyntaxKind.MultiLineCommentTrivia
                );

        int endOfLineIndex = trailingTrivia.IndexOf(static t => t.IsKind(SyntaxKind.EndOfLineTrivia));

        // If a comment exists, but no the end of a line trivia exists, then leave it as is - no changes required.
        // There is also the case when there is trivia between nodes or token but no new line, so the right side is on the same line.
        // That means it doesn't matter if there is a comment or not, no changes are required if no end-of-line trivia exists.
        if (endOfLineIndex == -1)
        {
            return null;
        }

        (bool changesExist, List<SyntaxTrivia> newTrailingTrivia) = ReformatTrivia(node, expectedIndentation, trailingTrivia);

        if (!changesExist)
        {
            return null;
        }

        if (newTrailingTrivia.Count == 1
            && newTrailingTrivia[0].IsKind(SyntaxKind.WhitespaceTrivia)
        )
        {
            return node.WithTrailingTrivia(SyntaxTriviaList.Empty);
        }

        if (newTrailingTrivia.Count == 2
            && newTrailingTrivia[0].IsKind(SyntaxKind.WhitespaceTrivia)
            && newTrailingTrivia[1].IsKind(SyntaxKind.EndOfLineTrivia)
        )
        {
            changesExist = true;
            newTrailingTrivia.RemoveAt(0);
        }

        if (changesExist)
        {
            return node.WithTrailingTrivia(newTrailingTrivia);
        }

        return null;
    }

    private (bool ChangesExist, List<SyntaxTrivia> NewTrivia) ReformatTrivia(
        SyntaxNodeOrToken node,
        string expectedIndentation,
        SyntaxTriviaList triviaList
    )
    {
        return ReformatTrivia(node, expectedIndentation, expectedIndentation, expectedIndentation, triviaList);
    }

    private (bool ChangesExist, List<SyntaxTrivia> NewTrivia) ReformatTrivia(
        SyntaxNodeOrToken node,
        string firstItemIndentation,
        string expectedIndentation,
        string lastItemIndentation,
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
            string indentation =
                index switch
                {
                    0 => firstItemIndentation,
                    _ when index == triviaList.Count - 1 => lastItemIndentation,
                    _ => expectedIndentation
                };

            if (trivia.IsKind(SyntaxKind.WhitespaceTrivia))
            {
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
            if (indentation.Length > 0
                && trivia.Kind() is SyntaxKind.SingleLineCommentTrivia or SyntaxKind.MultiLineCommentTrivia
                && (index == 0 || !triviaList[index - 1].IsKind(SyntaxKind.WhitespaceTrivia))
            )
            {
                changesExist = true;
                yield return SyntaxFactory.Whitespace(indentation);
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

                    if (minimumCommentIndentation != indentation.Length)
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
                            splitContent[i] = indentation + restOfTheLine.ToString();
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

    private bool CheckOnTheSameLine(SyntaxTree syntaxTree, SyntaxNodeOrToken left, SyntaxNodeOrToken right)
        => CheckOnTheSameLine(syntaxTree, left.Span.End, right.Span.Start);

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
    Justification = "The class should be here. It is a part of the analysis logic but extensions can be declared only on file level."
)]
file static class SyntaxNodeOrTokenExtensions
{
    /// <summary>
    /// There are some elements that are kind of virtual wrappers over the real nodes and tokens.
    /// They should be excluded from the check as they are on the same line as the real node.
    /// </summary>
    public static bool IsWrapper(this SyntaxNode nodeOrToken) => nodeOrToken.Kind().IsWrapperKind();

    /// <summary>
    /// There are some elements that are kind of virtual wrappers over the real nodes and tokens.
    /// They should be excluded from the check as they are on the same line as the real node.
    /// </summary>
    public static bool IsWrapper(this SyntaxNodeOrToken nodeOrToken) => nodeOrToken.Kind().IsWrapperKind();

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
