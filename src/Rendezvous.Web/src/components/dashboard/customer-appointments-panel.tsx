"use client"

import { useEffect, useState } from "react"
import type React from "react"
import { CalendarDays, X } from "lucide-react"

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
  cancelCustomerAppointment,
  getCustomerAppointments,
} from "@/lib/auth-api"
import type { AppointmentFilters, CustomerAppointment } from "@/lib/auth-api"

export function CustomerAppointmentsPanel() {
  const [appointments, setAppointments] = useState<CustomerAppointment[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [actingAppointmentId, setActingAppointmentId] = useState("")
  const [statusFilter, setStatusFilter] = useState("all")
  const [fromDate, setFromDate] = useState("")
  const [toDate, setToDate] = useState("")
  const [message, setMessage] = useState("")
  const [error, setError] = useState("")

  useEffect(() => {
    let isMounted = true

    async function loadInitialAppointments() {
      setIsLoading(true)
      setError("")

      try {
        const nextAppointments = await getCustomerAppointments()

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

    loadInitialAppointments()

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
      const nextAppointments = await getCustomerAppointments(filters)
      setAppointments(nextAppointments)
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

  async function handleCancel(appointmentId: string) {
    setActingAppointmentId(appointmentId)
    setMessage("")
    setError("")

    try {
      await cancelCustomerAppointment(appointmentId)
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
          <CalendarDays className="size-4 text-primary" aria-hidden="true" />
          <CardTitle>My appointments</CardTitle>
        </div>
        <CardDescription>
          Review your requests and cancel pending or cancellable approved
          appointments.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {message ? (
          <Alert>
            <AlertTitle>Updated</AlertTitle>
            <AlertDescription>{message}</AlertDescription>
          </Alert>
        ) : null}

        {error ? (
          <Alert className="border-destructive/30 bg-destructive/5 text-destructive">
            <AlertTitle>Appointment action failed</AlertTitle>
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        ) : null}

        <div className="grid gap-3 rounded-lg border border-border bg-background p-3 md:grid-cols-[180px_1fr_1fr_auto] md:items-end">
          <Field label="Status" id="customer-appointment-status">
            <Select value={statusFilter} onValueChange={setStatusFilter}>
              <SelectTrigger id="customer-appointment-status">
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
          <Field label="From" id="customer-appointment-from">
            <Input
              id="customer-appointment-from"
              type="date"
              value={fromDate}
              onChange={(event) => setFromDate(event.target.value)}
            />
          </Field>
          <Field label="To" id="customer-appointment-to">
            <Input
              id="customer-appointment-to"
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
            You do not have appointment requests yet.
          </p>
        ) : (
          <div className="grid gap-3">
            {appointments.map((appointment) => (
              <div
                key={appointment.id}
                className="rounded-lg border border-border bg-background p-3"
              >
                <div className="grid gap-3 lg:grid-cols-[minmax(0,1fr)_auto] lg:items-center">
                  <div className="min-w-0 space-y-2">
                    <div className="flex flex-wrap items-center gap-2">
                      <p className="font-medium text-foreground">
                        {appointment.businessName}
                      </p>
                      <Badge variant="outline">{appointment.status}</Badge>
                    </div>
                    <div className="grid gap-1 text-sm text-muted-foreground sm:grid-cols-2">
                      <p>{formatAppointmentTime(appointment.startsAtUtc)}</p>
                      <p>Service: {appointment.serviceName}</p>
                      <p>Staff: {appointment.staffDisplayName}</p>
                      <p>
                        Price: {appointment.priceAmount}{" "}
                        {appointment.currencyCode}
                      </p>
                    </div>
                  </div>
                  {canShowCancel(appointment) ? (
                    <Button
                      type="button"
                      size="sm"
                      variant="outline"
                      disabled={actingAppointmentId === appointment.id}
                      onClick={() => handleCancel(appointment.id)}
                    >
                      <X data-icon="inline-start" className="size-4" />
                      {actingAppointmentId === appointment.id
                        ? "Cancelling"
                        : "Cancel"}
                    </Button>
                  ) : null}
                </div>
              </div>
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

function canShowCancel(appointment: CustomerAppointment) {
  if (appointment.status === "Pending") {
    return true
  }

  if (appointment.status !== "Approved") {
    return false
  }

  return new Date(appointment.startsAtUtc).getTime() - Date.now() >= 60 * 60 * 1000
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

function formatAppointmentTime(value: string) {
  return new Intl.DateTimeFormat("en", {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(new Date(value))
}
