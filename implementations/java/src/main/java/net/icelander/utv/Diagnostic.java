package net.icelander.utv;

/** A result diagnostic: {code, severity, path, message}. Path is a JSON Pointer. */
public record Diagnostic(String code, String severity, String path, String message) {
    static Diagnostic error(String code, String path, String message) {
        return new Diagnostic(code, "error", path, message);
    }
}
