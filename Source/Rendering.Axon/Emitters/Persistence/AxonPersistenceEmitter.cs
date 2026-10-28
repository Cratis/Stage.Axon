// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Text;
using Cratis.Stage.Rendering.Axon.Emitters.Projections;
using Cratis.Stage.Rendering.Axon.Emitters.Runtime;
using Cratis.Stage.Rendering.Axon.Java;
using Cratis.Stage.Rendering.Axon.Sdk;

namespace Cratis.Stage.Rendering.Axon.Emitters.Persistence;

internal sealed class AxonPersistenceEmitter : IAxonEmitter
{
    public bool IsImplemented => true;

    public void Emit(AxonEmissionContext context)
    {
        var models = ScopeSelection.Select(context.Request).SelectMany(owner => owner.Slice.ReadModels).ToArray();
        if (!models.Any(model => PersistentViewAdmission.Supports(context, model.Id))) return;
        var types = new ViewJavaTypes(context.Request.Model.Application, context.BasePackage);
        var package = types.BasePackage + ".persistence";
        var ids = models.Select(model => model.Id).ToArray();

        // Hash the generated application contract, not filesystem bytes or a mutable runtime checkpoint.
        var contract = string.Join('\n', context.Artifacts.Where(artifact => artifact.RelativePath.StartsWith("src/main/java/", StringComparison.Ordinal))
            .OrderBy(artifact => artifact.RelativePath, StringComparer.Ordinal)
            .Select(artifact => artifact.RelativePath + "\n" + Encoding.UTF8.GetString(artifact.Bytes.AsSpan())));
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("flat-view-json-v2-row-sequenced\n" + contract)));
        void Add(string name, string source) => ViewJavaTypes.Add(
            context,
            package,
            name,
            PersistentJavaTemplate.Render(source.Replace("__PACKAGE__", package, StringComparison.Ordinal).Replace("__RUNTIME_MODULE__", AxonRuntimePackage.ModulePackage, StringComparison.Ordinal)),
            ids);
        Add("ViewRow", PersistentViewEntities.Row);
        Add("ViewGeneration", PersistentViewEntities.Generation);
        Add("JpaReadModelStore", PersistentViewStore.Source);
        Add("PersistentViewBootstrap", PersistentViewBootstrap.Source);
        Add("PersistentViewProcessorGuard", PersistentViewProcessorGuard.Source);
        var namespaces = ScopeSelection.Select(context.Request)
            .Where(owner => owner.Slice.ReadModels.Any(model => PersistentViewAdmission.Supports(context, model.Id)))
            .Select(types.Package).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var processors = namespaces.Select((name, index) => $$"""
                @Bean
                EventProcessorDefinition persistentViewProcessor{{index}}() {
                    return EventProcessorDefinition.pooledStreamingMatching("{{name}}")
                            .customized(config -> {
                                config.errorHandler(PropagatingErrorHandler.INSTANCE);
                                // A concrete first position, not Axon's default replay token without a current position:
                                // a segment that has not stored a token yet would otherwise make the restarted stream
                                // open at the current first event, whose gap-free tokens the other segments' stored
                                // tokens do not cover, so those segments would apply their events a second time.
                                config.initialToken(source -> source.firstToken(null));
                                return config;
                            });
                }
            """);
        Add("PersistentViewConfiguration", Configuration(package, types.BasePackage, fingerprint, ProcessorGuard(namespaces) + '\n' + string.Join('\n', processors)));
        new PersistentViewTests(context, types, package).Emit();
    }

    static string ProcessorGuard(IEnumerable<string> namespaces) => $$"""
            @Bean
            SmartInitializingSingleton persistentViewProcessorGuard(org.axonframework.common.configuration.Configuration configuration) {
                return () -> PersistentViewProcessorGuard.verify(configuration, Set.of({{string.Join(", ", namespaces.Select(JavaCodeBuilder.StringLiteral))}}));
            }
        """;

    static string Configuration(string package, string application, string fingerprint, string processors) => $$"""
        package {{package}};

        import jakarta.persistence.EntityManagerFactory;
        import java.util.Set;
        import org.axonframework.eventsourcing.eventstore.jpa.AggregateEventEntry;
        import org.axonframework.extension.spring.config.EventProcessorDefinition;
        import org.axonframework.messaging.eventhandling.processing.errorhandling.PropagatingErrorHandler;
        import org.axonframework.messaging.eventhandling.processing.streaming.token.store.jpa.TokenEntry;
        import org.springframework.beans.factory.SmartInitializingSingleton;
        import org.springframework.boot.persistence.autoconfigure.EntityScan;
        import org.springframework.context.annotation.Bean;
        import org.springframework.context.annotation.Configuration;
        import org.springframework.context.annotation.Profile;
        import {{AxonRuntimePackage.Type(application, "persistent.UniqueClaim")}};

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
