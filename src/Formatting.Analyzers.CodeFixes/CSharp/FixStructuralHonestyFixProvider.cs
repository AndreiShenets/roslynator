// Copyright (c) .NET Foundation and Contributors. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Roslynator.CSharp.Analysis.StructuralHonesty;

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
                        case SyntaxKind.AnonymousObjectCreationExpression:
                        case SyntaxKind.ArrayCreationExpression:
                        case SyntaxKind.ObjectCreationExpression:
                        case SyntaxKind.ImplicitArrayCreationExpression:
                        case SyntaxKind.StackAllocArrayCreationExpression:
                        case SyntaxKind.ImplicitObjectCreationExpression:
                        case SyntaxKind.ImplicitStackAllocArrayCreationExpression:
                        case SyntaxKind.WithInitializerExpression:
                        case SyntaxKind.WithExpression:
                        case SyntaxKind.AnonymousMethodExpression:
                        case SyntaxKind.QueryExpression:
                        case SyntaxKind.CollectionExpression:
                        case SyntaxKind.SwitchExpression:
                        case SyntaxKind.TupleExpression:
                        case SyntaxKind.ConditionalExpression:
                        case SyntaxKind.MultiLineRawStringLiteralToken:
                        case SyntaxKind.InterpolatedVerbatimStringStartToken:
                        case SyntaxKind.InterpolatedStringExpression:

                        case SyntaxKind.EqualsValueClause:
                        case SyntaxKind.AddAssignmentExpression:
                        case SyntaxKind.SubtractAssignmentExpression:
                        case SyntaxKind.MultiplyAssignmentExpression:
                        case SyntaxKind.DivideAssignmentExpression:
                        case SyntaxKind.ModuloAssignmentExpression:
                        case SyntaxKind.AndAssignmentExpression:
                        case SyntaxKind.ExclusiveOrAssignmentExpression:
                        case SyntaxKind.OrAssignmentExpression:
                        case SyntaxKind.LeftShiftAssignmentExpression:
                        case SyntaxKind.RightShiftAssignmentExpression:
                        case SyntaxKind.CoalesceAssignmentExpression:
                        case SyntaxKind.SimpleAssignmentExpression:
                        case SyntaxKind.UnsignedRightShiftAssignmentExpression:
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
        AnalyzerConfigOptions configOptions = document.GetConfigOptions(node.SyntaxTree);
        SyntaxNode? fixedNode = StructuralHonesty.Fix(node, configOptions, cancellationToken);

        if (fixedNode is null || ReferenceEquals(node, fixedNode))
        {
            // No changes required.
            return document;
        }

        return await document.ReplaceNodeAsync(node, fixedNode, cancellationToken).ConfigureAwait(false);
    }
}
