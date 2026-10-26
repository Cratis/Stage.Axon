// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters;
using Cratis.Stage.Rendering.Axon.Emitters.Commands;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.for_AxonCommandsEmitter;

public class when_a_command_is_named_command_validation_exception : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    string _code;

    void Because()
    {
        var located = ScopeSelection.Select(request).First(slice => !slice.Slice.Commands.IsEmpty);
        var command = located.Slice.Commands[0] with { Name = "CommandValidationException" };
        var specification = located.Slice.Specifications.First(specification => specification.When?.Command == command.Id) with { ThenErrors = [new(null, "Not allowed.")], ThenEvents = [] };
        var slice = located with { Slice = located.Slice with { Specifications = [specification] } };
        var context = new AxonEmissionContext(request, new(request.Profile));
        new CommandSpecificationsEmitter(context, slice, command, null, false).Emit();
        _code = Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/CommandValidationExceptionSpecificationsTest.java", StringComparison.Ordinal)).Bytes.AsSpan());
    }

    [Fact] void should_qualify_the_exception_class_literal() => _code.ShouldContain(".isInstanceOf(sample.runtime.CommandValidationException.class);");
    [Fact] void should_qualify_the_exception_cast() => _code.ShouldContain("var failure = (sample.runtime.CommandValidationException) error;");
    [Fact] void should_not_import_the_runtime_type_over_the_command() => _code.ShouldNotContain("import sample.runtime.CommandValidationException;");
    [Fact] void should_keep_the_expectation_executable() => _code.ShouldNotContain("@Disabled");
    [Fact] void should_still_check_the_error_message() => _code.ShouldContain(".contains(\"Not allowed.\");");
}
