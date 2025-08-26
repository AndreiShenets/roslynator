// Copyright (c) .NET Foundation and Contributors. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Roslynator.Formatting.CodeFixes.CSharp;
using Roslynator.Testing.CSharp;
using Xunit;

namespace Roslynator.Formatting.CSharp.Tests;

public class RCS1271FixStructuralHonestyTests :
    AbstractCSharpDiagnosticVerifier<FixStructuralHonestyAnalyzer, FixStructuralHonestyFixProvider>
{
    public override DiagnosticDescriptor Descriptor { get; } = DiagnosticRules.FixStructuralHonesty;

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task AwaitExpression_Method_SingleLine()
    {
        await VerifyNoDiagnosticAsync(
            """
            using System;
            using System.Threading.Tasks;

            int result = await MyMethodAsync();

            Task<int> MyMethodAsync() => Task.FromResult(1);
            """,
            options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task AwaitExpression_Method_SingleLine_with_comment_in_front()
    {
        await VerifyNoDiagnosticAsync(
            """
            using System;
            using System.Threading.Tasks;

            int result = /*comment*/ await MyMethodAsync();

            Task<int> MyMethodAsync() => Task.FromResult(1);
            """,
            options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task AwaitExpression_Method_SingleLine_next_line_formatted()
    {
        await VerifyNoDiagnosticAsync(
            """
            using System;
            using System.Threading.Tasks;

            int result = 
                await MyMethodAsync();

            Task<int> MyMethodAsync() => Task.FromResult(1);
            """,
            options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task AwaitExpression_Method_SingleLine_next_line()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System;
            using System.Threading.Tasks;

            int result [|=
            await MyMethodAsync()|];

            Task<int> MyMethodAsync() => Task.FromResult(1);
            """,
            """
            using System;
            using System.Threading.Tasks;

            int result =
                await MyMethodAsync();

            Task<int> MyMethodAsync() => Task.FromResult(1);
            """,
            options: Options.WithCompilationOptions(
                Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
            )
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task AwaitExpression_Method_SimpleMultiline_closing_paren_on_the_same_line()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System;
            using System.Threading.Tasks;

            int result [|=
                [|await [|MyMethodAsync[|(
                    1)|]|]|]|];

            Task<int> MyMethodAsync(int i)=> Task.FromResult(1);
            """,
            """
            using System;
            using System.Threading.Tasks;

            int result =
                await MyMethodAsync(
                    1
                );

            Task<int> MyMethodAsync(int i)=> Task.FromResult(1);
            """,
            options: Options.WithCompilationOptions(
                Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
            )
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task AwaitExpression_Method_SimpleMultiline_to_be_wrapped_and_closing_paren_on_the_same_line()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System;
            using System.Threading.Tasks;

            int result [|= [|await [|MyMethodAsync[|(
                    1)|]|]|]|];

            Task<int> MyMethodAsync(int i) => Task.FromResult(1);
            """,
            """
            using System;
            using System.Threading.Tasks;

            int result =
                await MyMethodAsync(
                    1
                );

            Task<int> MyMethodAsync(int i) => Task.FromResult(1);
            """,
            options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task AwaitExpression_Method_SimpleMultiline_parens_on_the_new_line()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System;
            using System.Threading.Tasks;

            int result [|=
                [|await [|MyMethodAsync
                [|(
                    1)|]|]|]|];

            Task<int> MyMethodAsync(int i)=> Task.FromResult(1);
            """,
            """
            using System;
            using System.Threading.Tasks;

            int result =
                await MyMethodAsync
                    (
                        1
                    );

            Task<int> MyMethodAsync(int i)=> Task.FromResult(1);
            """,
            options: Options.WithCompilationOptions(
                Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
            )
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task AwaitExpression_Method_Multiline_single_lined_parens_on_the_new_line()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System;
            using System.Threading.Tasks;

            int result [|=
                [|await [|MyMethodAsync
                (1)|]|]|];

            Task<int> MyMethodAsync(int i)=> Task.FromResult(1);
            """,
            """
            using System;
            using System.Threading.Tasks;

            int result =
                await MyMethodAsync
                    (1);

            Task<int> MyMethodAsync(int i)=> Task.FromResult(1);
            """,
            options: Options.WithCompilationOptions(
                Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
            )
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task AwaitExpression_Method_SimpleMultiline()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System;
            using System.Threading.Tasks;

            int result [|= await MyMethodAsync(
                1
            )|];

            Task<int> MyMethodAsync(int i)=> Task.FromResult(1);
            """,
            """
            using System;
            using System.Threading.Tasks;

            int result =
                await MyMethodAsync(
                    1
                );

            Task<int> MyMethodAsync(int i)=> Task.FromResult(1);
            """,
            options: Options.WithCompilationOptions(
                Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
            )
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task AwaitExpression_Method_with_InvocationExpression_on_new_line()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System;
            using System.Threading.Tasks;

            int result [|= [|await
            MyMethodAsync(
                1
            )|]|];

            Task<int> MyMethodAsync(int i)=> Task.FromResult(1);
            """,
            """
            using System;
            using System.Threading.Tasks;

            int result =
                await
                    MyMethodAsync(
                        1
                    );

            Task<int> MyMethodAsync(int i)=> Task.FromResult(1);
            """,
            options: Options.WithCompilationOptions(
                Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
            )
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task AwaitExpression_Method_SimpleMultiline_with_simple_comments()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System;
            using System.Threading.Tasks;

            int result [|= //comment1
            [|// comment2
                await [|MyMethodAsync[|( // Comment 3
                    /*comment4*/ 1 /*comment5*/) /*comment 6*/|]|]|]|]; //comment7

            Task<int> MyMethodAsync(int i) => Task.FromResult(1);
            """,
            """
            using System;
            using System.Threading.Tasks;

            int result = //comment1
                // comment2
                await MyMethodAsync( // Comment 3
                    /*comment4*/
                    1 /*comment5*/
                ) /*comment 6*/; //comment7

            Task<int> MyMethodAsync(int i) => Task.FromResult(1);
            """,
            options: Options.WithCompilationOptions(
                Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
            )
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task AwaitExpression_Method_SimpleMultiline_with_complex_comments()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System;
            using System.Threading.Tasks;

            int result [|=  /*comment1
                comment2
            comment3*/ /* comment4
            comment5
                comment6*/ /*comment7
                comment8
            comment9*/ [|await [|MyMethodAsync[|(/*comment10
            comment11*/ 1,
            /*comment12
            comment13*/ /*comment14
            comment15*/ 2,
            // Comment 16,
            // Comment 17
            /*
            Comment 18
               Comment 19
            Comment 20
            */ 3
            /*comment16
            comment17*/ /*comment18
            comment19*/)|]|]|]|];

            Task<int> MyMethodAsync(int i, int i2, int i3) => Task.FromResult(1);
            """,
            """
            using System;
            using System.Threading.Tasks;

            int result =  /*comment1
                comment2
            comment3*/ /* comment4
            comment5
                comment6*/ /*comment7
                comment8
            comment9*/
                await MyMethodAsync(/*comment10
            comment11*/
                    1,
                    /*comment12
                    comment13*/ /*comment14
                    comment15*/
                    2,
                    // Comment 16,
                    // Comment 17
                    /*
                    Comment 18
                       Comment 19
                    Comment 20
                    */
                    3
                    /*comment16
                    comment17*/ /*comment18
                    comment19*/
                );

            Task<int> MyMethodAsync(int i, int i2, int i3) => Task.FromResult(1);
            """,
            options: Options.WithCompilationOptions(
                Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
            )
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task AwaitExpression_Method_SimpleMultiline_without_arguments_but_with_trailing_comment()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System;
            using System.Threading.Tasks;

            int result [|=
                [|await [|MyMethodAsync[|(/*comment12
            comment13*/ /*comment14
            comment15*/
            // Comment 16,
            // Comment 17
            /*
            Comment 18
               Comment 19
            Comment 20
            */
                )|]|]|]|];

            Task<int> MyMethodAsync() => Task.FromResult(1);
            """,
            """
            using System;
            using System.Threading.Tasks;

            int result =
                await MyMethodAsync(/*comment12
            comment13*/ /*comment14
            comment15*/
                    // Comment 16,
                    // Comment 17
                    /*
                    Comment 18
                       Comment 19
                    Comment 20
                    */
                );

            Task<int> MyMethodAsync() => Task.FromResult(1);
            """,
            options: Options.WithCompilationOptions(
                Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
            )
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task AwaitExpression_Method_SimpleMultiline_without_arguments_but_with_comment()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System;
            using System.Threading.Tasks;

            int result [|=
                [|await [|MyMethodAsync[|(
                    /*comment12
            comment13*/ /*comment14
            comment15*/
            // Comment 16,
            // Comment 17
            /*
            Comment 18
               Comment 19
            Comment 20
            */
                )|]|]|]|];

            Task<int> MyMethodAsync() => Task.FromResult(1);
            """,
            """
            using System;
            using System.Threading.Tasks;

            int result =
                await MyMethodAsync(
                    /*comment12
                    comment13*/ /*comment14
                    comment15*/
                    // Comment 16,
                    // Comment 17
                    /*
                    Comment 18
                       Comment 19
                    Comment 20
                    */
                );

            Task<int> MyMethodAsync() => Task.FromResult(1);
            """,
            options: Options.WithCompilationOptions(
                Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
            )
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task AwaitExpression_Method_with_lambda_parameter_single_line_after_assignment()
    {
        await VerifyNoDiagnosticAsync(
            """
            using System;
            using System.Threading.Tasks;

            int result = await MyMethodAsync(async (int a, int b, int c) => await Task.Run(() => 10));

            Task<int> MyMethodAsync(Func<int, int, int, Task<int>> f)
                => Task.FromResult(1);
            """,
            options: Options.WithCompilationOptions(
                Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
            )
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task AwaitExpression_Method_with_simple_lambda_parameter_single_line_after_assignment()
    {
        await VerifyNoDiagnosticAsync(
            """
            using System;
            using System.Threading.Tasks;

            int result = await MyMethodAsync(() => 10);

            Task<int> MyMethodAsync(Func<int> f) => Task.FromResult(1);
            """,
            options: Options.WithCompilationOptions(
                Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
            )
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task AwaitExpression_Method_with_lambda_parameter_single_line_next_after_assignment_formatted()
    {
        await VerifyNoDiagnosticAsync(
            """
            using System;
            using System.Threading.Tasks;

            int result =
                await MyMethodAsync(async (int a, int b, int c) => await Task.Run(() => 10));

            Task<int> MyMethodAsync(Func<int, int, int, Task<int>> f)
                => Task.FromResult(1);
            """,
            options: Options.WithCompilationOptions(
                Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
            )
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task AwaitExpression_Method_with_simple_async_lambda_parameter_single_line_next_after_assignment_formatted()
    {
        await VerifyNoDiagnosticAsync(
            """
            using System;
            using System.Threading.Tasks;

            int result =
                await MyMethodAsync(async () => await Task.Run(() => 10));

            Task<int> MyMethodAsync(Func<Task<int>> f)
                => Task.FromResult(1);
            """,
            options: Options.WithCompilationOptions(
                Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
            )
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task AwaitExpression_Method_with_lambda_parameter_single_line_next_after_assignment()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System;
            using System.Threading.Tasks;

            int result [|=
            await MyMethodAsync(async (int a, int b, int c) => await Task.Run(() => 10))|];

            Task<int> MyMethodAsync(Func<int, int, int, Task<int>> f)
                => Task.FromResult(1);
            """,
            """
            using System;
            using System.Threading.Tasks;

            int result =
                await MyMethodAsync(async (int a, int b, int c) => await Task.Run(() => 10));

            Task<int> MyMethodAsync(Func<int, int, int, Task<int>> f)
                => Task.FromResult(1);
            """,
            options: Options.WithCompilationOptions(
                Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
            )
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task AwaitExpression_top_level_Method_with_lambda_parameter()
    {
        await VerifyNoDiagnosticAsync(
            """
            using System;
            using System.Threading.Tasks;

            await MyMethodAsync(async (int a, int b, int c) => await Task.Run(() => 10));

            Task<int> MyMethodAsync(Func<int, int, int, Task<int>> f)
                => Task.FromResult(1);
            """,
            options: Options.WithCompilationOptions(
                Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
            )
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task AwaitExpression_top_level_multiline_Method_with_lambda_parameter()
    {
        await VerifyNoDiagnosticAsync(
            """
            using System;
            using System.Threading.Tasks;

            await MyMethodAsync(
                async (int a, int b, int c) => 
                    await Task.Run(() => 10)
            );

            Task<int> MyMethodAsync(Func<int, int, int, Task<int>> f)
                => Task.FromResult(1);
            """,
            options: Options.WithCompilationOptions(
                Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
            )
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task AwaitExpression_top_level_multiline_Method_with_single_line_lambda_parameter_on_next_line()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System;
            using System.Threading.Tasks;

            [|await [|MyMethodAsync[|(
            async (int a, int b, int c) => await Task.Run(() => 10)
            )|]|]|];

            Task<int> MyMethodAsync(Func<int, int, int, Task<int>> f)
                => Task.FromResult(1);
            """,
            """
            using System;
            using System.Threading.Tasks;

            await MyMethodAsync(
                async (int a, int b, int c) => await Task.Run(() => 10)
            );

            Task<int> MyMethodAsync(Func<int, int, int, Task<int>> f)
                => Task.FromResult(1);
            """,
            options: Options.WithCompilationOptions(
                Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
            )
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task AwaitExpression_top_level_multiline_Method_with_single_line_simple_lambda_parameter_on_next_line()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System;
            using System.Threading.Tasks;

            [|await [|MyMethodAsync[|(
            () => 10
            )|]|]|];

            Task<int> MyMethodAsync(Func<int> f) => Task.FromResult(1);
            """,
            """
            using System;
            using System.Threading.Tasks;

            await MyMethodAsync(
                () => 10
            );

            Task<int> MyMethodAsync(Func<int> f) => Task.FromResult(1);
            """,
            options: Options.WithCompilationOptions(
                Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
            )
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task AwaitExpression_Method_with_lambda_parameter()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System;
            using System.Threading.Tasks;

            [|await [|MyMethodAsync[|([|async (int a, int b, int c) =>
            await Task.Run(() => 10)|])|]|]|];

            Task<int> MyMethodAsync(Func<int, int, int, Task<int>> f)
                => Task.FromResult(1);
            """,
            """
            using System;
            using System.Threading.Tasks;

            await MyMethodAsync(
                async (int a, int b, int c) =>
                    await Task.Run(() => 10)
            );

            Task<int> MyMethodAsync(Func<int, int, int, Task<int>> f)
                => Task.FromResult(1);
            """,
            options: Options.WithCompilationOptions(
                Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
            )
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task AwaitExpression_Method_with_single_line_statement_lambda_parameter()
    {
        await VerifyNoDiagnosticAsync(
            """
            using System;
            using System.Threading.Tasks;

            await MyMethodAsync(async (int a, int b, int c) => { return await Task.Run(() => 10); });

            Task<int> MyMethodAsync(Func<int, int, int, Task<int>> f)
                => Task.FromResult(1);
            """,
            options: Options.WithCompilationOptions(
                Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
            )
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task AwaitExpression_Method_with_statement_lambda_parameter()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System;
            using System.Threading.Tasks;

            [|await [|MyMethodAsync[|([|async (int a, int b, int c) => {
                return await Task.Run(() => 10); }|])|]|]|];

            Task<int> MyMethodAsync(Func<int, int, int, Task<int>> f)
                => Task.FromResult(1);
            """,
            """
            using System;
            using System.Threading.Tasks;

            await MyMethodAsync(
                async (int a, int b, int c) =>
                {
                    return await Task.Run(() => 10);
                }
            );

            Task<int> MyMethodAsync(Func<int, int, int, Task<int>> f)
                => Task.FromResult(1);
            """,
            options: Options.WithCompilationOptions(
                Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
            )
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task AwaitExpression_Method_with_statement_lambda_parameter_with_simple_comments()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System;
            using System.Threading.Tasks;

            int result [|=
            [|await [|MyMethodAsync[|(
            [|// Comment
                async (int a, int b, int c) =>
                {
                // Comment
                    return await Task.Run(() => 10);
                        // Comment
                }|]
            )|]|]|]|];

            Task<int> MyMethodAsync(Func<int, int, int, Task<int>> f)
                => Task.FromResult(1);
            """,
            """
            using System;
            using System.Threading.Tasks;

            int result =
                await MyMethodAsync(
                    // Comment
                    async (int a, int b, int c) =>
                    {
                        // Comment
                        return await Task.Run(() => 10);
                        // Comment
                    }
                );

            Task<int> MyMethodAsync(Func<int, int, int, Task<int>> f)
                => Task.FromResult(1);
            """,
            options: Options.WithCompilationOptions(
                Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
            )
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task AwaitExpression_Method_with_statement_lambda_parameter_with_complex_comments()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System;
            using System.Threading.Tasks;

            int result [|= [|await [|MyMethodAsync[|(/*comment*/[|async (int a, int b, int c) =>
            /*comment*/{
                return await Task.Run(() => 10);
            /*comment*/}/*comment*/|])|]|]|]|];

            [|// Multiline
            result =/*comment1
                comment2
            comment3*/ [|await [|MyMethodAsync[|(/*comment4
            comment5*/[|async (int a, int b, int c) =>
            /*comment6
            comment7*/{/*comment12
            comment13*/ return await Task.Run(() => 10);
            /*comment8
            comment9*/}/*comment10
            comment11*/|])|]|]|]|];

            Task<int> MyMethodAsync(Func<int, int, int, Task<int>> f)
                => Task.FromResult(1);
            """,
            """
            using System;
            using System.Threading.Tasks;

            int result =
                await MyMethodAsync(/*comment*/
                    async (int a, int b, int c) =>
                    /*comment*/
                    {
                        return await Task.Run(() => 10);
                        /*comment*/
                    }/*comment*/
                );

            // Multiline
            result =/*comment1
                comment2
            comment3*/
                await MyMethodAsync(/*comment4
            comment5*/
                    async (int a, int b, int c) =>
                    /*comment6
                    comment7*/
                    {/*comment12
            comment13*/
                        return await Task.Run(() => 10);
                        /*comment8
                        comment9*/
                    }/*comment10
            comment11*/
                );

            Task<int> MyMethodAsync(Func<int, int, int, Task<int>> f)
                => Task.FromResult(1);
            """,
            options: Options.WithCompilationOptions(
                Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
            )
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Method_with_lambda_parameter()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System;
            using System.Threading.Tasks;

            int result [|= [|MyMethod[|([|() =>
            10|])|]|]|];

            int MyMethod(Func<int> f) => 1;
            """,
            """
            using System;
            using System.Threading.Tasks;

            int result =
                MyMethod(
                    () =>
                        10
                );

            int MyMethod(Func<int> f) => 1;
            """,
            options: Options.WithCompilationOptions(
                Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
            )
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Method_with_statement_lambda_parameter()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System;
            using System.Threading.Tasks;

            [|MyMethod[|([|(int a, int b, int c) => {
                return 10; }|])|]|];

            int MyMethod(Func<int, int, int, int> f) => 1;
            """,
            """
            using System;
            using System.Threading.Tasks;

            MyMethod(
                (int a, int b, int c) =>
                {
                    return 10;
                }
            );

            int MyMethod(Func<int, int, int, int> f) => 1;
            """,
            options: Options.WithCompilationOptions(
                Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
            )
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Method_with_statement_lambda_with_one_parameter_parameter()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System;
            using System.Threading.Tasks;

            int result [|= [|MyMethod[|(x =>
            {
                return 10;
            })|]|]|];

            int MyMethod(Func<int, int> f) => 1;
            """,
            """
            using System;
            using System.Threading.Tasks;

            int result =
                MyMethod(
                    x =>
                    {
                        return 10;
                    }
                );

            int MyMethod(Func<int, int> f) => 1;
            """,
            options: Options.WithCompilationOptions(
                Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
            )
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Methods_that_are_misaligned()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System;
            using System.Threading.Tasks;

            int result [|=
                [|await [|MyMethodAsync[|(
                    1,
                    2
                    )|]|]|]|];
            int result2 [|=
                [|await [|MyMethodAsync[|(
                    1, 2
            )|]|]|]|];
            int result3 [|=
            [|await [|MyMethodAsync[|(
                    1, 2)|]|]|]|];
            int result4 [|=
            [|MyMethod[|(
                    1, 2
            )|]|]|];
            int result5 [|=
            [|MyMethod[|(
                    1, 2
                    )|]|]|];
            int result6 [|=
                [|MyMethod[|(
                    1, 2)|]|]|];

            Task<int> MyMethodAsync(int a, int b) => Task.FromResult(1);
            int MyMethod(int a, int b) => 1;
            """,
            """
            using System;
            using System.Threading.Tasks;

            int result =
                await MyMethodAsync(
                    1,
                    2
                );
            int result2 =
                await MyMethodAsync(
                    1,
                    2
                );
            int result3 =
                await MyMethodAsync(
                    1,
                    2
                );
            int result4 =
                MyMethod(
                    1,
                    2
                );
            int result5 =
                MyMethod(
                    1,
                    2
                );
            int result6 =
                MyMethod(
                    1,
                    2
                );

            Task<int> MyMethodAsync(int a, int b) => Task.FromResult(1);
            int MyMethod(int a, int b) => 1;
            """,
            options: Options.WithCompilationOptions(
                Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
            )
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Lamba_assignments_single_line()
    {
        await VerifyNoDiagnosticAsync(
            """
            using System;

            Action<int, int, int> myAction = (int a, int b, int c) => { /* comment */ };
            Action<int, int, int> myActionNextLine =
                (int a, int b, int c) => { /* comment */ };
            Action myActionNoParams = () => { /* comment */ };
            Action<int> myActionOneParam = x => { /* comment */ Console.WriteLine(x); /* comment */ };

            Func<int, int, int> myFunc = (int a, int b) => a + b;
            Func<int, int, int> myFuncStatementBody = (int a, int b) => { return a + b; };
            Func<int> myFuncNoParams = () => 10;
            Func<int> myFuncNoParamsStatementBody = () => { return 10; };
            Func<int, int> myFuncOneParam = x => x;
            Func<int, int> myFuncOneParamStatementBody = x => { return x; };
            """,
            options: Options.WithCompilationOptions(
                Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
            )
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Lamba_assignment_formatted()
    {
        await VerifyNoDiagnosticAsync(
            """
            using System;

            Action<int, int, int> myAction =
                (int a, int b, int c) =>
                {
                    /* comment */
                    /* comment */
                };
            Action myActionNoParams =
                () =>
                {
                    /* comment */
                    /* comment */
                };
            Action<int> myActionOneParam =
                x =>
                { /* comment */
                    // Comment
                    Console.WriteLine(x);
                    /* comment */
                };

            Func<int, int, int> myFunc =
                (int a, int b) =>
                    a + b;
            Func<int, int, int> myFuncArrowNextLine =
                (int a, int b)
                    => a + b;
            Func<int, int, int> myFuncStatementBody =
                (int a, int b) =>
                {
                    return a + b;
                };
            Func<int> myFuncNoParams =
                () =>
                    10;
            Func<int> myFuncNoParamsStatementBody =
                () =>
                {
                    return 10;
                };
            Func<int, int> myFuncOneParam =
                x =>
                    x;
            Func<int, int> myFuncOneParamStatementBody =
                x =>
                {
                    return x;
                };
            """,
            options: Options.WithCompilationOptions(
                Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
            )
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Lamba_assignment_Action_with_three_params()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System;

            Action<int, int, int> myAction [|= (int a, int b, int c) =>
            {
                /* comment */
                /* comment */
            }|];
            """,
            """
            using System;

            Action<int, int, int> myAction =
                (int a, int b, int c) =>
                {
                    /* comment */
                    /* comment */
                };
            """,
            options: Options.WithCompilationOptions(
                Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
            )
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Lamba_assignment_Action_with_three_params_open_brace_at_prev_line()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System;

            Action<int, int, int> myAction [|=
                [|(int a, int b, int c) => {
                    /* comment */
                    /* comment */
                }|]|];
            """,
            """
            using System;

            Action<int, int, int> myAction =
                (int a, int b, int c) =>
                {
                    /* comment */
                    /* comment */
                };
            """,
            options: Options.WithCompilationOptions(
                Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
            )
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Lamba_assignment_Action_no_params()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System;

            Action myAction [|= () =>
            {
                /* comment */
                /* comment */
            }|];
            """,
            """
            using System;

            Action myAction =
                () =>
                {
                    /* comment */
                    /* comment */
                };
            """,
            options: Options.WithCompilationOptions(
                Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
            )
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Lamba_assignment_Action_one_param()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System;

            Action<int> myAction [|= x =>
            { /* comment */
                // Comment
                Console.WriteLine(x);
                /* comment */
            }|];
            """,
            """
            using System;

            Action<int> myAction =
                x =>
                { /* comment */
                    // Comment
                    Console.WriteLine(x);
                    /* comment */
                };
            """,
            options: Options.WithCompilationOptions(
                Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
            )
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Lamba_assignment_Func_with_two_params()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System;

            Func<int, int, int> myFunc [|= [|(int a, int b) =>
            a + b|]|];
            """,
            """
            using System;

            Func<int, int, int> myFunc =
                (int a, int b) =>
                    a + b;
            """,
            options: Options.WithCompilationOptions(
                Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
            )
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Lamba_assignment_async_Func_with_two_params()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System;
            using System.Threading.Tasks;

            Func<int, int, Task<int>> myFunc [|= [|async (int a, int b) =>
            await Task.FromResult(a + b)|]|];
            """,
            """
            using System;
            using System.Threading.Tasks;

            Func<int, int, Task<int>> myFunc =
                async (int a, int b) =>
                    await Task.FromResult(a + b);
            """,
            options: Options.WithCompilationOptions(
                Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
            )
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Lamba_assignment_Func_with_two_params_arrow_next_line()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System;

            Func<int, int, int> myFunc [|= (int a, int b)
                => a + b|];
            """,
            """
            using System;

            Func<int, int, int> myFunc =
                (int a, int b)
                    => a + b;
            """,
            options: Options.WithCompilationOptions(
                Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
            )
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Lamba_assignment_Func_with_two_params_arrow_next_line_and_statement_body()
    {
        // The case with strange formatting, where I am not sure what I would expect. I wouldn't format this way at all.

        await VerifyDiagnosticAndFixAsync(
            """
            using System;

            Func<int, int, int> myFunc [|= [|(int a, int b)
                => {
                return a + b; }|]|];
            """,
            """
            using System;

            Func<int, int, int> myFunc =
                (int a, int b)
                    =>
                    {
                        return a + b;
                    };
            """,
            options: Options.WithCompilationOptions(
                Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
            )
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Lamba_assignment_Func_with_two_params_and_statement_body()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System;

            Func<int, int, int> myFunc [|= (int a, int b) =>
            {
                return a + b;
            }|];
            """,
            """
            using System;

            Func<int, int, int> myFunc =
                (int a, int b) =>
                {
                    return a + b;
                };
            """,
            options: Options.WithCompilationOptions(
                Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
            )
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Lamba_assignment_Func_with_no_params()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System;

            Func<int> myFunc [|= [|() =>
            10|]|];
            """,
            """
            using System;

            Func<int> myFunc =
                () =>
                    10;
            """,
            options: Options.WithCompilationOptions(
                Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
            )
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Lamba_assignment_Func_with_no_params_and_statement_body()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System;

            Func<int> myFunc [|= () =>
            {
                return 10;
            }|];
            """,
            """
            using System;

            Func<int> myFunc =
                () =>
                {
                    return 10;
                };
            """,
            options: Options.WithCompilationOptions(
                Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
            )
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Lamba_assignment_Func_with_one_params()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System;

            Func<int, int> myFunc [|= [|x =>
            x|]|];
            """,
            """
            using System;

            Func<int, int> myFunc =
                x =>
                    x;
            """,
            options: Options.WithCompilationOptions(
                Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
            )
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Lamba_assignment_Func_with_one_params_and_statement_body()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System;

            Func<int, int> myFunc [|= x =>
            {
                return x;
            }|];
            """,
            """
            using System;

            Func<int, int> myFunc =
                x =>
                {
                    return x;
                };
            """,
            options: Options.WithCompilationOptions(
                Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
            )
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Chained_method_with_statement_lambda_parameter()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System;
            using System.Threading.Tasks;

            [|await [|C.Instance.MyMethodAsync[|([|async (int a, int b, int c) =>
            {
                    await Task.Delay(100);
                return 10;
            }|])|]|]|];
            """,
            """
            using System;
            using System.Threading.Tasks;

            await C.Instance.MyMethodAsync(
                async (int a, int b, int c) =>
                {
                    await Task.Delay(100);
                    return 10;
                }
            );
            """,
            additionalFiles:
                new (string source, string expectedSource)[]
                {
                    (
                        source:
                        """
                        using System;
                        using System.Threading.Tasks;

                        public class C
                        {
                            public static C Instance { get; } = new C();

                            public Task<bool> MyMethodAsync(Func<int, int, int, Task<int>> f) => Task.FromResult(true);
                        }
                        """,
                        expectedSource: null
                    )
                },
            options: Options.WithCompilationOptions(
                Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
            )
        );
    }
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task New_class_instantiation_with_nested_new_class_instantiation()
//     {
//          await VerifyDiagnosticAndFixAsync(
//              """
//              MyType myVariable [|= [|new MyType
//              {
//                  Property1 = 1,
//                  Property2 = 2,
//                  [|Nested = new MyType
//                  {
//                      Property1 = 3,
//                      Property2 = 4,
//                      Nested = null
//                  }|]
//              }|]|];
//              """,
//              """
//              MyType myVariable =
//                  new MyType
//                  {
//                      Property1 = 1,
//                      Property2 = 2,
//                      Nested =
//                          new MyType
//                          {
//                              Property1 = 3,
//                              Property2 = 4,
//                              Nested = null
//                          }
//                  };
//              """,
//              additionalFiles:
//                  new (string source, string expectedSource)[]
//                  {
//                      (
//                          source:
//                              """
//                              public sealed class MyType
//                              {
//                                  public required int Property1 { get; init; }
//                                  public required int Property2 { get; init; }
//                                  public required MyType Nested { get; init; }
//                              }
//                              """,
//                          expectedSource: null
//                      )
//                  },
//              options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
//          );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task New_class_instantiation_with_nested_new_class_instantiation_open_brace_on_the_same_line()
//     {
//         await VerifyDiagnosticAndFixAsync(
//             """
//             MyType myVariable [|= [|new MyType {
//                         [|Property1 = 1|],
//                         [|Property2 = 2|],
//             [|Nested = new MyType
//             {
//                 Property1 = 3,
//                 Property2 = 4,
//                 Nested = null
//             }|]
//                     }|]|];
//             """,
//             """
//             MyType myVariable =
//                 new MyType
//                 {
//                     Property1 = 1,
//                     Property2 = 2,
//                     Nested =
//                         new MyType
//                         {
//                             Property1 = 3,
//                             Property2 = 4,
//                             Nested = null
//                         }
//                 };
//             """,
//             additionalFiles:
//             new (string source, string expectedSource)[]
//             {
//                 (
//                     source:
//                         """
//                         public sealed class MyType
//                         {
//                             public required int Property1 { get; init; }
//                             public required int Property2 { get; init; }
//                             public required MyType Nested { get; init; }
//                         }
//                         """,
//                     expectedSource: null
//                 )
//             },
//             options: Options.WithCompilationOptions(
//                 Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
//             )
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task New_class_instantiation_with_nested_single_lined_new_class_instantiation()
//     {
//         await VerifyDiagnosticAndFixAsync(
//             """
//             MyType myVariable [|= new MyType
//             {
//                 Property1 = 1,
//                 Property2 = 2,
//                 Nested = new MyType { Property1 = 3, Property2 = 4, Nested = null }
//             }|];
//             """,
//             """
//             MyType myVariable =
//                 new MyType
//                 {
//                     Property1 = 1,
//                     Property2 = 2,
//                     Nested = new MyType { Property1 = 3, Property2 = 4, Nested = null }
//                 };
//             """,
//             additionalFiles:
//             new (string source, string expectedSource)[]
//             {
//                 (
//                     source:
//                         """
//                         public sealed class MyType
//                         {
//                             public required int Property1 { get; init; }
//                             public required int Property2 { get; init; }
//                             public required MyType Nested { get; init; }
//                         }
//                         """,
//                     expectedSource: null
//                 )
//             },
//             options: Options.WithCompilationOptions(
//                 Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
//             )
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task New_single_lined_class_instantiation()
//     {
//         await VerifyNoDiagnosticAsync(
//             "MyType myVariable = new MyType { Property1 = 1, Property2 = 2 };",
//             additionalFiles:
//                 [
//                     """
//                     public sealed class MyType
//                     {
//                         public required int Property1 { get; init; }
//                         public required int Property2 { get; init; }
//                     }
//                     """
//                 ],
//             options: Options.WithCompilationOptions(
//                 Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
//             )
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task AwaitExpression_Method_with_new_class_instantiation_parameter()
//     {
//         await VerifyDiagnosticAndFixAsync(
//             """
//             using System;
//             using System.Threading.Tasks;
//
//             int myVariable [|= [|await [|MyMethodAsync(new MyType()
//             {
//                 Property1 = 1,
//                 Property2 = 2
//             })|]|]|];
//
//             Task<int> MyMethodAsync(MyType mt) => Task.FromResult(1);
//             """,
//             """
//             using System;
//             using System.Threading.Tasks;
//
//             int myVariable =
//                 await MyMethodAsync(
//                     new MyType()
//                     {
//                         Property1 = 1,
//                         Property2 = 2
//                     }
//                 );
//
//             Task<int> MyMethodAsync(MyType mt) => Task.FromResult(1);
//             """,
//             additionalFiles:
//                 new(string source, string expectedSource)[]
//                 {
//                     (
//                         source:
//                         """
//                         public sealed class MyType
//                         {
//                             public required int Property1 { get; init; }
//                             public required int Property2 { get; init; }
//                         }
//                         """,
//                         expectedSource: null
//                     )
//                 },
//             options: Options.WithCompilationOptions(
//                 Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
//             )
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task AwaitExpression_Method_with_new_class_instantiation_parameter_with_complex_comments()
//     {
//         await VerifyDiagnosticAndFixAsync(
//             """
//             using System;
//             using System.Threading.Tasks;
//
//             int myVariable [|= [|await [|MyMethodAsync( /* Comment0 */
//             [|/*
//             Comment1
//             Comment2
//             */new MyType()
//             // Comment 3
//             {
//             [|//Comment 4
//                 Property1 = 1|],
//                 Property2 = 2
//             // Comment 5
//             }/*Comment6*/|])|]|]|];
//
//             Task<int> MyMethodAsync(MyType mt)
//                 => Task.FromResult(1);
//             """,
//             """
//             using System;
//             using System.Threading.Tasks;
//
//             int myVariable =
//                 await MyMethodAsync( /* Comment0 */
//                     /*
//                     Comment1
//                     Comment2
//                     */
//                     new MyType()
//                     // Comment 3
//                     {
//                         //Comment 4
//                         Property1 = 1,
//                         Property2 = 2
//                         // Comment 5
//                     }/*Comment6*/
//                 );
//
//             Task<int> MyMethodAsync(MyType mt)
//                 => Task.FromResult(1);
//             """,
//             additionalFiles:
//                 new(string source, string expectedSource)[]
//                 {
//                     (
//                         source:
//                         """
//                         public sealed class MyType
//                         {
//                             public required int Property1 { get; init; }
//                             public required int Property2 { get; init; }
//                         }
//                         """,
//                         expectedSource: null
//                     )
//                 },
//             options: Options.WithCompilationOptions(
//                 Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
//             )
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task AwaitExpression_Method_with_new_single_lined_class_instantiation()
//     {
//         await VerifyDiagnosticAndFixAsync(
//             """
//             using System;
//             using System.Threading.Tasks;
//
//             int myVariable [|= [|await [|MyMethodAsync(
//                 new MyType() { Property1 = 1, Property2 = 2 })|]|]|];
//
//             Task<int> MyMethodAsync(MyType mt) => Task.FromResult(1);
//             """,
//             """
//             using System;
//             using System.Threading.Tasks;
//
//             int myVariable =
//                 await MyMethodAsync(
//                     new MyType() { Property1 = 1, Property2 = 2 }
//                 );
//
//             Task<int> MyMethodAsync(MyType mt) => Task.FromResult(1);
//             """,
//             additionalFiles:
//                 new (string source, string expectedSource)[]
//                 {
//                     (
//                         source:
//                         """
//                         public sealed class MyType
//                         {
//                             public required int Property1 { get; init; }
//                             public required int Property2 { get; init; }
//                         }
//                         """,
//                         expectedSource: null
//                     )
//                 },
//             options:
//                 Options.WithCompilationOptions(
//                     Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
//                 )
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task AwaitExpression_single_line_Method_with_new_single_lined_class_instantiation()
//     {
//         await VerifyNoDiagnosticAsync(
//             """
//             using System;
//             using System.Threading.Tasks;
//
//             int myVariable = await MyMethodAsync(new MyType() { Property1 = 1, Property2 = 2 });
//
//             Task<int> MyMethodAsync(MyType mt) => Task.FromResult(1);
//             """,
//             additionalFiles:
//                 [
//                     """
//                     public sealed class MyType
//                     {
//                         public required int Property1 { get; init; }
//                         public required int Property2 { get; init; }
//                     }
//                     """
//                 ],
//             options:
//                 Options.WithCompilationOptions(
//                     Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
//                 )
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task New_record_instantiation_with_With_expression()
//     {
//         await VerifyDiagnosticAndFixAsync(
//             """
//             var person [|= new Person("John", 30) with
//             {
//                 Age = 31
//             }|];
//             """,
//             """
//             var person =
//                 new Person("John", 30) with
//                 {
//                     Age = 31
//                 };
//             """,
//             additionalFiles:
//                 new(string source, string expectedSource)[]
//                 {
//                     (
//                         source:
//                         "public sealed record Person(string Name, int Age);",
//                         expectedSource: null
//                     )
//                 },
//             options: Options.WithCompilationOptions(
//                 Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
//             )
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task New_record_from_other_with_With_expression()
//     {
//         await VerifyDiagnosticAndFixAsync(
//             """
//             var person = new Person("John", 30);
//             var person2 [|= person with
//             {
//                 Age = 31
//             }|];
//             """,
//             """
//             var person = new Person("John", 30);
//             var person2 =
//                 person with
//                 {
//                     Age = 31
//                 };
//             """,
//             additionalFiles:
//                 new(string source, string expectedSource)[]
//                 {
//                     (
//                         source:
//                         "public sealed record Person(string Name, int Age);",
//                         expectedSource: null
//                     )
//                 },
//             options: Options.WithCompilationOptions(
//                 Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
//             )
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task New_record_instantiation_with_lambda_parameter_with_statement_body()
//     {
//         await VerifyDiagnosticAndFixAsync(
//             """
//             using System;
//
//             var person [|= [|new Person([|(int v1, int v2) => {
//                 return v1 + v2;
//             }|])|]|];
//             """,
//             """
//             using System;
//
//             var person =
//                 new Person(
//                     (int v1, int v2) =>
//                     {
//                         return v1 + v2;
//                     }
//                 );
//             """,
//             additionalFiles:
//                 new(string source, string expectedSource)[]
//                 {
//                     (
//                         source:
//                         """
//                         using System;
//
//                         public sealed record Person(Func<int, int, int> Action);
//                         """,
//                         expectedSource: null
//                     )
//                 },
//             options:
//                 Options.WithCompilationOptions(
//                     Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
//                 )
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task New_record_instantiation_with_async_lambda_parameter_with_statement_body()
//     {
//         await VerifyDiagnosticAndFixAsync(
//             """
//             using System;
//             using System.Threading.Tasks;
//
//             var person [|= [|new Person([|async (int v1, int v2) => {
//                 return await Task.Run(() => v1 + v2);
//             }|])|]|];
//             """,
//             """
//             using System;
//             using System.Threading.Tasks;
//
//             var person =
//                 new Person(
//                     async (int v1, int v2) =>
//                     {
//                         return await Task.Run(() => v1 + v2);
//                     }
//                 );
//             """,
//             additionalFiles:
//                 new(string source, string expectedSource)[]
//                 {
//                     (
//                         source:
//                         """
//                         using System;
//                         using System.Threading.Tasks;
//
//                         public sealed record Person(Func<int, int, Task<int>> Action);
//                         """,
//                         expectedSource: null
//                     )
//                 },
//             options:
//                 Options.WithCompilationOptions(
//                     Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
//                 )
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task New_record_instantiation_with_lambda_parameter()
//     {
//         await VerifyDiagnosticAndFixAsync(
//             """
//             using System;
//
//             var person [|= [|new Person((int v1, int v2) =>
//                 v1 + v2
//             )|]|];
//             """,
//             """
//             using System;
//
//             var person =
//                 new Person(
//                     (int v1, int v2) =>
//                         v1 + v2
//                 );
//             """,
//             additionalFiles:
//                 new(string source, string expectedSource)[]
//                 {
//                     (
//                         source:
//                         """
//                         using System;
//
//                         public sealed record Person(Func<int, int, int> Action);
//                         """,
//                         expectedSource: null
//                     )
//                 },
//             options:
//                 Options.WithCompilationOptions(
//                     Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
//                 )
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task New_record_instantiation_with_async_lambda_parameter()
//     {
//         await VerifyDiagnosticAndFixAsync(
//             """
//             using System;
//             using System.Threading.Tasks;
//
//             var person [|= [|new Person(async (int v1, int v2) =>
//                 await Task.Run(() => v1 + v2)
//             )|]|];
//             """,
//             """
//             using System;
//             using System.Threading.Tasks;
//
//             var person =
//                 new Person(
//                     async (int v1, int v2) =>
//                         await Task.Run(() => v1 + v2)
//                 );
//             """,
//             additionalFiles:
//                 new(string source, string expectedSource)[]
//                 {
//                     (
//                         source:
//                         """
//                         using System;
//                         using System.Threading.Tasks;
//
//                         public sealed record Person(Func<int, int, Task<int>> Action);
//                         """,
//                         expectedSource: null
//                     )
//                 },
//             options:
//                 Options.WithCompilationOptions(
//                     Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
//                 )
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task New_record_instantiation_with_single_line_lambda_parameter()
//     {
//         await VerifyNoDiagnosticAsync(
//             """
//             using System;
//
//             var person = new Person((int v1, int v2) => v1 + v2);
//             """,
//             additionalFiles:
//                 [
//                     """
//                     using System;
//
//                     public sealed record Person(Func<int, int, int> Action);
//                     """
//                 ],
//             options:
//                 Options.WithCompilationOptions(
//                     Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
//                 )
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task New_record_instantiation_with_async_single_line_lambda_parameter()
//     {
//         await VerifyNoDiagnosticAsync(
//             """
//             using System;
//             using System.Threading.Tasks;
//
//             var person = new Person(async (int v1, int v2) => await Task.Run(() => v1 + v2));
//             """,
//             additionalFiles:
//                 [
//                     """
//                     using System;
//                     using System.Threading.Tasks;
//
//                     public sealed record Person(Func<int, int, Task<int>> Action);
//                     """
//                 ],
//             options:
//                 Options.WithCompilationOptions(
//                     Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
//                 )
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task Delegate_assignment_one_params()
//     {
//         await VerifyDiagnosticAndFixAsync(
//             """
//             using System;
//
//             Func<int, int> square [|= delegate(int x)
//             {
//                 return x * x;
//             }|];
//             """,
//             """
//             using System;
//
//             Func<int, int> square =
//                 delegate(int x)
//                 {
//                     return x * x;
//                 };
//             """,
//             options:
//                 Options.WithCompilationOptions(
//                     Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
//                 )
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task Delegate_assignment_async_one_params()
//     {
//         await VerifyDiagnosticAndFixAsync(
//             """
//             using System;
//             using System.Threading.Tasks;
//
//             Func<int, Task<int>> square [|= async delegate(int x)
//             {
//                 return await Task.Run(() => x * x);
//             }|];
//             """,
//             """
//             using System;
//             using System.Threading.Tasks;
//
//             Func<int, Task<int>> square =
//                 async delegate(int x)
//                 {
//                     return await Task.Run(() => x * x);
//                 };
//             """,
//             options:
//                 Options.WithCompilationOptions(
//                     Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
//                 )
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task Anonymous_object_instantiation()
//     {
//         await VerifyDiagnosticAndFixAsync(
//             """
//             var person [|= new
//             {
//                 Name = "John",
//                 Age = 30
//             }|];
//             """,
//             """
//             var person =
//                 new
//                 {
//                     Name = "John",
//                     Age = 30
//                 };
//             """,
//             options: Options.WithCompilationOptions(
//                 Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
//             )
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task Anonymous_object_instantiation_brace_on_the_same_line()
//     {
//         await VerifyDiagnosticAndFixAsync(
//             """
//             var person [|= [|new {
//                 Name = "John",
//                 Age = 30
//             }|]|];
//             """,
//             """
//             var person =
//                 new
//                 {
//                     Name = "John",
//                     Age = 30
//                 };
//             """,
//             options: Options.WithCompilationOptions(
//                 Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
//             )
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task Anonymous_single_line_object_instantiation()
//     {
//         await VerifyNoDiagnosticAsync(
//             """
//             var person = new { Name = "John", Age = 30 };
//             """,
//             options: Options.WithCompilationOptions(
//                 Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
//             )
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task Anonymous_object_instantiation_as_method_param()
//     {
//         await VerifyDiagnosticAndFixAsync(
//             """
//             [|ProcessPerson(new
//             {
//                 Name = "John",
//                 Age = 30
//             })|];
//
//             static void ProcessPerson(object person)
//             {
//                 // Process the person object
//             }
//             """,
//             """
//             ProcessPerson(
//                 new
//                 {
//                     Name = "John",
//                     Age = 30
//                 }
//             );
//
//             static void ProcessPerson(object person)
//             {
//                 // Process the person object
//             }
//             """,
//             options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task Anonymous_object_instantiation_as_named_method_param()
//     {
//         await VerifyDiagnosticAndFixAsync(
//             """
//             [|ProcessPerson(10,
//                 person:
//             [|new
//             {
//                 Name = "John",
//                 Age = 30
//             }|])|];
//
//             static void ProcessPerson(int i, object person)
//             {
//                 // Process the person object
//             }
//             """,
//             """
//             ProcessPerson(10,
//                 person:
//                     new
//                     {
//                         Name = "John",
//                         Age = 30
//                     }
//             );
//
//             static void ProcessPerson(int i, object person)
//             {
//                 // Process the person object
//             }
//             """,
//             options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task SwitchExpression()
//     {
//         await VerifyDiagnosticAndFixAsync(
//             """
//             int x = 1;
//             var result [|= x switch
//             {
//                 1 => "One",
//                 2 => "Two",
//                 _ => "Other"
//             }|];
//             [|result = x switch
//             {
//                 1 => "One",
//                 2 => "Two",
//                 _ => "Other"
//             }|];
//             """,
//             """
//             int x = 1;
//             var result =
//                 x switch
//                 {
//                     1 => "One",
//                     2 => "Two",
//                     _ => "Other"
//                 };
//             result =
//                 x switch
//                 {
//                     1 => "One",
//                     2 => "Two",
//                     _ => "Other"
//                 };
//             """,
//             options: Options.WithCompilationOptions(
//                 Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
//             )
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task Tuple_instantiation()
//     {
//         await VerifyDiagnosticAndFixAsync(
//             """
//             var person [|= (
//                 Name: "John",
//                 Age: 30
//             )|];
//             """,
//             """
//             var person =
//                 (
//                     Name: "John",
//                     Age: 30
//                 );
//             """,
//             options: Options.WithCompilationOptions(
//                 Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
//             )
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task Tuple_instantiation_single_line()
//     {
//         await VerifyNoDiagnosticAsync(
//             """
//             var person = (Name: "John", Age: 30);
//             """,
//             options: Options.WithCompilationOptions(
//                 Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
//             )
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task Tuple_instantiation_with_nesting_anonymous_object()
//     {
//         await VerifyDiagnosticAndFixAsync(
//             """
//             var person [|= [|(
//                 Name: "John",
//                 Age: 30,
//                 Sister: [|new {
//                     Name = "Jane",
//                 }|]
//             )|]|];
//             """,
//             """
//             var person =
//                 (
//                     Name: "John",
//                     Age: 30,
//                     Sister:
//                         new
//                         {
//                             Name = "Jane",
//                         }
//                 );
//             """,
//             options: Options.WithCompilationOptions(
//                 Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
//             )
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task Tuple_instantiation_with_nesting_tuple()
//     {
//         await VerifyDiagnosticAndFixAsync(
//             """
//             var person [|= [|(
//                 Name: "John",
//                 Age: 30,
//                 Sister: [|(
//                     Name: "Jane",
//                     Age: 20)|]
//             )|]|];
//             """,
//             """
//             var person =
//                 (
//                     Name: "John",
//                     Age: 30,
//                     Sister:
//                         (
//                             Name: "Jane",
//                             Age: 20
//                         )
//                 );
//             """,
//             options: Options.WithCompilationOptions(
//                 Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
//             )
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task Array_creation_expression_explicit_type()
//     {
//         await VerifyDiagnosticAndFixAsync(
//             """
//             var list [|= new int[]
//             {
//                 1, 2, 3, 4, 5
//             }|];
//             """,
//             """
//             var list =
//                 new int[]
//                 {
//                     1, 2, 3, 4, 5
//                 };
//             """,
//             options: Options.WithCompilationOptions(
//                 Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
//             )
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task Array_creation_expression_explicit_type_one_line()
//     {
//         await VerifyNoDiagnosticAsync(
//             "var list = new int[] { 1, 2, 3, 4, 5 };",
//             options: Options.WithCompilationOptions(
//                 Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
//             )
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task Array_creation_expression_implicit_type()
//     {
//         await VerifyDiagnosticAndFixAsync(
//             """
//             var list [|= new[]
//             {
//                 1, 2, 3, 4, 5
//             }|];
//             """,
//             """
//             var list =
//                 new[]
//                 {
//                     1, 2, 3, 4, 5
//                 };
//             """,
//             options: Options.WithCompilationOptions(
//                 Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
//             )
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task Array_creation_expression_implicit_type_single_line()
//     {
//         await VerifyNoDiagnosticAsync(
//             "var list = new[] { 1, 2, 3, 4, 5 };",
//             options: Options.WithCompilationOptions(
//                 Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
//             )
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task Array_creation_expression_for_jagged_array()
//     {
//         await VerifyDiagnosticAndFixAsync(
//             """
//             var jaggedArray [|= new int[][]
//             {
//                 new int[] { 1, 2 },
//                 new int[] { 3, 4, 5 },
//                 new int[] { 6 }
//             }|];
//             """,
//             """
//             var jaggedArray =
//                 new int[][]
//                 {
//                     new int[] { 1, 2 },
//                     new int[] { 3, 4, 5 },
//                     new int[] { 6 }
//                 };
//             """,
//             options: Options.WithCompilationOptions(
//                 Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
//             )
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task Array_creation_expression_for_jagged_array_single_line()
//     {
//         await VerifyNoDiagnosticAsync(
//             "var jaggedArray = new int[][] { new int[] { 1, 2 }, new [] { 3, 4, 5 } };",
//             options: Options.WithCompilationOptions(
//                 Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
//             )
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task Array_instantiation_as_named_method_param()
//     {
//         await VerifyDiagnosticAndFixAsync(
//             """
//             [|ProcessPerson(
//                 10,
//                 ids: [|new []
//                 {
//                     1, 2, 3, 4, 5
//                 }|]
//             )|];
//             ProcessPerson(
//                 10,
//                 new []
//                 {
//                     1, 2, 3, 4, 5
//                 }
//             );
//             ProcessPerson(
//                 10,
//                 ids:
//                     new [] { 1, 2, 3, 4, 5 }
//             );
//             ProcessPerson(
//                 10,
//                 ids: new [] { 1, 2, 3, 4, 5 }
//             );
//
//             static void ProcessPerson(int i, int[] ids)
//             {
//                 // Process the person object
//             }
//             """,
//             """
//             ProcessPerson(
//                 10,
//                 ids:
//                     new []
//                     {
//                         1, 2, 3, 4, 5
//                     }
//             );
//             ProcessPerson(
//                 10,
//                 new []
//                 {
//                     1, 2, 3, 4, 5
//                 }
//             );
//             ProcessPerson(
//                 10,
//                 ids:
//                     new [] { 1, 2, 3, 4, 5 }
//             );
//             ProcessPerson(
//                 10,
//                 ids: new [] { 1, 2, 3, 4, 5 }
//             );
//
//             static void ProcessPerson(int i, int[] ids)
//             {
//                 // Process the person object
//             }
//             """,
//             options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task List_creation_expression_explicit_type()
//     {
//         await VerifyDiagnosticAndFixAsync(
//             """
//             using System.Collections.Generic;
//
//             var list [|= new List<int>()
//             {
//                 1, 2, 3, 4, 5
//             }|];
//             """,
//             """
//             using System.Collections.Generic;
//
//             var list =
//                 new List<int>()
//                 {
//                     1, 2, 3, 4, 5
//                 };
//             """,
//             options: Options.WithCompilationOptions(
//                 Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
//             )
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task List_creation_expression_explicit_type_one_line()
//     {
//         await VerifyNoDiagnosticAsync(
//             """
//             using System.Collections.Generic;
//
//             var list = new List<int>() { 1, 2, 3, 4, 5 };
//             """,
//             options: Options.WithCompilationOptions(
//                 Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
//             )
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task List_creation_expression_implicit_type()
//     {
//         await VerifyDiagnosticAndFixAsync(
//             """
//             using System.Collections.Generic;
//
//             List<int> list [|= new()
//             {
//                 1, 2, 3, 4, 5
//             }|];
//             """,
//             """
//             using System.Collections.Generic;
//
//             List<int> list =
//                 new()
//                 {
//                     1, 2, 3, 4, 5
//                 };
//             """,
//             options: Options.WithCompilationOptions(
//                 Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
//             )
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task List_creation_expression_implicit_type_single_line()
//     {
//         await VerifyNoDiagnosticAsync(
//             """
//             using System.Collections.Generic;
//
//             List<int> list = new() { 1, 2, 3, 4, 5 };
//             """,
//             options: Options.WithCompilationOptions(
//                 Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
//             )
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task List_creation_expression_for_list_of_lists()
//     {
//         await VerifyDiagnosticAndFixAsync(
//             """
//             using System.Collections.Generic;
//
//             List<List<int>> list [|= new ()
//             {
//                 new () { 1, 2 },
//                 new () { 3, 4, 5 },
//                 new() { 6 }
//             }|];
//             """,
//             """
//             using System.Collections.Generic;
//
//             List<List<int>> list =
//                 new ()
//                 {
//                     new () { 1, 2 },
//                     new () { 3, 4, 5 },
//                     new() { 6 }
//                 };
//             """,
//             options: Options.WithCompilationOptions(
//                 Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
//             )
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task List_creation_expression_for_list_of_lists_single_line()
//     {
//         await VerifyNoDiagnosticAsync(
//             """
//             using System.Collections.Generic;
//
//             List<List<int>> list = new() { new () { 1, 2 }, new() { 3, 4, 5 } };
//             """,
//             options: Options.WithCompilationOptions(
//                 Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
//             )
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task List_instantiation_as_named_method_param()
//     {
//         await VerifyDiagnosticAndFixAsync(
//             """
//             using System.Collections.Generic;
//
//             [|ProcessPerson(
//                 10,
//                 ids: [|new()
//                 {
//                     1, 2, 3, 4, 5
//                 }|]
//             )|];
//             ProcessPerson(
//                 10,
//                 new()
//                 {
//                     1, 2, 3, 4, 5
//                 }
//             );
//             ProcessPerson(
//                 10,
//                 ids:
//                     new() { 1, 2, 3, 4, 5 }
//             );
//             ProcessPerson(
//                 10,
//                 ids: new() { 1, 2, 3, 4, 5 }
//             );
//
//             static void ProcessPerson(int i, List<int> ids)
//             {
//                 // Process the person object
//             }
//             """,
//             """
//             using System.Collections.Generic;
//
//             ProcessPerson(
//                 10,
//                 ids:
//                     new()
//                     {
//                         1, 2, 3, 4, 5
//                     }
//             );
//             ProcessPerson(
//                 10,
//                 new()
//                 {
//                     1, 2, 3, 4, 5
//                 }
//             );
//             ProcessPerson(
//                 10,
//                 ids:
//                     new() { 1, 2, 3, 4, 5 }
//             );
//             ProcessPerson(
//                 10,
//                 ids: new() { 1, 2, 3, 4, 5 }
//             );
//
//             static void ProcessPerson(int i, List<int> ids)
//             {
//                 // Process the person object
//             }
//             """,
//             options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task Collection_expression()
//     {
//         await VerifyDiagnosticAndFixAsync(
//             """
//             using System.Collections.Generic;
//
//             List<int> list [|= [
//                 1, 2, 3, 4, 5
//             ]|];
//             [|list = [
//                 1,
//                 2,
//                 3
//             ]|];
//             [|list =
//             [|[
//                 1,
//                 2,
//                 3
//             ]|]|];
//             """,
//             """
//             using System.Collections.Generic;
//
//             List<int> list =
//                 [
//                     1, 2, 3, 4, 5
//                 ];
//             list =
//                 [
//                     1,
//                     2,
//                     3
//                 ];
//             list =
//                 [
//                     1,
//                     2,
//                     3
//                 ];
//             """,
//             options: Options.WithCompilationOptions(
//                 Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
//             )
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task Collection_expression_single_line()
//     {
//         await VerifyNoDiagnosticAsync(
//             """
//             using System.Collections.Generic;
//
//             List<int> list = [ 1, 2, 3, 4, 5 ];
//             """,
//             options: Options.WithCompilationOptions(
//                 Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
//             )
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task Collection_expression_of_collection_expression()
//     {
//         await VerifyDiagnosticAndFixAsync(
//             """
//             using System.Collections.Generic;
//
//             List<List<int>> list [|= [
//                 [ 1, 2 ],
//                 [ 3, 4, 5 ],
//                 [ 6 ]
//             ]|];
//             """,
//             """
//             using System.Collections.Generic;
//
//             List<List<int>> list =
//                 [
//                     [ 1, 2 ],
//                     [ 3, 4, 5 ],
//                     [ 6 ]
//                 ];
//             """,
//             options: Options.WithCompilationOptions(
//                 Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
//             )
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task Collection_expression_as_named_method_param()
//     {
//         await VerifyDiagnosticAndFixAsync(
//             """
//             using System.Collections.Generic;
//
//             [|ProcessPerson(
//                 10,
//                 ids: [|[
//                     1, 2, 3, 4, 5
//                 ]|]
//             )|];
//             ProcessPerson(
//                 10,
//                 [
//                     1, 2, 3, 4, 5
//                 ]
//             );
//             ProcessPerson(
//                 10,
//                 ids:
//                     [ 1, 2, 3, 4, 5 ]
//             );
//             ProcessPerson(
//                 10,
//                 ids: [ 1, 2, 3, 4, 5 ]
//             );
//
//             static void ProcessPerson(int i, List<int> ids)
//             {
//                 // Process the person object
//             }
//             """,
//             """
//             using System.Collections.Generic;
//
//             ProcessPerson(
//                 10,
//                 ids:
//                     [
//                         1, 2, 3, 4, 5
//                     ]
//             );
//             ProcessPerson(
//                 10,
//                 [
//                     1, 2, 3, 4, 5
//                 ]
//             );
//             ProcessPerson(
//                 10,
//                 ids:
//                     [ 1, 2, 3, 4, 5 ]
//             );
//             ProcessPerson(
//                 10,
//                 ids: [ 1, 2, 3, 4, 5 ]
//             );
//
//             static void ProcessPerson(int i, List<int> ids)
//             {
//                 // Process the person object
//             }
//             """,
//             options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task Linq_expression()
//     {
//          await VerifyDiagnosticAndFixAsync(
//              """
//              using System.Collections.Generic;
//              using System.Linq;
//
//              List<(string Name, string Value, bool IsValid)> collection = [];
//
//              var query [|= [|from item in collection
//                  where item.IsValid
//                  select new
//                  {
//                      Name = item.Name,
//                      Value = item.Value
//                  }|]|];
//              """,
//              """
//              using System.Collections.Generic;
//              using System.Linq;
//
//              List<(string Name, string Value, bool IsValid)> collection = [];
//
//              var query =
//                  from item in collection
//                  where item.IsValid
//                  select
//                      new
//                      {
//                          Name = item.Name,
//                          Value = item.Value
//                      };
//              """,
//              options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
//          );
//      }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task Interpolated_Verbatim_string_single_line_interpolation()
//     {
//         await VerifyDiagnosticAndFixAsync(
//             """
//             using System.Linq;
//
//             int? x = 1;
//             var message [|= $@"User details:
//             {Enumerable.Range(1, 10).Select(i => x.HasValue ? $"Value: {x.Value + i}" : "No value")}"|];
//             """,
//             """
//             using System.Linq;
//
//             int? x = 1;
//             var message =
//                 $@"User details:
//             {Enumerable.Range(1, 10).Select(i => x.HasValue ? $"Value: {x.Value + i}" : "No value")}";
//             """,
//             options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task Interpolated_Verbatim_string_multiline_interpolation()
//     {
//         await VerifyDiagnosticAndFixAsync(
//             """
//             using System.Linq;
//
//             int? x = 1;
//             var message [|= $@"User details:
//             {
//                 [|Enumerable.Range(1, 10).Select(i =>
//                     x.HasValue ? $"Value: {x.Value + i}" : "No value")|]
//             }"|];
//             """,
//             """
//             using System.Linq;
//
//             int? x = 1;
//             var message =
//                 $@"User details:
//             {
//                 Enumerable.Range(1, 10).Select(
//                     i =>
//                         x.HasValue ? $"Value: {x.Value + i}" : "No value"
//                 )
//             }";
//             """,
//             options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task Raw_string()
//     {
//         await VerifyDiagnosticAndFixAsync(
//             """"
//             string s [|= """
//                          abc
//                              cde
//                          fgh
//                          """|];
//             """",
//             """"
//             string s =
//                 """
//                 abc
//                     cde
//                 fgh
//                 """;
//             """",
//             options: Options.WithCompilationOptions(
//                 Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
//             )
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task Raw_string_with_single_line_interpolation()
//     {
//         await VerifyNoDiagnosticAsync(
//             """"
//             int x = 10;
//             string s = $"""{x}abc{x}cde{x}fgh{x}""";
//             """",
//             options: Options.WithCompilationOptions(
//                 Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
//             )
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task Raw_string_with_interpolation()
//     {
//         await VerifyDiagnosticAndFixAsync(
//             """"
//             int x = 10;
//             string s [|= $"""
//                          {x}abc
//                              {x}cde{x}
//                          fgh{x}
//                          """|];
//             """",
//             """"
//             int x = 10;
//             string s =
//                 $"""
//                 {x}abc
//                     {x}cde{x}
//                 fgh{x}
//                 """;
//             """",
//             options: Options.WithCompilationOptions(
//                 Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
//             )
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task Raw_string_with_interpolation_string_at_the_beginning_of_line()
//     {
//         await VerifyDiagnosticAndFixAsync(
//             """"
//             int x = 10;
//             string s [|= $"""
//             {x}abc
//
//                 {x}cde{x}
//
//             fgh{x}
//             aaa bbb
//             """|];
//             """",
//             """"
//             int x = 10;
//             string s =
//                 $"""
//                 {x}abc
//
//                     {x}cde{x}
//
//                 fgh{x}
//                 aaa bbb
//                 """;
//             """",
//             options: Options.WithCompilationOptions(
//                 Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
//             )
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task Raw_string_utf8()
//     {
//         await VerifyDiagnosticAndFixAsync(
//             """"
//             var s [|= """
//                     abc
//                         cde
//                     """u8|];
//             """",
//             """"
//             var s =
//                 """
//                 abc
//                     cde
//                 """u8;
//             """",
//             options: Options.WithCompilationOptions(
//                 Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
//             )
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task Raw_string_as_method_parameter()
//     {
//         await VerifyDiagnosticAndFixAsync(
//             """"
//             [|C.Check("""
//                 abc
//                 cde
//                 """)|];
//             """",
//             """"
//             C.Check(
//                 """
//                 abc
//                 cde
//                 """
//             );
//             """",
//             additionalFiles:
//                 new (string source, string expectedSource)[]
//                 {
//                     (
//                         source:
//                         """
//                         public static class C
//                         {
//                             public static bool Check(string s) => true;
//                         }
//                         """,
//                         expectedSource: null
//                     )
//                 },
//             options: Options.WithCompilationOptions(
//                 Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
//             )
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task Raw_string_as_named_method_parameter()
//     {
//         await VerifyDiagnosticAndFixAsync(
//             """"
//             [|C.Check(
//                 s: "tst",
//                 options: """
//                 abc
//                 cde
//                 """)|];
//             """",
//             """"
//             C.Check(
//                 s: "tst",
//                 options:
//                     """
//                     abc
//                     cde
//                     """
//             );
//             """",
//             additionalFiles:
//                 new (string source, string expectedSource)[]
//                 {
//                     (
//                         source:
//                         """
//                         public static class C
//                         {
//                             public static bool Check(string s, string options) => true;
//                         }
//                         """,
//                         expectedSource: null
//                     )
//                 },
//             options: Options.WithCompilationOptions(
//                 Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
//             )
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task Raw_string_with_complex_structure()
//     {
//         await VerifyDiagnosticAndFixAsync(
//             """"
//             using System.Threading.Tasks;
//
//             Tst c = new();
//             await c.Test();
//
//             [|public class Tst {
//                 public async Task Test()
//                 {
//                     [|await [|C.Check("""
//             class C
//             {
//                 void M()
//                 {
//                     var x = new[] { "" };
//                 }
//             }
//             """, """
//             class C
//             {
//                 void M()
//                 {
//                     var x = new string[] { "" };
//                 }
//             }
//             """, options: "tst")|]|];
//                 }
//             }|]
//             """",
//             """"
//             using System.Threading.Tasks;
//
//             Tst c = new();
//             await c.Test();
//
//             public class Tst
//             {
//                 public async Task Test()
//                 {
//                     await C.Check(
//                         """
//                         class C
//                         {
//                             void M()
//                             {
//                                 var x = new[] { "" };
//                             }
//                         }
//                         """,
//                         """
//                         class C
//                         {
//                             void M()
//                             {
//                                 var x = new string[] { "" };
//                             }
//                         }
//                         """, options: "tst"
//                     );
//                 }
//             }
//             """",
//             additionalFiles:
//                 new (string source, string expectedSource)[]
//                 {
//                     (
//                         source:
//                         """
//                         using System.Threading.Tasks;
//
//                         public static class C
//                         {
//                             public static Task<bool> Check(string s, string t, string options) => Task.FromResult(true);
//                         }
//                         """,
//                         expectedSource: null
//                     )
//                 },
//             options: Options.WithCompilationOptions(
//                 Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
//             )
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task Binary_expression_assignment_to_variable()
//     {
//         await VerifyDiagnosticAndFixAsync(
//             """
//             object obj = "abc";
//             var result [|= obj is string s
//                 && s.Length == 10|];
//             """,
//             """
//             object obj = "abc";
//             var result =
//                 obj is string s
//                 && s.Length == 10;
//             """,
//             options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task Binary_expression_with_complex_structure_and_brackets_assignment_to_variable()
//     {
//         await VerifyDiagnosticAndFixAsync(
//             """
//             object obj = "abc";
//             var result [|= obj is string s
//                 && s.Length == 10 || [|(
//                 obj is string s1
//                 && s1.Length == 11
//                 || [|(obj is string s2
//                     && s2.Length == 12 || ((13 ^ 14) == 15))|])|]|];
//             """,
//             """
//             object obj = "abc";
//             var result =
//                 obj is string s
//                 && s.Length == 10
//                 || (
//                     obj is string s1
//                     && s1.Length == 11
//                     || (
//                         obj is string s2
//                         && s2.Length == 12
//                         || ((13 ^ 14) == 15)
//                     )
//                 );
//             """,
//             options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task Parenthesized_Expression_with_await_and_chained_method()
//     {
//         await VerifyDiagnosticAndFixAsync(
//             """
//             using System.Linq;
//             using System.Threading.Tasks;
//
//             int i = 10;
//             var result [|= [|[|(await (
//                 i switch
//                 {
//                     > 20 => Task.Run(() => Enumerable.Range(1, 100)),
//                     > 10 => Task.Run(() => Enumerable.Range(1, 19)),
//                     > 0 => Task.Run(() => Enumerable.Range(1, 10)),
//                     _ => Task.FromResult(Enumerable.Empty<int>())
//                 }
//             ))|]
//             .ToList()|]|];
//             """,
//             """
//             using System.Linq;
//             using System.Threading.Tasks;
//
//             int i = 10;
//             var result =
//                 (
//                     await (
//                         i switch
//                         {
//                             > 20 => Task.Run(() => Enumerable.Range(1, 100)),
//                             > 10 => Task.Run(() => Enumerable.Range(1, 19)),
//                             > 0 => Task.Run(() => Enumerable.Range(1, 10)),
//                             _ => Task.FromResult(Enumerable.Empty<int>())
//                         }
//                     )
//                 )
//                 .ToList();
//             """,
//             options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task Property_get_only_with_lambda_body()
//     {
//         await VerifyDiagnosticAndFixAsync(
//             """
//             [|public class C {
//                 public bool Options
//                     => [|CheckOptionsCalculatedFor(
//                         "Option1",
//                             "Option2" // broken formatting is expected, no changes should be provided
//                     )|];
//
//                 public bool OptionsWithComment
//                     => /*what if comment is here? */ [|CheckOptionsCalculatedFor(
//                     "Option1", "Option2" // broken formatting is expected, no changes should be provided
//                     )|];
//
//                 public static bool CheckOptionsCalculatedFor(string option1, string option2) => true;
//             }|]
//             """,
//             """
//             public class C
//             {
//                 public bool Options
//                     =>
//                         CheckOptionsCalculatedFor(
//                             "Option1",
//                             "Option2" // broken formatting is expected, no changes should be provided
//                         );
//
//                 public bool OptionsWithComment
//                     => /*what if comment is here? */
//                         CheckOptionsCalculatedFor(
//                             "Option1", "Option2" // broken formatting is expected, no changes should be provided
//                         );
//
//                 public static bool CheckOptionsCalculatedFor(string option1, string option2) => true;
//             }
//             """
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task Method_with_lambda_body()
//     {
//         await VerifyDiagnosticAndFixAsync(
//             """
//             [|public class C {
//                 public bool GetOptions() => [|CheckOptionsCalculatedFor(
//                     "Option1",
//                         "Option2" // broken formatting is expected, no changes should be provided
//                 )|];
//
//                 public bool GetOptionsWithComment() => /*what if comment is here? */ [|CheckOptionsCalculatedFor(
//                     "Option1",
//                         "Option2" // broken formatting is expected, no changes should be provided
//                 )|];
//
//                 public static bool CheckOptionsCalculatedFor(string option1, string option2) => true;
//             }|]
//             """,
//             """
//             public class C
//             {
//                 public bool GetOptions() =>
//                     CheckOptionsCalculatedFor(
//                         "Option1",
//                         "Option2" // broken formatting is expected, no changes should be provided
//                     );
//
//                 public bool GetOptionsWithComment() => /*what if comment is here? */
//                     CheckOptionsCalculatedFor(
//                         "Option1",
//                         "Option2" // broken formatting is expected, no changes should be provided
//                     );
//
//                 public static bool CheckOptionsCalculatedFor(string option1, string option2) => true;
//             }
//             """
//         );
//     }
//
    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Chaining_single_lined()
    {
        await VerifyNoDiagnosticAsync(
            """
            using System.Linq;

            int x = Enumerable.Range(1, 10).Where(i => i > 5).Select(i => i).Count();
            """,
            options: Options.WithCompilationOptions(
                Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
            )
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Chaining_simple()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System;
            using System.Linq;

            C c = new C();

            [|c.M[|(i =>
                i > 5)|]|];
            """,
            """
            using System;
            using System.Linq;

            C c = new C();

            c.M(
                i =>
                    i > 5
            );
            """,
            additionalFiles:
            new (string source, string expectedSource)[]
            {
                (
                    source:
                    """
                    using System;
                    using System.Linq;
                    using System.Collections.Generic;

                    public sealed class C
                    {
                        public C P => this;
                        public C this[int index] => this;
                        public IEnumerable<C> E => Enumerable.Empty<C>();

                        public C M(Func<int, object> act = null) => this;
                    }
                    """,
                    expectedSource: null
                )
            },
            options: Options.WithCompilationOptions(
                Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
            )
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Chaining()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System.Linq;

            int x [|= [|[|[|[|[|[|Enumerable.Range(1, 10)
                .Select(a => a)
                    .Where|](b => b > 5)|]
            .Select|](c => c)|].Count|]()|]|];
            """,
            """
            using System.Linq;

            int x =
                Enumerable.Range(1, 10)
                    .Select(a => a)
                    .Where(b => b > 5)
                    .Select(c => c).Count();
            """,
            options: Options.WithCompilationOptions(
                Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
            )
        );
    }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task Chaining_top_level()
//     {
//         await VerifyDiagnosticAndFixAsync(
//             """
//             using System.Linq;
//
//             [|[|[|[|[|Enumerable.Range(1, 10).Select(i => i)|]
//             .Where([|i => {
//                 return i > 5; }|])|].Where([|i => {
//             return i > 6; }|])|]
//             .Select(i => i)|].Count()|];
//             """,
//             """
//             using System.Linq;
//
//             Enumerable.Range(1, 10)
//                 .Select(i => i)
//                 .Where(
//                     i =>
//                     {
//                         return i > 5;
//                     }
//                 )
//                 .Where(
//                     i =>
//                     {
//                         return i > 6;
//                     }
//                 )
//                 .Select(i => i)
//                 .Count();
//             """,
//             options: Options.WithCompilationOptions(
//                 Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
//             )
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task Chaining_as_method_param()
//     {
//         await VerifyDiagnosticAndFixAsync(
//             """
//             using System.Linq;
//
//             [|C.Check([|[|[|[|[|Enumerable.Range(1, 10)
//             .Select(i => i)|]
//             .Where([|i => {
//                 return i > 5; }|])|].Where([|i => {
//             return i > 6; }|])|]
//             .Select(i => i)|].Count()|])|];
//             """,
//             """
//             using System.Linq;
//
//             C.Check(
//                 Enumerable.Range(1, 10)
//                     .Select(i => i)
//                     .Where(
//                         i =>
//                         {
//                             return i > 5;
//                         }
//                     )
//                     .Where(
//                         i =>
//                         {
//                             return i > 6;
//                         }
//                     )
//                     .Select(i => i)
//                     .Count()
//             );
//             """,
//             additionalFiles:
//                 new (string source, string expectedSource)[]
//                 {
//                     (
//                         source:
//                         """
//                         public static class C
//                         {
//                             public static bool Check(int i) => true;
//                         }
//                         """,
//                         expectedSource: null
//                     )
//                 },
//             options: Options.WithCompilationOptions(
//                 Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
//             )
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task Chaining_in_the_chaining_comments_and_complex_accesses()
//     {
//         await VerifyDiagnosticAndFixAsync(
//             """
//             using System.Linq;
//
//                 var x = Enumerable.Range(1, 10).Select(i => new C());
//                 [|[|x.SelectMany([|c => {
//                         return [|[|c.M() // Trailing
//                     // leading
//                     .M() /*
//
//             trailing multiline1
//             trailing multiline2 */|]
//                     .M()|]
//                     ?[|[|.M()!.M()
//                     .X.X[0]
//                     .M()|]
//                     .E
//                     .SelectMany([|i => {
//                         return [|[|i.M() // Trailing
//                             // leading
//                             .M() /*
//                         multiline1
//                         multiline2 */|]
//                             .M()|]
//                             ?[|[|.M()!.M()
//                             .X.X[0]
//                             .M()|]
//                             .E
//                             .Select([|e => {
//                                 return e;
//                             }|])|];
//                     }|])|];
//                 }|])|].ToList()|];
//             """,
//             """
//             using System.Linq;
//
//             var x = Enumerable.Range(1, 10).Select(i => new C());
//             x
//                 .SelectMany(
//                     c =>
//                     {
//                         return c.M() // Trailing
//                             // leading
//                             .M() /*
//
//             trailing multiline1
//             trailing multiline2 */
//                             .M()
//                             ?.M()
//                             !.M()
//                             .X
//                             .X[0]
//                             .M()
//                             .E
//                             .SelectMany(
//                                 i =>
//                                 {
//                                     return i.M() // Trailing
//                                         // leading
//                                         .M() /*
//                                     multiline1
//                                     multiline2 */
//                                         .M()
//                                         ?.M()
//                                         !.M()
//                                         .X
//                                         .X[0]
//                                         .M()
//                                         .E
//                                         .Select(
//                                             e =>
//                                             {
//                                                 return e;
//                                             }
//                                         );
//                                 }
//                             );
//                     }
//                 )
//                 .ToList();
//             """,
//             additionalFiles:
//                 new (string source, string expectedSource)[]
//                 {
//                     (
//                         source:
//                         """
//                         using System.Linq;
//                         using System.Collections.Generic;
//
//                         public sealed class C
//                         {
//                             public C X => this;
//                             public C this[int index] => this;
//                             public IEnumerable<C> E => Enumerable.Empty<C>();
//
//                             public C M() => this;
//                         }
//                         """,
//                         expectedSource: null
//                     )
//                 },
//             options: Options.WithCompilationOptions(
//                 Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication)
//             )
//         );
//     }

    // Chaining in the chained chaining

//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task Fixes_Structural_Honesty_for_ternary_expression()
//     {
//         await VerifyDiagnosticAndFixAsync(
//             """
//             object obj = "abc";
//             var result = [|obj is string s
//                 ? s.Length
//                 : obj is int i
//                     ? i
//                     : 0|];
//             result =
//                 [|obj is string s1
//                         ? s1.Length
//                         : 0|];
//             result =
//                 [|obj is string s3
//                     ? s3.Length : 0|];
//             result =
//                 [|obj is string s4 ? s4.Length
//                     : 0|];
//             result =
//                 obj is string s5
//                     ?
//                         s5.Length
//                     :
//                         0;
//             result = obj is string s2 ? s2.Length : 0;
//             """,
//             """
//             object obj = "abc";
//             var result =
//                 obj is string s
//                     ? s.Length
//                     : obj is int i
//                         ? i
//                         : 0;
//             result =
//                 obj is string s1
//                     ? s1.Length
//                     : 0;
//             result =
//                 obj is string s3
//                     ? s3.Length
//                     : 0;
//             result =
//                 obj is string s4
//                     ? s4.Length
//                     : 0;
//             result =
//                 obj is string s5
//                     ?
//                         s5.Length
//                     :
//                         0;
//             result = obj is string s2 ? s2.Length : 0;
//             """,
//             options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
//         );
//     }

// class C
// {
//     C X => this;
//     C this[int index] => this;
//     IEnumerable<C> E => Enumerable<C>.Empty;
//
//     C M()
//     {
//         var x = new C();
//
//         return x.M() // Trailing
//             // leading
//             .M() /*
//         asdgasd
//         asdgasd */ .M()
//             ?.M()
//             !.M()
//             .X
//             .X[0]
//             .M();
//     }
// }

//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task Fixes_Structural_Honesty_for_lambda_inside_chaining()
//     {
//         await VerifyDiagnosticAndFixAsync(
//             """
//             int x =
//                 Enumerable.Range(1, 10)
//                     .Select(i => i)
//                     .Where([|i => {
//                         return i > 5; }|]
//                     )
//                     .Select(i => i).Count());
//             """,
//             """
//             int x =
//                 Enumerable.Range(1, 10)
//                     .Select(i => i)
//                     .Where(
//                         i =>
//                         {
//                             return i > 5;
//                         }
//                     )
//                     .Select(i => i).Count());
//             """,
//             options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task Fixes_Structural_Honesty_for_new_object_inside_chaining()
//     {
//         await VerifyDiagnosticAndFixAsync(
//             """
//             var x =
//                 Enumerable.Range(1, 10)
//                     .Select(i => i)
//                     .Where(i => i > 5
//                     .Select([|i => [|new {
//                         i = i,
//                         b = i + 1
//                     }|]|])
//                     .Count();
//             var y =
//                 Enumerable.Range(1, 10)
//                     .Select(i => i)
//                     .Where(i => i > 5
//                     .Select([|i => { return [|new {
//                         i = i,
//                         b = i + 1
//                     };|]}|])
//                     .Count();
//             """,
//             """
//             var x =
//                 Enumerable.Range(1, 10)
//                     .Select(i => i)
//                     .Where(i => i > 5
//                     .Select(
//                         i =>
//                             new {
//                                 i = i,
//                                 b = i + 1
//                             }
//                     )
//                     .Count();
//             var y =
//             Enumerable.Range(1, 10)
//                 .Select(i => i)
//                 .Where(i => i > 5
//                 .Select(
//                     i => {
//                         return
//                             new
//                             {
//                                 i = i,
//                                 b = i + 1
//                             };
//                     }
//                  )
//                 .Count();
//             """,
//             options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
//         );
//     }
//
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task Fixes_Structural_Honesty_when_return_in_front_of()
//     {
//         await VerifyDiagnosticAndFixAsync(
//             """
//             Func<object> f =
//                 () =>
//                 {
//                     return [|Enumerable.Range(1, 10)
//                         .Select(i => i)
//                         .Where(i => i > 5)
//                         .Select(i => i).Count()|];
//                 };
//             Func<object> f2 =
//                 () =>
//                 {
//                     return [|new {
//                         i = 1,
//                         b = 2
//                     }|];
//                 };
//             Func<object> f3 =
//                 () =>
//                 {
//                     return [|(int i, int b) =>
//                     {
//                         return i + b;
//                     }|];
//                 };
//             """,
//             """
//             Func<object> f =
//                 () =>
//                 {
//                     return
//                         Enumerable.Range(1, 10)
//                             .Select(i => i)
//                             .Where(i => i > 5)
//                             .Select(i => i).Count();
//                 };
//             Func<object> f2 =
//                 () =>
//                 {
//                     return
//                         new {
//                             i = 1,
//                             b = 2
//                         };
//                 };
//             Func<object> f3 =
//                 () =>
//                 {
//                     return
//                         (int i, int b) =>
//                         {
//                             return i + b;
//                         };
//                 };
//             """,
//             options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
//         );
//     }
//     [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
//     public async Task Fixes_Structural_Honesty_for_collection_expression_as_parameter()
//     {
//         await VerifyDiagnosticAndFixAsync(
//             """
//             using System.Threading.Tasks;
//
//             int myVariable =
//                 [|await MyMethodAsync(
//                     10, [|[
//                         1,
//                         2,
//                         3
//                     ]|]
//                 )|];
//
//             Task<int> MyMethodAsync(int i, int[] arr) => Task.FromResult(1);
//             """,
//             """
//             using System.Threading.Tasks;
//
//             int myVariable =
//                 await MyMethodAsync(
//                     10,
//                     [
//                         1,
//                         2,
//                         3
//                     ]
//                 );
//
//             Task<int> MyMethodAsync(int i, int[] arr) => Task.FromResult(1);
//             """,
//             options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
//         );
//     }

    // Parameter list
    // Parameter list with /* parameter comment */
    // Assignment syntax tests
    // BooleanExpression tests
    // Something after lambda arrow tests
    // Return expression the same way as EqualsExpression
    // AwaitExpression_Method_with_InvocationExpression_on_new_line for chaining
    // InvocationExpression inside InvocationExpression
    // directives in trivia
    // documentation trivia
    // Method with passed tuple
    // Multiline lambda parameters
    // Attribute and Attribute arguments
    // ? Relaxed option
    // Constructor initializers  Constr() : base( something
    //                                      )
    // Array as parameter
    // Interpolated multiline (by multiline code) string
    // Interpolated verbatim string
    // for, if, while, foreach, using, lock, switch in case of multiline inside
    // BinaryExpressions in brackets
    // Line concatenation with +

    // tokenKind
    //     is SyntaxKind.OpenParenToken
    //         or SyntaxKind.OpenBraceToken
    //         or SyntaxKind.EqualsGreaterThanToken

    // Variable, Property, Field, Const, other declaration syntaxes to be added as tests and into analyzer and fix providers.
    // Anything that can be on the left side of EqualsValueClauseSyntax
}
