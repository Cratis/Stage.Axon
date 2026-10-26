// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Emitters.Persistence;

internal static class PersistentViewEntities
{
    internal const string Row = """
        // Copyright (c) Cratis. All rights reserved.
        // Licensed under the MIT license. See LICENSE file in the project root for full license information.
        package __PACKAGE__;

        import jakarta.persistence.Column;
        import jakarta.persistence.Entity;
        import jakarta.persistence.Id;
        import jakarta.persistence.Lob;
        import jakarta.persistence.Table;

        @Entity(name = "StageViewRow")
        @Table(name = "stage_view_row")
        public class ViewRow {
            @Id @Column(length = 64) private String id;
            @Column(nullable = false, length = 1024) private String namespace;
            @Lob @Column(nullable = false) private String identity;
            @Lob @Column(nullable = false) private String payload;
            protected ViewRow() {}
            ViewRow(String id, String namespace, String identity, String payload) {
                this.id = id;
                this.namespace = namespace;
                this.identity = identity;
                this.payload = payload;
            }
            String identity() { return identity; }
            String payload() { return payload; }
            void payload(String value) { payload = value; }
        }
        """;

    internal const string Generation = """
        // Copyright (c) Cratis. All rights reserved.
        // Licensed under the MIT license. See LICENSE file in the project root for full license information.
        package __PACKAGE__;

        import jakarta.persistence.Column;
        import jakarta.persistence.Entity;
        import jakarta.persistence.Id;
        import jakarta.persistence.Table;

        @Entity(name = "StageViewGeneration")
        @Table(name = "stage_view_generation")
        public class ViewGeneration {
            @Id @Column(length = 64) private String id;
            @Column(nullable = false, length = 64) private String fingerprint;
            protected ViewGeneration() {}
            ViewGeneration(String id, String fingerprint) { this.id = id; this.fingerprint = fingerprint; }
            String fingerprint() { return fingerprint; }
        }
        """;
}
