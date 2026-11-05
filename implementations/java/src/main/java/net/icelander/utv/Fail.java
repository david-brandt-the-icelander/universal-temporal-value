package net.icelander.utv;

import java.util.List;
import java.util.function.Supplier;

/** Internal control flow for a semantic outcome other than success; never escapes a public operation. */
final class Fail extends RuntimeException {
    final Status status;
    final transient Diagnostic diagnostic;

    Fail(Status status, String code, String path, String message) {
        super(message, null, false, false);
        this.status = status;
        this.diagnostic = new Diagnostic(code, "error", path, message);
    }

    static Fail invalid(String code, String path, String message) {
        return new Fail(Status.INVALID, code, path, message);
    }

    static Fail unsupported(String code, String path, String message) {
        return new Fail(Status.UNSUPPORTED, code, path, message);
    }

    /** Runs an operation body, turning a {@link Fail} into its result and any other failure into {@code error}. */
    static UtvResult run(String op, Supplier<UtvResult> body) {
        try {
            return body.get();
        } catch (Fail f) {
            return new UtvResult(op, f.status, List.of(f.diagnostic));
        } catch (RuntimeException e) {
            return new UtvResult(op, Status.ERROR, List.of(new Diagnostic("utv.error", "error", "", String.valueOf(e))));
        }
    }
}
