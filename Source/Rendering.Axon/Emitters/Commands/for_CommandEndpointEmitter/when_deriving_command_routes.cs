// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Specifications;
using Cratis.Stage.Rendering.Axon.Sdk;
using Xunit;

namespace Cratis.Stage.Rendering.Axon.Emitters.Commands.for_CommandEndpointEmitter;

public class when_deriving_command_routes : for_AxonArtifactRenderPlanner.given.an_axon_request
{
    string _route;
    string _underscored;

    void Because()
    {
        var located = ScopeSelection.Select(request).First(slice => !slice.Slice.Commands.IsEmpty);
        var seed = located.Slice.Commands[0];
        var pinned = located with
        {
            Module = located.Module with { Name = "HR" },
            Features = [located.Features[0] with { Name = "TimeOff" }, located.Features[0] with { Name = "Leave_Requests" }]
        };
        _route = CommandEndpointEmitter.Route(pinned, seed with { Name = "RequestTimeOff" });
        _underscored = CommandEndpointEmitter.Route(pinned, seed with { Name = "Request_Time_Off" });
    }

    // Command package segments drop hyphens ("timeoff"); query routes use kebab segments ("time-off"). Both are pinned as they are today.
    [Fact] void should_join_package_segments_without_hyphens_and_kebab_the_command() => _route.ShouldEqual("/api/hr/timeoff/leaverequests/request-time-off");
    [Fact] void should_kebab_underscored_command_names() => _underscored.ShouldEqual("/api/hr/timeoff/leaverequests/request-time-off");
}
