// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

package __RUNTIME__.persistent;

@jakarta.persistence.Entity
@jakarta.persistence.Table(name = "stage_unique_claim")
public class UniqueClaim {
    @jakarta.persistence.Id
    @jakarta.persistence.Column(length = 8192)
    private String id;
    @jakarta.persistence.Column(nullable = false, length = 2048)
    private String constraintName;
    @jakarta.persistence.Column(nullable = false, length = 4096)
    private String owner;

    protected UniqueClaim() {}
    public UniqueClaim(String id, String constraintName, String owner) {
        this.id = id;
        this.constraintName = constraintName;
        this.owner = owner;
    }
    public String owner() { return owner; }
}
