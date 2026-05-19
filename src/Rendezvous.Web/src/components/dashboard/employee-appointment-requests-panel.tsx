"use client"

import { useEffect, useState } from "react"
import type React from "react"
import { Check, Clock, UserX, X } from "lucide-react"

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
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import {
  approveEmployeeAppointmentRequest,
  cancelEmployeeAppointment,
  completeEmployeeAppointment,
  getEmployeeAppointments,
  getEmployeeAppointmentRequests,
  markEmployeeAppointmentNoShow,
  rejectEmployeeAppointmentRequest,
} from "@/lib/auth-api"
import type {
  AppointmentFilters,
  EmployeeAppointment,
  EmployeeAppointmentRequest,
} from "@/lib/auth-api"

export function EmployeeAppointmentRequestsPanel() {
  const [requests, setRequests] = useState<EmployeeAppointmentRequest[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [actingRequestId, setActingRequestId] = useState("")
  const [message, setMessage] = useState("")
  const [error, setError] = useState("")

  useEffect(() => {
    let isMounted = true

    async function loadInitialRequests() {
      setIsLoading(true)
      setError("")

      try {
        const nextRequests = await getEmployeeAppointmentRequests()

        if (isMounted) {
          setRequests(nextRequests)
        }
      } catch {
        if (isMounted) {
          setError("Appointment requests could not be loaded.")
        }
      } finally {
        if (isMounted) {
          setIsLoading(false)
        }
      }
    }

    loadInitialRequests()

    return () => {
      isMounted = false
    }
  }, [])

  async function refreshRequests() {
    setIsLoading(true)
    setError("")

    try {
      setRequests(await getEmployeeAppointmentRequests())
    } catch {
      setError("Appointment requests could not be loaded.")
    } finally {
      setIsLoading(false)
    }
  }

  async function handleApprove(requestId: string) {
    setActingRequestId(requestId)
    setMessage("")
    setError("")

    try {
      const decision = await approveEmployeeAppointmentRequest(requestId)
      setMessage(
        decision.autoRejectedCount > 0
          ? `Request approved. ${decision.autoRejectedCount} overlapping pending request was rejected.`
          : "Request approved."
      )
      await refreshRequests()
    } catch {
      setError("Appointment request could not be approved.")
    } finally {
      setActingRequestId("")
    }
  }

  async function handleReject(requestId: string) {
    setActingRequestId(requestId)
    setMessage("")
    setError("")

    try {
      await rejectEmployeeAppointmentRequest(requestId)
      setMessage("Request rejected.")
      await refreshRequests()
    } catch {
      setError("Appointment request could not be rejected.")
    } finally {
      setActingRequestId("")
    }
  }

  return (
    <Card>
      <CardHeader>
        <div className="flex items-center gap-2">
          <Clock className="size-4 text-primary" aria-hidden="true" />
          <CardTitle>Requests</CardTitle>
        </div>
        <CardDescription>
          Approve or reject requests assigned to your staff profile.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <PanelMessages message={message} error={error} />
        {isLoading ? (
          <p className="text-sm leading-6 text-muted-foreground">
            Loading requests.
          </p>
        ) : requests.length === 0 ? (
          <p className="text-sm leading-6 text-muted-foreground">
            No pending appointment requests.
          </p>
        ) : (
          <div className="grid gap-3">
            {requests.map((request) => (
              <EmployeeAppointmentRow
                key={request.id}
                item={request}
                primaryLabel="Approve"
                secondaryLabel="Reject"
                actingId={actingRequestId}
                onPrimary={() => handleApprove(request.id)}
                onSecondary={() => handleReject(request.id)}
              />
            ))}
          </div>
        )}
      </CardContent>
    </Card>
  )
}

export function EmployeeApprovedAppointmentsPanel() {
  const [appointments, setAppointments] = useState<EmployeeAppointment[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [actingAppointmentId, setActingAppointmentId] = useState("")
  const [statusFilter, setStatusFilter] = useState("all")
  const [fromDate, setFromDate] = useState("")
  const [toDate, setToDate] = useState("")
  const [message, setMessage] = useState("")
  const [error, setError] = useState("")

  useEffect(() => {
    let isMounted = true

    async function loadAppointments() {
      setIsLoading(true)
      setError("")

      try {
        const nextAppointments = await getEmployeeAppointments()

        if (isMounted) {
          setAppointments(nextAppointments)
        }
      } catch {
        if (isMounted) {
          setError("Appointments could not be loaded.")
        }
      } finally {
        if (isMounted) {
          setIsLoading(false)
        }
      }
    }

    loadAppointments()

    return () => {
      isMounted = false
    }
  }, [])

  function getFilters(): AppointmentFilters | undefined {
    return buildAppointmentFilters(statusFilter, fromDate, toDate)
  }

  async function refreshAppointments(filters = getFilters()) {
    setIsLoading(true)
    setError("")

    try {
      setAppointments(await getEmployeeAppointments(filters))
    } catch {
      setError("Appointments could not be loaded.")
    } finally {
      setIsLoading(false)
    }
  }

  async function applyFilters() {
    setMessage("")
    await refreshAppointments()
  }

  async function clearFilters() {
    setStatusFilter("all")
    setFromDate("")
    setToDate("")
    setMessage("")
    await refreshAppointments(undefined)
  }

  async function handleCancelAppointment(appointmentId: string) {
    setActingAppointmentId(`cancel:${appointmentId}`)
    setMessage("")
    setError("")

    try {
      await cancelEmployeeAppointment(appointmentId)
      setMessage("Appointment cancelled.")
      await refreshAppointments()
    } catch {
      setError("Appointment could not be cancelled.")
    } finally {
      setActingAppointmentId("")
    }
  }

  async function handleCompleteAppointment(appointmentId: string) {
    setActingAppointmentId(`complete:${appointmentId}`)
    setMessage("")
    setError("")

    try {
      await completeEmployeeAppointment(appointmentId)
      setMessage("Appointment completed.")
      await refreshAppointments()
    } catch {
      setError("Appointment could not be completed.")
    } finally {
      setActingAppointmentId("")
    }
  }

  async function handleNoShowAppointment(appointmentId: string) {
    setActingAppointmentId(`no-show:${appointmentId}`)
    setMessage("")
    setError("")

    try {
      await markEmployeeAppointmentNoShow(appointmentId)
      setMessage("Appointment marked no-show.")
      await refreshAppointments()
    } catch {
      setError("Appointment could not be marked no-show.")
    } finally {
      setActingAppointmentId("")
    }
  }

  return (
    <Card>
      <CardHeader>
        <div className="flex items-center gap-2">
          <Clock className="size-4 text-primary" aria-hidden="true" />
          <CardTitle>Appointments</CardTitle>
        </div>
        <CardDescription>
          Review upcoming and historical appointments assigned to you.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <PanelMessages message={message} error={error} />
        <div className="grid gap-3 rounded-lg border border-border bg-background p-3 md:grid-cols-[180px_1fr_1fr_auto] md:items-end">
          <Field label="Status" id="employee-appointment-status">
            <Select value={statusFilter} onValueChange={setStatusFilter}>
              <SelectTrigger id="employee-appointment-status">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {appointmentStatusOptions.map((option) => (
                  <SelectItem key={option.value} value={option.value}>
                    {option.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </Field>
          <Field label="From" id="employee-appointment-from">
            <Input
              id="employee-appointment-from"
              type="date"
              value={fromDate}
              onChange={(event) => setFromDate(event.target.value)}
            />
          </Field>
          <Field label="To" id="employee-appointment-to">
            <Input
              id="employee-appointment-to"
              type="date"
              value={toDate}
              onChange={(event) => setToDate(event.target.value)}
            />
          </Field>
          <div className="flex flex-wrap gap-2">
            <Button type="button" size="sm" onClick={applyFilters}>
              Apply
            </Button>
            <Button
              type="button"
              size="sm"
              variant="outline"
              onClick={clearFilters}
            >
              Clear
            </Button>
          </div>
        </div>
        {isLoading ? (
          <p className="text-sm leading-6 text-muted-foreground">
            Loading appointments.
          </p>
        ) : appointments.length === 0 ? (
          <p className="text-sm leading-6 text-muted-foreground">
            No appointments found.
          </p>
        ) : (
          <div className="grid gap-3">
            {appointments.map((appointment) => (
              <EmployeeManagedAppointmentRow
                key={appointment.id}
                item={appointment}
                actingId={actingAppointmentId}
                onCancel={() => handleCancelAppointment(appointment.id)}
                onComplete={() => handleCompleteAppointment(appointment.id)}
                onNoShow={() => handleNoShowAppointment(appointment.id)}
              />
            ))}
          </div>
        )}
      </CardContent>
    </Card>
  )
}

const appointmentStatusOptions = [
  { value: "all", label: "All statuses" },
  { value: "Pending", label: "Pending" },
  { value: "Approved", label: "Approved" },
  { value: "Rejected", label: "Rejected" },
  { value: "Cancelled", label: "Cancelled" },
  { value: "Completed", label: "Completed" },
  { value: "NoShow", label: "No show" },
  { value: "Expired", label: "Expired" },
]

function EmployeeManagedAppointmentRow({
  item,
  actingId,
  onCancel,
  onComplete,
  onNoShow,
}: {
  item: EmployeeAppointment
  actingId: string
  onCancel: () => void
  onComplete: () => void
  onNoShow: () => void
}) {
  return (
    <div className="rounded-lg border border-border bg-background p-3">
      <div className="grid gap-3 lg:grid-cols-[minmax(0,1fr)_auto] lg:items-center">
        <div className="min-w-0 space-y-2">
          <div className="flex flex-wrap items-center gap-2">
            <p className="font-medium text-foreground">{item.serviceName}</p>
            <Badge variant="outline">{item.status}</Badge>
          </div>
          <div className="grid gap-1 text-sm text-muted-foreground sm:grid-cols-2">
            <p>{formatAppointmentTime(item.startsAtUtc)}</p>
            <p>Business: {item.businessName}</p>
            <p>Customer: {item.customerFullName || "Name not set"}</p>
            <p>
              Price: {item.priceAmount} {item.currencyCode}
            </p>
          </div>
        </div>
        <AppointmentActionButtons
          appointment={item}
          actingId={actingId}
          onCancel={onCancel}
          onComplete={onComplete}
          onNoShow={onNoShow}
        />
      </div>
    </div>
  )
}

function AppointmentActionButtons({
  appointment,
  actingId,
  onCancel,
  onComplete,
  onNoShow,
}: {
  appointment: EmployeeAppointment
  actingId: string
  onCancel: () => void
  onComplete: () => void
  onNoShow: () => void
}) {
  const isActing = actingId.endsWith(appointment.id)
  const canCancel = canCancelBusinessAppointment(appointment)
  const canComplete = canCompleteAppointment(appointment)
  const canNoShow = canMarkAppointmentNoShow(appointment)

  if (!canCancel && !canComplete && !canNoShow) {
    return null
  }

  return (
    <div className="flex flex-wrap gap-2">
      {canComplete ? (
        <Button
          type="button"
          size="sm"
          disabled={isActing}
          onClick={onComplete}
        >
          <Check data-icon="inline-start" className="size-4" />
          {actingId === `complete:${appointment.id}` ? "Completing" : "Complete"}
        </Button>
      ) : null}
      {canNoShow ? (
        <Button
          type="button"
          size="sm"
          variant="outline"
          disabled={isActing}
          onClick={onNoShow}
        >
          <UserX data-icon="inline-start" className="size-4" />
          {actingId === `no-show:${appointment.id}` ? "Saving" : "No show"}
        </Button>
      ) : null}
      {canCancel ? (
        <Button
          type="button"
          size="sm"
          variant="outline"
          disabled={isActing}
          onClick={onCancel}
        >
          <X data-icon="inline-start" className="size-4" />
          {actingId === `cancel:${appointment.id}` ? "Cancelling" : "Cancel"}
        </Button>
      ) : null}
    </div>
  )
}

function buildAppointmentFilters(
  status: string,
  fromDate: string,
  toDate: string
): AppointmentFilters | undefined {
  const filters: AppointmentFilters = {}

  if (status !== "all") {
    filters.status = status
  }

  if (fromDate) {
    filters.fromUtc = new Date(`${fromDate}T00:00:00`).toISOString()
  }

  if (toDate) {
    filters.toUtc = new Date(`${toDate}T23:59:59.999`).toISOString()
  }

  return Object.keys(filters).length > 0 ? filters : undefined
}

function canCancelBusinessAppointment(appointment: EmployeeAppointment) {
  if (appointment.status !== "Approved") {
    return false
  }

  return new Date(appointment.startsAtUtc).getTime() - Date.now() >= 60 * 60 * 1000
}

function canCompleteAppointment(appointment: EmployeeAppointment) {
  return appointment.status === "Approved"
    && new Date(appointment.startsAtUtc).getTime() <= Date.now()
}

function canMarkAppointmentNoShow(appointment: EmployeeAppointment) {
  const now = Date.now()

  if (appointment.status === "Approved") {
    return new Date(appointment.startsAtUtc).getTime() <= now
  }

  if (appointment.status === "Completed") {
    return new Date(appointment.endsAtUtc).getTime() + 24 * 60 * 60 * 1000 >= now
  }

  return false
}

function EmployeeAppointmentRow({
  item,
  primaryLabel,
  secondaryLabel,
  actingId,
  onPrimary,
  onSecondary,
}: {
  item: EmployeeAppointment | EmployeeAppointmentRequest
  primaryLabel: string
  secondaryLabel?: string
  actingId: string
  onPrimary: () => void
  onSecondary?: () => void
}) {
  return (
    <div className="rounded-lg border border-border bg-background p-3">
      <div className="grid gap-3 lg:grid-cols-[minmax(0,1fr)_auto] lg:items-center">
        <div className="min-w-0 space-y-2">
          <div className="flex flex-wrap items-center gap-2">
            <p className="font-medium text-foreground">{item.serviceName}</p>
            <Badge variant="outline">{item.status}</Badge>
          </div>
          <div className="grid gap-1 text-sm text-muted-foreground sm:grid-cols-2">
            <p>{formatAppointmentTime(item.startsAtUtc)}</p>
            <p>Business: {item.businessName}</p>
            <p>Customer: {item.customerFullName || "Name not set"}</p>
            <p>
              Price: {item.priceAmount} {item.currencyCode}
            </p>
          </div>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button
            type="button"
            size="sm"
            disabled={actingId === item.id}
            onClick={onPrimary}
          >
            {primaryLabel === "Cancel" ? (
              <X data-icon="inline-start" className="size-4" />
            ) : (
              <Check data-icon="inline-start" className="size-4" />
            )}
            {actingId === item.id ? "Working" : primaryLabel}
          </Button>
          {secondaryLabel && onSecondary ? (
            <Button
              type="button"
              size="sm"
              variant="outline"
              disabled={actingId === item.id}
              onClick={onSecondary}
            >
              <X data-icon="inline-start" className="size-4" />
              {secondaryLabel}
            </Button>
          ) : null}
        </div>
      </div>
    </div>
  )
}

function Field({
  label,
  id,
  children,
}: {
  label: string
  id: string
  children: React.ReactNode
}) {
  return (
    <div className="grid gap-2">
      <Label htmlFor={id}>{label}</Label>
      {children}
    </div>
  )
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

function formatAppointmentTime(value: string) {
  return new Intl.DateTimeFormat("en", {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(new Date(value))
}
