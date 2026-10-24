// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

package __RUNTIME__;

import java.util.List;
import java.util.Locale;

public final class UniqueValues {
    private UniqueValues() {}

    public static String key(List<?> values, boolean ignoreCasing) {
        var result = new StringBuilder();
        for (var value : values) {
            if (value == null) continue;
            var part = scalar(value, ignoreCasing);
            result.append(part.length()).append(':').append(part);
        }
        return result.toString();
    }

    public static String owner(Object value) { return scalar(value, false); }

    private static String scalar(Object value, boolean ignoreCasing) {
        if (value instanceof ConceptAs<?> concept) return scalar(concept.value(), ignoreCasing);
        if (value instanceof Number number) return "n" + new java.math.BigDecimal(number.toString()).stripTrailingZeros().toPlainString();
        if (value instanceof CharSequence text) return "s" + (ignoreCasing ? text.toString().toLowerCase(Locale.ROOT) : text.toString());
        return value.getClass().getName() + ":" + value;
    }
}
