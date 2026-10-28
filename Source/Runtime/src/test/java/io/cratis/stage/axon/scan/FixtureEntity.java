package io.cratis.stage.axon.scan;

import jakarta.persistence.Entity;
import jakarta.persistence.Id;
import jakarta.persistence.Table;

@Entity
@Table(name = "fixture_entity")
class FixtureEntity {
    @Id String id;
}
