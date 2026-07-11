import type { ActionStatus } from "./types";

// Mirror of the server's ActionWorkflow (pure state machine): Proposed → Approved → InProgress → Resolved.
// The client uses this only to drive the UI (show the right "advance" button); the server remains the gate of record
// and a 409 is still handled if the two ever disagree.
const NEXT: Record<ActionStatus, ActionStatus | null> = {
  Proposed: "Approved",
  Approved: "InProgress",
  InProgress: "Resolved",
  Resolved: null,
};

export function nextStatus(status: ActionStatus): ActionStatus | null {
  return NEXT[status];
}

/** Resolving is the only transition that requires an outcome (Sold / NotSold). */
export function requiresOutcome(target: ActionStatus): boolean {
  return target === "Resolved";
}

const VERB: Record<ActionStatus, string> = {
  Proposed: "Propose",
  Approved: "Approve",
  InProgress: "Start",
  Resolved: "Resolve",
};

/** The button label to advance *into* the given status (e.g. Approved → "Approve"). */
export function advanceLabel(target: ActionStatus): string {
  return VERB[target];
}
