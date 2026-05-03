"use client"

import { useEffect, useMemo, useState, useSyncExternalStore } from "react"
import Link from "next/link"
import { CalendarDays, Clock, UsersRound } from "lucide-react"

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert"
import { Badge } from "@/components/ui/badge"
import { Button, buttonVariants } from "@/components/ui/button"
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { ApiError } from "@/lib/api-client"
import { clearAuthTokens, getAccessToken } from "@/lib/auth-storage"
import {
  createAppointmentRequest,
  getBookingAvailability,
} from "@/lib/booking-api"
import type { AvailabilitySlot, BookingAvailability } from "@/lib/booking-api"
import type { PublicBusinessService } from "@/lib/public-api"
import { cn } from "@/lib/utils"

type BookingAvailabilityPanelProps = {
  businessId: string
  services: PublicBusinessService[]
}

export function BookingAvailabilityPanel({
  businessId,
  services,
}: BookingAvailabilityPanelProps) {
  const [selectedServiceId, setSelectedServiceId] = useState(
    services[0]?.id ?? ""
  )
  const [date, setDate] = useState(getDefaultDateValue)
  const [availability, setAvailability] =
    useState<BookingAvailability | null>(null)
  const [isLoading, setIsLoading] = useState(false)
  const [requestingStaffKey, setRequestingStaffKey] = useState("")
  const [successMessage, setSuccessMessage] = useState("")
  const [error, setError] = useState("")
  const isSignedIn = useSyncExternalStore(
    subscribeToAuthStorage,
    getAuthStorageSnapshot,
    getServerAuthStorageSnapshot
  )

  const selectedService = useMemo(
    () => services.find((service) => service.id === selectedServiceId) ?? null,
    [selectedServiceId, services]
  )

  useEffect(() => {
    if (!isSignedIn || !selectedServiceId || !date) {
      return
    }

    let isMounted = true

    async function loadAvailability() {
      setIsLoading(true)
      setError("")

      try {
        setSuccessMessage("")
        const nextAvailability = await getBookingAvailability(
          businessId,
          selectedServiceId,
          date
        )

        if (isMounted) {
          setAvailability(nextAvailability)
        }
      } catch (caughtError) {
        if (!isMounted) {
          return
        }

        if (caughtError instanceof ApiError && caughtError.status === 401) {
          clearAuthTokens()
          setAvailability(null)
          setError("Sign in again to view available appointment times.")
        } else {
          setAvailability(null)
          setError("Available appointment times could not be loaded.")
        }
      } finally {
        if (isMounted) {
          setIsLoading(false)
        }
      }
    }

    loadAvailability()

    return () => {
      isMounted = false
    }
  }, [businessId, date, isSignedIn, selectedServiceId])

  async function handleRequest(slot: AvailabilitySlot, staffMemberId: string) {
    setRequestingStaffKey(`${slot.startsAtUtc}-${staffMemberId}`)
    setError("")
    setSuccessMessage("")

    try {
      const appointmentRequest = await createAppointmentRequest({
        businessId,
        serviceId: selectedServiceId,
        staffMemberId,
        startsAtUtc: slot.startsAtUtc,
      })

      setSuccessMessage(
        `Appointment request ${appointmentRequest.status.toLowerCase()} for ${slot.startsAtLocal}.`
      )
    } catch (caughtError) {
      if (caughtError instanceof ApiError && caughtError.status === 401) {
        clearAuthTokens()
        setError("Sign in again to request an appointment.")
      } else {
        setError("Appointment request could not be created.")
      }
    } finally {
      setRequestingStaffKey("")
    }
  }

  if (services.length === 0) {
    return null
  }

  return (
    <Card>
      <CardHeader>
        <div className="flex items-center gap-2">
          <CalendarDays className="size-4 text-primary" aria-hidden="true" />
          <CardTitle>Available times</CardTitle>
        </div>
        <CardDescription>
          Sign in, select a service and date, then review available staff.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-5">
        {!isSignedIn ? (
          <Alert>
            <AlertTitle>Sign in required</AlertTitle>
            <AlertDescription>
              Guests can review businesses and services. Appointment times are
              visible after sign in.
            </AlertDescription>
          </Alert>
        ) : (
          <>
            <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_180px]">
              <div className="space-y-2">
                <Label>Service</Label>
                <div className="flex flex-wrap gap-2">
                  {services.map((service) => (
                    <Button
                      key={service.id}
                      type="button"
                      variant={
                        service.id === selectedServiceId ? "default" : "outline"
                      }
                      onClick={() => setSelectedServiceId(service.id)}
                    >
                      {service.name}
                    </Button>
                  ))}
                </div>
              </div>
              <div className="space-y-2">
                <Label htmlFor="availability-date">Date</Label>
                <Input
                  id="availability-date"
                  type="date"
                  value={date}
                  min={getTodayValue()}
                  onChange={(event) => setDate(event.target.value)}
                />
              </div>
            </div>

            {selectedService ? (
              <div className="flex flex-wrap gap-2 text-sm text-muted-foreground">
                <Badge variant="outline">
                  {selectedService.durationMinutes} min
                </Badge>
                <Badge variant="outline">
                  {selectedService.basePriceAmount}{" "}
                  {selectedService.currencyCode}
                </Badge>
              </div>
            ) : null}

            {error ? (
              <Alert className="border-destructive/30 bg-destructive/5 text-destructive">
                <AlertTitle>Availability unavailable</AlertTitle>
                <AlertDescription>{error}</AlertDescription>
              </Alert>
            ) : null}

            {successMessage ? (
              <Alert>
                <AlertTitle>Request sent</AlertTitle>
                <AlertDescription>{successMessage}</AlertDescription>
              </Alert>
            ) : null}

            {isLoading ? (
              <p className="text-sm leading-6 text-muted-foreground">
                Loading available times.
              </p>
            ) : availability ? (
              <AvailabilitySlotList
                slots={availability.slots}
                requestingStaffKey={requestingStaffKey}
                onRequest={handleRequest}
              />
            ) : null}
          </>
        )}

        <div className="flex flex-wrap gap-2">
          <Link
            href="/login"
            className={cn(buttonVariants({ variant: "outline" }))}
          >
            Sign in
          </Link>
        </div>
      </CardContent>
    </Card>
  )
}

function AvailabilitySlotList({
  slots,
  requestingStaffKey,
  onRequest,
}: {
  slots: AvailabilitySlot[]
  requestingStaffKey: string
  onRequest: (slot: AvailabilitySlot, staffMemberId: string) => void
}) {
  if (slots.length === 0) {
    return (
      <p className="text-sm leading-6 text-muted-foreground">
        No available times were found for this date.
      </p>
    )
  }

  return (
    <div className="grid gap-3">
      {slots.map((slot) => (
        <div
          key={`${slot.startsAtUtc}-${slot.endsAtUtc}`}
          className="rounded-lg border border-border bg-background p-3"
        >
          <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
            <div className="flex items-center gap-2 text-sm font-medium text-foreground">
              <Clock className="size-4 text-primary" aria-hidden="true" />
              {slot.startsAtLocal} - {slot.endsAtLocal}
            </div>
            <div className="flex min-w-0 flex-wrap gap-2">
              {slot.staffMembers.map((staffMember) => (
                <Button
                  key={staffMember.staffMemberId}
                  type="button"
                  size="sm"
                  variant="outline"
                  disabled={
                    requestingStaffKey ===
                    `${slot.startsAtUtc}-${staffMember.staffMemberId}`
                  }
                  onClick={() => onRequest(slot, staffMember.staffMemberId)}
                >
                  <UsersRound data-icon="inline-start" className="size-3" />
                  {requestingStaffKey ===
                  `${slot.startsAtUtc}-${staffMember.staffMemberId}`
                    ? "Requesting"
                    : staffMember.displayName}
                </Button>
              ))}
            </div>
          </div>
        </div>
      ))}
    </div>
  )
}

function getDefaultDateValue() {
  const date = new Date()

  for (let offset = 1; offset <= 7; offset += 1) {
    const candidate = new Date(date)
    candidate.setDate(date.getDate() + offset)

    if (candidate.getDay() !== 0) {
      return formatDateInputValue(candidate)
    }
  }

  return formatDateInputValue(date)
}

function getTodayValue() {
  return formatDateInputValue(new Date())
}

function formatDateInputValue(date: Date) {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, "0")
  const day = String(date.getDate()).padStart(2, "0")

  return `${year}-${month}-${day}`
}

function subscribeToAuthStorage(onStoreChange: () => void) {
  window.addEventListener("storage", onStoreChange)

  return () => window.removeEventListener("storage", onStoreChange)
}

function getAuthStorageSnapshot() {
  return Boolean(getAccessToken())
}

function getServerAuthStorageSnapshot() {
  return false
}
