// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

package __APPLICATION__.runtime;

import __APPLICATION__.accounts.aliases.assign.AssignAliasOn0Append;
import __APPLICATION__.accounts.aliases.assign.AssignAliasOn0AppendHandler;
import __APPLICATION__.accounts.aliases.assign.AssignAliasReaction;
import __APPLICATION__.accounts.aliases.release.ReleaseAliasOn0Append;
import __APPLICATION__.accounts.aliases.release.ReleaseAliasOn0AppendHandler;
import __APPLICATION__.accounts.aliases.release.ReleaseAliasReaction;
import __APPLICATION__.accounts.events.AliasAssigned;
import __APPLICATION__.accounts.events.AliasReleased;
import __APPLICATION__.accounts.events.EventTags;
import io.cratis.stage.axon.runtime.EventSourceId;
import io.cratis.stage.axon.runtime.UniqueValueStore;
import java.util.ArrayList;
import java.util.concurrent.TimeUnit;
import org.axonframework.common.configuration.Configuration;
import org.axonframework.eventsourcing.eventstore.EventStorageEngine;
import org.axonframework.eventsourcing.eventstore.SourcingCondition;
import org.axonframework.eventsourcing.eventstore.TerminalEventMessage;
import org.axonframework.messaging.commandhandling.gateway.CommandGateway;
import org.axonframework.messaging.core.MessageTypeResolver;
import org.axonframework.messaging.core.annotation.HandlerDefinition;
import org.axonframework.messaging.core.annotation.ParameterResolverFactory;
import org.axonframework.messaging.core.unitofwork.UnitOfWorkFactory;
import org.axonframework.messaging.eventhandling.GenericEventMessage;
import org.axonframework.messaging.eventhandling.annotation.AnnotatedEventHandlingComponent;
import org.axonframework.messaging.eventhandling.conversion.EventConverter;
import org.axonframework.messaging.eventhandling.processing.streaming.token.GlobalSequenceTrackingToken;
import org.axonframework.messaging.eventhandling.processing.streaming.token.ReplayToken;
import org.axonframework.messaging.eventhandling.processing.streaming.token.TrackingToken;
import org.axonframework.messaging.eventstreaming.EventCriteria;
import org.axonframework.messaging.eventstreaming.Tag;
import org.springframework.beans.factory.annotation.Autowired;

abstract class GeneratedReactionProofSupport {
    @Autowired protected Configuration configuration;
    @Autowired protected CommandGateway gateway;
    @Autowired protected UniqueValueStore claims;
    @Autowired protected AssignAliasOn0AppendHandler assignment;
    @Autowired protected ReleaseAliasOn0AppendHandler release;
    @Autowired protected AssignAliasReaction assignReaction;
    @Autowired protected ReleaseAliasReaction releaseReaction;

    protected void assign(String owner, String value) {
        gateway.sendAndWait(assignment(owner, value));
    }

    protected AssignAliasOn0Append assignment(String owner, String value) {
        var id = new EventSourceId(owner);
        return new AssignAliasOn0Append(id, new AliasAssigned(id, value));
    }

    protected ReleaseAliasOn0Append release(String owner) {
        var id = new EventSourceId(owner);
        return new ReleaseAliasOn0Append(id, new AliasReleased(id));
    }

    protected long events(String owner) throws Exception {
        var types = configuration.getComponent(MessageTypeResolver.class);
        var assigned = types.resolveOrThrow(AliasAssigned.class);
        var released = types.resolveOrThrow(AliasReleased.class);
        var stream = configuration.getComponent(EventStorageEngine.class).source(SourcingCondition.conditionFor(
                EventCriteria.havingTags(Tag.of(EventTags.EVENT_SOURCE_ID, owner))));
        try {
            // The aggregate JPA engine also returns the initiating events for this owner.
            return stream.filter(entry -> !(entry.message() instanceof TerminalEventMessage))
                    .filter(entry -> entry.message().type().equals(assigned) || entry.message().type().equals(released))
                    .collect(ArrayList<Object>::new, (all, entry) -> all.add(entry)).get(5, TimeUnit.SECONDS).size();
        } finally { stream.close(); }
    }

    protected Object injectedStore(Object handler) throws Exception {
        var field = handler.getClass().getDeclaredField("uniqueValues");
        field.setAccessible(true);
        return field.get(handler);
    }

    protected void deliver(Object handler, Object payload, boolean replay) throws Exception {
        var types = configuration.getComponent(MessageTypeResolver.class);
        var component = new AnnotatedEventHandlingComponent<>(handler,
                configuration.getComponent(ParameterResolverFactory.class),
                configuration.getComponent(HandlerDefinition.class), types,
                configuration.getComponent(EventConverter.class));
        var message = new GenericEventMessage(types.resolveOrThrow(payload), payload);
        var token = ReplayToken.createReplayToken(new GlobalSequenceTrackingToken(10), new GlobalSequenceTrackingToken(0));
        configuration.getComponent(UnitOfWorkFactory.class).create().executeWithResult(context -> {
            if (replay) context.putResource(TrackingToken.RESOURCE_KEY, token);
            return component.handle(message, context).asCompletableFuture();
        }).get(10, TimeUnit.SECONDS);
    }

    protected Throwable root(Throwable failure) {
        while (failure.getCause() != null) failure = failure.getCause();
        return failure;
    }
}
