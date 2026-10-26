// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Stage.Contracts.Rendering;
using Xunit;

namespace Cratis.Stage.Samples.MinimalTarget.for_ApplicationManifestPlanner;

public class when_planning_default_additional_scopes : given.an_application_request
{
    Exception exception;

    void Establish() => request = request with { AdditionalScopes = default };

    void Because() => exception = Record.Exception(() => planner.Plan(request));

    [Fact] void should_reject_an_uninitialized_collection() => Assert.IsType<InvalidArtifactRenderContract>(exception);
}
