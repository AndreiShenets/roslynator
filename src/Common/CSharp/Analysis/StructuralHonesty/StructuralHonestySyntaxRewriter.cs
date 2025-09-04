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
    public bool ChangesRequired { get; set; }

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

        if (ChangesRequired && DoAnalysisOnly)
        {
            // If at least one change was applied, then we should stop the analysis
            // and return the node as is.
            return node;
        }

        return base.Visit(node);
    }

    public override SyntaxToken VisitToken(SyntaxToken token)
    {
        if (ChangesRequired && DoAnalysisOnly)
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
        SyntaxTriviaList? equalsTokenNewLeadingTrivia = null;
        SyntaxTriviaList? equalsTokenNewTrailingTrivia = null;
        SyntaxTriviaList? valueNewLeadingTrivia = null;

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

        _indentationCache[node] = expectedIndentation;

        bool nothingInFrontOfMiddle = CheckNothingButTriviaInFront(node.EqualsToken);
        if (nothingInFrontOfMiddle)
        {
            equalsTokenNewLeadingTrivia = ReformatLeadingTrivia(node.EqualsToken, expectedIndentation);
            if (equalsTokenNewLeadingTrivia is not null)
            {
                ChangesRequired = true;
            }
            valueExpectedIndentation += _singleIndentation;
        }

        _indentationCache[node.Value] = valueExpectedIndentation;

        if (middleAndRightOnSameLine && (multilineInMiddle || multilineOnRight))
        {
            equalsTokenNewTrailingTrivia = node.EqualsToken.TrailingTrivia.AppendNewLine(_newLine);
            ChangesRequired = true;
            middleAndRightOnSameLine = false;
        }

        if (!middleAndRightOnSameLine)
        {
            // Moved the right side above or was already on the next line, check that indentation is correct
            valueNewLeadingTrivia = ReformatLeadingTrivia(node.Value, valueExpectedIndentation);
            if (valueNewLeadingTrivia is not null)
            {
                ChangesRequired = true;
            }
        }

        EqualsValueClauseSyntax? resultNode = (EqualsValueClauseSyntax?)base.VisitEqualsValueClause(node);
        if (resultNode is null)
        {
            return null;
        }

        if (equalsTokenNewLeadingTrivia is not null)
        {
            resultNode = resultNode.WithEqualsToken(resultNode.EqualsToken.WithLeadingTrivia(equalsTokenNewLeadingTrivia));
        }

        if (equalsTokenNewTrailingTrivia is not null)
        {
            resultNode = resultNode.WithEqualsToken(resultNode.EqualsToken.WithTrailingTrivia(equalsTokenNewTrailingTrivia));
        }

        if (valueNewLeadingTrivia is not null)
        {
            resultNode = resultNode.WithValue(resultNode.Value.WithLeadingTrivia(valueNewLeadingTrivia));
        }

        return resultNode;
    }

    public override SyntaxNode? VisitAssignmentExpression(AssignmentExpressionSyntax node)
    {
        SyntaxTriviaList? leftNewLeadingTrivia = null;
        SyntaxTriviaList? operatorNewLeadingTrivia = null;
        SyntaxTriviaList? operatorNewTrailingTrivia = null;
        SyntaxTriviaList? rightNewLeadingTrivia = null;

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
            leftNewLeadingTrivia = ReformatLeadingTrivia(node.Left, expectedIndentationLeft);
            if (leftNewLeadingTrivia is not null)
            {
                ChangesRequired = true;
            }
        }

        bool nothingInFrontOfMiddle = CheckNothingButTriviaInFront(node.OperatorToken);

        if (nothingInFrontOfMiddle)
        {
            expectedIndentationMiddle += _singleIndentation;
            expectedIndentationRight = expectedIndentationMiddle;

            operatorNewLeadingTrivia = ReformatLeadingTrivia(node.OperatorToken, expectedIndentationMiddle);
            if (operatorNewLeadingTrivia is not null)
            {
                ChangesRequired = true;
            }
        }

        if (!middleAndRightOnSameLine)
        {
            expectedIndentationRight += _singleIndentation;
        }

        _indentationCache[node.Right] = expectedIndentationRight;

        if (middleAndRightOnSameLine && (multilineInMiddle || multilineOnRight))
        {
            operatorNewTrailingTrivia = node.OperatorToken.TrailingTrivia.AppendNewLine(_newLine);
            ChangesRequired = true;
            nothingInFrontOfRight = true;
        }
        else
        {
            nothingInFrontOfRight = CheckNothingButTriviaInFront(node.Right);
        }

        if (nothingInFrontOfRight)
        {
            rightNewLeadingTrivia = ReformatLeadingTrivia(node.Right, expectedIndentationRight);
            if (rightNewLeadingTrivia is not null)
            {
                ChangesRequired = true;
            }
        }

        AssignmentExpressionSyntax? resultNode = (AssignmentExpressionSyntax?)base.VisitAssignmentExpression(node);
        if (resultNode is null)
        {
            return null;
        }

        if (leftNewLeadingTrivia is not null)
        {
            resultNode = resultNode.WithLeft(resultNode.Left.WithLeadingTrivia(leftNewLeadingTrivia));
        }

        if (operatorNewLeadingTrivia is not null)
        {
            resultNode = resultNode.WithOperatorToken(resultNode.OperatorToken.WithLeadingTrivia(operatorNewLeadingTrivia));
        }

        if (operatorNewTrailingTrivia is not null)
        {
            resultNode = resultNode.WithOperatorToken(resultNode.OperatorToken.WithTrailingTrivia(operatorNewTrailingTrivia));
        }

        if (rightNewLeadingTrivia is not null)
        {
            resultNode = resultNode.WithRight(resultNode.Right.WithLeadingTrivia(rightNewLeadingTrivia));
        }

        return resultNode;
    }

    public override SyntaxNode? VisitBinaryExpression(BinaryExpressionSyntax node)
    {
        SyntaxTriviaList? leftNewLeadingTrivia = null;
        SyntaxTriviaList? leftNewTrailingTrivia = null;
        SyntaxTriviaList? operatorNewLeadingTrivia = null;
        SyntaxTriviaList? operatorNewTrailingTrivia = null;
        SyntaxTriviaList? rightNewLeadingTrivia = null;

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
            leftNewLeadingTrivia = ReformatLeadingTrivia(node.Left, expectedIndentation);
            if (leftNewLeadingTrivia is not null)
            {
                ChangesRequired = true;
            }
        }

        bool nothingInFrontOfMiddle;

        if (leftAndMiddleOnSameLine && (multilineOnLeft || multilineInMiddle || multilineOnRight))
        {
            leftNewTrailingTrivia = node.Left.GetTrailingTrivia().AppendNewLine(_newLine);
            ChangesRequired = true;
            nothingInFrontOfMiddle = true;
        }
        else
        {
            nothingInFrontOfMiddle = CheckNothingButTriviaInFront(node.OperatorToken);
        }

        if (nothingInFrontOfMiddle)
        {
            operatorNewLeadingTrivia = ReformatLeadingTrivia(node.OperatorToken, expectedIndentation);
            if (operatorNewLeadingTrivia is not null)
            {
                ChangesRequired = true;
            }
        }

        _indentationCache[node.Right] = expectedIndentation;

        if (!middleAndRightOnSameLine && (multilineOnLeft || multilineInMiddle || multilineOnRight))
        {
            IEnumerable<SyntaxTrivia> leftTrailingTrivia =
                (leftNewTrailingTrivia ?? node.Left.GetTrailingTrivia())
                    .Concat(node.OperatorToken.TrailingTrivia)
                    .Concat(node.Right.GetLeadingTrivia());

            leftNewTrailingTrivia =
                SyntaxFactory.TriviaList(leftTrailingTrivia)
                    .AppendNewLine(_newLine);
            operatorNewTrailingTrivia = new SyntaxTriviaList(SyntaxFactory.Whitespace(" "));
            rightNewLeadingTrivia = SyntaxTriviaList.Empty;

            ChangesRequired = true;
        }

        BinaryExpressionSyntax? resultNode = (BinaryExpressionSyntax?)base.VisitBinaryExpression(node);

        if (resultNode is null)
        {
            return null;
        }

        if (leftNewLeadingTrivia is not null)
        {
            resultNode = resultNode.WithLeft(resultNode.Left.WithLeadingTrivia(leftNewLeadingTrivia));
        }

        if (leftNewTrailingTrivia is not null)
        {
            resultNode = resultNode.WithLeft(resultNode.Left.WithTrailingTrivia(leftNewTrailingTrivia));
        }

        if (operatorNewLeadingTrivia is not null)
        {
            resultNode = resultNode.WithOperatorToken(resultNode.OperatorToken.WithLeadingTrivia(operatorNewLeadingTrivia));
        }

        if (operatorNewTrailingTrivia is not null)
        {
            resultNode = resultNode.WithOperatorToken(resultNode.OperatorToken.WithTrailingTrivia(operatorNewTrailingTrivia));
        }

        if (rightNewLeadingTrivia is not null)
        {
            resultNode = resultNode.WithRight(resultNode.Right.WithLeadingTrivia(rightNewLeadingTrivia));
        }

        return resultNode;
    }

    public override SyntaxNode? VisitParenthesizedExpression(ParenthesizedExpressionSyntax node)
    {
        SyntaxTriviaList? openParenNewLeadingTrivia = null;
        SyntaxTriviaList? expressionNewTrailingTrivia = null;
        SyntaxTriviaList? closeParenNewLeadingTrivia = null;

        string expectedIndentation = GetSelfIndentation(node) ?? GetParentIndentation(node) ?? _rootNodeIndentation;
        _indentationCache[node] = expectedIndentation;

        if (CheckNothingButTriviaInFront(node.OpenParenToken))
        {
            if (!ReferenceEquals(_root, node))
            {
                expectedIndentation += _singleIndentation;
            }

            _indentationCache[node] = expectedIndentation;

            openParenNewLeadingTrivia = ReformatLeadingTrivia(node.OpenParenToken, expectedIndentation);
            if (openParenNewLeadingTrivia is not null)
            {
                ChangesRequired = true;
            }
        }

        // Parents are on different lines, but the closing paren is not on its own line, so move it to the next line
        if (!CheckOnTheSameLine(node.SyntaxTree, node.OpenParenToken.GetTrimmedFullSpan(), node.CloseParenToken.FullSpan)
            && !CheckNothingButTriviaInFront(node.CloseParenToken)
        )
        {
            expressionNewTrailingTrivia = node.Expression.GetTrailingTrivia().AppendNewLine(_newLine);

            closeParenNewLeadingTrivia =
                ReformatLeadingTrivia(node.CloseParenToken, expectedIndentation + _singleIndentation, expectedIndentation);

            ChangesRequired = true;
        }

        ParenthesizedExpressionSyntax? resultNode = (ParenthesizedExpressionSyntax?)base.VisitParenthesizedExpression(node);
        if (resultNode is null)
        {
            return null;
        }

        if (openParenNewLeadingTrivia is not null)
        {
            resultNode = resultNode.WithOpenParenToken(resultNode.OpenParenToken.WithLeadingTrivia(openParenNewLeadingTrivia));
        }

        if (expressionNewTrailingTrivia is not null)
        {
            resultNode = resultNode.WithExpression(resultNode.Expression.WithTrailingTrivia(expressionNewTrailingTrivia));
        }

        if (closeParenNewLeadingTrivia is not null)
        {
            resultNode = resultNode.WithCloseParenToken(resultNode.CloseParenToken.WithLeadingTrivia(closeParenNewLeadingTrivia));
        }

        return resultNode;
    }

    public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        if (node.Parent is MemberAccessExpressionSyntax)
        {
            // Such invocation expressions are indented in the parent
            return base.VisitInvocationExpression(node);
        }

        SyntaxTriviaList? nodeNewLeadingTrivia = null;

        bool nothingInFrontOfNode = CheckNothingButTriviaInFront(node);

        string expectedIndentation = GetExpectedIndentation(node);

        if (nothingInFrontOfNode)
        {
            _indentationCache[node] = expectedIndentation;
            _indentationCache[node.ArgumentList] = expectedIndentation;

            nodeNewLeadingTrivia = ReformatLeadingTrivia(node, expectedIndentation);
            if (nodeNewLeadingTrivia is not null)
            {
                ChangesRequired = true;
            }
        }

        InvocationExpressionSyntax? resultNode = (InvocationExpressionSyntax?)base.VisitInvocationExpression(node);
        if (resultNode is null)
        {
            return null;
        }

        if (nodeNewLeadingTrivia is not null)
        {
            resultNode = resultNode.WithLeadingTrivia(nodeNewLeadingTrivia);
        }

        return resultNode;
    }

    public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        SyntaxNode? fullRightPart = node.Parent;
        if (fullRightPart is null)
        {
            return base.VisitMemberAccessExpression(node);
        }

        SyntaxTriviaList? operatorNewLeadingTrivia = null;
        SyntaxTriviaList? operatorNewTrailingTrivia = null;
        SyntaxTriviaList? expressionNewTrailingTrivia = null;
        SyntaxTriviaList? nameNewLeadingTrivia = null;

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
            expressionNewTrailingTrivia = node.Expression.GetTrailingTrivia().AppendNewLine(_newLine);
            ChangesRequired = true;
            nothingInFrontOfMiddle = true;
        }
        else
        {
            nothingInFrontOfMiddle = CheckNothingButTriviaInFront(node.OperatorToken);
        }

        if (nothingInFrontOfMiddle)
        {
            operatorNewLeadingTrivia = ReformatLeadingTrivia(node.OperatorToken, expectedIndentationMiddle);
            if (operatorNewLeadingTrivia is not null)
            {
                ChangesRequired = true;
            }
        }

        if (!middleAndRightOnSameLine && (multilineOnLeft || multilineInMiddle || multilineOnRight))
        {
            IEnumerable<SyntaxTrivia> leftTrailingTrivia =
                (expressionNewTrailingTrivia ?? node.Expression.GetTrailingTrivia())
                    .Concat(node.OperatorToken.TrailingTrivia)
                    .Concat(node.Name.GetLeadingTrivia());

            expressionNewTrailingTrivia =
                SyntaxFactory.TriviaList(leftTrailingTrivia)
                    .AppendNewLine(_newLine);

            operatorNewTrailingTrivia = SyntaxTriviaList.Empty;
            nameNewLeadingTrivia = SyntaxTriviaList.Empty;

            _indentationCache[fullRightPart] = expectedIndentationMiddle;

            ChangesRequired = true;
        }

        MemberAccessExpressionSyntax? resultNode = (MemberAccessExpressionSyntax?)base.VisitMemberAccessExpression(node);
        if (resultNode is null)
        {
            return null;
        }

        if (operatorNewLeadingTrivia is not null)
        {
            resultNode = resultNode.WithOperatorToken(resultNode.OperatorToken.WithLeadingTrivia(operatorNewLeadingTrivia));
        }

        if (operatorNewTrailingTrivia is not null)
        {
            resultNode = resultNode.WithOperatorToken(resultNode.OperatorToken.WithTrailingTrivia(operatorNewTrailingTrivia));
        }

        if (expressionNewTrailingTrivia is not null)
        {
            resultNode = resultNode.WithExpression(resultNode.Expression.WithTrailingTrivia(expressionNewTrailingTrivia));
        }

        if (nameNewLeadingTrivia is not null)
        {
            resultNode = resultNode.WithName(resultNode.Name.WithLeadingTrivia(nameNewLeadingTrivia));
        }

        return resultNode;
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
                    // We cannot go up beyond the root as the upper elements don't have indentations in the cache.
                    parent =
                        ReferenceEquals(_root, memberAccessExpressionSyntax)
                            ? null
                            : memberAccessExpressionSyntax.Parent;
                    break;
                case InvocationExpressionSyntax invocationExpressionSyntax:
                    topInvocationExpression = invocationExpressionSyntax;
                    topLevelMemberAccessExpression = null;
                    // We cannot go up beyond the root as the upper elements don't have indentations in the cache.
                    parent =
                        ReferenceEquals(_root, invocationExpressionSyntax)
                            ? null
                            : invocationExpressionSyntax.Parent;
                    break;
                default:
                    parent = null;
                    break;
            }
        }

        if (topInvocationExpression is not null)
        {
            string? selfIndentation = GetSelfIndentation(topInvocationExpression);
            if (selfIndentation is null && ReferenceEquals(_root, topInvocationExpression))
            {
                selfIndentation = _rootNodeIndentation;
            }
            if (selfIndentation is not null)
            {
                return selfIndentation + _singleIndentation;
            }
        }

        if (topLevelMemberAccessExpression is not null)
        {
            string? selfIndentation = GetSelfIndentation(topLevelMemberAccessExpression);
            if (selfIndentation is null && ReferenceEquals(_root, topLevelMemberAccessExpression))
            {
                selfIndentation = _rootNodeIndentation;
            }
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

        SyntaxTriviaList? openParenNewLeadingTrivia = null;
        SyntaxTriviaList? openParenNewTrailingTrivia = null;
        SyntaxTriviaList? closeParenNewLeadingTrivia = null;
        List<(SyntaxToken Token, SyntaxTriviaList NewTrailingTrivia)> nodeTokensToReplace = [];
        SyntaxTriviaList? lastArgumentNewTrailingTrivia = null;

        if (CheckNothingButTriviaInFront(node.OpenParenToken))
        {
            if (!ReferenceEquals(_root, node))
            {
                expectedIndentation += _singleIndentation;
                _indentationCache[node] = expectedIndentation;
            }

            openParenNewLeadingTrivia = ReformatLeadingTrivia(node.OpenParenToken, expectedIndentation);
            if (openParenNewLeadingTrivia is not null)
            {
                ChangesRequired = true;
            }
        }

        TextSpan trimmedOpenParentTokenFullSpan = node.OpenParenToken.GetTrimmedFullSpan();
        if (!CheckOnTheSameLine(node.SyntaxTree, trimmedOpenParentTokenFullSpan, node.CloseParenToken.FullSpan))
        {
            if (node.Arguments.Any())
            {
                if (CheckOnTheSameLine(node.SyntaxTree, trimmedOpenParentTokenFullSpan, node.Arguments[0].FullSpan))
                {
                    openParenNewTrailingTrivia = node.OpenParenToken.TrailingTrivia.AppendNewLine(_newLine);
                    ChangesRequired = true;
                    // The indentation of the argument is done in VisitArgument
                }

                IEnumerable<SyntaxNodeOrToken> commaTokens = node.ChildNodesAndTokens().Where(n => n.IsKind(SyntaxKind.CommaToken));
                foreach (SyntaxNodeOrToken commaToken in commaTokens)
                {
                    SyntaxToken token = commaToken.AsToken();
                    SyntaxTriviaList trailingTrivia = token.TrailingTrivia;
                    if (!trailingTrivia.HasEndOfLineTriviaAtTheEnd())
                    {
                        nodeTokensToReplace.Add((token, trailingTrivia.AppendNewLine(_newLine)));
                        ChangesRequired = true;
                    }
                }
            }

            // Parents are on different lines, but the closing paren is not on its own line, so move it to the next line
            if (!CheckNothingButTriviaInFront(node.CloseParenToken))
            {
                if (node.Arguments.Any())
                {
                    ArgumentSyntax lastArgument = node.Arguments.Last();

                    lastArgumentNewTrailingTrivia = lastArgument.GetTrailingTrivia().AppendNewLine(_newLine);
                }
                else
                {
                    // If parens are on the different lines but there are no arguments,
                    // then nothing should be in front apart from multiline comments attached to the open paren
                    openParenNewTrailingTrivia =
                        (openParenNewTrailingTrivia ?? node.OpenParenToken.TrailingTrivia).AppendNewLine(_newLine);
                }

                ChangesRequired = true;
            }

            closeParenNewLeadingTrivia =
                ReformatLeadingTrivia(node.CloseParenToken, expectedIndentation + _singleIndentation, expectedIndentation);
            if (closeParenNewLeadingTrivia is not null)
            {
                ChangesRequired = true;
            }
        }

        ArgumentListSyntax? resultNode = (ArgumentListSyntax?)base.VisitArgumentList(node);
        if (resultNode is null)
        {
            return null;
        }

        if (openParenNewLeadingTrivia is not null)
        {
            resultNode = resultNode.WithOpenParenToken(resultNode.OpenParenToken.WithLeadingTrivia(openParenNewLeadingTrivia));
        }

        if (openParenNewTrailingTrivia is not null)
        {
            resultNode = resultNode.WithOpenParenToken(resultNode.OpenParenToken.WithTrailingTrivia(openParenNewTrailingTrivia));
        }

        if (closeParenNewLeadingTrivia is not null)
        {
            resultNode = resultNode.WithCloseParenToken(resultNode.CloseParenToken.WithLeadingTrivia(closeParenNewLeadingTrivia));
        }

        if (lastArgumentNewTrailingTrivia is not null)
        {
            ArgumentSyntax lastArgument = resultNode.Arguments.Last();

            resultNode =
                resultNode.WithArguments(
                    resultNode.Arguments.Replace(
                        lastArgument,
                        lastArgument.WithTrailingTrivia(lastArgumentNewTrailingTrivia)
                    )
                );
        }

        for (int index = 0; index < nodeTokensToReplace.Count; index++)
        {
            (SyntaxToken oldToken, SyntaxTriviaList newTrailingTrivia) = nodeTokensToReplace[index];
            resultNode = resultNode.ReplaceToken(oldToken, oldToken.WithTrailingTrivia(newTrailingTrivia));
        }

        return resultNode;
    }

    public override SyntaxNode? VisitArgument(ArgumentSyntax node)
    {
        string expectedIndentation = GetExpectedIndentation(node);
        _indentationCache[node] = expectedIndentation;
        _indentationCache[node.Expression] = expectedIndentation;

        SyntaxTriviaList? newLeadingTrivia = null;
        SyntaxTriviaList? nameColonNewTrailingTrivia = null;
        SyntaxTriviaList? expressionNewLeadingTrivia = null;

        bool nothingInFrontOfNode = CheckNothingButTriviaInFront(node);

        if (nothingInFrontOfNode)
        {
            newLeadingTrivia = ReformatLeadingTrivia(node, expectedIndentation);
            if (newLeadingTrivia is not null)
            {
                ChangesRequired = true;
            }
        }

        if (!node.IsSingleLine(cancellationToken: _cancellationToken))
        {
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
                    nameColonNewTrailingTrivia = node.NameColon.GetTrailingTrivia().AppendNewLine(_newLine);
                    ChangesRequired = true;
                    nothingInFrontOfRight = true;
                }
                else
                {
                    nothingInFrontOfRight = CheckNothingButTriviaInFront(node.Expression);
                }

                if (nothingInFrontOfRight)
                {
                    string rightExpectedIndentation = expectedIndentation + _singleIndentation;
                    expressionNewLeadingTrivia = ReformatLeadingTrivia(node.Expression, rightExpectedIndentation);
                    if (expressionNewLeadingTrivia is not null)
                    {
                        ChangesRequired = true;
                    }
                }
            }
        }

        ArgumentSyntax? resultNode = (ArgumentSyntax?)base.VisitArgument(node);
        if (resultNode is null)
        {
            return null;
        }

        if (newLeadingTrivia is not null)
        {
            resultNode = resultNode.WithLeadingTrivia(newLeadingTrivia);
        }

        if (nameColonNewTrailingTrivia is not null && resultNode.NameColon is not null)
        {
            resultNode = resultNode.WithNameColon(resultNode.NameColon.WithTrailingTrivia(nameColonNewTrailingTrivia));
        }

        if (expressionNewLeadingTrivia is not null)
        {
            resultNode = resultNode.WithExpression(resultNode.Expression.WithLeadingTrivia(expressionNewLeadingTrivia));
        }

        return resultNode;
    }

    public override SyntaxNode? VisitAwaitExpression(AwaitExpressionSyntax node)
    {
        bool nothingInFrontOfAwait = CheckNothingButTriviaInFront(node);

        SyntaxTriviaList? nodeNewLeadingTrivia = null;
        SyntaxTriviaList? expressionNewLeadingTrivia = null;

        if (nothingInFrontOfAwait)
        {
            string expectedIndentation = GetExpectedIndentation(node);
            _indentationCache[node] = expectedIndentation;

            nodeNewLeadingTrivia = ReformatLeadingTrivia(node, expectedIndentation);
            if (nodeNewLeadingTrivia is not null)
            {
                ChangesRequired = true;
            }
        }

        bool nothingInFrontOfExpression = CheckNothingButTriviaInFront(node.Expression);
        if (nothingInFrontOfExpression)
        {
            string expectedIndentation = GetExpectedIndentation(node);
            string expectedExpressionIndentation = expectedIndentation + _singleIndentation;
            _indentationCache[node.Expression] = expectedExpressionIndentation;

            expressionNewLeadingTrivia = ReformatLeadingTrivia(node.Expression, expectedExpressionIndentation);
            if (expressionNewLeadingTrivia is not null)
            {
                ChangesRequired = true;
            }
        }

        AwaitExpressionSyntax? resultNode = (AwaitExpressionSyntax?)base.VisitAwaitExpression(node);
        if (resultNode is null)
        {
            return null;
        }

        if (nodeNewLeadingTrivia is not null)
        {
            resultNode = resultNode.WithAwaitKeyword(resultNode.AwaitKeyword.WithLeadingTrivia(nodeNewLeadingTrivia));
        }

        if (expressionNewLeadingTrivia is not null)
        {
            resultNode = resultNode.WithExpression(resultNode.Expression.WithLeadingTrivia(expressionNewLeadingTrivia));
        }

        return resultNode;
    }

    public override SyntaxNode? VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
    {
        SyntaxTriviaList? nodeNewLeadingTrivia = null;
        SyntaxTriviaList? arrowTokenNewLeadingTrivia = null;
        SyntaxTriviaList? arrowTokenNewTrailingTrivia = null;
        SyntaxTriviaList? bodyNewLeadingTrivia = null;

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
            nodeNewLeadingTrivia = ReformatLeadingTrivia(node, expectedIndentationLeft);
            if (nodeNewLeadingTrivia is not null)
            {
                ChangesRequired = true;
            }
        }

        bool nothingInFrontOfMiddle = CheckNothingButTriviaInFront(node.ArrowToken);

        if (nothingInFrontOfMiddle)
        {
            expectedIndentationMiddle += _singleIndentation;
            expectedIndentationRight += _singleIndentation;

            arrowTokenNewLeadingTrivia = ReformatLeadingTrivia(node.ArrowToken, expectedIndentationMiddle);
            if (arrowTokenNewLeadingTrivia is not null)
            {
                ChangesRequired = true;
            }
        }

        if (!middleAndRightOnSameLine && node.Block is null)
        {
            expectedIndentationRight += _singleIndentation;
        }

        _indentationCache[node.Body] = expectedIndentationRight;

        if (middleAndRightOnSameLine && (multilineOnLeft || multilineInMiddle || multilineOnRight))
        {
            arrowTokenNewTrailingTrivia = node.ArrowToken.TrailingTrivia.AppendNewLine(_newLine);

            ChangesRequired = true;

            nothingInFrontOfRight = true;
        }
        else
        {
            nothingInFrontOfRight = CheckNothingButTriviaInFront(node.Body);
        }

        if (nothingInFrontOfRight)
        {
            bodyNewLeadingTrivia = ReformatLeadingTrivia(node.Body, expectedIndentationRight);
            if (bodyNewLeadingTrivia is not null)
            {
                ChangesRequired = true;
            }
        }

        SimpleLambdaExpressionSyntax? resultNode = (SimpleLambdaExpressionSyntax?)base.VisitSimpleLambdaExpression(node);
        if (resultNode is null)
        {
            return null;
        }

        if (nodeNewLeadingTrivia is not null)
        {
            resultNode = resultNode.WithLeadingTrivia(nodeNewLeadingTrivia);
        }

        if (arrowTokenNewLeadingTrivia is not null)
        {
            resultNode = resultNode.WithArrowToken(resultNode.ArrowToken.WithLeadingTrivia(arrowTokenNewLeadingTrivia));
        }

        if (arrowTokenNewTrailingTrivia is not null)
        {
            resultNode = resultNode.WithArrowToken(resultNode.ArrowToken.WithTrailingTrivia(arrowTokenNewTrailingTrivia));
        }

        if (bodyNewLeadingTrivia is not null)
        {
            resultNode = resultNode.WithBody(resultNode.Body.WithLeadingTrivia(bodyNewLeadingTrivia));
        }

        return resultNode;
    }

    public override SyntaxNode? VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
    {
        SyntaxTriviaList? nodeNewLeadingTrivia = null;
        SyntaxTriviaList? arrowTokenNewLeadingTrivia = null;
        SyntaxTriviaList? arrowTokenNewTrailingTrivia = null;
        SyntaxTriviaList? bodyNewLeadingTrivia = null;

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
            nodeNewLeadingTrivia = ReformatLeadingTrivia(node, expectedIndentationLeft);
            if (nodeNewLeadingTrivia is not null)
            {
                ChangesRequired = true;
            }
        }

        bool nothingInFrontOfMiddle = CheckNothingButTriviaInFront(node.ArrowToken);

        if (nothingInFrontOfMiddle)
        {
            expectedIndentationMiddle += _singleIndentation;
            expectedIndentationRight += _singleIndentation;

            arrowTokenNewLeadingTrivia = ReformatLeadingTrivia(node.ArrowToken, expectedIndentationMiddle);
            if (arrowTokenNewLeadingTrivia is not null)
            {
                ChangesRequired = true;
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
            arrowTokenNewTrailingTrivia = node.ArrowToken.TrailingTrivia.AppendNewLine(_newLine);

            ChangesRequired = true;

            nothingInFrontOfRight = true;
        }
        else
        {
            nothingInFrontOfRight = CheckNothingButTriviaInFront(node.Body);
        }

        if (nothingInFrontOfRight)
        {
            bodyNewLeadingTrivia = ReformatLeadingTrivia(node.Body, expectedIndentationRight);
            if (bodyNewLeadingTrivia is not null)
            {
                ChangesRequired = true;
            }
        }

        ParenthesizedLambdaExpressionSyntax? resultNode =
            (ParenthesizedLambdaExpressionSyntax?)base.VisitParenthesizedLambdaExpression(node);
        if (resultNode is null)
        {
            return null;
        }

        if (nodeNewLeadingTrivia is not null)
        {
            resultNode = resultNode.WithLeadingTrivia(nodeNewLeadingTrivia);
        }

        if (arrowTokenNewLeadingTrivia is not null)
        {
            resultNode = resultNode.WithArrowToken(resultNode.ArrowToken.WithLeadingTrivia(arrowTokenNewLeadingTrivia));
        }

        if (arrowTokenNewTrailingTrivia is not null)
        {
            resultNode = resultNode.WithArrowToken(resultNode.ArrowToken.WithTrailingTrivia(arrowTokenNewTrailingTrivia));
        }

        if (bodyNewLeadingTrivia is not null)
        {
            resultNode = resultNode.WithBody(resultNode.Body.WithLeadingTrivia(bodyNewLeadingTrivia));
        }

        return resultNode;
    }

    public override SyntaxNode? VisitBlock(BlockSyntax node)
    {
        SyntaxTriviaList? openBraceNewLeadingTrivia = null;
        SyntaxTriviaList? openBraceNewTrailingTrivia = null;
        List<(SyntaxNodeOrToken Element, SyntaxTriviaList NewLeadingTrivia)> replacements = [];
        SyntaxTriviaList? lastStatementNewTrailingTrivia = null;
        SyntaxTriviaList? closeBraceNewLeadingTrivia = null;

        string expectedIndentation = GetSelfIndentation(node) ?? GetParentIndentation(node) ?? _rootNodeIndentation;
        _indentationCache[node] = expectedIndentation;

        if (CheckNothingButTriviaInFront(node.OpenBraceToken))
        {
            openBraceNewLeadingTrivia = ReformatLeadingTrivia(node.OpenBraceToken, expectedIndentation);
            if (openBraceNewLeadingTrivia is not null)
            {
                ChangesRequired = true;
            }
        }

        TextSpan trimmedOpenParentTokenFullSpan = node.OpenBraceToken.GetTrimmedFullSpan();
        if (!CheckOnTheSameLine(node.SyntaxTree, trimmedOpenParentTokenFullSpan, node.CloseBraceToken.FullSpan))
        {
            if (node.Statements.Any())
            {
                if (CheckOnTheSameLine(node.SyntaxTree, trimmedOpenParentTokenFullSpan, node.Statements[0].FullSpan))
                {
                    openBraceNewTrailingTrivia = node.OpenBraceToken.TrailingTrivia.AppendNewLine(_newLine);
                    ChangesRequired = true;
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
                        SyntaxTriviaList? newLeadingTrivia = ReformatLeadingTrivia(child, expectedStatementIndentation);
                        if (newLeadingTrivia is not null)
                        {
                            replacements.Add((child, newLeadingTrivia.Value));
                            ChangesRequired = true;
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

                    lastStatementNewTrailingTrivia = lastStatement.GetTrailingTrivia().AppendNewLine(_newLine);
                }
                else
                {
                    // If parens are on the different lines but there are no arguments,
                    // then nothing should be in front apart from multiline comments attached to the open paren
                    openBraceNewTrailingTrivia =
                        (openBraceNewTrailingTrivia ?? node.OpenBraceToken.TrailingTrivia).AppendNewLine(_newLine);
                }

                ChangesRequired = true;
            }

            closeBraceNewLeadingTrivia =
                ReformatLeadingTrivia(node.CloseBraceToken, expectedIndentation + _singleIndentation, expectedIndentation);
            if (closeBraceNewLeadingTrivia is not null)
            {
                ChangesRequired = true;
            }
        }

        BlockSyntax? resultNode = (BlockSyntax?)base.VisitBlock(node);
        if (resultNode is null)
        {
            return null;
        }

        if (openBraceNewLeadingTrivia is not null)
        {
            resultNode = resultNode.WithOpenBraceToken(resultNode.OpenBraceToken.WithLeadingTrivia(openBraceNewLeadingTrivia));
        }

        if (openBraceNewTrailingTrivia is not null)
        {
            resultNode = resultNode.WithOpenBraceToken(resultNode.OpenBraceToken.WithTrailingTrivia(openBraceNewTrailingTrivia));
        }

        if (closeBraceNewLeadingTrivia is not null)
        {
            resultNode = resultNode.WithCloseBraceToken(resultNode.CloseBraceToken.WithLeadingTrivia(closeBraceNewLeadingTrivia));
        }

        if (lastStatementNewTrailingTrivia is not null)
        {
            StatementSyntax lastStatement = resultNode.Statements.Last();
            StatementSyntax newLastStatement = lastStatement.WithTrailingTrivia(lastStatementNewTrailingTrivia);
            resultNode = resultNode.ReplaceNode(lastStatement, newLastStatement);
        }

        foreach ((SyntaxNodeOrToken element, SyntaxTriviaList newLeadingTrivia) in replacements)
        {
            if (element.IsToken)
            {
                SyntaxToken syntaxToken = element.AsToken();
                resultNode = resultNode.ReplaceToken(syntaxToken, syntaxToken.WithLeadingTrivia(newLeadingTrivia));
            }
            else
            {
                SyntaxNode syntaxNode = element.AsNode()!;
                resultNode = resultNode.ReplaceNode(syntaxNode, syntaxNode.WithLeadingTrivia(newLeadingTrivia));
            }
        }

        return resultNode;
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

    private SyntaxTriviaList? ReformatLeadingTrivia(
        SyntaxNodeOrToken node,
        string expectedIndentation
    )
    {
        return ReformatLeadingTrivia(node, expectedIndentation, expectedIndentation);
    }

    private SyntaxTriviaList? ReformatLeadingTrivia(
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
            return new SyntaxTriviaList(SyntaxFactory.Whitespace(expectedLastIndentation));
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
            return new SyntaxTriviaList(newLeadingTrivia);
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
