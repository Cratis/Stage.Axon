package io.cratis.stage.axon.runtime;

import java.util.Map;
import java.util.Set;
import java.util.stream.Collectors;
import org.springframework.security.authentication.AnonymousAuthenticationToken;
import org.springframework.security.core.context.SecurityContextHolder;

public record Caller(String identity, boolean authenticated, Set<String> roles, Map<String, String> claims) {
    public Caller {
        roles = Set.copyOf(roles);
        claims = Map.copyOf(claims);
    }

    public static Caller anonymous() { return new Caller("", false, Set.of(), Map.of()); }

    public static Caller current() {
        var authentication = SecurityContextHolder.getContext().getAuthentication();
        if (authentication == null) return anonymous();
        if (!authentication.isAuthenticated()) return anonymous();
        if (authentication instanceof AnonymousAuthenticationToken) return anonymous();
        var roles = authentication.getAuthorities().stream().map(authority -> authority.getAuthority())
            .filter(authority -> authority.startsWith("ROLE_")).map(authority -> authority.substring(5)).collect(Collectors.toSet());
        return new Caller(authentication.getName(), true, roles, Map.of());
    }
}
