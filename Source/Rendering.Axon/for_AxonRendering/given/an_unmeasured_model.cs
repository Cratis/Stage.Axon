// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Screenplay.Semantics.Execution;
using Cratis.Specifications;

namespace Cratis.Stage.Rendering.Axon.for_AxonRendering.given;

public class an_unmeasured_model : a_screen_composition_model
{
    void Establish()
    {
        var application = new SemanticApplication(loaded.Model.Application.Id, loaded.Model.Application.Name, [], [], []);
        var model = ExecutableSemanticModel.Create(LanguageVersion.V1, SemanticVersion.V1, application);
        var compilation = SemanticExecutionPlan.Compile(model);
        compilation.Success.ShouldBeTrue();
        loaded = loaded with { Model = model, Plan = compilation.Plan! };
    }
}
