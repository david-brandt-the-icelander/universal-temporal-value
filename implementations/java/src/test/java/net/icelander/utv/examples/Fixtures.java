package net.icelander.utv.examples;

import java.io.IOException;
import java.io.UncheckedIOException;
import java.nio.file.Path;
import net.icelander.utv.ZoneRules;

/** Shared test data for the example tests. Each factory returns a fresh, valid value. */
public final class Fixtures {
    private Fixtures() {}

    /**
     * The pinned zone rules of {@code conformance/rules/demo-zones.json}: America/New_York, Europe/Paris and
     * Atlantic/Reykjavik, for 2026 and 2027 only. UTV never reads the host's time zone database.
     *
     * <p>The file is read in place from the repository; Maven runs the tests in {@code implementations/java}.
     */
    public static ZoneRules demoZoneRules() {
        Path path = Path.of(System.getProperty("user.dir")).resolve("../../conformance/rules/demo-zones.json").normalize();
        try {
            return ZoneRules.load(path);
        } catch (IOException e) {
            throw new UncheckedIOException("cannot read the demo zone rules at " + path, e);
        }
    }
}
