import { useEffect, useRef, useState } from "react";
import { CheckCircle2, History, Loader2, Plus } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { ApiError } from "@/lib/api";
import { ACTION_STATUS_STYLES, formatCurrency, formatDateTime, humanizeEnum } from "@/lib/format";
import { useCreateAction, useTransitionAction } from "@/lib/hooks";
import { advanceLabel, nextStatus, requiresOutcome } from "@/lib/lifecycle";
import { cn } from "@/lib/utils";
import {
  ACTION_TYPES,
  type ActionOutcome,
  type ActionType,
  type InventoryAction,
} from "@/lib/types";

export interface ActionDraft {
  type: ActionType;
  proposedValue: string;
  note: string;
}

export function ActionPanel({
  vehicleId,
  history,
  draft,
  onDraftChange,
  flashKey = 0,
  readOnly = false,
}: {
  vehicleId: string;
  history: InventoryAction[];
  draft: ActionDraft;
  onDraftChange: (draft: ActionDraft) => void;
  flashKey?: number;
  readOnly?: boolean;
}) {
  const createAction = useCreateAction(vehicleId);
  const [error, setError] = useState<string | null>(null);
  const proposedValueRef = useRef<HTMLInputElement>(null);

  // When the AI recommendation is pulled in, bring the prefilled field into view if it's off-screen.
  // block: "nearest" is a no-op when it's already visible, so on-screen fields don't jump.
  useEffect(() => {
    if (flashKey > 0) {
      proposedValueRef.current?.scrollIntoView({ behavior: "smooth", block: "nearest" });
    }
  }, [flashKey]);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    const trimmedNote = draft.note.trim();
    if (trimmedNote.length === 0) {
      setError("A note is required.");
      return;
    }
    const proposedValue =
      draft.proposedValue.trim() === "" ? null : Number(draft.proposedValue);
    if (proposedValue !== null && Number.isNaN(proposedValue)) {
      setError("Proposed value must be a number.");
      return;
    }
    try {
      await createAction.mutateAsync({ type: draft.type, proposedValue, note: trimmedNote });
      onDraftChange({ type: "PriceReduction", proposedValue: "", note: "" });
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Couldn't log the action.");
    }
  }

  // Newest first for the history feed.
  const ordered = [...history].sort(
    (a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime(),
  );

  return (
    <Card>
      <CardHeader className="p-5 pb-3">
        <CardTitle className="font-display flex items-center gap-2 text-[15px] font-bold tracking-[-0.01em]">
          <History className="h-4 w-4" />
          Actions &amp; history
        </CardTitle>
        <CardDescription>
          {readOnly
            ? "This vehicle has left inventory — its action history is retained for review, read-only."
            : "Advance an action through its lifecycle, or log a new one. Every action is retained."}
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-5 p-5 pt-0">
        <div className="space-y-2">
          {ordered.length === 0 ? (
            <p className="rounded-md border border-dashed py-6 text-center text-sm text-muted-foreground">
              No actions logged yet.
            </p>
          ) : (
            ordered.map((action) => (
              <ActionRow
                key={action.id}
                vehicleId={vehicleId}
                action={action}
                readOnly={readOnly}
              />
            ))
          )}
        </div>

        {readOnly ? null : (
          <form onSubmit={submit} className="space-y-3 rounded-md border p-3">
          <p className="text-[10.5px] font-semibold uppercase tracking-[0.08em] text-muted-foreground">
            Log a new action
          </p>
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            <div className="space-y-1">
              <Label>Action type</Label>
              <Select
                value={draft.type}
                onValueChange={(v) => onDraftChange({ ...draft, type: v as ActionType })}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {ACTION_TYPES.map((type) => (
                    <SelectItem key={type} value={type}>
                      {humanizeEnum(type)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1">
              <Label htmlFor="proposedValue">Proposed value (optional)</Label>
              {/* Keyed by flashKey so pulling in the AI recommendation remounts this field and replays the
                  one-shot .field-flash pulse; flashKey 0 (initial paint) stays quiet. */}
              <Input
                key={flashKey}
                ref={proposedValueRef}
                id="proposedValue"
                inputMode="decimal"
                placeholder="e.g. 25900"
                // scroll-margin gives scrollIntoView a buffer so the field lands clear of the sheet's
                // bottom edge (and reveals the note + submit below it) instead of flush against it.
                className={cn("scroll-mb-24", flashKey > 0 && "field-flash")}
                value={draft.proposedValue}
                onChange={(e) => onDraftChange({ ...draft, proposedValue: e.target.value })}
              />
            </div>
          </div>
          <div className="space-y-1">
            <Label htmlFor="note">Note</Label>
            <Input
              id="note"
              placeholder="Why this action?"
              value={draft.note}
              onChange={(e) => onDraftChange({ ...draft, note: e.target.value })}
            />
          </div>
          {error ? (
            <p className="text-sm text-destructive" role="alert">
              {error}
            </p>
          ) : null}
          <Button type="submit" size="sm" disabled={createAction.isPending}>
            {createAction.isPending ? (
              <Loader2 className="h-4 w-4 animate-spin" />
            ) : (
              <Plus className="h-4 w-4" />
            )}
            Log action
          </Button>
        </form>
        )}
      </CardContent>
    </Card>
  );
}

function ActionRow({
  vehicleId,
  action,
  readOnly = false,
}: {
  vehicleId: string;
  action: InventoryAction;
  readOnly?: boolean;
}) {
  const transition = useTransitionAction(vehicleId);
  const [error, setError] = useState<string | null>(null);
  const [outcome, setOutcome] = useState<ActionOutcome>("Sold");

  // On a closed (frozen) vehicle the history is read-only: never surface an advance control, even mid-lifecycle.
  const target = readOnly ? null : nextStatus(action.status);
  const needsOutcome = target !== null && requiresOutcome(target);

  async function advance() {
    if (!target) return;
    setError(null);
    try {
      await transition.mutateAsync({
        actionId: action.id,
        body: { status: target, outcome: needsOutcome ? outcome : null },
      });
    } catch (err) {
      // The server is the gate of record: an invalid transition returns 409 ProblemDetails, surfaced here.
      setError(err instanceof ApiError ? err.message : "Couldn't update the action.");
    }
  }

  return (
    <div className="rounded-md border p-3">
      <div className="flex items-start justify-between gap-2">
        <div className="space-y-0.5">
          <div className="flex items-center gap-2">
            <span className="text-sm font-medium">{humanizeEnum(action.type)}</span>
            <Badge
              variant="outline"
              className={cn("text-[11px]", ACTION_STATUS_STYLES[action.status])}
            >
              {humanizeEnum(action.status)}
            </Badge>
            {action.outcome ? (
              <Badge variant="outline" className="text-[11px] text-muted-foreground">
                {action.outcome === "Sold" ? "Sold" : "Not sold"}
              </Badge>
            ) : null}
          </div>
          <p className="text-xs text-muted-foreground">{action.note}</p>
          <p className="text-[11px] text-muted-foreground">
            {action.proposedValue !== null
              ? `${formatCurrency(action.proposedValue)} · `
              : ""}
            {formatDateTime(action.createdAt)}
          </p>
        </div>

        {target ? (
          <div className="flex shrink-0 flex-col items-end gap-1.5">
            {needsOutcome ? (
              <Select value={outcome} onValueChange={(v) => setOutcome(v as ActionOutcome)}>
                <SelectTrigger className="h-7 w-28 text-xs">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Sold">Sold</SelectItem>
                  <SelectItem value="NotSold">Not sold</SelectItem>
                </SelectContent>
              </Select>
            ) : null}
            <Button size="sm" variant="outline" disabled={transition.isPending} onClick={advance}>
              {transition.isPending ? (
                <Loader2 className="h-3.5 w-3.5 animate-spin" />
              ) : null}
              {advanceLabel(target)}
            </Button>
          </div>
        ) : action.status === "Resolved" ? (
          <span className="flex items-center gap-1 text-xs text-tier-fresh">
            <CheckCircle2 className="h-3.5 w-3.5" />
            Resolved
          </span>
        ) : (
          // Read-only (frozen) vehicle with an unresolved action: show its status as an archived state, no advance.
          <span className="text-xs text-muted-foreground">{humanizeEnum(action.status)}</span>
        )}
      </div>
      {error ? (
        <p className="mt-2 text-xs text-destructive" role="alert">
          {error}
        </p>
      ) : null}
    </div>
  );
}
