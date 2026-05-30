"use client"

import { useEffect, useState } from "react"
import type React from "react"
import { CalendarDays, MessageSquare, Star, X } from "lucide-react"

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
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"
import {
  cancelCustomerAppointment,
  createCustomerAppointmentReview,
  getCustomerAppointments,
} from "@/lib/auth-api"
import { ApiError } from "@/lib/api-client"
import type { AppointmentFilters, CustomerAppointment } from "@/lib/auth-api"

export function CustomerAppointmentsPanel() {
  const [appointments, setAppointments] = useState<CustomerAppointment[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [actingAppointmentId, setActingAppointmentId] = useState("")
  const [reviewingAppointment, setReviewingAppointment] =
    useState<CustomerAppointment | null>(null)
  const [reviewRating, setReviewRating] = useState(5)
  const [reviewComment, setReviewComment] = useState("")
  const [reviewError, setReviewError] = useState("")
  const [isSubmittingReview, setIsSubmittingReview] = useState(false)
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

  function openReviewDialog(appointment: CustomerAppointment) {
    setReviewingAppointment(appointment)
    setReviewRating(5)
    setReviewComment("")
    setReviewError("")
    setMessage("")
    setError("")
  }

  function closeReviewDialog() {
    if (isSubmittingReview) {
      return
    }

    setReviewingAppointment(null)
    setReviewRating(5)
    setReviewComment("")
    setReviewError("")
  }

  async function handleReviewSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()

    if (!reviewingAppointment) {
      return
    }

    const comment = reviewComment.trim()
    if (!comment) {
      setReviewError("Review comment is required.")
      return
    }

    if (comment.length > 1200) {
      setReviewError("Review comment cannot exceed 1200 characters.")
      return
    }

    setIsSubmittingReview(true)
    setReviewError("")
    setError("")

    try {
      await createCustomerAppointmentReview(reviewingAppointment.id, {
        rating: reviewRating,
        comment,
      })
      setReviewingAppointment(null)
      setReviewComment("")
      setMessage("Review submitted.")
      await refreshAppointments()
    } catch (caughtError) {
      setReviewError(
        getApiErrorMessage(caughtError, "Review could not be submitted.")
      )
    } finally {
      setIsSubmittingReview(false)
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
                  <div className="flex flex-wrap justify-end gap-2">
                    {appointment.status === "Completed" && appointment.hasReview ? (
                      <Badge variant="outline">Review submitted</Badge>
                    ) : null}
                    {canShowReview(appointment) ? (
                      <Button
                        type="button"
                        size="sm"
                        variant="outline"
                        onClick={() => openReviewDialog(appointment)}
                      >
                        <MessageSquare data-icon="inline-start" className="size-4" />
                        Review
                      </Button>
                    ) : null}
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
              </div>
            ))}
          </div>
        )}

        <Dialog
          open={reviewingAppointment !== null}
          onOpenChange={(open) => {
            if (!open) {
              closeReviewDialog()
            }
          }}
        >
          <DialogContent>
            <DialogHeader>
              <DialogTitle>Leave a review</DialogTitle>
              <DialogDescription>
                {reviewingAppointment?.businessName}
              </DialogDescription>
            </DialogHeader>
            <form className="space-y-4" onSubmit={handleReviewSubmit}>
              {reviewError ? (
                <Alert className="border-destructive/30 bg-destructive/5 text-destructive">
                  <AlertTitle>Review failed</AlertTitle>
                  <AlertDescription>{reviewError}</AlertDescription>
                </Alert>
              ) : null}

              <Field label="Rating" id="customer-review-rating">
                <div
                  id="customer-review-rating"
                  className="flex items-center gap-1"
                  role="radiogroup"
                  aria-label="Rating"
                >
                  {[1, 2, 3, 4, 5].map((rating) => (
                    <button
                      key={rating}
                      type="button"
                      role="radio"
                      aria-checked={reviewRating === rating}
                      title={`${rating} star${rating === 1 ? "" : "s"}`}
                      className="rounded-md p-1 text-muted-foreground transition-colors hover:text-[#f6b73c] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                      onClick={() => setReviewRating(rating)}
                    >
                      <Star
                        className={
                          rating <= reviewRating
                            ? "size-6 fill-[#f6b73c] text-[#f6b73c]"
                            : "size-6"
                        }
                      />
                      <span className="sr-only">
                        {rating} star{rating === 1 ? "" : "s"}
                      </span>
                    </button>
                  ))}
                </div>
              </Field>

              <Field label="Comment" id="customer-review-comment">
                <textarea
                  id="customer-review-comment"
                  value={reviewComment}
                  maxLength={1200}
                  rows={5}
                  className="min-h-28 w-full resize-y rounded-lg border border-border bg-background px-3 py-2 text-sm outline-none transition-colors placeholder:text-muted-foreground focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50"
                  onChange={(event) => setReviewComment(event.target.value)}
                />
              </Field>

              <DialogFooter>
                <Button
                  type="button"
                  variant="outline"
                  disabled={isSubmittingReview}
                  onClick={closeReviewDialog}
                >
                  Cancel
                </Button>
                <Button type="submit" disabled={isSubmittingReview}>
                  <MessageSquare data-icon="inline-start" className="size-4" />
                  {isSubmittingReview ? "Submitting" : "Submit review"}
                </Button>
              </DialogFooter>
            </form>
          </DialogContent>
        </Dialog>
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

function canShowReview(appointment: CustomerAppointment) {
  return appointment.status === "Completed" && !appointment.hasReview
}

function getApiErrorMessage(caughtError: unknown, fallback: string) {
  if (caughtError instanceof ApiError && isMessageBody(caughtError.body)) {
    return caughtError.body.message
  }

  return fallback
}

function isMessageBody(value: unknown): value is { message: string } {
  return (
    typeof value === "object" &&
    value !== null &&
    "message" in value &&
    typeof (value as { message: unknown }).message === "string"
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
