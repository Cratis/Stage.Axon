// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Screenplay.Semantics.Execution;
using Cratis.Specifications;
using Cratis.Stage.Contracts.Rendering;

namespace Cratis.Stage.Samples.MinimalTarget.for_ApplicationManifestPlanner.given;

public class an_application_request : Specification
{
    protected ArtifactRenderRequest request;
    protected IArtifactRenderPlanner planner;

    void Establish()
    {
        var identity = ApplicationIdentity.Create("Sample");
        var application = new SemanticApplication(SemanticId.Create(SemanticAddress.ForApplication(identity)), "Sample", [], [], []);
        var model = ExecutableSemanticModel.Create(LanguageVersion.V1, SemanticVersion.V1, application);
        request = new(model, SemanticExecutionPlan.Compile(model).Plan!, ArtifactRenderProfile.Create("markdown-manifest", "1", "minimal-target", "1", []), new(ArtifactRenderScopeKind.Application, application.Id));
        planner = new ApplicationManifestPlanner();
    }
}
