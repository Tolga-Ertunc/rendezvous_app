"use client"

import { useEffect, useMemo, useState } from "react"
import { CalendarOff, Save, Trash2 } from "lucide-react"

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { ApiError } from "@/lib/api-client"
import {
  createEmployeeAvailabilityException,
  createOwnerAvailabilityException,
  deleteEmployeeAvailabilityException,
  deleteOwnerAvailabilityException,
  getEmployeeAvailabilityExceptions,
  getOwnerAvailabilityExceptions,
  updateEmployeeAvailabilityException,
  updateOwnerAvailabilityException,
} from "@/lib/auth-api"
import type {
  AvailabilityException,
  AvailabilityExceptionConflict,
  AvailabilityExceptionRequest,
  BusinessDetail,
  BusinessMembership,
} from "@/lib/auth-api"

type ExceptionDraft = {
  businessId: string
  staffMemberId: string
  type: AvailabilityExceptionRequest["type"]
  date: string
  isFullDay: boolean
  startsAt: string
  endsAt: string
  note: string
}

type PendingAction = {
  request: AvailabilityExceptionRequest
  exceptionId?: string
  conflict: AvailabilityExceptionConflict
}

const defaultDraft: ExceptionDraft = {
  businessId: "",
  staffMemberId: "",
  type: "BusinessClosed",
  date: new Date().toISOString().slice(0, 10),
  isFullDay: true,
  startsAt: "09:00",
  endsAt: "17:00",
  note: "",
}

export function OwnerAvailabilityExceptionsPanel({
  business,
}: {
  business: BusinessDetail
}) {
  return (
    <AvailabilityExceptionsPanel
      mode="owner"
      title="Scheduling exceptions"
      description="Close a business day, mark a holiday, or add leave for any staff member."
      business={business}
    />
  )
}

export function EmployeeAvailabilityExceptionsPanel({
  memberships,
}: {
  memberships: BusinessMembership[]
}) {
  return (
    <AvailabilityExceptionsPanel
      mode="employee"
      title="My leave"
      description="Create and manage leave records for your active employee profile."
      employeeMemberships={memberships}
    />
  )
}

function AvailabilityExceptionsPanel({
  mode,
  title,
  description,
  business,
  employeeMemberships = [],
}: {
  mode: "owner" | "employee"
  title: string
  description: string
  business?: BusinessDetail
  employeeMemberships?: BusinessMembership[]
}) {
  const employeeBusinesses = useMemo(
    () =>
      employeeMemberships.filter(
        (membership) =>
          membership.role === "Employee" && membership.status === "Active"
      ),
    [employeeMemberships]
  )
  const initialBusinessId = business?.id ?? employeeBusinesses[0]?.businessId ?? ""
  const [exceptions, setExceptions] = useState<AvailabilityException[]>([])
  const [draft, setDraft] = useState<ExceptionDraft>({
    ...defaultDraft,
    businessId: initialBusinessId,
    type: mode === "employee" ? "StaffLeave" : "BusinessClosed",
  })
  const [editingId, setEditingId] = useState("")
  const [actingId, setActingId] = useState("")
  const [pendingAction, setPendingAction] = useState<PendingAction | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [message, setMessage] = useState("")
  const [error, setError] = useState("")

  useEffect(() => {
    let isMounted = true

    async function loadExceptions() {
      setIsLoading(true)
      setError("")

      try {
        const nextExceptions =
          mode === "owner" && business
            ? await getOwnerAvailabilityExceptions(business.id)
            : await getEmployeeAvailabilityExceptions()

        if (isMounted) {
          setExceptions(nextExceptions)
        }
      } catch {
        if (isMounted) {
          setError("Scheduling exceptions could not be loaded.")
        }
      } finally {
        if (isMounted) {
          setIsLoading(false)
        }
      }
    }

    loadExceptions()

    return () => {
      isMounted = false
    }
  }, [business, mode])

  async function refreshExceptions() {
    const nextExceptions =
      mode === "owner" && business
        ? await getOwnerAvailabilityExceptions(business.id)
        : await getEmployeeAvailabilityExceptions()
    setExceptions(nextExceptions)
  }

  async function submitDraft(cancelConflictingAppointments: boolean) {
    setMessage("")
    setError("")

    const request = toRequest(draft, cancelConflictingAppointments, initialBusinessId)
    if (!request.date) {
      setError("Date is required.")
      return
    }

    if (mode === "owner") {
      if (!business) {
        return
      }
      if (request.type === "StaffLeave" && !request.staffMemberId) {
        setError("Select a staff member for staff leave.")
        return
      }
    }

    if (mode === "employee" && !request.businessId) {
      setError("Select a business.")
      return
    }

    setActingId(editingId || "new")

    try {
      if (mode === "owner" && business) {
        if (editingId) {
          await updateOwnerAvailabilityException(business.id, editingId, request)
        } else {
          await createOwnerAvailabilityException(business.id, request)
        }
      } else if (editingId) {
        await updateEmployeeAvailabilityException(editingId, request)
      } else {
        await createEmployeeAvailabilityException(request)
      }

      setMessage(editingId ? "Exception updated." : "Exception created.")
      resetDraft()
      await refreshExceptions()
    } catch (caughtError) {
      const conflict = getConflictBody(caughtError)
      if (conflict) {
        setPendingAction({
          request,
          exceptionId: editingId || undefined,
          conflict,
        })
      } else {
        setError("Scheduling exception could not be saved.")
      }
    } finally {
      setActingId("")
    }
  }

  async function proceedWithConflicts() {
    if (!pendingAction) {
      return
    }

    setActingId(pendingAction.exceptionId ?? "new")
    setError("")
    setMessage("")

    try {
      const request = {
        ...pendingAction.request,
        cancelConflictingAppointments: true,
      }

      if (mode === "owner" && business) {
        if (pendingAction.exceptionId) {
          await updateOwnerAvailabilityException(
            business.id,
            pendingAction.exceptionId,
            request
          )
        } else {
          await createOwnerAvailabilityException(business.id, request)
        }
      } else if (pendingAction.exceptionId) {
        await updateEmployeeAvailabilityException(pendingAction.exceptionId, request)
      } else {
        await createEmployeeAvailabilityException(request)
      }

      setPendingAction(null)
      resetDraft()
      setMessage("Exception saved and affected active reservations cancelled.")
      await refreshExceptions()
    } catch {
      setError("Scheduling exception could not be saved.")
    } finally {
      setActingId("")
    }
  }

  async function handleDelete(exceptionId: string) {
    setActingId(exceptionId)
    setMessage("")
    setError("")

    try {
      if (mode === "owner" && business) {
        await deleteOwnerAvailabilityException(business.id, exceptionId)
      } else {
        await deleteEmployeeAvailabilityException(exceptionId)
      }

      setMessage("Exception deleted.")
      if (editingId === exceptionId) {
        resetDraft()
      }
      await refreshExceptions()
    } catch {
      setError("Scheduling exception could not be deleted.")
    } finally {
      setActingId("")
    }
  }

  function startEdit(exception: AvailabilityException) {
    setEditingId(exception.id)
    setDraft({
      businessId: exception.businessId,
      staffMemberId: exception.staffMemberId ?? "",
      type: exception.type,
      date: exception.date,
      isFullDay: exception.isFullDay,
      startsAt: exception.startsAt ?? "09:00",
      endsAt: exception.endsAt ?? "17:00",
      note: exception.note ?? "",
    })
    setMessage("")
    setError("")
  }

  function resetDraft() {
    setEditingId("")
    setDraft({
      ...defaultDraft,
      businessId: initialBusinessId,
      type: mode === "employee" ? "StaffLeave" : "BusinessClosed",
    })
  }

  return (
    <Card>
      <CardHeader>
        <div className="flex items-center gap-2">
          <CalendarOff className="size-4 text-primary" aria-hidden="true" />
          <CardTitle>{title}</CardTitle>
        </div>
        <CardDescription>{description}</CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <PanelMessages message={message} error={error} />

        <div className="grid gap-3 rounded-lg border border-border bg-background p-3">
          <div className="grid gap-3 md:grid-cols-3">
            {mode === "employee" ? (
              <Field label="Business">
                <select
                  className="h-9 w-full rounded-lg border border-input bg-background px-3 text-sm outline-none focus-visible:ring-3 focus-visible:ring-ring/35"
                  value={draft.businessId}
                  onChange={(event) =>
                    setDraft({ ...draft, businessId: event.target.value })
                  }
                >
                  {employeeBusinesses.map((membership) => (
                    <option
                      key={membership.businessId}
                      value={membership.businessId}
                    >
                      {membership.businessName}
                    </option>
                  ))}
                </select>
              </Field>
            ) : (
              <Field label="Type">
                <select
                  className="h-9 w-full rounded-lg border border-input bg-background px-3 text-sm outline-none focus-visible:ring-3 focus-visible:ring-ring/35"
                  value={draft.type}
                  onChange={(event) =>
                    setDraft({
                      ...draft,
                      type: event.target.value as ExceptionDraft["type"],
                      staffMemberId:
                        event.target.value === "StaffLeave"
                          ? draft.staffMemberId
                          : "",
                    })
                  }
                >
                  <option value="BusinessClosed">Business closed</option>
                  <option value="Holiday">Holiday</option>
                  <option value="StaffLeave">Staff leave</option>
                </select>
              </Field>
            )}

            {mode === "owner" && draft.type === "StaffLeave" ? (
              <Field label="Staff member">
                <select
                  className="h-9 w-full rounded-lg border border-input bg-background px-3 text-sm outline-none focus-visible:ring-3 focus-visible:ring-ring/35"
                  value={draft.staffMemberId}
                  onChange={(event) =>
                    setDraft({ ...draft, staffMemberId: event.target.value })
                  }
                >
                  <option value="">Select staff</option>
                  {business?.staffMembers.map((staff) => (
                    <option key={staff.id} value={staff.id}>
                      {staff.displayName}
                    </option>
                  ))}
                </select>
              </Field>
            ) : null}

            <Field label="Date">
              <Input
                type="date"
                value={draft.date}
                onChange={(event) =>
                  setDraft({ ...draft, date: event.target.value })
                }
              />
            </Field>

            <label className="flex items-center gap-2 self-end text-sm text-muted-foreground">
              <input
                type="checkbox"
                checked={draft.isFullDay}
                onChange={(event) =>
                  setDraft({ ...draft, isFullDay: event.target.checked })
                }
              />
              Full day
            </label>
          </div>

          {!draft.isFullDay ? (
            <div className="grid gap-3 md:grid-cols-2">
              <Field label="Starts at">
                <Input
                  type="time"
                  value={draft.startsAt}
                  onChange={(event) =>
                    setDraft({ ...draft, startsAt: event.target.value })
                  }
                />
              </Field>
              <Field label="Ends at">
                <Input
                  type="time"
                  value={draft.endsAt}
                  onChange={(event) =>
                    setDraft({ ...draft, endsAt: event.target.value })
                  }
                />
              </Field>
            </div>
          ) : null}

          <Field label="Note">
            <Input
              value={draft.note}
              maxLength={500}
              placeholder="Optional"
              onChange={(event) =>
                setDraft({ ...draft, note: event.target.value })
              }
            />
          </Field>

          <div className="flex flex-wrap gap-2">
            <Button
              type="button"
              disabled={actingId === (editingId || "new")}
              onClick={() => submitDraft(false)}
            >
              <Save data-icon="inline-start" className="size-4" />
              {editingId ? "Save exception" : "Add exception"}
            </Button>
            {editingId ? (
              <Button type="button" variant="outline" onClick={resetDraft}>
                Cancel edit
              </Button>
            ) : null}
          </div>
        </div>

        {isLoading ? (
          <p className="text-sm leading-6 text-muted-foreground">
            Loading scheduling exceptions.
          </p>
        ) : exceptions.length === 0 ? (
          <p className="text-sm leading-6 text-muted-foreground">
            No scheduling exceptions are recorded.
          </p>
        ) : (
          <div className="grid gap-3">
            {exceptions.map((exception) => (
              <ExceptionRow
                key={exception.id}
                exception={exception}
                businessName={getBusinessName(exception.businessId, employeeBusinesses)}
                actingId={actingId}
                onEdit={() => startEdit(exception)}
                onDelete={() => handleDelete(exception.id)}
              />
            ))}
          </div>
        )}

        <Dialog
          open={pendingAction !== null}
          onOpenChange={(open) => {
            if (!open) {
              setPendingAction(null)
            }
          }}
        >
          <DialogContent>
            <DialogHeader>
              <DialogTitle>Active reservations overlap</DialogTitle>
              <DialogDescription>
                The selected closure/leave overlaps active appointments. If you
                proceed, all affected active reservations in this range will be
                cancelled.
              </DialogDescription>
            </DialogHeader>
            {pendingAction ? (
              <div className="rounded-lg border border-border bg-muted/30 p-3 text-sm text-muted-foreground">
                {pendingAction.conflict.appointmentCount} active reservation
                {pendingAction.conflict.appointmentCount === 1 ? "" : "s"} will
                be cancelled.
              </div>
            ) : null}
            <DialogFooter>
              <Button
                type="button"
                variant="outline"
                onClick={() => setPendingAction(null)}
              >
                Cancel
              </Button>
              <Button
                type="button"
                variant="destructive"
                disabled={actingId !== ""}
                onClick={proceedWithConflicts}
              >
                Proceed
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      </CardContent>
    </Card>
  )
}

function ExceptionRow({
  exception,
  businessName,
  actingId,
  onEdit,
  onDelete,
}: {
  exception: AvailabilityException
  businessName: string
  actingId: string
  onEdit: () => void
  onDelete: () => void
}) {
  return (
    <div className="grid gap-3 rounded-lg border border-border bg-background p-3 lg:grid-cols-[minmax(0,1fr)_auto] lg:items-center">
      <div className="min-w-0 space-y-2">
        <div className="flex flex-wrap items-center gap-2">
          <p className="font-medium text-foreground">
            {formatExceptionType(exception.type)}
          </p>
          <Badge variant="outline">{exception.date}</Badge>
          {exception.staffDisplayName ? (
            <Badge variant="secondary">{exception.staffDisplayName}</Badge>
          ) : null}
        </div>
        <div className="grid gap-1 text-sm text-muted-foreground sm:grid-cols-2">
          {businessName ? <p>Business: {businessName}</p> : null}
          <p>
            Time:{" "}
            {exception.isFullDay
              ? "Full day"
              : `${exception.startsAt} - ${exception.endsAt}`}
          </p>
          {exception.note ? <p className="sm:col-span-2">{exception.note}</p> : null}
        </div>
      </div>
      <div className="flex flex-wrap gap-2">
        <Button type="button" size="sm" variant="outline" onClick={onEdit}>
          Edit
        </Button>
        <Button
          type="button"
          size="sm"
          variant="outline"
          disabled={actingId === exception.id}
          onClick={onDelete}
        >
          <Trash2 data-icon="inline-start" className="size-4" />
          Delete
        </Button>
      </div>
    </div>
  )
}

function toRequest(
  draft: ExceptionDraft,
  cancelConflictingAppointments: boolean,
  fallbackBusinessId: string
): AvailabilityExceptionRequest {
  return {
    businessId: draft.businessId || fallbackBusinessId || undefined,
    staffMemberId:
      draft.type === "StaffLeave" && draft.staffMemberId
        ? draft.staffMemberId
        : null,
    type: draft.type,
    date: draft.date,
    isFullDay: draft.isFullDay,
    startsAt: draft.isFullDay ? null : draft.startsAt,
    endsAt: draft.isFullDay ? null : draft.endsAt,
    note: draft.note.trim() || null,
    cancelConflictingAppointments,
  }
}

function getConflictBody(error: unknown) {
  if (!(error instanceof ApiError) || error.status !== 409) {
    return null
  }

  if (isAvailabilityExceptionConflict(error.body)) {
    return error.body
  }

  return null
}

function isAvailabilityExceptionConflict(
  value: unknown
): value is AvailabilityExceptionConflict {
  return (
    typeof value === "object" &&
    value !== null &&
    "appointmentCount" in value &&
    typeof (value as { appointmentCount: unknown }).appointmentCount === "number"
  )
}

function getBusinessName(
  businessId: string,
  memberships: BusinessMembership[]
) {
  return memberships.find((membership) => membership.businessId === businessId)
    ?.businessName ?? ""
}

function formatExceptionType(type: AvailabilityException["type"]) {
  if (type === "BusinessClosed") {
    return "Business closed"
  }

  if (type === "StaffLeave") {
    return "Staff leave"
  }

  return "Holiday"
}

function PanelMessages({ message, error }: { message: string; error: string }) {
  return (
    <>
      {message ? (
        <Alert>
          <AlertTitle>Updated</AlertTitle>
          <AlertDescription>{message}</AlertDescription>
        </Alert>
      ) : null}
      {error ? (
        <Alert className="border-destructive/30 bg-destructive/5 text-destructive">
          <AlertTitle>Action failed</AlertTitle>
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      ) : null}
    </>
  )
}

function Field({
  label,
  children,
}: {
  label: string
  children: React.ReactNode
}) {
  return (
    <div className="space-y-2">
      <Label>{label}</Label>
      {children}
    </div>
  )
}
