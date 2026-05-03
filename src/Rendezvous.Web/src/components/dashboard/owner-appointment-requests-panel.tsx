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
  approveOwnerAppointmentRequest,
  getOwnerAppointmentRequests,
  rejectOwnerAppointmentRequest,
} from "@/lib/auth-api"
import type { OwnerAppointmentRequest } from "@/lib/auth-api"

type OwnerAppointmentRequestsPanelProps = {
  businessId: string
}

export function OwnerAppointmentRequestsPanel({
  businessId,
}: OwnerAppointmentRequestsPanelProps) {
  const [requests, setRequests] = useState<OwnerAppointmentRequest[]>([])
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
        const nextRequests = await getOwnerAppointmentRequests(businessId)

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
  }, [businessId])

  async function refreshRequests() {
    setIsLoading(true)
    setError("")

    try {
      const nextRequests = await getOwnerAppointmentRequests(businessId)
      setRequests(nextRequests)
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
      const decision = await approveOwnerAppointmentRequest(
        businessId,
        requestId
      )
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
      await rejectOwnerAppointmentRequest(businessId, requestId)
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
          <CardTitle>Pending appointment requests</CardTitle>
        </div>
        <CardDescription>
          Approve one request to create a real appointment, or reject requests
          that should not continue.
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
            <AlertTitle>Request action failed</AlertTitle>
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        ) : null}

        {isLoading ? (
          <p className="text-sm leading-6 text-muted-foreground">
            Loading pending requests.
          </p>
        ) : requests.length === 0 ? (
          <p className="text-sm leading-6 text-muted-foreground">
            There are no pending appointment requests.
          </p>
        ) : (
          <div className="grid gap-3">
            {requests.map((request) => (
              <div
                key={request.id}
                className="rounded-lg border border-border bg-background p-3"
              >
                <div className="grid gap-3 lg:grid-cols-[minmax(0,1fr)_auto] lg:items-center">
                  <div className="min-w-0 space-y-2">
                    <div className="flex flex-wrap items-center gap-2">
                      <p className="font-medium text-foreground">
                        {request.serviceName}
                      </p>
                      <Badge variant="outline">{request.status}</Badge>
                    </div>
                    <div className="grid gap-1 text-sm text-muted-foreground sm:grid-cols-2">
                      <p>{formatAppointmentTime(request.startsAtUtc)}</p>
                      <p>Staff: {request.staffDisplayName}</p>
                      <p>Customer: {request.customerPublicNumber}</p>
                      <p>
                        Price: {request.priceAmount} {request.currencyCode}
                      </p>
                    </div>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    <Button
                      type="button"
                      size="sm"
                      disabled={actingRequestId === request.id}
                      onClick={() => handleApprove(request.id)}
                    >
                      <Check data-icon="inline-start" className="size-4" />
                      Approve
                    </Button>
                    <Button
                      type="button"
                      size="sm"
                      variant="outline"
                      disabled={actingRequestId === request.id}
                      onClick={() => handleReject(request.id)}
                    >
                      <X data-icon="inline-start" className="size-4" />
                      Reject
                    </Button>
                  </div>
                </div>
              </div>
            ))}
          </div>
        )}
      </CardContent>
    </Card>
  )
}

function formatAppointmentTime(value: string) {
  return new Intl.DateTimeFormat("en", {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(new Date(value))
}
