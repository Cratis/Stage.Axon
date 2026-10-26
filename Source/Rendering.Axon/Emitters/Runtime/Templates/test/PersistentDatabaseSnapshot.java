// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

package __RUNTIME__;

import java.sql.Blob;
import java.sql.Clob;
import java.sql.DriverManager;
import java.util.ArrayList;
import java.util.Base64;
import java.util.List;
import java.util.Map;
import java.util.TreeMap;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;

/** Logical SELECT-only inspection of all user-table values, never token fetch/claim or JPA startup. */
record PersistentDatabaseSnapshot(Map<String, List<String>> tables) {
    static PersistentDatabaseSnapshot read(String database) throws Exception {
        var tables = new TreeMap<String, List<String>>();
        try (var connection = DriverManager.getConnection(database + ";IFEXISTS=TRUE", "sa", "")) {
            connection.setReadOnly(true);
            try (var metadata = connection.getMetaData().getTables(null, "PUBLIC", "%", new String[] { "TABLE", "BASE TABLE" })) {
                while (metadata.next()) {
                    var table = metadata.getString("TABLE_NAME");
                    var rows = new ArrayList<String>();
                    try (var statement = connection.createStatement();
                         var values = statement.executeQuery("SELECT * FROM \"PUBLIC\".\"" + table.replace("\"", "\"\"") + "\"")) {
                        while (values.next()) {
                            var row = new ArrayList<String>();
                            for (var column = 1; column <= values.getMetaData().getColumnCount(); column++) {
                                var value = normalize(values.getObject(column));
                                row.add(values.getMetaData().getColumnName(column) + "=" + value.length() + ":" + value);
                            }
                            rows.add(row.toString());
                        }
                    }
                    rows.sort(String::compareTo);
                    tables.put(table, List.copyOf(rows));
                }
            }
        }
        assertFalse(tables.isEmpty(), "Inspection must find user tables");
        return new PersistentDatabaseSnapshot(Map.copyOf(tables));
    }

    void assertSeeded(String owner, String checkpoint) {
        assertTrue(tables.values().stream().flatMap(List::stream).anyMatch(row -> row.contains(owner)), "Seed event/claim owner");
        assertTrue(tables.getOrDefault("STAGE_UNIQUE_CLAIM", List.of()).stream().anyMatch(row -> row.contains(owner)), "Nonempty unique claim");
        assertTrue(tables.getOrDefault("AGGREGATE_EVENT_ENTRY", List.of()).size() > 0, "Nonempty event rows");
        var tokens = tables.getOrDefault("TOKEN_ENTRY", List.of());
        assertTrue(tokens.stream().anyMatch(row -> row.contains(checkpoint) && !row.contains("TOKEN=6:<null>")), "Non-null synthetic token row");
        System.out.println("Nonempty snapshot: events=" + tables.get("AGGREGATE_EVENT_ENTRY").size()
                + ", claims=" + tables.get("STAGE_UNIQUE_CLAIM").size() + ", tokens=" + tokens.size());
    }

    private static String normalize(Object value) throws Exception {
        if (value == null) return "<null>";
        if (value instanceof byte[] bytes) return Base64.getEncoder().encodeToString(bytes);
        if (value instanceof Blob blob) return Base64.getEncoder().encodeToString(blob.getBytes(1, Math.toIntExact(blob.length())));
        if (value instanceof Clob clob) return clob.getSubString(1, Math.toIntExact(clob.length()));
        return value.getClass().getName() + ":" + value;
    }
}
