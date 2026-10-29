package __RUNTIME__;

import __APPLICATION__.accounts.events.EventTags;
import __APPLICATION__.accounts.registry.close.CloseAccount;
import __APPLICATION__.accounts.registry.register.RegisterAccount;
import __APPLICATION__.concepts.AccountId;
import java.util.ArrayList;
import java.util.List;
import java.util.UUID;
import java.util.concurrent.Callable;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.TimeUnit;
import org.axonframework.common.configuration.Configuration;
import org.axonframework.eventsourcing.eventstore.EventStorageEngine;
import org.axonframework.eventsourcing.eventstore.SourcingCondition;
import org.axonframework.eventsourcing.eventstore.TerminalEventMessage;
import org.axonframework.messaging.commandhandling.gateway.CommandGateway;
import org.axonframework.messaging.core.Metadata;
import org.axonframework.messaging.eventhandling.EventMessage;
import org.axonframework.messaging.eventstreaming.EventCriteria;
import org.axonframework.messaging.eventstreaming.Tag;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.security.core.authority.SimpleGrantedAuthority;
import org.springframework.security.core.context.SecurityContextHolder;

final class GeneratedIngressSupport {
    private GeneratedIngressSupport() {}

    static <T> T as(String name, String role, Callable<T> action) throws Exception {
        var previous = SecurityContextHolder.getContext();
        var context = SecurityContextHolder.createEmptyContext();
        if (name != null) context.setAuthentication(new UsernamePasswordAuthenticationToken(name, "unused", List.of(new SimpleGrantedAuthority("ROLE_" + role))));
        SecurityContextHolder.setContext(context);
        try { return action.call(); }
        finally { SecurityContextHolder.setContext(previous); }
    }

    static Metadata metadata(String name, String role) {
        return name == null ? Metadata.emptyInstance() : Metadata.with("username", name).and("authorities", "ROLE_" + role);
    }

    static CompletableFuture<Void> send(CommandGateway gateway, Object command) {
        try {
            as("alice", "Writer", () -> gateway.send(command, Void.class, metadata("alice", "Writer"), null).get(10, TimeUnit.SECONDS));
            return CompletableFuture.completedFuture(null);
        } catch (Exception failure) { return CompletableFuture.failedFuture(root(failure)); }
    }

    static CompletableFuture<Void> register(CommandGateway gateway, UUID owner, String value) {
        return send(gateway, new RegisterAccount(new AccountId(owner), value, "forged-body-principal"));
    }

    static CompletableFuture<Void> release(CommandGateway gateway, UUID owner) {
        return send(gateway, new CloseAccount(new AccountId(owner), "released"));
    }

    static List<EventMessage> facts(Configuration configuration, UUID owner) throws Exception {
        var stream = configuration.getComponent(EventStorageEngine.class).source(SourcingCondition.conditionFor(
                EventCriteria.havingTags(Tag.of(EventTags.ACCOUNT_ID, owner.toString()))));
        try {
            return stream.filter(entry -> !(entry.message() instanceof TerminalEventMessage))
                    .collect(ArrayList<EventMessage>::new, (all, entry) -> all.add(entry)).get(10, TimeUnit.SECONDS);
        } finally { stream.close(); }
    }

    static Throwable root(Throwable failure) {
        while (failure.getCause() != null) failure = failure.getCause();
        return failure;
    }
}
