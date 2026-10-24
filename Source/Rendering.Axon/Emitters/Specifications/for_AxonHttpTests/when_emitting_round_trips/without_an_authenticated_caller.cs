// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Specifications.for_AxonHttpTests.when_emitting_round_trips;

public class without_an_authenticated_caller : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    AxonEmissionContext context;
    string code;

    void Establish()
    {
        context = new(request, new AxonDegradation(request.Profile));
        var id = ScopeSelection.Select(request).SelectMany(slice => slice.Slice.Specifications).First().Id;
        AxonHttpTests.Register(context, new(id, [new(new("GET", "/orders"), new(401))], new("ignored", ["ADMIN"], false)));
    }

    void Because()
    {
        AxonHttpTests.Emit(context);
        code = Encoding.UTF8.GetString(context.Artifacts.Single().Bytes.AsSpan());
    }

    [Fact] void should_keep_the_request_anonymous() => code.ShouldContain(".with(anonymous())");
    [Fact] void should_not_grant_the_supplied_roles() => code.ShouldNotContain(".roles(");
    [Fact] void should_not_send_an_unmodeled_payload() => code.ShouldNotContain(".content(");
    [Fact] void should_not_invent_a_response_body_assertion() => code.ShouldNotContain(".andExpect(content()");
}
