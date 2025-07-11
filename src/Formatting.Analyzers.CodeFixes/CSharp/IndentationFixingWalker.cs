#nullable enable

using System.Collections.Generic;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Roslynator.CSharp;

namespace Roslynator.Formatting.CodeFixes.CSharp;

/// <summary>
/// Bypass the whole child nodes, if a child node has leading trivia that starts at the beginning of a line,
/// then the front trivial is sized to the passed indentation size.
/// Edge case: The trivia in front contains a comment that is longer than the expected indentation,
/// in this case nothing should happen, it is a problem of a user to fix that case
/// </summary>
public sealed class IndentationFixingWalker : CSharpSyntaxWalker
{
    public static IndentationFixingWalker Create(
        SyntaxNode parent,
        string expectedIndentation,
        string singleIndentation,
        CancellationToken cancellationToken
    )
    {
        SyntaxTree syntaxTree = parent.SyntaxTree;

        return new IndentationFixingWalker(
            parent,
            expectedIndentation,
            singleIndentation,
            syntaxTree.GetText(cancellationToken).Lines
        );
    }

    private readonly SyntaxNode _parent;

    private readonly string _expectedIndentation;
    private readonly string _singleIndentation;
    private readonly TextLineCollection _textLines;

    private readonly Dictionary<SyntaxNode, string> _indentationCache = new();

    public List<TextChange> TextChanges { get; } = [];

    private IndentationFixingWalker(
        SyntaxNode parent,
        string expectedIndentation,
        string singleIndentation,
        TextLineCollection textLines
    )
    {
        _parent = parent;
        _expectedIndentation = expectedIndentation;
        _singleIndentation = singleIndentation;
        _textLines = textLines;
    }

    public override void Visit(SyntaxNode? node)
    {
        if (node is null)
        {
            return;
        }

        if (ReferenceEquals(node, _parent))
        {
            base.Visit(node);
            return;
        }

        string expectedIndentation = _expectedIndentation;
        SyntaxNode? parent = node.Parent;
        while (parent is not null)
        {
            if (_indentationCache.TryGetValue(parent, out string? indentation))
            {
                expectedIndentation = indentation + _singleIndentation;
                break;
            }

            parent = parent.Parent;
        }

        bool applicable = FixChildIndentationNonHarmfully(node, expectedIndentation);

        if (node is BlockSyntax block && block.IsMultiLine())
        {
            // Not sure why, but open and close braces of a block syntax are not recognized neither as Token nor as Node,
            // so we need to handle them explicitly
            applicable |= FixChildIndentationNonHarmfully(block.CloseBraceToken, expectedIndentation);
        }

        if (applicable && !_indentationCache.ContainsKey(node))
        {
            _indentationCache[node] = expectedIndentation;
        }

        base.Visit(node);
    }

    private bool FixChildIndentationNonHarmfully(SyntaxNodeOrToken nodeOrToken, string expectedIndentation)
    {
        (bool applicable, IReadOnlyList<TextChange> changes) =
            CodeFixHelpers.FixIndentationNonHarmfully(nodeOrToken, expectedIndentation, _textLines);

        if (changes.Count > 0)
        {
            TextChanges.AddRange(changes);
        }

        return applicable;
    }
}
