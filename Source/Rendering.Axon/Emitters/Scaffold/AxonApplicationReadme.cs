// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Stage.Rendering.Axon.Admission;

namespace Cratis.Stage.Rendering.Axon.Emitters.Scaffold;

internal static class AxonApplicationReadme
{
    internal static string Text(bool bearer, bool persistent) =>
        "# Generated Axon application\n\nRequires JDK 25 or later.\n\n```sh\nchmod +x mvnw\n./mvnw verify\n./mvnw spring-boot:run\n```\n\nOn Windows use `mvnw.cmd`. Maven Wrapper 3.3.4 downloads Maven 3.9.16 on first use. The wrapper scripts retain their Apache-2.0 license headers.\n\nThe default event store, tokens and read-model stores are in memory. Data is lost on restart; do not use this default for production. The service binds to loopback by default. " + (bearer ? "Protected endpoints accept bearer JWTs in the `Authorization` header. Set `spring.security.oauth2.resourceserver.jwt.issuer-uri` (or `jwk-set-uri` together with `issuer-uri`, or `public-key-location`) and `spring.security.oauth2.resourceserver.jwt.audiences`; until a provider is configured every token is rejected. A configured decoder source requires non-empty `spring.security.oauth2.resourceserver.jwt.audiences`, otherwise startup is refused. Roles are read from the `roles` claim (`authorities-claim-name`, `authority-prefix`)." : "Supply an identity provider before exposing protected endpoints.") + " No users or credentials are generated.\n\n" + ProfileParagraph(persistent) + "\n\nFor a local demo only, run `./mvnw spring-boot:run -Dspring-boot.run.profiles=demo` (or set `SPRING_PROFILES_ACTIVE=demo`). The opt-in `demo` profile authenticates every HTTP request as `demo` with all modeled roles, ignores Basic and bearer credentials, and logs a startup WARN. It is off by default. Never activate it in production.\n\nThis backend does not use Axon Server or containers. An optional application-owned `axonserver` profile may enable Axon Server; it is not generated or exercised in CI. Unsupported Screenplay constructs produce diagnostics and explicit intent comments; inspect these before deployment.\n";

    static string ProfileParagraph(bool persistent) => persistent
        ? "Durable storage is optional: run with the `persistent` profile (`./mvnw spring-boot:run -Dspring-boot.run.profiles=persistent`, combinable as `demo,persistent`). It stores events with Axon's `AggregateBasedJpaEventStorageEngine`, processor tokens with `JpaTokenStore`, and read models in the same H2 file database under `data/`. Set `spring.datasource.*` and add a driver to use another database. This engine keys event streams by identifier value: event sources that share an identifier value share one stream."
        : $"The optional `persistent` profile is not generated for this model: its constraints need multi-tag event consistency, which Axon's `AggregateBasedJpaEventStorageEngine` does not provide ({AxonRefusalCodes.PersistentProfile}). Activating `persistent` changes nothing; events stay in memory.";
}
