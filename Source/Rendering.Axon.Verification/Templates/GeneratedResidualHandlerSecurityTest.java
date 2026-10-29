package __RUNTIME__;

import __APPLICATION__.accounts.registry.active.ActiveAccountsRepository;
import __APPLICATION__.accounts.registry.active.AuthenticatedSnapshot;
import __APPLICATION__.accounts.registry.active.NotBlockedSnapshot;
import __APPLICATION__.accounts.registry.active.WriterReviewerSnapshot;
import __APPLICATION__.accounts.registry.active.NamedSnapshot;
import __APPLICATION__.concepts.AccountId;
import java.util.Arrays;
import java.util.List;
import java.util.UUID;
import java.util.concurrent.Callable;
import java.util.concurrent.TimeUnit;
import org.axonframework.messaging.core.Metadata;
import org.axonframework.messaging.queryhandling.gateway.QueryGateway;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.security.access.AccessDeniedException;
import org.springframework.security.authentication.AuthenticationCredentialsNotFoundException;
import org.springframework.security.core.authority.SimpleGrantedAuthority;
import org.springframework.security.core.context.SecurityContextHolder;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.security.oauth2.server.resource.authentication.JwtAuthenticationToken;
import org.springframework.test.context.bean.override.mockito.MockitoSpyBean;
import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.mockito.Mockito.clearInvocations;
import static org.mockito.Mockito.verifyNoInteractions;

@SpringBootTest(properties = "spring.security.oauth2.resourceserver.jwt.audiences=stage-tests")
class GeneratedResidualHandlerSecurityTest {
    @Autowired private QueryGateway queries;
    @MockitoSpyBean private ActiveAccountsRepository repository;

    @Test
    void residualQueryPoliciesGuardNonHttpDispatchUsingTheHandlerThreadContext() throws Exception {
        var id = new AccountId(UUID.randomUUID());
        var spoofed = Metadata.with("username", "writer").and("authorities", "ROLE_Writer,ROLE_Reviewer");
        try {
            SecurityContextHolder.clearContext();
            denied(() -> queries.query(new AuthenticatedSnapshot(id), AuthenticatedSnapshot.Result.class, spoofed, null).get(10, TimeUnit.SECONDS));
            caller("other", "Blocked");
            denied(() -> queries.query(new NotBlockedSnapshot(id), NotBlockedSnapshot.Result.class, spoofed, null).get(10, TimeUnit.SECONDS));
            caller("writer", "Writer");
            denied(() -> queries.query(new WriterReviewerSnapshot(id), WriterReviewerSnapshot.Result.class, spoofed, null).get(10, TimeUnit.SECONDS));
            caller("other", "Writer", "Reviewer");
            denied(() -> queries.query(new NamedSnapshot(id), NamedSnapshot.Result.class, spoofed, null).get(10, TimeUnit.SECONDS));
            caller("writer", "Writer", "Reviewer");
            assertNotNull(queries.query(new AuthenticatedSnapshot(id), AuthenticatedSnapshot.Result.class, Metadata.emptyInstance(), null).get(10, TimeUnit.SECONDS));
            assertNotNull(queries.query(new NotBlockedSnapshot(id), NotBlockedSnapshot.Result.class, Metadata.emptyInstance(), null).get(10, TimeUnit.SECONDS));
            assertNotNull(queries.query(new WriterReviewerSnapshot(id), WriterReviewerSnapshot.Result.class, Metadata.emptyInstance(), null).get(10, TimeUnit.SECONDS));
            assertNotNull(queries.query(new NamedSnapshot(id), NamedSnapshot.Result.class, Metadata.emptyInstance(), null).get(10, TimeUnit.SECONDS));
        } finally {
            SecurityContextHolder.clearContext();
        }
    }

    private void denied(Callable<?> dispatch) {
        clearInvocations((Object) repository);
        Throwable failure = assertThrows(Exception.class, dispatch::call);
        while (failure.getCause() != null) failure = failure.getCause();
        assertTrue(failure instanceof AccessDeniedException || failure instanceof AuthenticationCredentialsNotFoundException, failure.toString());
        verifyNoInteractions(repository);
    }

    private static void caller(String name, String... roles) {
        var token = Jwt.withTokenValue("fixture").header("alg", "none").subject(name).build();
        var authorities = Arrays.stream(roles).map(role -> new SimpleGrantedAuthority("ROLE_" + role)).toList();
        SecurityContextHolder.getContext().setAuthentication(new JwtAuthenticationToken(token, authorities));
    }
}
