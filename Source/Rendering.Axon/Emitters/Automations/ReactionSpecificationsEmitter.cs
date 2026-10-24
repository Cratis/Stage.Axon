// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Semantics;
using Cratis.Stage.Rendering.Axon.Emitters.Commands;
using Cratis.Stage.Rendering.Axon.Emitters.Specifications;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Automations;

internal sealed class ReactionSpecificationsEmitter(AxonEmissionContext context, LocatedSemanticSlice located, SemanticReaction reaction)
{
    const string Refusal = "STAGE-AXON-0504";

    internal void Emit()
    {
        var package = CommandContractsEmitter.Package(new(context.Request.Model.Application), located);
        foreach (var specification in located.Slice.Specifications.Where(specification => specification.WhenTrigger?.Kind == SemanticReactionTriggerKind.ApplicationTrigger))
        {
            var index = Enumerable.Range(0, reaction.Triggers.Length).FirstOrDefault(index => reaction.Triggers[index].Source == specification.WhenTrigger!.Trigger, -1);
            if (index >= 0) EmitSpecification(package, specification, index);
        }
    }

    void EmitSpecification(string package, SemanticSpecification specification, int index)
    {
        var scenario = new ReactionSpecificationScenario(context, specification, index);
        var supported = scenario.CanEmit;
        var name = JavaNames.Type(reaction.Name) + JavaNames.Type(specification.Name) + "SpecificationsTest";
        var method = "spec_" + JavaNames.Member(specification.Name);
        var code = new JavaCodeBuilder(package);
        if (supported) code.Line("@org.springframework.boot.test.context.SpringBootTest").Line("@org.springframework.test.annotation.DirtiesContext");
        code.Block("class " + name, body =>
        {
            if (supported) Fields(body);
            body.Line("@org.junit.jupiter.api.Test").Line($"@org.junit.jupiter.api.DisplayName({JavaCodeBuilder.StringLiteral(specification.Id.ToString())})");
            if (!supported) body.Line($"@org.junit.jupiter.api.Disabled({JavaCodeBuilder.StringLiteral(Refusal + ": unsupported application-trigger specification fixtures")})");
            body.Block("void " + method + "() throws Exception", test =>
            {
                if (supported)
                {
                    scenario.Emit(test);
                }
                else
                {
                    test.UnsupportedIntent("verify the complete application-trigger reaction fixtures", specification.Id.ToString(), Refusal)
                        .Line("throw new UnsupportedOperationException(\"Unsupported application-trigger reaction fixtures\");");
                }
            });
        });
        AxonSpecificationsEmitter.Register(context, specification.Id, package + "." + name, method, supported ? null : Refusal);
        if (!supported) context.Degradation.Unsupported(Refusal, specification.Id, "Axon cannot yet verify these reaction fixtures.", "verify the complete application-trigger reaction fixtures", specification.Id.ToString());
        context.AddJava(package, name, code.Build(), test: true, sources: [reaction.Id, specification.Id]);
    }

    void Fields(JavaCodeBuilder body)
    {
        body.Line($"@org.springframework.beans.factory.annotation.Autowired private {JavaNames.Type(reaction.Name)}Reaction reaction;")
            .Line("@org.springframework.beans.factory.annotation.Autowired private org.axonframework.eventsourcing.eventstore.EventStorageEngine events;")
            .Line("@org.springframework.test.context.bean.override.mockito.MockitoBean private java.time.Clock clock;")
            .Line("@org.junit.jupiter.api.AfterEach void clearCaller() { org.springframework.security.core.context.SecurityContextHolder.clearContext(); }");
    }
}
