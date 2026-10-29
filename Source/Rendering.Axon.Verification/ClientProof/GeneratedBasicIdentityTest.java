package __RUNTIME__;

import java.util.UUID;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.test.context.TestConfiguration;
import org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Import;
import org.springframework.security.core.userdetails.User;
import org.springframework.security.core.userdetails.UserDetailsService;
import org.springframework.security.core.userdetails.UsernameNotFoundException;
import org.springframework.security.provisioning.InMemoryUserDetailsManager;
import org.springframework.test.web.servlet.MockMvc;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.hamcrest.Matchers.startsWith;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.httpBasic;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.header;

@SpringBootTest(properties = {"spring.security.user.name=user", "spring.security.user.password=boot-default-would-accept"})
@AutoConfigureMockMvc
class GeneratedBasicIdentityTest {
    @Autowired private MockMvc http;
    @Autowired private UserDetailsService users;

    @Test
    void bootDefaultUserCannotAuthenticateWithConfiguredOrArbitraryPassword() throws Exception {
        assertThrows(UsernameNotFoundException.class, () -> users.loadUserByUsername("user"));
        for (var password : new String[] {"boot-default-would-accept", "any-password"}) {
            http.perform(get("/api/work/items/work-item-by-id").param("workItemId", UUID.randomUUID().toString())
                .with(httpBasic("user", password))).andExpect(status().isUnauthorized())
                .andExpect(header().string("WWW-Authenticate", startsWith("Basic")));
        }
    }
}

@SpringBootTest
@AutoConfigureMockMvc
@Import(GeneratedApplicationIdentityTest.Identity.class)
class GeneratedApplicationIdentityTest {
    @Autowired private MockMvc http;
    @Autowired private UserDetailsService users;

    @Test
    void applicationIdentityProviderWinsWhileNativeAuthorizationStillDeniesWrongRole() throws Exception {
        users.loadUserByUsername("application-reader");
        http.perform(get("/api/work/items/work-item-by-id").param("workItemId", UUID.randomUUID().toString())
            .with(httpBasic("application-reader", "application-password"))).andExpect(status().isForbidden());
    }

    @TestConfiguration
    static class Identity {
        @Bean
        UserDetailsService applicationUsers() {
            return new InMemoryUserDetailsManager(User.withUsername("application-reader")
                .password("{noop}application-password").roles("Reader").build());
        }
    }
}
