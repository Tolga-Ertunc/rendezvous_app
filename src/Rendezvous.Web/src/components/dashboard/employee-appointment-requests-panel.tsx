"use client"

import { useEffect, useState } from "react"
import { Check, Clock, X } from "lucide-react"

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
  approveEmployeeAppointmentRequest,
  cancelEmployeeAppointment,
  getEmployeeAppointments,
  getEmployeeAppointmentRequests,
  rejectEmployeeAppointmentRequest,
} from "@/lib/auth-api"
import type {
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

  async function refreshAppointments() {
    setIsLoading(true)
    setError("")

    try {
      setAppointments(await getEmployeeAppointments())
    } catch {
      setError("Appointments could not be loaded.")
    } finally {
      setIsLoading(false)
    }
  }

  async function handleCancelAppointment(appointmentId: string) {
    setActingAppointmentId(appointmentId)
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

  return (
    <Card>
      <CardHeader>
        <div className="flex items-center gap-2">
          <Clock className="size-4 text-primary" aria-hidden="true" />
          <CardTitle>Appointments</CardTitle>
        </div>
        <CardDescription>
          Upcoming approved appointments can be cancelled until one hour before
          start.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <PanelMessages message={message} error={error} />
        {isLoading ? (
          <p className="text-sm leading-6 text-muted-foreground">
            Loading appointments.
          </p>
        ) : appointments.length === 0 ? (
          <p className="text-sm leading-6 text-muted-foreground">
            No approved upcoming appointments.
          </p>
        ) : (
          <div className="grid gap-3">
            {appointments.map((appointment) => (
              <EmployeeAppointmentRow
                key={appointment.id}
                item={appointment}
                primaryLabel="Cancel"
                actingId={actingAppointmentId}
                onPrimary={() => handleCancelAppointment(appointment.id)}
              />
            ))}
          </div>
        )}
      </CardContent>
    </Card>
  )
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
