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
    private readonly SyntaxNode _root;
    private readonly string _rootNodeIndentation;
    private readonly string _singleIndentation;
    private readonly SyntaxTrivia _newLine;
    private readonly CancellationToken _cancellationToken;

    private string[]? _splitParameter;

    private readonly Dictionary<SyntaxNode, string> _indentationCache = [];

    public bool DoAnalysisOnly { get; set; }
    public bool ChangesApplied { get; set; }

    public StructuralHonestySyntaxRewriter(
        SyntaxNode root,
        string rootNodeIndentation,
        string singleIndentation,
        SyntaxTrivia newLine,
        CancellationToken cancellationToken
    )
    {
        _root = root;
        _rootNodeIndentation = rootNodeIndentation;
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

        // It is not expected to visit trivia within this rewriter, so I can just return the token
        return token;
    }

    public override SyntaxNode? VisitEqualsValueClause(EqualsValueClauseSyntax node)
    {
        SyntaxTree syntaxTree = node.SyntaxTree;

        // The middle is multi-lined in the case of the trailing multi-line comments
        TextSpan trimmedTokenFullSpan = node.EqualsToken.GetTrimmedFullSpan();
        bool singleLineInMiddle = trimmedTokenFullSpan.IsSingleLine(syntaxTree, _cancellationToken);
        bool multilineInMiddle = !singleLineInMiddle;

        bool singleLineOnRight = node.IsSingleLine(cancellationToken: _cancellationToken);
        bool multilineOnRight = !singleLineOnRight;

        bool middleAndRightOnSameLine = CheckOnTheSameLine(syntaxTree, trimmedTokenFullSpan, node.Value.FullSpan);

        string expectedIndentation = GetExpectedIndentation(node);
        string valueExpectedIndentation = GetExpectedIndentation(node.Value);

        bool nothingInFrontOfMiddle = CheckNothingButTriviaInFront(node.EqualsToken);
        if (nothingInFrontOfMiddle)
        {
            SyntaxNodeOrToken? newEqualsToken = ReformatLeadingTrivia(node.EqualsToken, expectedIndentation);
            if (newEqualsToken is not null)
            {
                node = node.WithEqualsToken(newEqualsToken.Value.AsToken());
                ChangesApplied = true;
            }
            valueExpectedIndentation += _singleIndentation;
        }

        if (middleAndRightOnSameLine && (multilineInMiddle || multilineOnRight))
        {
            node =
                node.WithEqualsToken(
                    node.EqualsToken.WithTrailingTrivia(
                        node.EqualsToken.TrailingTrivia.AppendNewLine(_newLine)
                    )
                );
            ChangesApplied = true;
            middleAndRightOnSameLine = false;

            _indentationCache[node.Value] = valueExpectedIndentation;
        }

        if (!middleAndRightOnSameLine)
        {
            // Moved the right side above or was already on the next line, check that indentation is correct
            SyntaxNodeOrToken? newValue = ReformatLeadingTrivia(node.Value, valueExpectedIndentation);
            if (newValue is not null)
            {
                node = node.WithValue((ExpressionSyntax)newValue.Value.AsNode()!);
                _indentationCache[node.Value] = valueExpectedIndentation;
                ChangesApplied = true;
            }
        }

        return base.VisitEqualsValueClause(node);
    }

    public override SyntaxNode? VisitAssignmentExpression(AssignmentExpressionSyntax node)
    {
        SyntaxTree syntaxTree = node.SyntaxTree;

        // The middle is multi-lined in the case of the trailing multi-line comments
        TextSpan trimmedOperatorTokenFullSpan = node.OperatorToken.GetTrimmedFullSpan();
        bool singleLineInMiddle = trimmedOperatorTokenFullSpan.IsSingleLine(syntaxTree, _cancellationToken);
        bool multilineInMiddle = !singleLineInMiddle;

        bool singleLineOnRight = node.Right.IsSingleLine(cancellationToken: _cancellationToken);
        bool multilineOnRight = !singleLineOnRight;

        string expectedIndentationLeft = GetExpectedIndentation(node);
        string expectedIndentationMiddle = expectedIndentationLeft;
        string expectedIndentationRight = expectedIndentationLeft;

        _indentationCache[node] = expectedIndentationLeft;
        _indentationCache[node.Left] = expectedIndentationLeft;

        bool middleAndRightOnSameLine = CheckOnTheSameLine(syntaxTree, trimmedOperatorTokenFullSpan, node.Right.FullSpan);

        bool nothingInFrontOfLeft = CheckNothingButTriviaInFront(node.Left);
        bool nothingInFrontOfRight;

        if (nothingInFrontOfLeft)
        {
            SyntaxNodeOrToken? newLeft = ReformatLeadingTrivia(node.Left, expectedIndentationLeft);
            if (newLeft is not null)
            {
                node = node.WithLeft((ExpressionSyntax)newLeft.Value.AsNode()!);
                _indentationCache[node] = expectedIndentationLeft;
                _indentationCache[node.Left] = expectedIndentationLeft;
                ChangesApplied = true;
            }
        }

        bool nothingInFrontOfMiddle = CheckNothingButTriviaInFront(node.OperatorToken);

        if (nothingInFrontOfMiddle)
        {
            expectedIndentationMiddle += _singleIndentation;
            expectedIndentationRight = expectedIndentationMiddle;

            SyntaxNodeOrToken? newMiddle = ReformatLeadingTrivia(node.OperatorToken, expectedIndentationMiddle);
            if (newMiddle is not null)
            {
                node = node.WithOperatorToken(newMiddle.Value.AsToken());
                _indentationCache[node] = expectedIndentationLeft;
                _indentationCache[node.Left] = expectedIndentationLeft;
                ChangesApplied = true;
            }
        }

        if (!middleAndRightOnSameLine)
        {
            expectedIndentationRight += _singleIndentation;
        }

        _indentationCache[node.Right] = expectedIndentationRight;

        if (middleAndRightOnSameLine && (multilineInMiddle || multilineOnRight))
        {
            node =
                node.WithOperatorToken(
                    node.OperatorToken.WithTrailingTrivia(
                        node.OperatorToken.TrailingTrivia.AppendNewLine(_newLine)
                    )
                );
            _indentationCache[node] = expectedIndentationLeft;
            _indentationCache[node.Left] = expectedIndentationLeft;

            ChangesApplied = true;

            nothingInFrontOfRight = true;
        }
        else
        {
            nothingInFrontOfRight = CheckNothingButTriviaInFront(node.Right);
        }

        if (nothingInFrontOfRight)
        {
            SyntaxNodeOrToken? newRight = ReformatLeadingTrivia(node.Right, expectedIndentationRight);
            if (newRight is not null)
            {
                node = node.WithRight((ExpressionSyntax)newRight.Value.AsNode()!);
                _indentationCache[node] = expectedIndentationLeft;
                _indentationCache[node.Left] = expectedIndentationLeft;
                _indentationCache[node.Right] = expectedIndentationRight;
                ChangesApplied = true;
            }
        }

        return base.VisitAssignmentExpression(node);
    }

    public override SyntaxNode? VisitBinaryExpression(BinaryExpressionSyntax node)
    {
        SyntaxTree syntaxTree = node.SyntaxTree;

        bool singleLineOnLeft = node.Left.IsSingleLine(cancellationToken: _cancellationToken);
        bool multilineOnLeft = !singleLineOnLeft;

        // The middle is multi-lined in the case of the trailing multi-line comments
        TextSpan trimmedOperatorTokenFullSpan = node.OperatorToken.GetTrimmedFullSpan();
        bool singleLineInMiddle = trimmedOperatorTokenFullSpan.IsSingleLine(syntaxTree, _cancellationToken);
        bool multilineInMiddle = !singleLineInMiddle;

        bool singleLineOnRight = node.Right.IsSingleLine(cancellationToken: _cancellationToken);
        bool multilineOnRight = !singleLineOnRight;

        string expectedIndentation = GetExpectedIndentation(node);

        _indentationCache[node] = expectedIndentation;
        _indentationCache[node.Left] = expectedIndentation;

        bool leftAndMiddleOnSameLine = CheckOnTheSameLine(syntaxTree, node.Left.GetTrimmedFullSpan(), node.OperatorToken.FullSpan);
        bool middleAndRightOnSameLine = CheckOnTheSameLine(syntaxTree, trimmedOperatorTokenFullSpan, node.Right.FullSpan);

        bool nothingInFrontOfLeft = CheckNothingButTriviaInFront(node.Left);

        if (nothingInFrontOfLeft)
        {
            SyntaxNodeOrToken? newLeft = ReformatLeadingTrivia(node.Left, expectedIndentation);
            if (newLeft is not null)
            {
                node = node.WithLeft((ExpressionSyntax)newLeft.Value.AsNode()!);
                _indentationCache[node] = expectedIndentation;
                _indentationCache[node.Left] = expectedIndentation;
                ChangesApplied = true;
            }
        }

        bool nothingInFrontOfMiddle;

        if (leftAndMiddleOnSameLine && (multilineOnLeft || multilineInMiddle || multilineOnRight))
        {
            node =
                node.WithLeft(
                    node.Left.WithTrailingTrivia(
                        node.Left.GetTrailingTrivia().AppendNewLine(_newLine)
                    )
                );
            _indentationCache[node] = expectedIndentation;
            _indentationCache[node.Left] = expectedIndentation;

            ChangesApplied = true;

            nothingInFrontOfMiddle = true;
        }
        else
        {
            nothingInFrontOfMiddle = CheckNothingButTriviaInFront(node.OperatorToken);
        }

        if (nothingInFrontOfMiddle)
        {
            SyntaxNodeOrToken? newMiddle = ReformatLeadingTrivia(node.OperatorToken, expectedIndentation);
            if (newMiddle is not null)
            {
                node = node.WithOperatorToken(newMiddle.Value.AsToken());
                _indentationCache[node] = expectedIndentation;
                _indentationCache[node.Left] = expectedIndentation;
                ChangesApplied = true;
            }
        }

        _indentationCache[node.Right] = expectedIndentation;

        if (!middleAndRightOnSameLine && (multilineOnLeft || multilineInMiddle || multilineOnRight))
        {
            IEnumerable<SyntaxTrivia> leftTrailingTrivia =
                node.GetTrailingTrivia()
                    .Concat(node.OperatorToken.TrailingTrivia)
                    .Concat(node.Right.GetLeadingTrivia());

            SyntaxTriviaList newLeftTrailingTrivia =
                SyntaxFactory.TriviaList(leftTrailingTrivia)
                    .AppendNewLine(_newLine);

            node =
                node.Update(
                    node.Left.WithTrailingTrivia(newLeftTrailingTrivia),
                    node.OperatorToken.WithTrailingTrivia(SyntaxFactory.Whitespace(" ")),
                    node.Right.WithoutLeadingTrivia()
                );
            _indentationCache[node] = expectedIndentation;
            _indentationCache[node.Left] = expectedIndentation;
            _indentationCache[node.Right] = expectedIndentation;

            ChangesApplied = true;
        }

        return base.VisitBinaryExpression(node);
    }

    public override SyntaxNode? VisitParenthesizedExpression(ParenthesizedExpressionSyntax node)
    {
        string expectedIndentation = GetSelfIndentation(node) ?? GetParentIndentation(node) ?? _rootNodeIndentation;
        _indentationCache[node] = expectedIndentation;

        if (CheckNothingButTriviaInFront(node.OpenParenToken))
        {
            if (!ReferenceEquals(_root, node))
            {
                expectedIndentation += _singleIndentation;
            }

            _indentationCache[node] = expectedIndentation;

            SyntaxNodeOrToken? newNode = ReformatLeadingTrivia(node.OpenParenToken, expectedIndentation);
            if (newNode is not null)
            {
                node = node.WithOpenParenToken(newNode.Value.AsToken());
                _indentationCache[node] = expectedIndentation;
                ChangesApplied = true;
            }
        }

        if (CheckOnTheSameLine(node.SyntaxTree, node.OpenParenToken.GetTrimmedFullSpan(), node.CloseParenToken.FullSpan))
        {
            return base.VisitParenthesizedExpression(node);
        }

        // Parents are on different lines, but the closing paren is not on its own line, so move it to the next line
        if (!CheckNothingButTriviaInFront(node.CloseParenToken))
        {
            node =
                node.WithExpression(
                    node.Expression.WithTrailingTrivia(
                        node.Expression.GetTrailingTrivia().AppendNewLine(_newLine)
                    )
                );

            SyntaxNodeOrToken? newNode =
                ReformatLeadingTrivia(node.CloseParenToken, expectedIndentation + _singleIndentation, expectedIndentation);
            if (newNode is not null)
            {
                node = node.WithCloseParenToken(newNode.Value.AsToken());
            }

            _indentationCache[node] = expectedIndentation;

            ChangesApplied = true;
        }

        return base.VisitParenthesizedExpression(node);
    }

    public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        if (node.Parent is MemberAccessExpressionSyntax)
        {
            // Such invocation expressions are indented in the parent
            return base.VisitInvocationExpression(node);
        }

        bool nothingInFrontOfNode = CheckNothingButTriviaInFront(node);

        string expectedIndentation = GetExpectedIndentation(node);

        if (nothingInFrontOfNode)
        {
            _indentationCache[node] = expectedIndentation;
            _indentationCache[node.ArgumentList] = expectedIndentation;

            SyntaxNodeOrToken? newNode = ReformatLeadingTrivia(node, expectedIndentation);
            if (newNode is not null)
            {
                node = (InvocationExpressionSyntax)newNode.Value.AsNode()!;
                _indentationCache[node] = expectedIndentation;
                _indentationCache[node.ArgumentList] = expectedIndentation;
                ChangesApplied = true;
            }
        }

        return base.VisitInvocationExpression(node);
    }

    public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        SyntaxNode? fullRightPart = node.Parent;
        if (fullRightPart is null)
        {
            return base.VisitMemberAccessExpression(node);
        }

        SyntaxTree syntaxTree = node.SyntaxTree;

        bool singleLineOnLeft =
            node.Expression
                .GetBodyAndTrimmedTrailingTriviaSpan()
                .IsSingleLine(syntaxTree, _cancellationToken);
        bool multilineOnLeft = !singleLineOnLeft;

        // The middle is multi-lined in the case of the trailing multi-line comments
        TextSpan trimmedOperatorTokenFullSpan = node.OperatorToken.GetTrimmedFullSpan();
        bool singleLineInMiddle = trimmedOperatorTokenFullSpan.IsSingleLine(syntaxTree, _cancellationToken);
        bool multilineInMiddle = !singleLineInMiddle;

        bool leftAndMiddleOnSameLine = CheckOnTheSameLine(syntaxTree, node.Expression.GetTrimmedFullSpan(), node.OperatorToken.FullSpan);
        bool middleAndRightOnSameLine = CheckOnTheSameLine(syntaxTree, trimmedOperatorTokenFullSpan, node.Name.FullSpan);

        (bool multilinePartsBefore, int dotsBefore, int dotsBeforeOnNewLine, int dotsAfter) = AnalyzeChain(node);

        string? expectedIndentationLeft = GetSelfIndentation(node);
        string expectedIndentationMiddle =
            GetMemberAccessExpressionDotExpectedIndentation(node, nothingInFront: !leftAndMiddleOnSameLine);

        _indentationCache[node] = expectedIndentationMiddle;
        if (expectedIndentationLeft is not null)
        {
            _indentationCache[node.Expression] = expectedIndentationLeft;
        }

        bool singleLineOnRight = true;
        if (fullRightPart is InvocationExpressionSyntax invocationExpressionSyntax)
        {
            TextSpan nameFullSpan = node.Name.FullSpan;
            TextSpan rightPartEndTokenSpan = invocationExpressionSyntax.ArgumentList.GetLastToken().GetTrimmedFullSpan();
            singleLineOnRight = CheckOnTheSameLine(syntaxTree, nameFullSpan.Start, rightPartEndTokenSpan.End);

            if (!leftAndMiddleOnSameLine)
            {
                _indentationCache[invocationExpressionSyntax.ArgumentList] = expectedIndentationMiddle;
            }
        }
        bool multilineOnRight = !singleLineOnRight;

        bool nothingInFrontOfMiddle;

        // Because of the structure of the syntax tree, the left put is indented in another place.
        // Here only logic of moving the dot to a new line, dot indentation and returning the right part
        // after the dot should be implemented.

        if (leftAndMiddleOnSameLine
            && (multilineOnLeft
                || multilineInMiddle
                || multilineOnRight
                || dotsBeforeOnNewLine > 0
            )
            // This magic number is a preference of complexity or number of dots before the member access expression to trigger the next line
            && (multilinePartsBefore || dotsBefore > 1 || dotsBeforeOnNewLine > 0)
        )
        {
            node =
                node.WithExpression(
                    node.Expression.WithTrailingTrivia(
                        node.Expression.GetTrailingTrivia().AppendNewLine(_newLine)
                    )
                );
            _indentationCache[node] = expectedIndentationMiddle;
            if (expectedIndentationLeft is not null)
            {
                _indentationCache[node.Expression] = expectedIndentationLeft;
            }

            ChangesApplied = true;

            nothingInFrontOfMiddle = true;
        }
        else
        {
            nothingInFrontOfMiddle = CheckNothingButTriviaInFront(node.OperatorToken);
        }

        if (nothingInFrontOfMiddle)
        {
            SyntaxNodeOrToken? newMiddle = ReformatLeadingTrivia(node.OperatorToken, expectedIndentationMiddle);
            if (newMiddle is not null)
            {
                node = node.WithOperatorToken(newMiddle.Value.AsToken());
                _indentationCache[node] = expectedIndentationMiddle;
                if (expectedIndentationLeft is not null)
                {
                    _indentationCache[node.Expression] = expectedIndentationLeft;
                }
                ChangesApplied = true;
            }
        }

        if (!middleAndRightOnSameLine && (multilineOnLeft || multilineInMiddle || multilineOnRight))
        {
            IEnumerable<SyntaxTrivia> leftTrailingTrivia =
                node.GetTrailingTrivia()
                    .Concat(node.OperatorToken.TrailingTrivia)
                    .Concat(node.Name.GetLeadingTrivia());

            SyntaxTriviaList newLeftTrailingTrivia =
                SyntaxFactory.TriviaList(leftTrailingTrivia)
                    .AppendNewLine(_newLine);

            node =
                node.Update(
                    node.Expression.WithTrailingTrivia(newLeftTrailingTrivia),
                    node.OperatorToken.WithoutTrailingTrivia(),
                    node.Name.WithoutLeadingTrivia()
                );
            _indentationCache[node] = expectedIndentationMiddle;
            if (expectedIndentationLeft is not null)
            {
                _indentationCache[node.Expression] = expectedIndentationLeft;
            }
            _indentationCache[fullRightPart] = expectedIndentationMiddle;

            ChangesApplied = true;
        }

        return base.VisitMemberAccessExpression(node);
    }

    private (bool MultilinePartsBefore, int DotsBefore, int DotsBeforeOnNewLine, int DotsAfter)
        AnalyzeChain(MemberAccessExpressionSyntax node)
    {
        bool multilinePartsBefore = false;
        int dotsBefore = 0;
        int dotsBeforeOnNewLine = 0;
        int dotsAfter = 0;

        SyntaxTree syntaxTree = node.SyntaxTree;

        SyntaxNode? parent = node.Expression;
        while (parent != null)
        {
            switch (parent)
            {
                case MemberAccessExpressionSyntax memberAccessExpressionSyntax:
                    ++dotsBefore;
                    bool leftAndMiddleOnSameLine =
                        CheckOnTheSameLine(syntaxTree, node.Expression.GetTrimmedFullSpan(), node.OperatorToken.FullSpan);
                    if (!leftAndMiddleOnSameLine)
                    {
                        ++dotsBeforeOnNewLine;
                    }

                    SyntaxNode? accessExpressionParent = memberAccessExpressionSyntax.Parent;
                    if (!multilinePartsBefore
                        && accessExpressionParent is InvocationExpressionSyntax parentAsInvocationExpressionSyntax
                        && CheckInvocationExpressionSyntaxMultiline(parentAsInvocationExpressionSyntax)
                    )
                    {
                        multilinePartsBefore = true;
                    }

                    parent = memberAccessExpressionSyntax.Expression;

                    break;
                case InvocationExpressionSyntax invocationExpressionSyntax:
                    parent = invocationExpressionSyntax.Expression;
                    break;
                default:
                    parent = null;
                    break;
            }
        }

        parent = node.Parent;
        while (parent != null)
        {
            switch (parent)
            {
                case MemberAccessExpressionSyntax memberAccessExpressionSyntax:
                    ++dotsAfter;
                    parent = memberAccessExpressionSyntax.Parent;
                    break;
                case InvocationExpressionSyntax invocationExpressionSyntax:
                    parent = invocationExpressionSyntax.Parent;
                    break;
                default:
                    parent = null;
                    break;
            }
        }

        return (multilinePartsBefore, dotsBefore, dotsBeforeOnNewLine, dotsAfter);
    }

    private bool CheckInvocationExpressionSyntaxMultiline(InvocationExpressionSyntax invocationExpressionSyntax)
    {
        TextSpan nameFullSpan = invocationExpressionSyntax.GetFirstToken().GetBodyAndTrimmedTrailingTriviaSpan();
        TextSpan rightPartEndTokenSpan = invocationExpressionSyntax.ArgumentList.GetLastToken().GetTrimmedFullSpan();
        bool singleLine = CheckOnTheSameLine(invocationExpressionSyntax.SyntaxTree, nameFullSpan.Start, rightPartEndTokenSpan.End);
        return !singleLine;
    }

    private string GetMemberAccessExpressionDotExpectedIndentation(
        MemberAccessExpressionSyntax node,
        bool nothingInFront
    )
    {
        if (ReferenceEquals(_root, node))
        {
            if (nothingInFront)
            {
                return _rootNodeIndentation + _singleIndentation;
            }

            return _rootNodeIndentation;
        }

        MemberAccessExpressionSyntax? topLevelMemberAccessExpression = null;
        InvocationExpressionSyntax? topInvocationExpression = null;
        SyntaxNode? parent = node.Parent;
        while (parent != null)
        {
            switch (parent)
            {
                case MemberAccessExpressionSyntax memberAccessExpressionSyntax:
                    topLevelMemberAccessExpression = memberAccessExpressionSyntax;
                    topInvocationExpression = null;
                    parent = memberAccessExpressionSyntax.Parent;
                    break;
                case InvocationExpressionSyntax invocationExpressionSyntax:
                    topInvocationExpression = invocationExpressionSyntax;
                    topLevelMemberAccessExpression = null;
                    parent = invocationExpressionSyntax.Parent;
                    break;
                default:
                    parent = null;
                    break;
            }
        }

        if (topInvocationExpression is not null)
        {
            string? selfIndentation = GetSelfIndentation(topInvocationExpression);
            if (selfIndentation is not null)
            {
                return selfIndentation + _singleIndentation;
            }
        }

        if (topLevelMemberAccessExpression is not null)
        {
            string? selfIndentation = GetSelfIndentation(topLevelMemberAccessExpression);
            if (selfIndentation is not null)
            {
                return selfIndentation;
            }
        }

        return GetExpectedIndentation(node);
    }

    public override SyntaxNode? VisitArgumentList(ArgumentListSyntax node)
    {
        string expectedIndentation = GetSelfIndentation(node) ?? GetParentIndentation(node) ?? _rootNodeIndentation;
        _indentationCache[node] = expectedIndentation;

        if (CheckNothingButTriviaInFront(node.OpenParenToken))
        {
            if (!ReferenceEquals(_root, node))
            {
                expectedIndentation += _singleIndentation;
            }

            _indentationCache[node] = expectedIndentation;

            SyntaxNodeOrToken? newNode = ReformatLeadingTrivia(node.OpenParenToken, expectedIndentation);
            if (newNode is not null)
            {
                node = node.WithOpenParenToken(newNode.Value.AsToken());
                _indentationCache[node] = expectedIndentation;
                ChangesApplied = true;
            }
        }

        TextSpan trimmedOpenParentTokenFullSpan = node.OpenParenToken.GetTrimmedFullSpan();
        if (CheckOnTheSameLine(node.SyntaxTree, trimmedOpenParentTokenFullSpan, node.CloseParenToken.FullSpan))
        {
            return base.VisitArgumentList(node);
        }

        if (node.Arguments.Any())
        {
            if (CheckOnTheSameLine(node.SyntaxTree, trimmedOpenParentTokenFullSpan, node.Arguments[0].FullSpan))
            {
                node =
                    node.WithOpenParenToken(
                        node.OpenParenToken.WithTrailingTrivia(
                            node.OpenParenToken.TrailingTrivia.AppendNewLine(_newLine)
                        )
                    );
                _indentationCache[node] = expectedIndentation;
                ChangesApplied = true;

                // The indentation of the argument is done in VisitArgument
            }

            IEnumerable<SyntaxNodeOrToken> commaTokens = node.ChildNodesAndTokens().Where(n => n.IsKind(SyntaxKind.CommaToken));
            foreach (SyntaxNodeOrToken commaToken in commaTokens)
            {
                SyntaxToken token = commaToken.AsToken();
                SyntaxTriviaList trailingTrivia = token.TrailingTrivia;
                if (!trailingTrivia.HasEndOfLineTriviaAtTheEnd())
                {
                    node = node.ReplaceToken(token, token.WithTrailingTrivia(trailingTrivia.AppendNewLine(_newLine)));
                    _indentationCache[node] = expectedIndentation;
                    ChangesApplied = true;
                }
            }
        }

        // Parents are on different lines, but the closing paren is not on its own line, so move it to the next line
        if (!CheckNothingButTriviaInFront(node.CloseParenToken))
        {
            if (node.Arguments.Any())
            {
                ArgumentSyntax lastArgument = node.Arguments.Last();

                ArgumentSyntax newLastArgument =
                    lastArgument.WithTrailingTrivia(
                        lastArgument.GetTrailingTrivia().AppendNewLine(_newLine)
                    );

                node =
                    node.WithArguments(
                        node.Arguments.Replace(lastArgument, newLastArgument)
                    );
            }
            else
            {
                // If parens are on the different lines but there are no arguments,
                // then nothing should be in front apart from multiline comments attached to the open paren
                node =
                    node.WithOpenParenToken(
                        node.OpenParenToken.WithTrailingTrivia(
                            node.OpenParenToken.TrailingTrivia.AppendNewLine(_newLine)
                        )
                    );
            }

            _indentationCache[node] = expectedIndentation;
            ChangesApplied = true;
        }

        SyntaxNodeOrToken? newToken =
            ReformatLeadingTrivia(node.CloseParenToken, expectedIndentation + _singleIndentation, expectedIndentation);
        if (newToken is not null)
        {
            node = node.WithCloseParenToken(newToken.Value.AsToken());
            _indentationCache[node] = expectedIndentation;
            ChangesApplied = true;
        }

        return base.VisitArgumentList(node);
    }

    public override SyntaxNode? VisitArgument(ArgumentSyntax node)
    {
        string expectedIndentation = GetExpectedIndentation(node);
        _indentationCache[node] = expectedIndentation;
        _indentationCache[node.Expression] = expectedIndentation;

        bool nothingInFrontOfNode = CheckNothingButTriviaInFront(node);

        if (nothingInFrontOfNode)
        {
            SyntaxNodeOrToken? newNode = ReformatLeadingTrivia(node, expectedIndentation);
            if (newNode is not null)
            {
                node = node.WithLeadingTrivia(newNode.Value.GetLeadingTrivia());
                _indentationCache[node] = expectedIndentation;
                _indentationCache[node.Expression] = expectedIndentation;
                ChangesApplied = true;
            }
        }

        if (node.IsSingleLine(cancellationToken: _cancellationToken))
        {
            return base.VisitArgument(node);
        }

        if (node.NameColon is not null)
        {
            bool leftAndRightOnSameLine = CheckOnTheSameLine(node.SyntaxTree, node.NameColon.GetTrimmedFullSpan(), node.Expression.FullSpan);
            bool leftOnSingleLine = node.NameColon.IsSingleLine(cancellationToken: _cancellationToken);
            bool leftMultiLine = !leftOnSingleLine;
            bool rightOnSingleLine = node.Expression.IsSingleLine(cancellationToken: _cancellationToken);
            bool rightMultiLine = !rightOnSingleLine;

            bool nothingInFrontOfRight;

            if (leftAndRightOnSameLine && (leftMultiLine || rightMultiLine))
            {
                NameColonSyntax newEqualsToken =
                    node.NameColon.WithTrailingTrivia(
                        node.NameColon.GetTrailingTrivia().AppendNewLine(_newLine)
                    );
                node = node.WithNameColon(newEqualsToken);
                _indentationCache[node] = expectedIndentation;
                _indentationCache[node.Expression] = expectedIndentation;
                ChangesApplied = true;
                nothingInFrontOfRight = true;
            }
            else
            {
                nothingInFrontOfRight = CheckNothingButTriviaInFront(node.Expression);
            }

            if (nothingInFrontOfRight)
            {
                string rightExpectedIndentation = expectedIndentation + _singleIndentation;
                SyntaxNodeOrToken? newRight = ReformatLeadingTrivia(node.Expression, rightExpectedIndentation);
                if (newRight is not null)
                {
                    node = node.WithExpression((ExpressionSyntax)newRight.Value.AsNode()!);
                    _indentationCache[node] = expectedIndentation;
                    _indentationCache[node.Expression] = rightExpectedIndentation;
                    ChangesApplied = true;
                }
            }
        }

        return base.VisitArgument(node);
    }

    public override SyntaxNode? VisitAwaitExpression(AwaitExpressionSyntax node)
    {
        bool nothingInFrontOfAwait = CheckNothingButTriviaInFront(node);

        if (nothingInFrontOfAwait)
        {
            string expectedIndentation = GetExpectedIndentation(node);
            _indentationCache[node] = expectedIndentation;

            SyntaxNodeOrToken? newNode = ReformatLeadingTrivia(node, expectedIndentation);
            if (newNode is not null)
            {
                node = (AwaitExpressionSyntax)newNode.Value.AsNode()!;
                _indentationCache[node] = expectedIndentation;
                ChangesApplied = true;
            }
        }

        bool nothingInFrontOfExpression = CheckNothingButTriviaInFront(node.Expression);
        if (nothingInFrontOfExpression)
        {
            string expectedIndentation = GetExpectedIndentation(node);
            string expectedExpressionIndentation = expectedIndentation + _singleIndentation;
            _indentationCache[node.Expression] = expectedExpressionIndentation;

            SyntaxNodeOrToken? newNode = ReformatLeadingTrivia(node.Expression, expectedExpressionIndentation);
            if (newNode is not null)
            {
                node = node.WithExpression((ExpressionSyntax)newNode.Value.AsNode()!);
                _indentationCache[node] = expectedIndentation;
                _indentationCache[node.Expression] = expectedExpressionIndentation;
                ChangesApplied = true;
            }
        }

        return base.VisitAwaitExpression(node);
    }

    public override SyntaxNode? VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
    {
        SyntaxTree syntaxTree = node.SyntaxTree;

        SyntaxToken firstToken = node.GetFirstToken();

        bool singleLineOnLeft = CheckOnTheSameLine(syntaxTree, firstToken.GetTrimmedFullSpan(), node.Parameter.FullSpan);
        bool multilineOnLeft = !singleLineOnLeft;

        // The middle is multi-lined in the case of the trailing multi-line comments
        TextSpan trimmedOperatorTokenFullSpan = node.ArrowToken.GetTrimmedFullSpan();
        bool singleLineInMiddle = trimmedOperatorTokenFullSpan.IsSingleLine(syntaxTree, _cancellationToken);
        bool multilineInMiddle = !singleLineInMiddle;

        bool singleLineOnRight = node.Body.IsSingleLine(cancellationToken: _cancellationToken);
        bool multilineOnRight = !singleLineOnRight;

        string expectedIndentationLeft = GetExpectedIndentation(node);
        string expectedIndentationMiddle = expectedIndentationLeft;
        string expectedIndentationRight = expectedIndentationLeft;

        _indentationCache[node] = expectedIndentationLeft;

        bool middleAndRightOnSameLine = CheckOnTheSameLine(syntaxTree, trimmedOperatorTokenFullSpan, node.Body.FullSpan);

        bool nothingInFrontOfLeft = CheckNothingButTriviaInFront(node);
        bool nothingInFrontOfRight;

        if (nothingInFrontOfLeft)
        {
            SyntaxNodeOrToken? newLeft = ReformatLeadingTrivia(node, expectedIndentationLeft);
            if (newLeft is not null)
            {
                node = (SimpleLambdaExpressionSyntax)newLeft.Value.AsNode()!;
                _indentationCache[node] = expectedIndentationLeft;
                ChangesApplied = true;
            }
        }

        bool nothingInFrontOfMiddle = CheckNothingButTriviaInFront(node.ArrowToken);

        if (nothingInFrontOfMiddle)
        {
            expectedIndentationMiddle += _singleIndentation;
            expectedIndentationRight += _singleIndentation;

            SyntaxNodeOrToken? newMiddle = ReformatLeadingTrivia(node.ArrowToken, expectedIndentationMiddle);
            if (newMiddle is not null)
            {
                node = node.WithArrowToken(newMiddle.Value.AsToken());
                _indentationCache[node] = expectedIndentationLeft;
                ChangesApplied = true;
            }
        }

        if (!middleAndRightOnSameLine && node.Block is null)
        {
            expectedIndentationRight += _singleIndentation;
        }

        _indentationCache[node.Body] = expectedIndentationRight;

        if (middleAndRightOnSameLine && (multilineOnLeft || multilineInMiddle || multilineOnRight))
        {
            node =
                node.WithArrowToken(
                    node.ArrowToken.WithTrailingTrivia(
                        node.ArrowToken.TrailingTrivia.AppendNewLine(_newLine)
                    )
                );

            _indentationCache[node] = expectedIndentationLeft;
            _indentationCache[node.Body] = expectedIndentationRight;

            ChangesApplied = true;

            nothingInFrontOfRight = true;
        }
        else
        {
            nothingInFrontOfRight = CheckNothingButTriviaInFront(node.Body);
        }

        if (nothingInFrontOfRight)
        {
            SyntaxNodeOrToken? newRight = ReformatLeadingTrivia(node.Body, expectedIndentationRight);
            if (newRight is not null)
            {
                node = node.WithBody((CSharpSyntaxNode)newRight.Value.AsNode()!);
                _indentationCache[node] = expectedIndentationLeft;
                _indentationCache[node.Body] = expectedIndentationRight;
                ChangesApplied = true;
            }
        }

        return base.VisitSimpleLambdaExpression(node);
    }

    public override SyntaxNode? VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
    {
        SyntaxTree syntaxTree = node.SyntaxTree;

        SyntaxToken firstToken = node.GetFirstToken();

        bool singleLineOnLeft = CheckOnTheSameLine(syntaxTree, firstToken.GetTrimmedFullSpan(), node.ParameterList.CloseParenToken.FullSpan);
        bool multilineOnLeft = !singleLineOnLeft;

        // The middle is multi-lined in the case of the trailing multi-line comments
        TextSpan trimmedOperatorTokenFullSpan = node.ArrowToken.GetTrimmedFullSpan();
        bool singleLineInMiddle = trimmedOperatorTokenFullSpan.IsSingleLine(syntaxTree, _cancellationToken);
        bool multilineInMiddle = !singleLineInMiddle;

        bool singleLineOnRight = node.Body.IsSingleLine(cancellationToken: _cancellationToken);
        bool multilineOnRight = !singleLineOnRight;

        string expectedIndentationLeft = GetExpectedIndentation(node);
        string expectedIndentationMiddle = expectedIndentationLeft;
        string expectedIndentationRight = expectedIndentationLeft;

        _indentationCache[node] = expectedIndentationLeft;

        bool middleAndRightOnSameLine = CheckOnTheSameLine(syntaxTree, trimmedOperatorTokenFullSpan, node.Body.FullSpan);

        bool nothingInFrontOfLeft = CheckNothingButTriviaInFront(node);
        bool nothingInFrontOfRight;

        if (nothingInFrontOfLeft)
        {
            SyntaxNodeOrToken? newLeft = ReformatLeadingTrivia(node, expectedIndentationLeft);
            if (newLeft is not null)
            {
                node = (ParenthesizedLambdaExpressionSyntax)newLeft.Value.AsNode()!;
                _indentationCache[node] = expectedIndentationLeft;
                ChangesApplied = true;
            }
        }

        bool nothingInFrontOfMiddle = CheckNothingButTriviaInFront(node.ArrowToken);

        if (nothingInFrontOfMiddle)
        {
            expectedIndentationMiddle += _singleIndentation;
            expectedIndentationRight += _singleIndentation;

            SyntaxNodeOrToken? newMiddle = ReformatLeadingTrivia(node.ArrowToken, expectedIndentationMiddle);
            if (newMiddle is not null)
            {
                node = node.WithArrowToken(newMiddle.Value.AsToken());
                _indentationCache[node] = expectedIndentationLeft;
                ChangesApplied = true;
            }
        }

        // For block bodies indentation should stay the same
        if (!middleAndRightOnSameLine && node.Block is null)
        {
            expectedIndentationRight += _singleIndentation;
        }

        _indentationCache[node.Body] = expectedIndentationRight;

        if (middleAndRightOnSameLine && (multilineOnLeft || multilineInMiddle || multilineOnRight))
        {
            node =
                node.WithArrowToken(
                    node.ArrowToken.WithTrailingTrivia(
                        node.ArrowToken.TrailingTrivia.AppendNewLine(_newLine)
                    )
                );

            _indentationCache[node] = expectedIndentationLeft;
            _indentationCache[node.Body] = expectedIndentationRight;

            ChangesApplied = true;

            nothingInFrontOfRight = true;
        }
        else
        {
            nothingInFrontOfRight = CheckNothingButTriviaInFront(node.Body);
        }

        if (nothingInFrontOfRight)
        {
            SyntaxNodeOrToken? newRight = ReformatLeadingTrivia(node.Body, expectedIndentationRight);
            if (newRight is not null)
            {
                node = node.WithBody((CSharpSyntaxNode)newRight.Value.AsNode()!);
                _indentationCache[node] = expectedIndentationLeft;
                _indentationCache[node.Body] = expectedIndentationRight;
                ChangesApplied = true;
            }
        }

        return base.VisitParenthesizedLambdaExpression(node);
    }

    public override SyntaxNode? VisitBlock(BlockSyntax node)
    {
        string expectedIndentation = GetSelfIndentation(node) ?? GetParentIndentation(node) ?? _rootNodeIndentation;
        _indentationCache[node] = expectedIndentation;

        if (CheckNothingButTriviaInFront(node.OpenBraceToken))
        {
            SyntaxNodeOrToken? newNode = ReformatLeadingTrivia(node.OpenBraceToken, expectedIndentation);
            if (newNode is not null)
            {
                node = node.WithOpenBraceToken(newNode.Value.AsToken());
                _indentationCache[node] = expectedIndentation;
                ChangesApplied = true;
            }
        }

        TextSpan trimmedOpenParentTokenFullSpan = node.OpenBraceToken.GetTrimmedFullSpan();
        if (CheckOnTheSameLine(node.SyntaxTree, trimmedOpenParentTokenFullSpan, node.CloseBraceToken.FullSpan))
        {
            return base.VisitBlock(node);
        }

        if (node.Statements.Any())
        {
            if (CheckOnTheSameLine(node.SyntaxTree, trimmedOpenParentTokenFullSpan, node.Statements[0].FullSpan))
            {
                node =
                    node.WithOpenBraceToken(
                        node.OpenBraceToken.WithTrailingTrivia(
                            node.OpenBraceToken.TrailingTrivia.AppendNewLine(_newLine)
                        )
                    );
                _indentationCache[node] = expectedIndentation;
                ChangesApplied = true;
            }

            string expectedStatementIndentation = expectedIndentation + _singleIndentation;

            IEnumerable<SyntaxNodeOrToken> children =
                node.ChildNodesAndTokens()
                    .Where(
                        n => n.Kind() is not (SyntaxKind.CloseBraceToken or SyntaxKind.OpenBraceToken)
                    );
            foreach (SyntaxNodeOrToken child in children)
            {
                if (CheckNothingButTriviaInFront(child))
                {
                    SyntaxNodeOrToken? newChild = ReformatLeadingTrivia(child, expectedStatementIndentation);
                    if (newChild is not null)
                    {
                        if (child.IsToken)
                        {
                            node = node.ReplaceToken(child.AsToken(), newChild.Value.AsToken());
                        }
                        else
                        {
                            node = node.ReplaceNode(child.AsNode()!, newChild.Value.AsNode()!);
                        }

                        _indentationCache[node] = expectedIndentation;
                        ChangesApplied = true;
                    }
                }
            }
        }

        // Parents are on different lines, but the closing paren is not on its own line, so move it to the next line
        if (!CheckNothingButTriviaInFront(node.CloseBraceToken))
        {
            if (node.Statements.Any())
            {
                StatementSyntax lastStatement = node.Statements.Last();

                StatementSyntax newLastStatement =
                    lastStatement.WithTrailingTrivia(
                        lastStatement.GetTrailingTrivia().AppendNewLine(_newLine)
                    );

                node = node.ReplaceNode(lastStatement, newLastStatement);
            }
            else
            {
                // If parens are on the different lines but there are no arguments,
                // then nothing should be in front apart from multiline comments attached to the open paren
                node =
                    node.WithOpenBraceToken(
                        node.OpenBraceToken.WithTrailingTrivia(
                            node.OpenBraceToken.TrailingTrivia.AppendNewLine(_newLine)
                        )
                    );
            }

            _indentationCache[node] = expectedIndentation;
            ChangesApplied = true;
        }

        SyntaxNodeOrToken? newToken =
            ReformatLeadingTrivia(node.CloseBraceToken, expectedIndentation + _singleIndentation, expectedIndentation);
        if (newToken is not null)
        {
            node = node.WithCloseBraceToken(newToken.Value.AsToken());
            _indentationCache[node] = expectedIndentation;
            ChangesApplied = true;
        }

        return base.VisitBlock(node);
    }

    // public override SyntaxNode? VisitSelectClause(SelectClauseSyntax node)
    // {
    //     bool tokensOnDifferentLines = !CheckOnTheSameLine(node.SyntaxTree, node.SelectKeyword.Span, node.Expression.Span);
    //
    //     // Either the equals token and value are on the different lines
    //     if (tokensOnDifferentLines
    //         // Or they are on the same line, but the value is single-lined
    //         || node.Expression.IsSingleLine(cancellationToken: _cancellationToken)
    //     )
    //     {
    //         return base.VisitSelectClause(node);
    //     }
    //
    //     node =
    //         DoWithIndentationCacheCorrection(
    //             node,
    //             static (rewriter, oldNode) =>
    //                 oldNode.WithSelectKeyword(
    //                     oldNode.SelectKeyword.WithTrailingTrivia(
    //                         oldNode.SelectKeyword.TrailingTrivia.AppendNewLine(rewriter._newLine)
    //                     )
    //                 )
    //         );
    //
    //     ChangesApplied = true;
    //     // Immediate stop if at least one change was applied
    //     if (DoAnalysisOnly)
    //     {
    //         return node;
    //     }
    //
    //     return base.VisitSelectClause(node);
    // }

    // public override SyntaxNode? VisitTupleExpression(TupleExpressionSyntax node)
    // {
    //     if (node.IsSingleLine(cancellationToken: _cancellationToken)
    //         || CheckNothingButTriviaInFront(node.CloseParenToken)
    //     )
    //     {
    //         return base.VisitTupleExpression(node);
    //     }
    //
    //     node =
    //         DoWithIndentationCacheCorrection(
    //             node,
    //             static (rewriter, oldNode) =>
    //             {
    //                 ArgumentSyntax lastArgument = oldNode.Arguments.Last();
    //
    //                 ArgumentSyntax newLastArgument =
    //                     lastArgument.WithTrailingTrivia(
    //                         lastArgument.GetTrailingTrivia().AppendNewLine(rewriter._newLine)
    //                     );
    //
    //                 return oldNode.WithArguments(
    //                     oldNode.Arguments.Replace(lastArgument, newLastArgument)
    //                 );
    //             }
    //         );
    //
    //     ChangesApplied = true;
    //     // Immediate stop if at least one change was applied
    //     if (DoAnalysisOnly)
    //     {
    //         return node;
    //     }
    //
    //     return base.VisitTupleExpression(node);
    // }

    public override SyntaxNode VisitInterpolatedStringExpression(InterpolatedStringExpressionSyntax node)
    {
        // No further processing for interpolated strings.
        // If there is something inside it that should be formatted, then it will be picked by the trigger inside.
        // That's why the method always returns the incoming node as is without calling the base method.

        // The method still needs to reformat an incoming Interpolated Multi Line Raw Strings

        if (!node.GetFirstToken().IsKind(SyntaxKind.InterpolatedMultiLineRawStringStartToken))
        {
            return node;
        }

        string parentIndentation = GetParentIndentation(node) ?? _rootNodeIndentation;
        string expectedIndentation = parentIndentation + _singleIndentation;
        string? result = ReformatMultilineString(node.ToFullString(), expectedIndentation);

        if (result is not null
            && SyntaxFactory.ParseExpression(result) is InterpolatedStringExpressionSyntax interpolatedStringText
        )
        {
            node = interpolatedStringText;
        }

        return node;
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
                changesExist = true;
            }
            if (lastTrivia.Span.Length != expectedLastIndentation.Length)
            {
                newLeadingTrivia[newLeadingTrivia.Count - 1] = SyntaxFactory.Whitespace(expectedLastIndentation);
            }
        }
        else
        {
            if (!lastTrivia.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                newLeadingTrivia.Add(_newLine);
                changesExist = true;
            }

            if (expectedLastIndentation.Length > 0)
            {
                newLeadingTrivia.Add(SyntaxFactory.Whitespace(expectedLastIndentation));
                changesExist = true;
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
                string? result = ReformatMultilineString(trivia.ToFullString(), expectedIndentation);
                if (result is not null)
                {
                    changesExist = true;
                    yield return SyntaxFactory.Comment(result);
                    yield break;
                }
            }

            yield return trivia;
        }
    }

    /// <summary>
    /// For multi-line comment trivia, or for raw string or etc., we need to check that the indentation of the content is correct
    /// The content is expected to be the text between the start and end tokens
    /// Returns null if no changes exist.
    /// </summary>
    private string? ReformatMultilineString(string strContent, string expectedIndentation)
    {
        string[] splitContent = strContent.Split(GetSplitParameter(), StringSplitOptions.None);

        if (splitContent.Length > 1)
        {
            bool changesExist = false;

            // The trivia on index 0 or the raw string start token should be already corrected.
            // Only the following lines should be checked
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
                    int currentIndentationLength = line.Length - endSliceLength;
                    if (currentIndentationLength != expectedIndentation.Length)
                    {
                        changesExist = true;
                        ReadOnlySpan<char> restOfTheLine =
                            line.AsSpan().Slice(line.Length - endSliceLength, endSliceLength);
                        splitContent[i] = expectedIndentation + restOfTheLine.ToString();
                    }
                }

                if (changesExist)
                {
                    return string.Join(_newLine.ToString(), splitContent);
                }
            }
        }

        return null;
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
            // Let's count both cases as 1
            && line[currentIndentationLength] is ' ' or '\t'
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

        SyntaxToken token =
            nodeOrToken.IsToken
                ? nodeOrToken.AsToken()
                : nodeOrToken.AsNode()!.GetFirstToken();

        SyntaxToken previousToken = token.GetPreviousToken();
        if (!previousToken.IsKind(SyntaxKind.None)
            && previousToken != token
            && CheckOnTheSameLine(syntaxTree, previousToken.Span, nodeOrToken.Span)
        )
        {
            return false;
        }

        TextLineCollection textLines = syntaxTree.GetText(_cancellationToken).Lines;

        // if (CheckParentOnTheSameLine(textLines, nodeOrToken))
        // {
        //     return false;
        // }

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

        // if (CheckNothingButMultilineCommentFromParentTrailingTrivia(syntaxTree, textLines, nodeOrToken))
        // {
        //     return true;
        // }

        return false;
    }

    private static bool CheckParentOnTheSameLine(TextLineCollection textLines, SyntaxNodeOrToken nodeOrToken)
    {
        LinePosition nodeLinePosition = textLines.GetLinePosition(nodeOrToken.SpanStart);

        SyntaxNode? parent = nodeOrToken.Parent;
        while (parent is not null)
        {
            // These parents are top-level wrappers. If they are reached, then there is no parent in front
            if (parent.Kind() is SyntaxKind.ExpressionStatement or SyntaxKind.GlobalStatement)
            {
                return false;
            }

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

            if (c is not (' ' or '\t'))
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

    private string GetExpectedIndentation(SyntaxNode node, bool skipIndentation = false)
    {
        string? selfIndentation = GetSelfIndentation(node);
        if (selfIndentation is not null)
        {
            return selfIndentation;
        }

        string? parentIndentation = GetParentIndentation(node);

        if (parentIndentation is null)
        {
            if (ReferenceEquals(_root, node))
            {
                return _rootNodeIndentation;
            }

            parentIndentation = _rootNodeIndentation;
        }

        if (skipIndentation)
        {
            return parentIndentation;
        }

        return parentIndentation + _singleIndentation;
    }

    private string? GetSelfIndentation(SyntaxNode node)
    {
        if (_indentationCache.TryGetValue(node, out string? indentation))
        {
            return indentation;
        }

        return null;
    }

    private string? GetParentIndentation(SyntaxNodeOrToken nodeOrToken)
    {
        SyntaxNode? parent = nodeOrToken.Parent;

        while (parent is not null)
        {
            SyntaxKind? parentKind = parent.Kind();

            // if (parentKind is SyntaxKind.InvocationExpression)
            // {
            //     InvocationExpressionSyntax invocationExpressionSyntax = (InvocationExpressionSyntax)parent;
            //     MemberAccessExpressionSyntax? memberAccessExpressionSyntax =
            //         (MemberAccessExpressionSyntax?)invocationExpressionSyntax.ChildNodesAndTokens()
            //             .FirstOrDefault(n => n.IsNode && n.AsNode() is MemberAccessExpressionSyntax);
            //     if (memberAccessExpressionSyntax is not null && CheckNothingButTriviaInFront(memberAccessExpressionSyntax))
            //     {
            //         if (_indentationCache.TryGetValue(memberAccessExpressionSyntax, out string? memberAccessIndentation))
            //         {
            //             return memberAccessIndentation;
            //         }
            //     }
            // }

            if (_indentationCache.TryGetValue(parent, out string? indentation))
            {
                return indentation;
            }

            parent = parent.Parent;
        }

        return null;
    }

    private string[] GetSplitParameter()
    {
        if (_splitParameter is not null)
        {
            return _splitParameter;
        }

        _splitParameter = new[] { _newLine.ToString() };

        return _splitParameter;
    }
}

[SuppressMessage(
    "Style",
    "RCS1060",
    Justification =
        "The class should be here. It is a part of the analysis logic but extensions can be declared only on file level."
)]
file static class SyntaxNodeOrTokenExtensions
{
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

    public static TextSpan GetBodyAndTrimmedTrailingTriviaSpan(this SyntaxNode node)
    {
        SyntaxTriviaList trailingTrivia = node.GetTrailingTrivia();
        if (trailingTrivia.Any())
        {
            return GetTrimmedSpan(TextSpan.FromBounds(node.SpanStart, trailingTrivia.FullSpan.End), trailingTrivia);
        }

        return node.Span;
    }

    public static TextSpan GetBodyAndTrimmedTrailingTriviaSpan(this SyntaxToken token)
    {
        SyntaxTriviaList trailingTrivia = token.TrailingTrivia;
        if (trailingTrivia.Any())
        {
            return GetTrimmedSpan(TextSpan.FromBounds(token.SpanStart, trailingTrivia.FullSpan.End), trailingTrivia);
        }

        return token.Span;
    }

    public static TextSpan GetTrimmedFullSpan(this SyntaxNode node)
        => GetTrimmedSpan(node.FullSpan, node.GetTrailingTrivia());

    public static TextSpan GetTrimmedFullSpan(this SyntaxToken token)
        => GetTrimmedSpan(token.FullSpan, token.TrailingTrivia);

    private static TextSpan GetTrimmedSpan(TextSpan span, SyntaxTriviaList trailingTrivia)
    {
        if (trailingTrivia.Any())
        {
            SyntaxTrivia lastTrivia = trailingTrivia.Last();

            if (lastTrivia.IsEndOfLineTrivia())
            {
                return TextSpan.FromBounds(span.Start, lastTrivia.SpanStart);
            }

            if (trailingTrivia.Count > 1
                && lastTrivia.IsKind(SyntaxKind.WhitespaceTrivia)
                && trailingTrivia[trailingTrivia.Count - 2].IsEndOfLineTrivia()
            )
            {
                return TextSpan.FromBounds(span.Start, trailingTrivia[trailingTrivia.Count - 2].SpanStart);
            }
        }

        return span;
    }

    public static bool HasEndOfLineTriviaAtTheEnd(this SyntaxTriviaList trivia)
    {
        switch (trivia.Count)
        {
            case 0:
                return false;
            case 1:
                return trivia[0].IsEndOfLineTrivia();
            default:
                SyntaxTrivia lastTrivia = trivia.Last();
                return lastTrivia.IsEndOfLineTrivia()
                    || (trivia[trivia.Count - 2].IsEndOfLineTrivia()
                        && lastTrivia.IsWhitespaceTrivia()
                    );
        }
    }
}
