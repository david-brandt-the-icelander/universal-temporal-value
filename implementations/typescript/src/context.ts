/**
 * `TemporalContext` (docs/library-api.md §4): holds the zone rules once, so they aren't threaded
 * through every call. There is no default context: nothing reads the host's time zone database.
 */
import { Multiple, Unique, failed, Ordering, type Outcome, type ResolutionPolicy } from "./outcome.js";
import type { ZoneRules } from "./zones.js";
import { Instant, type ZonedIntent } from "./values.js";
import type { Diagnostic } from "./result.js";

export class TemporalContext {
  private constructor(readonly rules: ZoneRules) {}

  /** A context over explicit rules, e.g. `TemporalContext.with(ZoneRules.load(path))`. */
  static with(rules: ZoneRules): TemporalContext { return new TemporalContext(rules); }

  /** Same as `zoned.resolve(ctx, policy)`. */
  resolve(zoned: ZonedIntent, policy?: ResolutionPolicy): Outcome<Instant> { return zoned.resolve(this, policy); }

  /** Same as `instant.inZone(zoneId, ctx)`. */
  inZone(instant: Instant, zoneId: string): Outcome<ZonedIntent> { return instant.inZone(zoneId, this); }

  /**
   * Orders two values when either is zoned. Both are resolved strictly, then compared. If an overlap
   * makes the order depend on which instant is meant, the answer is `multiple` (each possible order).
   */
  compare(a: ZonedIntent | Instant, b: ZonedIntent | Instant): Outcome<Ordering> {
    const ra = this.#instants(a), rb = this.#instants(b);
    const diagnostics: Diagnostic[] = [...ra.diagnostics, ...rb.diagnostics];
    for (const r of [ra, rb]) {
      if (r.status !== "unique" && r.status !== "multiple") return failed(r.status, diagnostics, "compare");
    }
    const xs = ra.status === "unique" ? [ra.value] : ra.status === "multiple" ? ra.values : [];
    const ys = rb.status === "unique" ? [rb.value] : rb.status === "multiple" ? rb.values : [];
    const found = new Set(xs.flatMap((x) => ys.map((y) => x.compare(y))));
    const orders = ([-1, 0, 1] as const).filter((o) => found.has(o)).map((o) => [Ordering.Before, Ordering.Equal, Ordering.After][o + 1]!);
    return orders.length === 1 ? new Unique(orders[0]!, diagnostics, "compare") : new Multiple(orders, diagnostics, "compare");
  }

  #instants(v: ZonedIntent | Instant): Outcome<Instant> {
    return v instanceof Instant ? new Unique(v, [], "resolve") : v.resolve(this);
  }
}
