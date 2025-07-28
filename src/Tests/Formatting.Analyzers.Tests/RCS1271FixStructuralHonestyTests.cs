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
    public async Task Test()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System;
            using System.Threading.Tasks;

            int result = 
            [|await MyMethodAsync(
            [|// Comment
                async (int a, int b, int c) =>
                {
                // Comment
                    return await Task.Run(() => 10);
                        // Comment
                }|]
            )|];

            Task<int> MyMethodAsync(Func<int, int, int, Task<int>> f)
                => Task.FromResult(1);

            //int MyMethod(Func<int> f) => 1;
            int MyMethod(Func<int, int, int, int> f) => 1;
            int MyMethod2(Func<int, int> f) => 1;
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

            //int MyMethod(Func<int> f) => 1;
            int MyMethod(Func<int, int, int, int> f) => 1;
            int MyMethod2(Func<int, int> f) => 1;
            """,
            options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Fixes_Structural_Honesty_in_case_of_comments()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System;
            using System.Threading.Tasks;

            // Cases with comments
            // One line comments
            
            int result = 
            [|await MyMethodAsync(
            [|// Comment
                async (int a, int b, int c) =>
                {
                // Comment
                    return await Task.Run(() => 10);
                        // Comment
                }|]
            )|];
            
            result = 
            [|await MyMethodAsync(
                // Comment
                async (int a, int b, int c) =>
                {
                    // Comment
                    return await Task.Run(() => 10);
                    // Comment
                }
            )|];

            result = 
                    [|await MyMethodAsync(
                        // Comment
                        async (int a, int b, int c) =>
                        {
                            // Comment
                            return await Task.Run(() => 10);
                            // Comment
                        }
                    )|];
            
            result = 
            [|await MyMethodAsync(// Comment
                async (int a, int b, int c) =>
                {
                    // Comment
                    return await Task.Run(() => 10);
                    // Comment
                }
            )|];
            
            result = [|await MyMethodAsync(/*comment*/[|async (int a, int b, int c) =>
            /*comment*/{
                return await Task.Run(() => 10);
            /*comment*/}/*comment*/|])|];

            // Multiline
            result =/*comment1
                comment2
            comment3*/ [|await MyMethodAsync(/*comment4
            comment5*/[|async (int a, int b, int c) =>
            /*comment6
            comment7*/{/*comment12
            comment13*/ return await Task.Run(() => 10);
            /*comment8
            comment9*/}/*comment10
            comment11*/|])|];
            
            Task<int> MyMethodAsync(Func<int, int, int, Task<int>> f)
                => Task.FromResult(1);
            
            int MyMethod(Func<int> f) => 1;
            int MyMethod2(Func<int, int> f) => 1;
            """,
            """
            using System;
            using System.Threading.Tasks;
            
            // Cases with comments
            // One line comments
            
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
            
            result = 
                await MyMethodAsync(
                    // Comment
                    async (int a, int b, int c) =>
                    {
                        // Comment
                        return await Task.Run(() => 10);
                        // Comment
                    }
                );
            
            result = 
                await MyMethodAsync(
                    // Comment
                    async (int a, int b, int c) =>
                    {
                        // Comment
                        return await Task.Run(() => 10);
                        // Comment
                    }
                );
            
            result = 
                await MyMethodAsync(// Comment
                    async (int a, int b, int c) =>
                    {
                        // Comment
                        return await Task.Run(() => 10);
                        // Comment
                    }
                );
            
            result = 
                await MyMethodAsync(/*comment*/
                    async (int a, int b, int c) =>
                    /*comment*/
                    {
                        return await Task.Run(() => 10);
                        /*comment*/
                    }
                    /*comment*/
                );

            // Multiline
            result =
                /*comment1
                comment2
                comment3*/ 
                await MyMethodAsync(
                    /*comment4
                    comment5*/
                    async (int a, int b, int c) =>
                    /*comment6
                    comment7*/
                    {
                        /*comment12
                        comment13*/ 
                        return await Task.Run(() => 10);
                        /*comment8
                        comment9*/
                    }
                    /*comment10
                    comment11*/
                );
            
            Task<int> MyMethodAsync(Func<int, int, int, Task<int>> f)
                => Task.FromResult(1);
            
            int MyMethod(Func<int> f) => 1;
            int MyMethod2(Func<int, int> f) => 1;
            """,
            options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Fixes_Structural_Honesty_for_func_returning_variable_and_accepting_lambda()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System;
            using System.Threading.Tasks;

            int result = [|await MyMethodAsync([|async (int a, int b, int c) =>
            {
                return await Task.Run(() => 10);
            }|])|];
            int result2 = [|MyMethod([|() =>
            {
                return 10;
            }|])|];
            int result3 = [|MyMethod([|() =>
            10|])|];
            int result4 = [|MyMethod2([|x =>
            {
                return 10;
            }|])|];

            Task<int> MyMethodAsync(Func<int, int, int, Task<int>> f)
                => Task.FromResult(1);

            int MyMethod(Func<int> f) => 1;
            int MyMethod2(Func<int, int> f) => 1;
            """,
            """
            using System;
            using System.Threading.Tasks;

            int result = 
                await MyMethodAsync(
                    async (int a, int b, int c) =>
                    {
                        return await Task.Run(() => 10);
                    }
                );
            int result2 = 
                MyMethod(
                    () =>
                    {
                        return 10;
                    }
                );
            int result3 = 
                MyMethod(
                    () =>
                        10
                );
            int result4 = 
                MyMethod2(
                    x =>
                    {
                        return 10;
                    }
                );

            Task<int> MyMethodAsync(Func<int, int, int, Task<int>> f)
                => Task.FromResult(1);

            int MyMethod(Func<int> f) => 1;
            int MyMethod2(Func<int, int> f) => 1;
            """,
            options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Fixes_Structural_Honesty_for_misaligned_method()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System;
            using System.Threading.Tasks;

            int result = 
                [|await MyMethodAsync(
                    1,
                    2
                    )|];
            int result2 = 
                [|await MyMethodAsync(
                    1, 2
            )|];
            int result3 = 
            [|await MyMethodAsync(
                    1, 2)|];
            int result4 = 
            [|MyMethod(
                    1, 2
            )|];
            int result5 = 
            [|MyMethod(
                    1, 2
                    )|];
            int result6 = 
                [|MyMethod(
                    1, 2)|];

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
                    1, 2
                );
            int result3 = 
                await MyMethodAsync(
                    1, 2
                );
            int result4 = 
                MyMethod(
                    1, 2
                );
            int result5 = 
                MyMethod(
                    1, 2
                );
            int result6 = 
                MyMethod(
                    1, 2
                );

            Task<int> MyMethodAsync(int a, int b) => Task.FromResult(1);
            int MyMethod(int a, int b) => 1;
            """,
            options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Fixes_Structural_Honesty_for_non_async_func_returning_variable_and_accepting_lambda()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System;
            
            int result = [|MyMethod([|(int a, int b, int c) =>
            {
                return 10;
            }|])|];
            int result2 = [|MyMethod([|(int a, int b, int c)=> 
                10|])|];
            int result3 = [|MyMethod([|(int a, int b, int c) 
                => 10|])|];

            int MyMethod(Func<int, int, int, int> f) => 1;
            """,
            """
            using System;
            
            int result = 
                MyMethod(
                    (int a, int b, int c) =>
                    {
                        return 10;
                    }
                );
            int result2 = 
                MyMethod(
                    (int a, int b, int c)=> 
                        10
                );
            int result3 = 
                MyMethod(
                    (int a, int b, int c) 
                        => 10
                );

            int MyMethod(Func<int, int, int, int> f) => 1;
            """,
            options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Fixes_Structural_Honesty_for_chained_method_and_accepting_lambda()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System;
            using System.Threading.Tasks;
            
            [|await C.Instance.MyMethodAsync([|async (int a, int b, int c) =>
            {
                    await Task.Delay(100);
                return 10;
            }|])|];
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
                            
                            public class C {
                                public static C Instance { get; } = new C();

                                public Task<bool> MyMethodAsync(Func<int, int, int, Task<int>> f) => Task.FromResult(true);
                            }
                            """,
                        expectedSource: null
                    )
                },
            options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Fixes_Structural_Honesty_for_lambda_variable_declaration()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System;

            Action<int, int, int> myAction = [|(int a, int b, int c) =>
            {
                // ...
            }|];
            Action<int, int, int> myAction2 = [|(int a, int b, int c) => {
                // ...
            }|];
            """,
            """
            using System;

            Action<int, int, int> myAction = 
                (int a, int b, int c) =>
                {
                    // ...
                };
            Action<int, int, int> myAction2 = 
                (int a, int b, int c) => 
                {
                    // ...
                };
            """,
            options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task No_diagnostic_for_single_lined_lambda_variable_declaration()
    {
        await VerifyNoDiagnosticAsync(
            """
            using System;
            
            Action<int, int, int> myAction = (int a, int b, int c) => { /* ... */ };
            Action<int, int, int> myAction2 = 
                (int a, int b, int c) => { /* ... */ };
            """,
            options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Fixes_Structural_Honesty_for_func_returning_variable_and_accepting_new_object()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System;
            using System.Threading.Tasks;
            
            int myVariable = [|await MyMethodAsync([|new MyType()
            {
                Property1 = 1,
                Property2 = 2
            }|])|];

            myVariable = [|await MyMethodAsync(/*
            Comment1
            Comment2
            */[|new MyType()
            // Comment 3
            {
            //Comment 4
                Property1 = 1,
                Property2 = 2
            // Comment 5
            }/*Comment6*/|])|];
            
            Task<int> MyMethodAsync(MyType mt)
                => Task.FromResult(1);
            """,
            """
            using System;
            using System.Threading.Tasks;
            
            int myVariable = 
                await MyMethodAsync(
                    new MyType()
                    {
                        Property1 = 1,
                        Property2 = 2
                    }
                );
            
            myVariable = 
                await MyMethodAsync(
                    /*
                    Comment1
                    Comment2
                    */
                    new MyType()
                    // Comment 3
                    {
                        //Comment 4
                        Property1 = 1,
                        Property2 = 2
                        // Comment 5
                    }
                    /*Comment6*/
                );
            
            Task<int> MyMethodAsync(MyType mt)
                => Task.FromResult(1);
            """,
            additionalFiles:
                new (string source, string expectedSource)[]
                {
                    (
                        source:
                        """
                        public sealed class MyType
                        {
                            public required int Property1 { get; init; }
                            public required int Property2 { get; init; }
                        }
                        """,
                        expectedSource: null
                    )
                },
            options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Fixes_Structural_Honesty_for_func_returning_variable_and_accepting_single_lined_new_object()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System;
            using System.Threading.Tasks;
            
            int myVariable = [|await MyMethodAsync(
                new MyType() { Property1 = 1, Property2 = 2 })|];

            Task<int> MyMethodAsync(MyType mt) => Task.FromResult(1);
            """,
            """
            using System;
            using System.Threading.Tasks;
            
            int myVariable = 
                await MyMethodAsync(
                    new MyType() { Property1 = 1, Property2 = 2 }
                );

            Task<int> MyMethodAsync(MyType mt) => Task.FromResult(1);
            """,
            additionalFiles:
                new (string source, string expectedSource)[]
                {
                    (
                        source:
                        """
                        public sealed class MyType
                        {
                            public required int Property1 { get; init; }
                            public required int Property2 { get; init; }
                        }
                        """,
                        expectedSource: null
                    )
                },
            options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task No_Structural_Honesty_diagnostic_for_single_lined_func_that_accepting_single_lined_new_object()
    {
        await VerifyNoDiagnosticAsync(
            """
            using System;
            using System.Threading.Tasks;

            int myVariable = await MyMethodAsync(new MyType() { Property1 = 1, Property2 = 2 });

            Task<int> MyMethodAsync(MyType mt) => Task.FromResult(1);
            """,
            additionalFiles:
                [
                    """
                    public sealed class MyType
                    {
                        public required int Property1 { get; init; }
                        public required int Property2 { get; init; }
                    }
                    """
                ],
            options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Fixes_Structural_Honesty_for_new_object()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            MyType myVariable = [|new MyType
            {
                Property1 = 1,
                Property2 = 2,
                Nested = [|new MyType
                {
                    Property1 = 3,
                    Property2 = 4,
                    Nested = null
                }|]
            }|];
            MyType myVariable2 = [|new MyType {
                        Property1 = 1,
                        Property2 = 2,
            Nested = [|new MyType
            {
                Property1 = 3,
                Property2 = 4,
                Nested = null
            }|]
                    }|];
            """,
            """
            MyType myVariable = 
                new MyType
                {
                    Property1 = 1,
                    Property2 = 2,
                    Nested = 
                        new MyType
                        {
                            Property1 = 3,
                            Property2 = 4,
                            Nested = null
                        }
                };
            MyType myVariable2 = 
                new MyType 
                {
                    Property1 = 1,
                    Property2 = 2,
                    Nested = 
                        new MyType
                        {
                            Property1 = 3,
                            Property2 = 4,
                            Nested = null
                        }
                };
            """,
            additionalFiles:
                new (string source, string expectedSource)[]
                {
                    (
                        source:
                        """
                        public sealed class MyType
                        {
                            public required int Property1 { get; init; }
                            public required int Property2 { get; init; }
                            public required MyType Nested { get; init; }
                        }
                        """,
                        expectedSource: null
                    )
                },
            options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Fixes_Structural_Honesty_for_new_record_with_with()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            var person = [|new Person("John", 30) with
            {
                Age = 31
            }|];
            var person2 = [|person with
            {
                Age = 31
            }|];
            """,
            """
            var person = 
                new Person("John", 30) with
                {
                    Age = 31
                };
            var person2 = 
                person with
                {
                    Age = 31
                };
            """,
            additionalFiles:
                new (string source, string expectedSource)[]
                {
                    (
                        source:
                        "public sealed record Person(string Name, int Age);",
                        expectedSource: null
                    )
                },
            options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Fixes_Structural_Honesty_new_object_that_accepting_lambda()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System;
            
            var person = [|new Person([|(int v1, int v2) => 
            {
                return v1 + v2;
            }|])|];
            var person2 = [|new Person([|(int v1, int v2) => {
                return v1 + v2;
            }|])|];
            var person3 = [|new Person([|(int v1, int v2) =>
                v1 + v2|]
            )|];
            var person4 = new Person((int v1, int v2) => v1 + v2);
            """,
            """
            using System;
            
            var person = 
                new Person(
                    (int v1, int v2) => 
                    {
                        return v1 + v2;
                    }
                );
            var person2 = 
                new Person(
                    (int v1, int v2) => 
                    {
                        return v1 + v2;
                    }
                );
            var person3 = 
                new Person(
                    (int v1, int v2) =>
                        v1 + v2
                );
            var person4 = new Person((int v1, int v2) => v1 + v2);
            """,
            additionalFiles:
                new (string source, string expectedSource)[]
                {
                    (
                        source:
                            """
                            using System;
                
                            public sealed record Person(Func<int, int, int> Action);
                            """,
                        expectedSource: null
                    )
                },
            options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Fixes_Structural_Honesty_for_new_object_with_nested_single_lined_new_object()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            MyType myVariable = [|new MyType
            {
                Property1 = 1,
                Property2 = 2,
                Nested = new MyType { Property1 = 3, Property2 = 4, Nested = null }
            }|];
            """,
            """
            MyType myVariable = 
                new MyType
                {
                    Property1 = 1,
                    Property2 = 2,
                    Nested = new MyType { Property1 = 3, Property2 = 4, Nested = null }
                };
            """,
            additionalFiles:
                new (string source, string expectedSource)[]
                {
                    (
                        source:
                        """
                        public sealed class MyType
                        {
                            public required int Property1 { get; init; }
                            public required int Property2 { get; init; }
                            public required MyType Nested { get; init; }
                        }
                        """,
                        expectedSource: null
                    )
                },
            options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task No_Structural_Honesty_diagnostic_for_single_lined_new_object()
    {
        await VerifyNoDiagnosticAsync(
            "MyType myVariable = new MyType { Property1 = 1, Property2 = 2 };",
            additionalFiles:
                [
                        """
                        public sealed class MyType
                        {
                            public required int Property1 { get; init; }
                            public required int Property2 { get; init; }
                        }
                        """
                ],
            options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Fixes_Structural_Honesty_for_delegate()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System;
            
            Func<int, int> square = [|delegate(int x)
            {
                return x * x;
            }|];
            """,
            """
            using System;

            Func<int, int> square = 
                delegate(int x)
                {
                    return x * x;
                };
            """,
            options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Fixes_Structural_Honesty_for_linq_expression()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System.Collections.Generic;
            using System.Linq;
            
            List<(string Name, string Value, bool IsValid)> collection = [];
            
            var query = [|from item in collection
                where item.IsValid
                select [|new
                {
                    Name = item.Name,
                    Value = item.Value
                }|]|];
            """,
            """
            using System.Collections.Generic;
            using System.Linq;
            
            List<(string Name, string Value, bool IsValid)> collection = [];
            
            var query = 
                from item in collection
                where item.IsValid
                select 
                    new
                    {
                        Name = item.Name,
                        Value = item.Value
                    };
            """,
            options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Fixes_Structural_Honesty_for_aligned_linq_expression()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System.Collections.Generic;
            using System.Linq;
            
            List<(string Name, string Value, bool IsValid)> collection = [];
            
            var query = [|from item in collection
                        where item.IsValid
                        select [|new
                        {
                            Name = item.Name,
                            Value = item.Value
                        }|]|];
            """,
            """
            using System.Collections.Generic;
            using System.Linq;
            
            List<(string Name, string Value, bool IsValid)> collection = [];
            
            var query = 
                from item in collection
                where item.IsValid
                select 
                    new
                    {
                        Name = item.Name,
                        Value = item.Value
                    };
            """,
            options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Fixes_Structural_Honesty_for_anonymous_object()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            var person = [|new
            {
                Name = "John",
                Age = 30
            }|];
            """,
            """
            var person = 
                new
                {
                    Name = "John",
                    Age = 30
                };
            """,
            options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Fixes_Structural_Honesty_for_new_array()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            var list = [|new[]
            {
                1, 2, 3, 4, 5
            }|];
            var jaggedArray = [|new int[][]
            {
                new int[] { 1, 2 },
                new int[] { 3, 4, 5 },
                new int[] { 6 }
            }|];
            """,
            """
            var list = 
                new[]
                {
                    1, 2, 3, 4, 5
                };
            var jaggedArray = 
                new int[][]
                {
                    new int[] { 1, 2 },
                    new int[] { 3, 4, 5 },
                    new int[] { 6 }
                };
            """,
            options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Fixes_Structural_Honesty_for_new_list()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System.Collections.Generic;

            List<int> list = [|new List<int>()
            {
                1, 2, 3, 4, 5
            }|];
            list = [|new ()
            {
                1, 2, 3, 4, 5
            }|];
            list = [|new()
            {
                1, 2, 3, 4, 5
            }|];
            list = new () { 1, 2, 3, 4, 5 };
            list = [|[
                1, 2, 3, 4, 5
            ]|];
            list = 
            [|[
                1, 2, 3, 4, 5
            ]|];
            list = [ 1, 2, 3, 4, 5 ];
            list = 
                [ 1, 2, 3, 4, 5 ];
            """,
            """
            using System.Collections.Generic;

            List<int> list = 
                new List<int>()
                {
                    1, 2, 3, 4, 5
                };
            list = 
                new ()
                {
                    1, 2, 3, 4, 5
                };
            list = 
                new()
                {
                    1, 2, 3, 4, 5
                };
            list = new () { 1, 2, 3, 4, 5 };
            list = 
                [
                    1, 2, 3, 4, 5
                ];
            list = 
                [
                    1, 2, 3, 4, 5
                ];
            list = [ 1, 2, 3, 4, 5 ];
            list = 
                [ 1, 2, 3, 4, 5 ];
            """,
            options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Fixes_Structural_Honesty_for_switch_expression()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            int x = 1;
            var result = [|x switch
            {
                1 => "One",
                2 => "Two",
                _ => "Other"
            }|];
            """,
            """
            int x = 1;
            var result = 
                x switch
                {
                    1 => "One",
                    2 => "Two",
                    _ => "Other"
                };
            """,
            options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Fixes_Structural_Honesty_for_switch_expression2()
    {
        await VerifyDiagnosticAndFixAsync(
            """
                int x = 1;
                var result = 
            [|x switch
                        {
                            1 => "One",
                            2 => "Two",
                            _ => "Other"
                        }|];
            """,
            """
                int x = 1;
                var result = 
                    x switch
                    {
                        1 => "One",
                        2 => "Two",
                        _ => "Other"
                    };
            """,
            options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Fixes_Structural_Honesty_for_value_tuple()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            var person = [|(
                Name: "John", 
                Age: 30
            )|];
            var person2 = [|(
                Name: "John", 
                Age: 30,
                Sister: [|new {
                    Name = "Jane",
                }|]
            )|];
            var person3 = [|(
                Name: "John", 
                Age: 30,
                Sister: [|(
                    Name: "Jane",
                    Age: 20)|]
            )|];
            """,
            """
            var person = 
                (
                    Name: "John", 
                    Age: 30
                );
            var person2 = 
                (
                    Name: "John", 
                    Age: 30,
                    Sister: 
                        new 
                        {
                            Name = "Jane",
                        }
                );
            var person3 = 
                (
                    Name: "John", 
                    Age: 30,
                    Sister: 
                        (
                            Name: "Jane",
                            Age: 20
                        )
                );
            """,
            options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task No_diagnostic_for_single_lined_value_tuple()
    {
        await VerifyNoDiagnosticAsync(
            """
            var person = (Name: "John", Age: 30);
            """,
            options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Fixes_Structural_Honesty_for_ternary_expression()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            object obj = "abc";
            var result = [|obj is string s
                ? s.Length
                : obj is int i
                    ? i
                    : 0|];
            result = 
                [|obj is string s1
                        ? s1.Length
                        : 0|];
            result = 
                [|obj is string s3 
                    ? s3.Length : 0|];
            result = 
                [|obj is string s4 ? s4.Length 
                    : 0|];
            result = 
                obj is string s5 
                    ? 
                        s5.Length 
                    : 
                        0;
            result = obj is string s2 ? s2.Length : 0;
            """,
            """
            object obj = "abc";
            var result = 
                obj is string s
                    ? s.Length
                    : obj is int i
                        ? i
                        : 0;
            result = 
                obj is string s1
                    ? s1.Length
                    : 0;
            result = 
                obj is string s3 
                    ? s3.Length 
                    : 0;
            result = 
                obj is string s4 
                    ? s4.Length 
                    : 0;
            result = 
                obj is string s5 
                    ? 
                        s5.Length 
                    : 
                        0;
            result = obj is string s2 ? s2.Length : 0;
            """,
            options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Fixes_Structural_Honesty_for_binary_expression()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            object obj = "abc";
            var result = [|obj is string s
                && s.Length == 10|];
            """,
            """
            object obj = "abc";
            var result = 
                obj is string
                && s.Length == 10;
            """,
            options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Fixes_Structural_Honesty_for_anonymous_object_passed_into_method()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            [|ProcessPerson([|new
            {
                Name = "John",
                Age = 30
            }|])|];
            
            static void ProcessPerson(object person)
            {
                // Process the person object
            }
            """,
            """
            ProcessPerson(
                new
                {
                    Name = "John",
                    Age = 30
                }
            );
            
            static void ProcessPerson(object person)
            {
                // Process the person object
            }
            """,
            options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Fixes_Structural_Honesty_inside_interpolated_string()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System.Linq;
            
            int? x = 1;
            var message = $@"User details:
            {
                [|Enumerable.Range(1, 10).Select([|i => 
                    x.HasValue ? $"Value: {x.Value + i}" : "No value"|])|]
            }";
            """,
            """
            using System.Linq;

            int? x = 1;
            var message = $@"User details:
            {
                Enumerable.Range(1, 10).Select(
                    i => 
                        x.HasValue ? $"Value: {x.Value + i}" : "No value"
                )
            }";
            """,
            options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task No_diagnostic_for_interpolated_string()
    {
        await VerifyNoDiagnosticAsync(
            """
            using System.Linq;

            int? x = 1;
            var message = $@"User details: 
            {
                x.HasValue ? $"Value: {x.Value}" : "No value"
            }";
            var message2 = $@"User details: {
                x.HasValue ? $"Value: {x.Value}" : "No value"
            }";
            """
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Fixes_Structural_Honesty_for_lambda_function()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            public class C {
                public bool Options 
                    => [|CheckOptionsCalculatedFor(
                        "Option1",
                            "Option2" // broken formatting is expected, no changes should be provided
                    )|]

                public bool OptionsWithComment 
                    => /*what if comment is here? */ [|CheckOptionsCalculatedFor(
                        "Option1",
                            "Option2" // broken formatting is expected, no changes should be provided
                    )|]

                public static bool CheckOptionsCalculatedFor(string option1, string option2) => true;
            }
            """,
            """
            public class C {
                public bool Options 
                    => 
                        CheckOptionsCalculatedFor(
                            "Option1",
                                "Option2" // broken formatting is expected, no changes should be provided
                        )

                public bool OptionsWithComment 
                    => /*what if comment is here? */ 
                        CheckOptionsCalculatedFor(
                            "Option1",
                                "Option2" // broken formatting is expected, no changes should be provided
                        )

                public static bool CheckOptionsCalculatedFor(string option1, string option2) => true;
            }
            """
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Fixes_Structural_Honesty_for_named_parameters()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            [|C.CheckOptionsCalculatedFor(
                string.Empty,
                option2: [|new {
                    i = 1,
                    b = 2
                }|]
            )|];
            """,
            """
            C.CheckOptionsCalculatedFor(
                string.Empty,
                option2: 
                    new 
                    {
                        i = 1,
                        b = 2
                    }
            );
            """,
            additionalFiles:
                new (string source, string expectedSource)[]
                {
                    (
                        source:
                            """
                            public static class C {
                                public static bool CheckOptionsCalculatedFor(string option1 = "", object option2 = null) => true;
                            }
                            """,
                        expectedSource: null
                    )
                },
            options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Fixes_Structural_Honesty_for_chaining()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System.Linq;

            int x = [|Enumerable.Range(1, 10)
                .Select(i => i)
                .Where(i => i > 5)
                .Select(i => i).Count()|];
            """,
            """
            using System.Linq;

            int x = 
                Enumerable.Range(1, 10)
                    .Select(i => i)
                    .Where(i => i > 5)
                    .Select(i => i).Count();
            """,
            options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Fixes_Structural_Honesty_for_chaining_as_method_param()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            [|C.Check([|Enumerable.Range(1, 10)
                    .Select(i => i)
                    .Where(i => i > 5)
                    .Select(i => i).Count()|])|];
            """,
            """
            C.Check(
                Enumerable.Range(1, 10)
                    .Select(i => i)
                    .Where(i => i > 5)
                    .Select(i => i).Count()
            );
            """,
            additionalFiles:
                new (string source, string expectedSource)[]
                {
                    (
                        source:
                        """
                        public static class C {
                            public static bool Check(int i) => true;
                        }
                        """,
                        expectedSource: null
                    )
                },
            options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Fixes_Structural_Honesty_for_lambda_inside_chaining()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            int x =
                Enumerable.Range(1, 10)
                    .Select(i => i)
                    .Where([|i => { 
                        return i > 5; }|]
                    )
                    .Select(i => i).Count());
            """,
            """
            int x =
                Enumerable.Range(1, 10)
                    .Select(i => i)
                    .Where(
                        i => 
                        { 
                            return i > 5; 
                        }
                    )
                    .Select(i => i).Count());
            """,
            options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Fixes_Structural_Honesty_for_new_object_inside_chaining()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            var x =
                Enumerable.Range(1, 10)
                    .Select(i => i)
                    .Where(i => i > 5
                    .Select([|i => [|new {
                        i = i,
                        b = i + 1
                    }|]|])
                    .Count();
            var y =
                Enumerable.Range(1, 10)
                    .Select(i => i)
                    .Where(i => i > 5
                    .Select([|i => { return [|new {
                        i = i,
                        b = i + 1
                    };|]}|])
                    .Count();
            """,
            """
            var x =
                Enumerable.Range(1, 10)
                    .Select(i => i)
                    .Where(i => i > 5
                    .Select(
                        i => 
                            new {
                                i = i,
                                b = i + 1
                            }
                    )
                    .Count();
            var y =
            Enumerable.Range(1, 10)
                .Select(i => i)
                .Where(i => i > 5
                .Select(
                    i => { 
                        return 
                            new 
                            {
                                i = i,
                                b = i + 1
                            };
                    }
                 )
                .Count();
            """,
            options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Fixes_Structural_Honesty_when_return_in_front_of()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            Func<object> f = 
                () => 
                {
                    return [|Enumerable.Range(1, 10)
                        .Select(i => i)
                        .Where(i => i > 5)
                        .Select(i => i).Count()|];
                };
            Func<object> f2 = 
                () => 
                {
                    return [|new {
                        i = 1,
                        b = 2
                    }|];
                };
            Func<object> f3 = 
                () => 
                {
                    return [|(int i, int b) => 
                    {
                        return i + b;
                    }|];
                };
            """,
            """
            Func<object> f = 
                () => 
                {
                    return 
                        Enumerable.Range(1, 10)
                            .Select(i => i)
                            .Where(i => i > 5)
                            .Select(i => i).Count();
                };
            Func<object> f2 = 
                () => 
                {
                    return 
                        new {
                            i = 1,
                            b = 2
                        };
                };
            Func<object> f3 = 
                () => 
                {
                    return 
                        (int i, int b) => 
                        {
                            return i + b;
                        };
                };
            """,
            options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Fixes_Structural_Honesty_for_raw_string()
    {
        await VerifyDiagnosticAndFixAsync(
            """"
            string s = [|"""
                abc
                cde
                """|];
            """",
            """"
            string s = 
                """
                abc
                cde
                """;
            """",
            options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Fixes_Structural_Honesty_for_raw_string_utf8()
    {
        await VerifyDiagnosticAndFixAsync(
            """"
            var s = [|"""
                abc
                cde
                """u8|];
            """",
            """"
            var s = 
                """
                abc
                cde
                """u8;
            """",
            options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Fixes_Structural_Honesty_for_raw_string_if_parameter()
    {
        await VerifyDiagnosticAndFixAsync(
            """"
            [|C.Check([|"""
                abc
                cde
                """|])|];
            """",
            """"
            C.Check(
                """
                abc
                cde
                """
            );
            """",
            additionalFiles:
                new (string source, string expectedSource)[]
                {
                    (
                        source:
                        """
                        public static class C {
                            public static bool Check(string s) => true;
                        }
                        """,
                        expectedSource: null
                    )
                },
            options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Fixes_Structural_Honesty_for_raw_string_if_named_parameter()
    {
        await VerifyDiagnosticAndFixAsync(
            """"
            [|C.Check(
                s: "tst",
                options: [|"""
                abc
                cde
                """|])|];
            """",
            """"
            C.Check(
                s: "tst",
                options: 
                    """
                    abc
                    cde
                    """
            );
            """",
            additionalFiles:
                new (string source, string expectedSource)[]
                {
                    (
                        source:
                        """
                        public static class C {
                            public static bool Check(string s, string options) => true;
                        }
                        """,
                        expectedSource: null
                    )
                },
            options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Fixes_Structural_Honesty_for_raw_strings_and_named_parameter()
    {
        await VerifyDiagnosticAndFixAsync(
            """"
            using System.Threading.Tasks;

            Tst c = new();
            await c.Test();
            
            public class Tst {
                public async Task Test()
                {
                    [|await C.Check([|"""
            class C
            {
                void M()
                {
                    var x = [|new[]|] { "" };
                }
            }
            """|], [|"""
            class C
            {
                void M()
                {
                    var x = new string[] { "" };
                }
            }
            """|], options: "tst")|];
                }
            }
            """",
            """"
            using System.Threading.Tasks;

            public class Tst {
                public async Task Test()
                {
                    await C.Check(
                        """
                        class C
                        {
                            void M()
                            {
                                var x = [|new[]|] { "" };
                            }
                        }
                        """, 
                        """
                        class C
                        {
                            void M()
                            {
                                var x = new string[] { "" };
                            }
                        }
                        """, options: "tst"
                    );
                }
            }
            );
            """",
            additionalFiles:
                new (string source, string expectedSource)[]
                {
                    (
                        source:
                        """
                        using System.Threading.Tasks;

                        public static class C {
                            public static Task<bool> Check(string s, string t, string options) => Task.FromResult(true);
                        }
                        """,
                        expectedSource: null
                    )
                },
            options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
        );
    }

    [Fact, Trait(Traits.Analyzer, DiagnosticIdentifiers.FixStructuralHonesty)]
    public async Task Fixes_Structural_Honesty_for_collection_expression_as_parameter()
    {
        await VerifyDiagnosticAndFixAsync(
            """
            using System.Threading.Tasks;

            int myVariable = 
                [|await MyMethodAsync(
                    10, [|[
                        1,
                        2,
                        3
                    ]|]
                )|];

            Task<int> MyMethodAsync(int i, int[] arr) => Task.FromResult(1);
            """,
            """
            using System.Threading.Tasks;
            
            int myVariable = 
                await MyMethodAsync(
                    10, 
                    [
                        1,
                        2,
                        3
                    ]
                );

            Task<int> MyMethodAsync(int i, int[] arr) => Task.FromResult(1);
            """,
            options: Options.WithCompilationOptions(Options.CompilationOptions.WithOutputKind(OutputKind.ConsoleApplication))
        );
    }

    // Parameter list
}
