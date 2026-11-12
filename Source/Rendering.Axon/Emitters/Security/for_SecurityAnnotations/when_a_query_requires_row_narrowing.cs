// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Emitters.Queries;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Security.for_SecurityAnnotations;

public class when_a_query_requires_row_narrowing : Admission.for_AxonBehaviorAdmission.given.a_model_with_behaviors
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    void should_deny_http_and_non_http_access_even_without_authorization(bool authorized)
    {
        languageVersion = LanguageVersion.V50;
        semanticVersion = SemanticVersion.V50;
        var policy = new SemanticPolicy("Authenticated", new SemanticAuthenticatedCondition());
        Replace(request.Model.Application with { Policies = [.. request.Model.Application.Policies, policy] });
        SemanticFeature Feature(SemanticFeature feature) => feature with
        {
            Features = [.. feature.Features.Select(Feature)],
            Slices = [.. feature.Slices.Select(slice => slice with
            {
                Queries = [.. slice.Queries.Select(query => query with
                {
                    Narrowing = new("IsSales"),
                    Cardinality = SemanticQueryCardinality.Many,
                    Authorization = authorized ? new SemanticPolicyReference("Authenticated") : null
                })]
            })]
        };
        Replace(request.Model.Application with { Modules = [.. request.Model.Application.Modules.Select(module => module with { Features = [.. module.Features.Select(Feature)] })] });
        var queries = ScopeSelection.Select(request).SelectMany(located => located.Slice.Queries).ToArray();
        queries.ShouldNotBeEmpty();
        context = new(request, new(request.Profile));
        new AxonQueriesEmitter().Emit(context);
        foreach (var query in queries)
        {
            Source(query.Name + "Endpoint").ShouldContain("@PreAuthorize(\"denyAll()\")");
            Source(query.Name + "Handler").ShouldContain("@Secured({})");
        }
        context.Degradation.UnsupportedConstructs.Any(diagnostic => diagnostic.Code == "STAGE-AXON-AUTHORIZATION" && diagnostic.Message.Contains("row narrowing", StringComparison.Ordinal)).ShouldBeTrue();
        var decisions = Admission.AxonUnsupportedSemantics.Decisions(request, "example").Where(decision => decision.Code == "STAGE-AXON-AUTHORIZATION").ToArray();
        decisions.Length.ShouldEqual(queries.Length);
        var strict = new AxonEmissionContext(request, new(AxonRendering.CreateProfile(strict: true)));
        Admission.AxonBehaviorAdmission.Apply(strict, decisions);
        strict.Degradation.Refusals.SelectMany(refusal => refusal.Codes).Distinct().ShouldContainOnly("STAGE-AXON-AUTHORIZATION");
    }

    string Source(string name) => Encoding.UTF8.GetString(context.Artifacts.Single(artifact => artifact.RelativePath.EndsWith("/" + name + ".java", StringComparison.Ordinal)).Bytes.AsSpan());
}
