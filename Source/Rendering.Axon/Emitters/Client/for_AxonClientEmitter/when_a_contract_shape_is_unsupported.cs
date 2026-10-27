// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Admission;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Client.for_AxonClientEmitter;

public class when_a_contract_shape_is_unsupported : Axon.for_AxonArtifactRenderPlanner.given.an_axon_request
{
    AxonEmissionContext lenient;
    AxonEmissionContext strict;
    TsType lenientType;
    TsFile file;

    AxonEmissionContext Context(bool isStrict) => new(request with { Profile = AxonRendering.WithTypeScriptClient(AxonRendering.CreateProfile(strict: isStrict)) }, new(AxonRendering.WithTypeScriptClient(AxonRendering.CreateProfile(strict: isStrict))));

    void Because()
    {
        lenient = Context(false);
        strict = Context(true);
        file = new("concepts.ts");
        var owner = loaded.Model.Application.Concepts[0].Id;
        lenientType = new ClientTypes(lenient, new("concepts.ts")).Primitive(file, (SemanticPrimitiveType)999, owner, "concept Test");
        new ClientTypes(strict, new("concepts.ts")).Primitive(file, (SemanticPrimitiveType)999, owner, "concept Test");
    }

    [Fact] void should_type_the_value_as_unknown() => lenientType.Type.ShouldEqual("unknown");
    [Fact] void should_explain_why() => lenientType.Note.ShouldNotBeNull();
    [Fact] void should_warn_when_not_strict() => lenient.Degradation.UnsupportedConstructs.Any(diagnostic => diagnostic.Code == AxonRefusalCodes.Client && diagnostic.Severity == ArtifactRenderDiagnosticSeverity.Warning).ShouldBeTrue();
    [Fact] void should_not_refuse_when_not_strict() => lenient.Degradation.Refusals.IsEmpty.ShouldBeTrue();
    [Fact] void should_refuse_when_strict() => strict.Degradation.Refusals.IsEmpty.ShouldBeFalse();
    [Fact] void should_report_the_client_code_when_strict() => strict.Degradation.Refusals.Any(refusal => refusal.Codes.Contains(AxonRefusalCodes.Client)).ShouldBeTrue();
}
