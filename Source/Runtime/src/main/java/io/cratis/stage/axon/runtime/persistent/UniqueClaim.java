package io.cratis.stage.axon.runtime.persistent;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.Id;
import jakarta.persistence.Table;

@Entity
@Table(name = "stage_unique_claim")
public class UniqueClaim {
    @Id
    @Column(length = 8192)
    private String id;
    @Column(nullable = false, length = 2048)
    private String constraintName;
    @Column(nullable = false, length = 4096)
    private String owner;

    protected UniqueClaim() {}
    public UniqueClaim(String id, String constraintName, String owner) {
        this.id = id;
        this.constraintName = constraintName;
        this.owner = owner;
    }
    public String owner() { return owner; }
}
