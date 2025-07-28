#nullable enable

namespace Roslynator.CSharp.Analysis.StructuralHonesty.SyntaxRewriters;

public sealed class IndentingStructuralHonestySyntaxRewriter : StructuralHonestySyntaxRewriter
{
    public IndentingStructuralHonestySyntaxRewriter(string expectedIndentation, string singleIndentation)
        : base(expectedIndentation, singleIndentation)
    {
        throw new System.NotImplementedException();
    }
}