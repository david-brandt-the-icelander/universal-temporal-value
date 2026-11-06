package net.icelander.utv;

/** utv-result/1.0 status values. */
public enum Status {
    INVALID("invalid"), UNSUPPORTED("unsupported"), INDETERMINATE("indeterminate"), NONE("none"),
    UNIQUE("unique"), MULTIPLE("multiple"), RANGE("range"), SYMBOLIC("symbolic"),
    RESOURCE_LIMIT("resource-limit"), ERROR("error"), VALID("valid");

    private final String wire;
    Status(String wire) { this.wire = wire; }
    /** The JSON spelling of this status. */
    public String wire() { return wire; }
    @Override public String toString() { return wire; }
}
