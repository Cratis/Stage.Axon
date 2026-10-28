package io.cratis.stage.axon.fixture;

import org.springframework.boot.SpringBootConfiguration;
import org.springframework.boot.autoconfigure.EnableAutoConfiguration;

/** An application outside the runtime module package, with no entity scan, as a generated application without views is. */
@SpringBootConfiguration
@EnableAutoConfiguration
class FixtureApplication {}
