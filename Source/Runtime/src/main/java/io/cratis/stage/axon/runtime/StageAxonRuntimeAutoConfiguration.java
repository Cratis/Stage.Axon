package io.cratis.stage.axon.runtime;

import org.springframework.boot.autoconfigure.AutoConfiguration;
import org.springframework.context.annotation.Import;

/** Registers the runtime helpers every generated Axon application relied on component scanning for. */
@AutoConfiguration(beforeName = "org.springframework.boot.webmvc.autoconfigure.WebMvcAutoConfiguration")
@Import({LiveStreams.class, ProblemDetailsAdvice.class, InMemoryUniqueConfiguration.class})
public class StageAxonRuntimeAutoConfiguration {
}
