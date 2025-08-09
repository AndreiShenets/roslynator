// Copyright (c) .NET Foundation and Contributors. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Roslynator.CSharp;
using Roslynator.CSharp.Analysis.StructuralHonesty;

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
            static f => AnalyzeNode(f),
            SyntaxKind.SimpleLambdaExpression,
            SyntaxKind.ParenthesizedLambdaExpression,
            SyntaxKind.InvocationExpression,
            SyntaxKind.AwaitExpression,
            SyntaxKind.AnonymousObjectCreationExpression,
            SyntaxKind.ArrayCreationExpression,
            SyntaxKind.ObjectCreationExpression,
            SyntaxKind.ImplicitArrayCreationExpression,
            SyntaxKind.StackAllocArrayCreationExpression,
            SyntaxKind.ImplicitObjectCreationExpression,
            SyntaxKind.ImplicitStackAllocArrayCreationExpression,
            SyntaxKind.WithInitializerExpression,
            SyntaxKind.AnonymousMethodExpression,
            SyntaxKind.QueryExpression,
#if ROSLYN_4_7
            SyntaxKind.CollectionExpression,
#endif
            SyntaxKind.SwitchExpression,
            SyntaxKind.TupleExpression,
            SyntaxKind.ConditionalExpression,
            SyntaxKind.MultiLineRawStringLiteralToken,
            SyntaxKind.InterpolatedStringExpression,

            SyntaxKind.EqualsValueClause,
            SyntaxKind.AddAssignmentExpression,
            SyntaxKind.SubtractAssignmentExpression,
            SyntaxKind.MultiplyAssignmentExpression,
            SyntaxKind.DivideAssignmentExpression,
            SyntaxKind.ModuloAssignmentExpression,
            SyntaxKind.AndAssignmentExpression,
            SyntaxKind.ExclusiveOrAssignmentExpression,
            SyntaxKind.OrAssignmentExpression,
            SyntaxKind.LeftShiftAssignmentExpression,
            SyntaxKind.RightShiftAssignmentExpression,
            SyntaxKind.CoalesceAssignmentExpression,
            SyntaxKind.UnsignedRightShiftAssignmentExpression,

            SyntaxKind.ClassDeclaration,
            SyntaxKind.ParenthesizedExpression,

            SyntaxKind.SimpleMemberAccessExpression,
            SyntaxKind.ConditionalAccessExpression,
            SyntaxKind.MemberBindingExpression,

            SyntaxKind.ArgumentList
        );
    }

    private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        CancellationToken cancellationToken = context.CancellationToken;
        SyntaxNode node = context.Node;

        AnalyzerConfigOptions configOptions = context.GetConfigOptions();
        bool changesRequired = StructuralHonesty.Analyze(node, configOptions, cancellationToken);

        if (!changesRequired)
        {
            return;
        }

        TextSpan span = node.GetSpan();

        bool absorbingDiagnosticExists =
            node.SyntaxTree.GetDiagnostics()
                .Any(
                    d =>
                        d.Id == DiagnosticRules.FixStructuralHonesty.Id
                        && d.Location.SourceSpan.Contains(span)
                );

        if (absorbingDiagnosticExists)
        {
            return;
        }

        DiagnosticHelpers.ReportDiagnostic(
            context,
            DiagnosticRules.FixStructuralHonesty,
            Location.Create(node.SyntaxTree, span)
        );
    }
}
