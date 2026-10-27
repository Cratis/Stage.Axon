package __RUNTIME__;

import java.util.List;

public final class QueryResults {
    private QueryResults() {}

    public static <T> List<T> exactlyOne(List<T> rows) {
        if (rows.size() != 1) throw new QueryCardinalityException("exactly one", rows.size());
        return rows;
    }

    public static <T> List<T> atMostOne(List<T> rows) {
        if (rows.size() > 1) throw new QueryCardinalityException("at most one", rows.size());
        return rows;
    }
}
