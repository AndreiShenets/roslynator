// Copyright (c) .NET Foundation and Contributors. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Roslynator.CSharp;

namespace Roslynator.Formatting.CodeFixes.CSharp;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(FixStructuralHonestyFixProvider))]
[Shared]
public sealed class FixStructuralHonestyFixProvider : BaseCodeFixProvider
{
    private const string Title = "Fix structural honesty";

    public override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArray.Create(DiagnosticIdentifiers.FixStructuralHonesty);

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode? root = await context.GetSyntaxRootAsync().ConfigureAwait(false);

        if (
            !TryFindFirstAncestorOrSelf(
                root,
                context.Span,
                out SyntaxNode node,
                predicate: f =>
                {
                    switch (f.Kind())
                    {
                        case SyntaxKind.SimpleLambdaExpression:
                        case SyntaxKind.ParenthesizedLambdaExpression:
                        case SyntaxKind.InvocationExpression:
                        case SyntaxKind.AwaitExpression:
                            return true;
                        default:
                            return false;
                    }
                }
            )
        )
        {
            return;
        }

        Document document = context.Document;
        Diagnostic diagnostic = context.Diagnostics[0];

        CodeAction codeAction =
            CodeAction.Create(
                Title,
                ct => Fix(document, node, ct),
                GetEquivalenceKey(diagnostic)
            );

        context.RegisterCodeFix(codeAction, diagnostic);
    }

    private static async Task<Document> Fix(
        Document document,
        SyntaxNode node,
        CancellationToken cancellationToken
    )
    {
        // Indentation analysis should be done on the parent of the node.
        // Weather the indentation is correct, we can say only relatively to parent.
        if (node.Parent is null)
        {
            return document;
        }

        AnalyzerConfigOptions configOptions = document.GetConfigOptions(node.SyntaxTree);
        IndentationAnalysis indentationAnalysis =
            SyntaxTriviaAnalysis.AnalyzeIndentation(node.Parent, configOptions, cancellationToken);

        // Assumption:
        // If we are here, then the node is multilined we can expect that the first and last tokens are on the different lines.
        switch (node.Kind())
        {
            case SyntaxKind.SimpleLambdaExpression:
                break;
            case SyntaxKind.ParenthesizedLambdaExpression:
                break;
            case SyntaxKind.InvocationExpression:
                InvocationExpressionSyntax invocationExpression = (InvocationExpressionSyntax)node;
                document =
                    await FixInvocationExpressionAsync(
                        document,
                        invocationExpression,
                        indentationAnalysis,
                        invocationExpression.GetFirstToken(),
                        cancellationToken
                    )
                        .ConfigureAwait(false);
                break;
            case SyntaxKind.AwaitExpression
                when node.ChildNodes().FirstOrDefault() is InvocationExpressionSyntax invocationExpressionFromAwaitExpression:
            {
                AwaitExpressionSyntax awaitExpression = (AwaitExpressionSyntax)node;
                document =
                    await FixInvocationExpressionAsync(
                        document,
                        invocationExpressionFromAwaitExpression,
                        indentationAnalysis,
                        awaitExpression.GetFirstToken(),
                        cancellationToken
                    )
                        .ConfigureAwait(false);
                break;
            }
            default:
                throw new InvalidOperationException($"Unexpected node kind: {node.Kind()}");
        }

        return document;
    }

    private static async Task<Document> FixInvocationExpressionAsync(
        Document document,
        InvocationExpressionSyntax node,
        IndentationAnalysis indentationAnalysis,
        SyntaxToken firstToken,
        CancellationToken cancellationToken
    )
    {
        ArgumentListSyntax argumentList = node.ArgumentList;

        SourceText sourceText = await node.SyntaxTree.GetTextAsync(cancellationToken);
        TextLineCollection textLines = sourceText.Lines;

        string increasedIndentation = indentationAnalysis.GetIncreasedIndentation();
        string childrenIncreasedIndentation = increasedIndentation + indentationAnalysis.GetSingleIndentation();

        List<TextChange> textChanges = [];

        if (SyntaxTriviaAnalysis.CheckNothingButTriviaInFrontOnTheSameLine(firstToken, cancellationToken) is false)
        {
            textChanges.Add(
                CodeFixHelpers.GetNewLineBeforeTextChange(firstToken, increasedIndentation)
            );
        }
        else
        {
            (_, IReadOnlyList<TextChange> changes) =
                CodeFixHelpers.FixIndentationNonHarmfully(firstToken, increasedIndentation, textLines);
            if (changes.Count > 0)
            {
                textChanges.AddRange(changes);
            }
        }

        SyntaxToken lastTokenToBeWrapped = argumentList.CloseParenToken;

        if (SyntaxTriviaAnalysis.CheckNothingButTriviaInFrontOnTheSameLine(lastTokenToBeWrapped, cancellationToken) is false)
        {
            textChanges.Add(
                CodeFixHelpers.GetNewLineBeforeTextChange(lastTokenToBeWrapped, increasedIndentation)
            );
        }
        else
        {
            (_, IReadOnlyList<TextChange> changes) =
                CodeFixHelpers.FixIndentationNonHarmfully(lastTokenToBeWrapped, increasedIndentation, textLines);
            if (changes.Count > 0)
            {
                textChanges.AddRange(changes);
            }
        }

        if (argumentList.ChildNodes().Any())
        {
            SyntaxNode firstChild = argumentList.ChildNodes().First();

            int nodeStartLine = node.GetSpanStartLine(cancellationToken);
            int firstChildStartLine = firstChild.GetSpanStartLine(cancellationToken);
            if (nodeStartLine == firstChildStartLine)
            {
                // If the first child is on the same line as the node, then we need to add a new line before it.
                textChanges.Add(
                    CodeFixHelpers.GetNewLineBeforeTextChange(firstChild.GetFirstToken(), childrenIncreasedIndentation)
                );
            }

            IndentationFixingWalker fixingWalker =
                new(
                    argumentList,
                    childrenIncreasedIndentation,
                    indentationAnalysis.GetSingleIndentation(),
                    textLines
                );

            fixingWalker.Visit(argumentList);

            if (fixingWalker.TextChanges.Count > 0)
            {
                textChanges.AddRange(fixingWalker.TextChanges);
            }
        }

        if (textChanges.Count == 0)
        {
            return document;
        }

        document = await document.WithTextChangesAsync(textChanges, cancellationToken).ConfigureAwait(false);

        return document;
    }
}
