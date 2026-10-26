// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Text;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Persistence;

internal sealed class AxonPersistenceEmitter : IAxonEmitter
{
    public bool IsImplemented => true;

    public void Emit(AxonEmissionContext context)
    {
        var models = ScopeSelection.Select(context.Request).SelectMany(owner => owner.Slice.ReadModels).ToArray();
        if (!models.Any(model => PersistentViewAdmission.Supports(context, model.Id))) return;
        var types = new ViewJavaTypes(context.Request.Model.Application);
        var package = types.BasePackage + ".persistence";
        var runtime = types.BasePackage + ".runtime";
        var ids = models.Select(model => model.Id).ToArray();

        // Hash the generated application contract, not filesystem bytes or a mutable runtime checkpoint.
        var contract = string.Join('\n', context.Artifacts.Where(artifact => artifact.RelativePath.StartsWith("src/main/java/", StringComparison.Ordinal))
            .OrderBy(artifact => artifact.RelativePath, StringComparer.Ordinal)
            .Select(artifact => artifact.RelativePath + "\n" + Encoding.UTF8.GetString(artifact.Bytes.AsSpan())));
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("flat-view-json-v1\n" + contract)));
        void Add(string name, string source) => ViewJavaTypes.Add(
            context,
            package,
            name,
            source.Replace("__PACKAGE__", package, StringComparison.Ordinal).Replace("__RUNTIME__", runtime, StringComparison.Ordinal),
            ids);
        Add("ViewRow", PersistentViewEntities.Row);
        Add("ViewGeneration", PersistentViewEntities.Generation);
        Add("JpaReadModelStore", PersistentViewStore.Source);
        Add("PersistentViewBootstrap", PersistentViewBootstrap.Source);
        var processors = ScopeSelection.Select(context.Request)
            .Where(owner => owner.Slice.ReadModels.Any(model => PersistentViewAdmission.Supports(context, model.Id)))
            .Select(types.Package).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)
            .Select((name, index) => $$"""
                @org.springframework.context.annotation.Bean
                org.axonframework.extension.spring.config.EventProcessorDefinition persistentViewProcessor{{index}}() {
                    return org.axonframework.extension.spring.config.EventProcessorDefinition.pooledStreamingMatching("{{name}}")
                            .customized(config -> {
                                config.errorHandler(org.axonframework.messaging.eventhandling.processing.errorhandling.PropagatingErrorHandler.INSTANCE);
                                return config;
                            });
                }
            """);
        Add("PersistentViewConfiguration", Configuration(package, runtime, types.BasePackage, fingerprint, string.Join('\n', processors)));
        new PersistentViewTests(context, types, package).Emit();
    }

    static string Configuration(string package, string runtime, string application, string fingerprint, string processors) => $$"""
        // Copyright (c) Cratis. All rights reserved.
        // Licensed under the MIT license. See LICENSE file in the project root for full license information.
        package {{package}};

        import jakarta.persistence.EntityManagerFactory;
        import org.axonframework.eventsourcing.eventstore.jpa.AggregateEventEntry;
        import org.axonframework.messaging.eventhandling.processing.streaming.token.store.jpa.TokenEntry;
        import org.springframework.boot.persistence.autoconfigure.EntityScan;
        import org.springframework.context.annotation.Configuration;
        import org.springframework.context.annotation.Profile;
        import {{runtime}}.persistent.UniqueClaim;

        @Configuration(proxyBeanMethods = false)
        @Profile("persistent")
        @EntityScan(basePackageClasses = { ViewRow.class, ViewGeneration.class, UniqueClaim.class, AggregateEventEntry.class, TokenEntry.class })
        public class PersistentViewConfiguration {
            public static final String FINGERPRINT = "{{fingerprint}}";
            public PersistentViewConfiguration(EntityManagerFactory factory) {
                PersistentViewBootstrap.verify(factory, "{{application}}", FINGERPRINT);
            }
        {{processors}}
        }
        """;
}
