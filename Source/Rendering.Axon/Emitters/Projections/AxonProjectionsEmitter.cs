// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Contracts.Rendering;
using Cratis.Stage.Rendering.Axon.Admission;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;
using Cratis.Stage.Rendering.Axon.Specifications.Views;

namespace Cratis.Stage.Rendering.Axon.Emitters.Projections;

internal sealed class AxonProjectionsEmitter : IAxonEmitter
{
    /// <summary>The Axon namespace carried by every view writer, and the name of the processor that applies them.</summary>
    internal const string ViewNamespace = "views";

    public bool IsImplemented => true;

    public void Emit(AxonEmissionContext context)
    {
        var types = new ViewJavaTypes(context.Request.Model.Application, context.BasePackage);
        foreach (var located in ScopeSelection.Select(context.Request))
        {
            var package = types.Package(located);
            foreach (var model in located.Slice.ReadModels) EmitModel(context, types, package, model);
            foreach (var projection in located.Slice.Projections)
            {
                var model = located.Slice.ReadModels.FirstOrDefault(candidate => candidate.Id == projection.ReadModel);
                if (model is null)
                {
                    const string reason = "read model outside slice";
                    const string intent = "project events";
                    var path = projection.Id.ToString();
                    context.Degradation.Unsupported(AxonRefusalCodes.ProjectionMapping, projection.Id, reason, intent, path);
                    var stub = new JavaCodeBuilder(package).DiffersFromScreenplay(reason)
                        .UnsupportedIntent(intent, path, AxonRefusalCodes.ProjectionMapping)
                        .Block($"public class {JavaNames.Type(projection.Name)}Projector", _ => { });
                    context.AddJava(package, JavaNames.Type(projection.Name) + "Projector", stub.Build(), sources: [projection.Id]);
                    continue;
                }
                EmitProjector(context, types, package, projection, model);
            }
            EmitReducers(context, types, located);
        }
        EmitProcessing(context, types);
        new AxonViewSpecsEmitter().Emit(context);
    }

    static void EmitProcessing(AxonEmissionContext context, ViewJavaTypes types)
    {
        if (ScopeSelection.Select(context.Request).All(owner => owner.Slice.ReadModels.IsEmpty)) return;

        // One pooled streaming processor with a sequential policy applies every view writer, so a read-modify-write of a shared row never interleaves. Writers are matched by their @Namespace, which nothing else carries.
        var package = types.BasePackage + ".infrastructure";
        var code = new JavaCodeBuilder(package).Declare("ViewProcessingConfiguration");
        var definition = code.Reference("org.axonframework.extension.spring.config.EventProcessorDefinition");
        code.Line($"@{code.Reference("org.springframework.context.annotation.Configuration")}({JavaCodeBuilder.StringLiteral(package + ".ViewProcessingConfiguration")})")
            .Block("public class ViewProcessingConfiguration", body => body
                .Line($"@{code.Reference("org.springframework.context.annotation.Bean")}")
                .Block($"{definition} views()", method => method
                    .Line($"return {definition}.pooledStreamingMatching({JavaCodeBuilder.StringLiteral(ViewNamespace)})")
                    .Line($"    .customized(configuration -> configuration.sequencingPolicy({code.Reference("org.axonframework.messaging.core.sequencing.SequentialPolicy")}.INSTANCE));")));
        context.AddJava(package, "ViewProcessingConfiguration", code.Build());
        context.Degradation.Note($"{AxonRefusalCodes.EventuallyConsistentViews}: Views are processed by a pooled streaming processor with a sequential policy, so reads are eventually consistent: a query may not yet reflect an event that was just appended. Generated tests fill views by calling the projector directly or by seeding the repository, so they do not depend on the processor; tests you write that append an event and then read a view must wait for the result.");
    }

    static void EmitReducers(AxonEmissionContext context, ViewJavaTypes types, LocatedSemanticSlice located)
    {
        foreach (var reducer in located.Slice.Reducers)
        {
            var model = located.Slice.ReadModels.FirstOrDefault(candidate => candidate.Id == reducer.ReadModel);
            var admission = ReducerBodies.Admission(context, located, reducer);
            if (model is null)
            {
                context.Degradation.Unsupported(AxonRefusalCodes.Reduction, located.Slice.Id, admission.Blocked ?? "The reducer read model is outside its slice.");
                continue;
            }
            new ReducerProjection(context, types).Emit(located, model, reducer, admission);
        }
    }

    static void EmitModel(AxonEmissionContext context, ViewJavaTypes types, string package, SemanticReadModel model)
    {
        ViewJavaTypes.Add(context, package, JavaNames.Type(model.Name), ReadModelRecords.Code(types, package, model), model.Id);
        ReadModelEntities.Emit(context, types, package, model);
        if (!ReadModelWriters.Writerless(context, model.Id)) return;
        context.Degradation.Unsupported(AxonRefusalCodes.UnwrittenReadModel, model.Id, "No projection or reducer in the rendered scope writes this read model, so it always reads empty unless a query performer writes through its repository. Add a projection or reducer that writes it.");
    }

    static void EmitProjector(AxonEmissionContext context, ViewJavaTypes types, string package, SemanticProjection projection, SemanticReadModel model)
    {
        if (ProjectionHandlerShape.RootOnly(context.Request.Model.Application, projection, model))
        {
            new RootProjectionEmitter(context, types, package, projection, model).Emit();
            return;
        }
        if (ProjectionHandlerShape.Renderable(context, types, projection, model))
        {
            new ScopedProjectionEmitter(context, types, package, projection, model).Emit();
            return;
        }
        const string reason = "The projection uses scoped state (every, children, nested scopes, joins, join removals or parented removals) whose scope or key cannot be resolved to a stored row; the read model stays empty.";
        const string intent = "project scoped read-model state";
        var path = projection.Id.ToString();
        context.Degradation.Unsupported(AxonRefusalCodes.ScopedProjection, projection.Id, reason, intent, path);
        var name = JavaNames.Type(projection.Name) + "Projector";
        var stub = new JavaCodeBuilder(package).DiffersFromScreenplay(reason)
            .UnsupportedIntent(intent, path, AxonRefusalCodes.ScopedProjection)
            .Block($"public class {name}", _ => { });
        ViewJavaTypes.Add(context, package, name, stub.Build(), projection.Id);
    }
}
