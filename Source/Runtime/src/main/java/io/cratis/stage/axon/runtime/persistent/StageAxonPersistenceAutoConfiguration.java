package io.cratis.stage.axon.runtime.persistent;

import org.springframework.boot.autoconfigure.AutoConfiguration;
import org.springframework.boot.autoconfigure.AutoConfigurationPackage;
import org.springframework.boot.autoconfigure.condition.ConditionalOnClass;
import org.springframework.context.annotation.Import;

/**
 * Registers the persistent runtime helpers of a Stage Axon application: the claim entity package for entity scanning
 * and the persistent profile configuration. Nothing is registered when JPA is not on the classpath.
 */
@AutoConfiguration
@ConditionalOnClass(name = "jakarta.persistence.EntityManagerFactory")
@AutoConfigurationPackage(basePackageClasses = UniqueClaim.class)
@Import(PersistentConfiguration.class)
public class StageAxonPersistenceAutoConfiguration {}
