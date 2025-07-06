// Copyright (c) .NET Foundation and Contributors. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Roslynator.CSharp;
using Roslynator.CSharp.CodeStyle;

namespace Roslynator.Formatting.CSharp;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FixStructuralHonestyAnalyzer : BaseDiagnosticAnalyzer
{
    private static ImmutableArray<DiagnosticDescriptor> _supportedDiagnostics;

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
    {
        get
        {
            if (_supportedDiagnostics.IsDefault)
            {
                Immutable.InterlockedInitialize(ref _supportedDiagnostics, DiagnosticRules.FixStructuralHonesty);
            }

            return _supportedDiagnostics;
        }
    }

    public override void Initialize(AnalysisContext context)
    {
        base.Initialize(context);

        context.RegisterSyntaxNodeAction(f => AnalyzeSimpleLambdaExpression(f), SyntaxKind.SimpleLambdaExpression);
        context.RegisterSyntaxNodeAction(f => AnalyzeParenthesizedLambdaExpression(f), SyntaxKind.ParenthesizedLambdaExpression);

        // context.RegisterSyntaxNodeAction(f => AnalyzeArgumentList(f), SyntaxKind.NewKeyword);
        // context.RegisterSyntaxNodeAction(f => AnalyzeArgumentList(f), SyntaxKind.AnonymousMethodExpression);
        // context.RegisterSyntaxNodeAction(f => AnalyzeArgumentList(f), SyntaxKind.AnonymousObjectCreationExpression);
        // context.RegisterSyntaxNodeAction(f => AnalyzeArgumentList(f), SyntaxKind.ArrayCreationExpression);
        // context.RegisterSyntaxNodeAction(f => AnalyzeArgumentList(f), SyntaxKind.ArrayInitializerExpression);
        // context.RegisterSyntaxNodeAction(f => AnalyzeArgumentList(f), SyntaxKind.ArrowExpressionClause);
        // context.RegisterSyntaxNodeAction(f => AnalyzeArgumentList(f), SyntaxKind.FromClause);
        // context.RegisterSyntaxNodeAction(f => AnalyzeArgumentList(f), SyntaxKind.SelectClause);
        // context.RegisterSyntaxNodeAction(f => AnalyzeArgumentList(f), SyntaxKind.SingleLineRawStringLiteralToken);
        // context.RegisterSyntaxNodeAction(f => AnalyzeArgumentList(f), SyntaxKind.MultiLineRawStringLiteralToken);
        // context.RegisterSyntaxNodeAction(f => AnalyzeArgumentList(f), SyntaxKind.Utf8StringLiteralExpression);
        //context.RegisterSyntaxNodeAction(f => AnalyzeArgumentList(f), SyntaxKind.InterpolatedVerbatimStringStartToken);
        //context.RegisterSyntaxNodeAction(f => AnalyzeArgumentList(f), SyntaxKind.InterpolatedStringText);
        // context.RegisterSyntaxNodeAction(f => AnalyzeArgumentList(f), SyntaxKind.NameColon);
        // context.RegisterSyntaxNodeAction(f => AnalyzeArgumentList(f), SyntaxKind.ObjectCreationExpression);
        // context.RegisterSyntaxNodeAction(f => AnalyzeArgumentList(f), SyntaxKind.ObjectInitializerExpression);
        // context.RegisterSyntaxNodeAction(f => AnalyzeArgumentList(f), SyntaxKind.QueryExpression);
        // context.RegisterSyntaxNodeAction(f => AnalyzeArgumentList(f), SyntaxKind.SwitchExpression);
        // context.RegisterSyntaxNodeAction(f => AnalyzeArgumentList(f), SyntaxKind.WithKeyword);
        // context.RegisterSyntaxNodeAction(f => AnalyzeArgumentList(f), SyntaxKind.WithInitializerExpression);
        // context.RegisterSyntaxNodeAction(f => AnalyzeArgumentList(f), SyntaxKind.WithInitializerExpression);

        // context.RegisterSyntaxNodeAction(f => AnalyzeArgumentList(f), SyntaxKind.ArgumentList);
        // // context.RegisterSyntaxNodeAction(f => AnalyzeBracketedArgumentList(f), SyntaxKind.BracketedArgumentList);

        // context.RegisterSyntaxNodeAction(f => AnalyzeTupleType(f), SyntaxKind.TupleType);
        // context.RegisterSyntaxNodeAction(f => AnalyzeTupleExpression(f), SyntaxKind.TupleElement);
        // context.RegisterSyntaxNodeAction(f => AnalyzeTupleExpression(f), SyntaxKind.TupleExpression);
#if ROSLYN_4_7
        // context.RegisterSyntaxNodeAction(f => AnalyzeCollectionExpression(f), SyntaxKind.CollectionExpression);
#endif
    }

    private void AnalyzeSimpleLambdaExpression(SyntaxNodeAnalysisContext context)
    {
        CancellationToken cancellationToken = context.CancellationToken;
        SimpleLambdaExpressionSyntax lambda = (SimpleLambdaExpressionSyntax)context.Node;

        if (lambda.IsSingleLine(cancellationToken: cancellationToken))
        {
            return;
        }

        TextSpan span = lambda.GetSpan();

        DiagnosticHelpers.ReportDiagnostic(
            context,
            DiagnosticRules.FixStructuralHonesty,
            Location.Create(
                lambda.SyntaxTree,
                span
            )
        );
    }

    private void AnalyzeParenthesizedLambdaExpression(SyntaxNodeAnalysisContext context)
    {
        ParenthesizedLambdaExpressionSyntax lambda = (ParenthesizedLambdaExpressionSyntax)context.Node;
    }

    private static void AnalyzeStructuralHonesty(SyntaxNodeAnalysisContext context)
    {
        TargetBracesStyle bracesStyle = context.GetTargetBracesStyle();

        if (bracesStyle == TargetBracesStyle.None)
        {
            return;
        }

        CancellationToken cancellationToken = context.CancellationToken;

        var binaryExpression = (BinaryExpressionSyntax)context.Node;

        ExpressionSyntax left = binaryExpression.Left;

        if (left.IsMissing)
        {
            return;
        }

        ExpressionSyntax right = binaryExpression.Right;

        if (right.IsMissing)
        {
            return;
        }

        SyntaxToken firstToken = binaryExpression.GetFirstToken();
        SyntaxToken lastToken = binaryExpression.GetLastToken();

        IEnumerable<(SyntaxNode Parent, SyntaxToken OpenBracket, SyntaxToken CloseBracket)> nodesWithBrackets =
            FindNodesWithBrackets(binaryExpression);

        SemanticModel semanticModel = context.SemanticModel;

        foreach ((SyntaxNode parent, SyntaxToken openBracket, SyntaxToken closeBracket) in nodesWithBrackets)
        {
            if (openBracket.GetSpanStartLine(cancellationToken) == closeBracket.GetSpanEndLine(cancellationToken))
            {
                continue;
            }

            if (ShouldFixOpeningBracket(bracesStyle, openBracket, firstToken, cancellationToken)
                || ShouldFixClosingBracket(bracesStyle, parent, closeBracket, lastToken, cancellationToken)
            )
            {
                TextSpan span = TextSpan.FromBounds(openBracket.SpanStart, closeBracket.Span.End);

                Diagnostic? existingDiagnostic =
                    semanticModel.GetDiagnostic(
                        DiagnosticIdentifiers.FixStructuralHonesty,
                        span,
                        cancellationToken
                    );

                if (existingDiagnostic is not null)
                {
                    continue;
                }

                DiagnosticHelpers.ReportDiagnostic(
                    context,
                    DiagnosticRules.FixStructuralHonesty,
                    Location.Create(
                        parent.SyntaxTree,
                        span
                    ),
                    GetTitle(parent)
                );
            }
        }
    }

    private static IEnumerable<(SyntaxNode Parent, SyntaxToken OpenBracket, SyntaxToken CloseBracket)>
        FindNodesWithBrackets(SyntaxNode syntaxNode)
    {
        // Braced nodes can be in braces and in braces and ...
        // if the node itself is not ParenthesizedExpressionSyntax or the first parent doesn't have parentheses
        // then further processing should be stopped, otherwise multiple diagnostics for the same place will be reported

        SyntaxNode? parent = syntaxNode;
        int depth = 0;
        while (parent is not null)
        {
            var stop = false;

            switch (parent)
            {
                case ParenthesizedExpressionSyntax parenthesizedExpressionSyntax:
                    yield return (
                        parent,
                        parenthesizedExpressionSyntax.OpenParenToken,
                        parenthesizedExpressionSyntax.CloseParenToken
                    );
                    break;
                case IfStatementSyntax ifStatement:
                    yield return (
                        parent,
                        ifStatement.OpenParenToken,
                        ifStatement.CloseParenToken
                    );
                    // If-statement is considered as a final node.
                    stop = true;
                    break;
                case WhileStatementSyntax whileStatement:
                    yield return (
                        parent,
                        whileStatement.OpenParenToken,
                        whileStatement.CloseParenToken
                    );
                    // While-statement is considered as a final node.
                    stop = true;
                    break;
                case DoStatementSyntax doWhileStatement:
                    yield return (
                        parent,
                        doWhileStatement.OpenParenToken,
                        doWhileStatement.CloseParenToken
                    );
                    // Do-while-statement is considered as a final node.
                    stop = true;
                    break;
                default:
                    if (depth > 0)
                    {
                        stop = true;
                    }
                    break;
            }

            depth++;

            if (stop)
            {
                break;
            }

            parent = parent.FirstAncestor<SyntaxNode>();
        }
    }

    private static string GetTitle(SyntaxNode node)
    {
        return node.Kind() switch
        {
            SyntaxKind.IfStatement
                => "an 'if' statement",

            SyntaxKind.ParenthesizedExpression
                => "a parenthesized expression",

            SyntaxKind.WhileStatement
                => "a 'while' statement",

            SyntaxKind.DoStatement
                => "a 'do-while' statement",

            _ => throw new InvalidOperationException()
        };
    }

    private static bool ShouldFixOpeningBracket(
        TargetBracesStyle bracesStyle,
        SyntaxToken leftBracket,
        SyntaxToken first,
        CancellationToken cancellationToken
    )
    {
        if ((bracesStyle & TargetBracesStyle.Opening) == 0)
        {
            return false;
        }

        return leftBracket.GetSpanStartLine(cancellationToken) == first.GetSpanStartLine(cancellationToken);
    }

    private static bool ShouldFixClosingBracket(
        TargetBracesStyle bracesStyle,
        SyntaxNode listNode,
        SyntaxToken rightBracket,
        SyntaxToken last,
        CancellationToken cancellationToken
    )
    {
        if ((bracesStyle & TargetBracesStyle.Closing) == 0)
        {
            return false;
        }

        if (rightBracket.GetSpanEndLine(cancellationToken) == last.GetSpanEndLine(cancellationToken))
        {
            return true;
        }

        SyntaxTrivia listNodeIndent =
            SyntaxTriviaAnalysis.DetermineIndentation(listNode, searchInAccessors: false, cancellationToken);
        SyntaxTrivia bracketIndent =
            SyntaxTriviaAnalysis.DetermineIndentation(rightBracket, searchInAccessors: false, cancellationToken);

        return listNodeIndent.Span.Length != bracketIndent.Span.Length;
    }
}
