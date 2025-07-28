using Microsoft.CodeAnalysis.CSharp;

namespace Roslynator.CSharp.Analysis.StructuralHonesty.SyntaxRewriters;

public abstract class StructuralHonestySyntaxRewriter : CSharpSyntaxRewriter
{
    public bool DoAnalysisOnly { get; set; }
    public bool HasStructuralHonestyIssues { get; set; }

    protected StructuralHonestySyntaxRewriter(string expectedIndentation, string singleIndentation)
    {
        throw new System.NotImplementedException();
    }
}
