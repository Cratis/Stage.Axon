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
        var package = CommandContractsEmitter.Package(new(context.Request.Model.Application, context.BasePackage), located);
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
        var code = new JavaCodeBuilder(package).Declare(name).Declare(JavaNames.Type(reaction.Name) + "Reaction");
        if (supported)
        {
            scenario.Claim(code);
            code.Line("@" + code.Reference("org.springframework.boot.test.context.SpringBootTest")).Line("@" + code.Reference("org.springframework.test.annotation.DirtiesContext"));
        }
        code.Block("class " + name, body =>
        {
            if (supported) Fields(body);
            body.Line("@" + body.Reference("org.junit.jupiter.api.Test")).Line($"@{body.Reference("org.junit.jupiter.api.DisplayName")}({JavaCodeBuilder.StringLiteral(specification.Id.ToString())})");
            if (!supported) body.Line($"@{body.Reference("org.junit.jupiter.api.Disabled")}({JavaCodeBuilder.StringLiteral(Refusal + ": unsupported application-trigger specification fixtures")})");
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
        var autowired = body.Reference("org.springframework.beans.factory.annotation.Autowired");
        body.Line($"@{autowired} private {JavaNames.Type(reaction.Name)}Reaction reaction;")
            .Line($"@{autowired} private {body.Reference("org.axonframework.eventsourcing.eventstore.EventStorageEngine")} events;")
            .Line($"@{body.Reference("org.springframework.test.context.bean.override.mockito.MockitoBean")} private {body.Reference("java.time.Clock")} clock;")
            .Line($"@{body.Reference("org.junit.jupiter.api.AfterEach")} void clearCaller() {{ {body.Reference("org.springframework.security.core.context.SecurityContextHolder")}.clearContext(); }}");
    }
}
