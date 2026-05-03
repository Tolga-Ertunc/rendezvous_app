"use client"

import { useEffect, useState } from "react"
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
import {
  cancelCustomerAppointment,
  getCustomerAppointments,
} from "@/lib/auth-api"
import type { CustomerAppointment } from "@/lib/auth-api"

export function CustomerAppointmentsPanel() {
  const [appointments, setAppointments] = useState<CustomerAppointment[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [actingAppointmentId, setActingAppointmentId] = useState("")
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

  async function refreshAppointments() {
    setIsLoading(true)
    setError("")

    try {
      const nextAppointments = await getCustomerAppointments()
      setAppointments(nextAppointments)
    } catch {
      setError("Appointments could not be loaded.")
    } finally {
      setIsLoading(false)
    }
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
                  {canShowCancel(appointment.status) ? (
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

function canShowCancel(status: string) {
  return status === "Pending" || status === "Approved"
}

function formatAppointmentTime(value: string) {
  return new Intl.DateTimeFormat("en", {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(new Date(value))
}
