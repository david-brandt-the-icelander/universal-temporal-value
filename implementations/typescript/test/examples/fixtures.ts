// Shared test data for the example tests. Each factory returns a fresh, valid value.
import { ZoneRules } from "../../src/index.js";

/**
 * The pinned zone rules of `conformance/rules/demo-zones.json`: America/New_York, Europe/Paris and
 * Atlantic/Reykjavik, for 2026 and 2027 only. UTV never reads the host's time zone database.
 */
export function demoZoneRules(): ZoneRules {
  // Compiled to dist/test/examples/, so the repository root is five levels up.
  return ZoneRules.load(new URL("../../../../../conformance/rules/demo-zones.json", import.meta.url));
}
