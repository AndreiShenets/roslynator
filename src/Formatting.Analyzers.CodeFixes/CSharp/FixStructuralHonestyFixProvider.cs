// Copyright (c) .NET Foundation and Contributors. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

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
using Roslynator.CSharp.SyntaxWalkers;

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
            return document;
        }

        AnalyzerConfigOptions configOptions = document.GetConfigOptions(node.SyntaxTree);
        IndentationAnalysis indentationAnalysis =
            SyntaxTriviaAnalysis.AnalyzeIndentation(node.Parent, configOptions, cancellationToken);

        SourceText sourceText = await node.SyntaxTree.GetTextAsync(cancellationToken);
        TextLineCollection textLines = sourceText.Lines;

        bool rootExpression =
            node.Parent is GlobalStatementSyntax
                || (node.Parent is ExpressionStatementSyntax expression && expression.Parent is GlobalStatementSyntax);

        string expectedIndentation =
            rootExpression
                ? string.Empty
                : indentationAnalysis.GetIncreasedIndentation();

        string singleIndentation = indentationAnalysis.GetSingleIndentation();

        IndentationAnalyzingWalker walker =
            new(
                node.SyntaxTree,
                expectedIndentation,
                singleIndentation,
                textLines,
                static (walker, textChange) =>
                {
                    //walker.RequiredChanges.Add(textChange);
                    return false; // Do not stop the walker.
                }
            );

        walker.Visit(node);

        if (walker.RequiredChanges.Count == 0)
        {
            return document;
        }

        return await document.WithTextChangesAsync(walker.RequiredChanges, cancellationToken).ConfigureAwait(false);
    }
}
