// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Emitters.Persistence;

internal static class PersistentViewLegacyProof
{
    internal const string Source = """
        package __PACKAGE__;

        import java.nio.file.Path;
        import java.sql.Blob;
        import java.sql.Clob;
        import java.sql.DriverManager;
        import java.util.ArrayList;
        import java.util.Base64;
        import java.util.List;
        import java.util.Map;
        import java.util.TreeMap;
        import org.junit.jupiter.api.Test;
        import static org.junit.jupiter.api.Assertions.*;
        import static __PACKAGE__.__NAME__Support.*;

        class __NAME__LegacyTest {
            @Test
            void retainedDataWithoutMatchingGenerationIsNeverAdoptedOrReset() throws Exception {
                for (var missing : List.of(true, false)) {
                    var path = database();
                    try (var application = start(path)) {
        __APPENDS__
                        await(() -> !store(application).all().isEmpty() && tokens(application).values().stream().anyMatch(value -> !value.equals("<null>")),
                                () -> ": expected rows: " + normalized(expected()) + ", actual rows: " + normalized(store(application).all()) + ", count: " + count(application) + ", tokens: " + tokens(application));
                    }
                    // Deliberately construct isolated legacy/incompatible test databases, never a live application database.
                    try (var connection = DriverManager.getConnection(url(path), "sa", ""); var statement = connection.createStatement()) {
                        assertEquals(1, statement.executeUpdate(missing ? "delete from stage_view_generation" :
                                "update stage_view_generation set fingerprint = 'incompatible'"));
                    }
                    var before = snapshot(path);
                    assertFalse(before.getOrDefault("AGGREGATE_EVENT_ENTRY", List.of()).isEmpty());
                    assertFalse(before.getOrDefault("TOKEN_ENTRY", List.of()).isEmpty());
                    assertFalse(before.getOrDefault("STAGE_VIEW_ROW", List.of()).isEmpty());
                    var failure = assertThrows(Exception.class, () -> { try (var unexpected = start(path)) { assertTrue(unexpected.isActive()); fail("Legacy startup must refuse"); } });
                    Throwable root = failure;
                    while (root.getCause() != null) root = root.getCause();
                    assertTrue(root.getMessage().contains("STAGE-AXON-0308"), root.toString());
                    assertEquals(before, snapshot(path), "Refusal must retain event, claim, token and view values exactly");
                }
            }
            private static String url(Path path) { return "jdbc:h2:file:" + path + ";IFEXISTS=TRUE"; }
            private static Map<String, List<String>> snapshot(Path path) throws Exception {
                var tables = new TreeMap<String, List<String>>();
                try (var connection = DriverManager.getConnection(url(path), "sa", "")) {
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
                assertFalse(tables.isEmpty());
                return tables;
            }
            private static String normalize(Object value) throws Exception {
                if (value == null) return "<null>";
                if (value instanceof byte[] bytes) return Base64.getEncoder().encodeToString(bytes);
                if (value instanceof Blob blob) return Base64.getEncoder().encodeToString(blob.getBytes(1, Math.toIntExact(blob.length())));
                if (value instanceof Clob clob) return clob.getSubString(1, Math.toIntExact(clob.length()));
                return value.getClass().getName() + ":" + value;
            }
        }
        """;
}
