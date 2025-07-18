// Copyright (c) .NET Foundation and Contributors. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Roslynator.CSharp;
using Roslynator.CSharp.SyntaxWalkers;

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

        context.RegisterSyntaxNodeAction(
            f => AnalyzeNode(f),
            SyntaxKind.SimpleLambdaExpression,
            SyntaxKind.ParenthesizedLambdaExpression,
            SyntaxKind.InvocationExpression
        );
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

    private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        CancellationToken cancellationToken = context.CancellationToken;
        SyntaxNode node = context.Node;

        if (node.IsSingleLine(cancellationToken: cancellationToken))
        {
            return;
        }

        node =
            node switch
            {
                // Correction of the node as for awaitables the whole awaitable expression should be analyzed
                InvocationExpressionSyntax invocation when invocation.Parent is AwaitExpressionSyntax
                    => invocation.Parent,
                _ => node
            };

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
            return;
        }

        SourceText sourceText = node.SyntaxTree.GetText(cancellationToken);
        TextLineCollection textLines = sourceText.Lines;

        AnalyzerConfigOptions configOptions = context.GetConfigOptions();
        IndentationAnalysis indentationAnalysis =
            SyntaxTriviaAnalysis.AnalyzeIndentation(parent, configOptions, cancellationToken);

        bool issueFound = false;

        IndentationAnalyzingWalker walker =
            new(
                node.SyntaxTree,
                indentationAnalysis.GetIncreasedIndentation(),
                indentationAnalysis.GetSingleIndentation(),
                textLines,
                (_, _) => issueFound = true // this also return true to stop the walker after the first issue
            );

        walker.Visit(node);

        if (!issueFound)
        {
            return;
        }

        TextSpan span = node.GetSpan();

        DiagnosticHelpers.ReportDiagnostic(
            context,
            DiagnosticRules.FixStructuralHonesty,
            Location.Create(node.SyntaxTree, span)
        );
    }
}
