// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Cratis.Screenplay.Semantics;
using Cratis.Screenplay.Semantics.Execution;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Conformance;
using Cratis.Stage.Samples.MinimalTarget;

var identity = ApplicationIdentity.Create("Sample");
var application = new SemanticApplication(SemanticId.Create(SemanticAddress.ForApplication(identity)), "Sample", [], [], []);
var model = ExecutableSemanticModel.Create(LanguageVersion.V1, SemanticVersion.V1, application);
var executionPlan = SemanticExecutionPlan.Compile(model).Plan!;
var profile = ArtifactRenderProfile.Create("markdown-manifest", "1", "minimal-target", "1", []);
var request = new ArtifactRenderRequest(model, executionPlan, profile, new(ArtifactRenderScopeKind.Application, application.Id));
var planner = new ApplicationManifestPlanner();
var plan = planner.Plan(request);
var coverage = RenderCoverage.Check(request, plan, new HashSet<string>());
var determinism = RenderDeterminism.Check(planner, request);
if (!plan.Success || !coverage.IsClean || !determinism.IsClean)
{
    Console.Error.WriteLine("The manifest plan failed conformance checks.");
    return 1;
}

foreach (var artifact in plan.Artifacts)
{
    Console.Write(Encoding.UTF8.GetString(artifact.Bytes.AsSpan()));
}

return 0;
