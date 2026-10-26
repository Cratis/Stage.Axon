// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Sdk.for_SemanticSurfaceLedger;

public class when_compiling_inline_implementations : Specification
{
    [Fact]
    void should_construct_the_compiler_and_compile_an_inline_body()
    {
        const string source = """
            module Example
              feature Queries
                slice StateView Lookup
                  readmodel Item
                    id String key
                  query FindItems => Item[]
                    performer
                      ```java
                      return java.util.List.of();
                      ```
            """;
        var result = new ScreenplayCompiler().Compile(source);
        Assert.True(result.Success, string.Join('\n', result.Diagnostics));
    }

    [Fact]
    void should_expose_the_implementation_contract()
    {
        typeof(SemanticImplementationRequirement).GetProperty(nameof(SemanticImplementationRequirement.RequirementId)).ShouldNotBeNull();
        typeof(SemanticImplementationRequirement).GetProperty(nameof(SemanticImplementationRequirement.ContextVersion)).ShouldNotBeNull();
        typeof(SemanticImplementationRequirement).GetProperty(nameof(SemanticImplementationRequirement.ResultVersion)).ShouldNotBeNull();
    }
}
