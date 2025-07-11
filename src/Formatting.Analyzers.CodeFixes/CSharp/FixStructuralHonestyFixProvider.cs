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

        // string endOfLine = SyntaxTriviaAnalysis.DetermineEndOfLine(node).ToString();
        //
        // List<TextChange> textChanges = [];
        //
        // if ((bracesStyle & TargetBracesStyle.Opening) != 0)
        // {
        //     SyntaxToken nextToken = openNodeOrToken.GetNextToken();
        //
        //     int bracketLine = openNodeOrToken.GetSpanStartLine(cancellationToken);
        //     int nextTokenLine = nextToken.GetSpanStartLine(cancellationToken);
        //
        //     if (bracketLine == nextTokenLine)
        //     {
        //         string indentation = SyntaxTriviaAnalysis.GetIncreasedIndentation(node, configOptions, cancellationToken);
        //
        //         textChanges.Add(
        //             new TextChange(
        //                 TextSpan.FromBounds(openNodeOrToken.Span.End, nextToken.SpanStart),
        //                 endOfLine + indentation
        //             )
        //         );
        //     }
        // }
        //
        // if ((bracesStyle & TargetBracesStyle.Closing) != 0)
        // {
        //     SyntaxToken previousToken = closeNodeOrToken.GetPreviousToken();
        //
        //     int bracketLine = closeNodeOrToken.GetSpanStartLine(cancellationToken);
        //     int previousTokenLine = previousToken.GetSpanEndLine(cancellationToken);
        //
        //     if (bracketLine == previousTokenLine)
        //     {
        //         string indentation = SyntaxTriviaAnalysis.DetermineIndentation(node, searchInAccessors: false, cancellationToken).ToString();
        //
        //         textChanges.Add(
        //             new TextChange(
        //                 closeNodeOrToken.Span,
        //                 endOfLine + indentation + closeNodeOrToken
        //             )
        //         );
        //     }
        //     else
        //     {
        //         SyntaxTrivia listNodeIndent = SyntaxTriviaAnalysis.DetermineIndentation(node, searchInAccessors: false, cancellationToken);
        //         SyntaxTrivia bracketIndent = SyntaxTriviaAnalysis.DetermineIndentation(closeNodeOrToken, searchInAccessors: false, cancellationToken);
        //         if (listNodeIndent.Span.Length != bracketIndent.Span.Length)
        //         {
        //             TextSpan span =
        //                 (bracketIndent.Span.Length == 0) // there is no indentation
        //                     ? new TextSpan(closeNodeOrToken.Span.Start, 0)
        //                     : bracketIndent.Span;
        //
        //             textChanges.Add(
        //                 new TextChange(
        //                     span,
        //                     listNodeIndent.ToString()
        //                 )
        //             );
        //         }
        //     }
        // }
        //
        // return await document.WithTextChangesAsync(textChanges, cancellationToken).ConfigureAwait(false);

    }

    private static async Task<Document> FixInvocationExpressionAsync(
        Document document,
        InvocationExpressionSyntax node,
        SyntaxToken firstToken,
        CancellationToken cancellationToken
    )
    {
        AnalyzerConfigOptions configOptions = document.GetConfigOptions(node.SyntaxTree);
        IndentationAnalysis indentationAnalysis =
            SyntaxTriviaAnalysis.AnalyzeIndentation(node, configOptions, cancellationToken);

        ArgumentListSyntax argumentList = node.ArgumentList;

        TextLineCollection textLines = (await node.SyntaxTree.GetTextAsync(cancellationToken)).Lines;

        int indentationLength = indentationAnalysis.IndentationLength;
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
                IndentationFixingWalker.Create(
                    argumentList,
                    childrenIncreasedIndentation,
                    indentationAnalysis.GetSingleIndentation(),
                    cancellationToken
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
