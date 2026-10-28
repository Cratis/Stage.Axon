// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Events.for_AxonEventsEmitter;

public class when_an_event_is_named_event_source_id : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    string _code;
    string _identity;
    AxonEmissionContext context;

    void Because()
    {
        var target = Sdk.ScopeSelection.Select(request).SelectMany(slice => slice.Slice.Events).Single(contract => contract.Name == "OrderPlaced").Id;
        SemanticFeature Feature(SemanticFeature feature) => feature with
        {
            Features = [.. feature.Features.Select(Feature)],
            Slices = [.. feature.Slices.Select(slice => slice with { Commands = slice.Events.Any(contract => contract.Id == target) ? [] : slice.Commands, Specifications = [], Events = [.. slice.Events.Select(contract => contract.Name == "OrderPlaced" ? contract with { Name = "EventSourceId" } : contract)] })]
        };
        var application = request.Model.Application;
        var model = ExecutableSemanticModel.Create(request.Model.LanguageVersion, request.Model.SemanticVersion, application with { Modules = [.. application.Modules.Select(module => module with { Features = [.. module.Features.Select(Feature)] })] });
        context = new AxonEmissionContext(request with { Model = model }, new(request.Profile));
        new AxonEventsEmitter().Emit(context);
        var identity = context.BasePackage.Replace('.', '/') + "/events/EventSourceId.java";
        string Text(Func<string, bool> match) => Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/EventSourceId.java", StringComparison.Ordinal) && match(artifact.RelativePath)).Bytes.AsSpan());
        _code = Text(path => !path.EndsWith(identity, StringComparison.Ordinal));
        _identity = Text(path => path.EndsWith(identity, StringComparison.Ordinal));
    }

    [Fact] void should_keep_the_application_event_name() => _code.ShouldContain("public record EventSourceId(");
    [Fact] void should_qualify_the_application_identity_component() => _code.ShouldContain(context.BasePackage + ".events.EventSourceId eventSourceId");
    [Fact] void should_not_import_the_application_identity_over_the_event() => _code.ShouldNotContain("import " + context.BasePackage + ".events.EventSourceId;");
    [Fact] void should_emit_the_application_identity_as_a_plain_record() => _identity.ShouldContain("public record EventSourceId(@JsonValue String value)");
    [Fact] void should_keep_the_identity_scalar_on_the_wire() => _identity.ShouldContain("@JsonCreator(mode = JsonCreator.Mode.DELEGATING)");
    [Fact] void should_not_depend_on_the_runtime_module() => _identity.ShouldNotContain("io.cratis");
    [Fact] void should_print_the_scalar_value() => _identity.ShouldContain("return value;");
}
