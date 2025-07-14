#nullable enable

using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Roslynator.CSharp.SyntaxWalkers;

/// <summary>
/// Bypass the whole child nodes, if a child node has leading trivia that starts at the beginning of a line,
/// then the walker checks if it has expected indentation.
/// </summary>
public sealed class IndentationAnalyzingWalker : CSharpSyntaxWalker
{
    private readonly SyntaxNode _parent;

    private readonly string _expectedIndentation;
    private readonly string _singleIndentation;
    private readonly TextLineCollection _textLines;

    private readonly Dictionary<SyntaxNode, string> _indentationCache = new();

    public bool Valid { get; private set; } = true;

    public IndentationAnalyzingWalker(
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
        _indentationCache[parent] = expectedIndentation;
    }

    public override void Visit(SyntaxNode? node)
    {
        if (node is null)
        {
            return;
        }

        string expectedIndentation = _expectedIndentation;
        SyntaxNode? parent = node.Parent;
        while (parent is not null)
        {
            if (_indentationCache.TryGetValue(parent, out string? indentation))
            {
                // Blocks should have indentation of the parent node
                if (node is BlockSyntax)
                {
                    expectedIndentation = indentation;
                    break;
                }

                expectedIndentation = indentation + _singleIndentation;
                break;
            }

            parent = parent.Parent;
        }

        (bool applicable, bool valid) = CheckIndentation(node, expectedIndentation);
        if (applicable && !valid)
        {
            Valid = false;
            return;
        }

        if (node is BlockSyntax block && block.IsMultiLine())
        {
            // Not sure why, but open and close braces of a block syntax are not recognized neither as Token nor as Node,
            // so we need to handle them explicitly
            (applicable, valid) = CheckIndentation(block.CloseBraceToken, expectedIndentation);
            if (applicable && !valid)
            {
                Valid = false;
                return;
            }
        }

        if (applicable && !_indentationCache.ContainsKey(node))
        {
            _indentationCache[node] = expectedIndentation;
        }

        base.Visit(node);
    }

    private (bool Applicable, bool Valid) CheckIndentation(SyntaxNodeOrToken nodeOrToken, string expectedIndentation)
    {
        // Argument syntax is kind of a virtual wrapper over the real argument.
        // Only the real argument should be checked for indentation.
        if (nodeOrToken.IsNode && nodeOrToken.AsNode() is ArgumentSyntax)
        {
            return (false, false);
        }

        SyntaxTriviaList leadingTrivia = nodeOrToken.GetLeadingTrivia();

        LinePosition nodeLinePosition = _textLines.GetLinePosition(nodeOrToken.SpanStart);
        bool nodeIsTheFirstCharOnLine = nodeLinePosition.Character == 0;

        bool triviaExists = leadingTrivia.Any();
        if (!triviaExists && !nodeIsTheFirstCharOnLine)
        {
            return (false, false);
        }

        LinePosition triviaLinePosition = _textLines.GetLinePosition(leadingTrivia.Span.Start);

        // If trivia doesn't start from the first character of the line, then it is not a valid case
        if (triviaLinePosition.Character > 0)
        {
            return (false, false);
        }

        // If trivia already has expected indentation length then return true
        int triviaLength = leadingTrivia.Span.Length;
        if (triviaLength == expectedIndentation.Length)
        {
            return (true, true);
        }

        // Applicable case but trivia has incorrect indentation
        return (true, false);
    }
}
