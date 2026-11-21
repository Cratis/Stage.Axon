// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Queries;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Security.for_SecurityAnnotations;

public class when_a_query_is_under_an_availability_gate : Admission.for_AxonBehaviorAdmission.given.a_model_with_behaviors
{
    static readonly System.Collections.Immutable.ImmutableArray<SemanticAvailabilityRequirement> _gate = [new SemanticAudienceRequirement("Staff")];

    [Theory]
    [InlineData("module")]
    [InlineData("feature")]
    [InlineData("slice")]
    [InlineData("query")]
    void should_deny_http_and_non_http_access_and_refuse_in_strict_mode(string owner)
    {
        languageVersion = LanguageVersion.V53;
        semanticVersion = SemanticVersion.V53;
        SemanticFeature Feature(SemanticFeature feature) => feature with
        {
            Available = owner == "feature" ? _gate : feature.Available,
            Features = [.. feature.Features.Select(Feature)],
            Slices = [.. feature.Slices.Select(slice => slice with
            {
                Available = owner == "slice" ? _gate : slice.Available,
                Queries = [.. slice.Queries.Select(query => query with { Available = owner == "query" ? _gate : query.Available })]
            })]
        };
        Replace(request.Model.Application with
        {
            Audiences = [new("Staff")],
            Modules = [.. request.Model.Application.Modules.Select(module => module with
            {
                Available = owner == "module" ? _gate : module.Available,
                Features = [.. module.Features.Select(Feature)]
            })]
        });
        var queries = ScopeSelection.Select(request).SelectMany(located => located.Slice.Queries).ToArray();
        queries.ShouldNotBeEmpty();
        context = new(request, new(request.Profile));
        new AxonQueriesEmitter().Emit(context);
        foreach (var query in queries)
        {
            Source(query.Name + "Endpoint").ShouldContain("@PreAuthorize(\"denyAll()\")");
            Source(query.Name + "Handler").ShouldContain("@Secured({})");
        }
        context.Degradation.UnsupportedConstructs.Any(diagnostic => diagnostic.Code == "STAGE-AXON-AUTHORIZATION" && diagnostic.Message.Contains("Availability gates", StringComparison.Ordinal)).ShouldBeTrue();
        var decisions = Admission.AxonUnsupportedSemantics.Decisions(request, "example").Where(decision => decision.Code == "STAGE-AXON-AUTHORIZATION").ToArray();
        decisions.Length.ShouldBeGreaterThanOrEqual(1);
        var strict = new AxonEmissionContext(request, new(AxonRendering.CreateProfile(strict: true)));
        Admission.AxonBehaviorAdmission.Apply(strict, decisions);
        strict.Degradation.Refusals.SelectMany(refusal => refusal.Codes).Distinct().ShouldContainOnly("STAGE-AXON-AUTHORIZATION");
    }

    string Source(string name) => Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/" + name + ".java", StringComparison.Ordinal)).Bytes.AsSpan());
}
