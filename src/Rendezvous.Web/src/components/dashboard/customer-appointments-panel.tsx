"use client"

import { useEffect, useMemo, useState } from "react"
import type React from "react"
import {
  CalendarDays,
  Clock,
  MessageSquare,
  Scissors,
  SlidersHorizontal,
  Star,
  UserRound,
  WalletCards,
  X,
} from "lucide-react"

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import {
  Card,
  CardContent,
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
import { cn } from "@/lib/utils"

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

  const summary = useMemo(() => {
    return {
      total: appointments.length,
      active: appointments.filter((appointment) =>
        ["Pending", "Approved"].includes(appointment.status)
      ).length,
      pending: appointments.filter((appointment) => appointment.status === "Pending")
        .length,
      completed: appointments.filter(
        (appointment) => appointment.status === "Completed"
      ).length,
    }
  }, [appointments])

  const activeFilterCount = [
    statusFilter !== "all",
    Boolean(fromDate),
    Boolean(toDate),
  ].filter(Boolean).length

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
    <div className="grid gap-6">
      {message ? (
        <Alert className="border-[#cfe7c7] bg-[#f4fbf1] text-[#2f6d22]">
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

      <div className="grid gap-4 md:grid-cols-4">
        <MetricCard icon={CalendarDays} label="Total" value={summary.total} />
        <MetricCard icon={Clock} label="Active" value={summary.active} />
        <MetricCard icon={SlidersHorizontal} label="Pending" value={summary.pending} />
        <MetricCard icon={Star} label="Completed" value={summary.completed} />
      </div>

      <Card className="border-[#e5e7eb] bg-white shadow-xs">
        <CardContent className="p-4">
          <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
            <div className="flex items-center gap-3">
              <div className="flex size-10 items-center justify-center rounded-lg border border-[#e5e7eb] bg-[#fafafa] text-[#111111]">
                <SlidersHorizontal className="size-5" aria-hidden="true" />
              </div>
              <h2 className="text-lg font-bold tracking-normal text-[#111111]">
                Filters
              </h2>
            </div>
            {activeFilterCount > 0 ? (
              <Badge
                variant="outline"
                className="border-[#cfe7c7] bg-[#f4fbf1] text-[#4f9d3a]"
              >
                {activeFilterCount} active
              </Badge>
            ) : null}
          </div>

          <div className="grid gap-3 md:grid-cols-[180px_minmax(0,1fr)_minmax(0,1fr)_auto] md:items-end">
            <Field label="Status" id="customer-appointment-status">
              <Select value={statusFilter} onValueChange={setStatusFilter}>
                <SelectTrigger
                  id="customer-appointment-status"
                  className="h-11 rounded-lg border-[#d4d4d8] bg-white"
                >
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
                className="h-11 rounded-lg border-[#d4d4d8] bg-white"
                onChange={(event) => setFromDate(event.target.value)}
              />
            </Field>
            <Field label="To" id="customer-appointment-to">
              <Input
                id="customer-appointment-to"
                type="date"
                value={toDate}
                className="h-11 rounded-lg border-[#d4d4d8] bg-white"
                onChange={(event) => setToDate(event.target.value)}
              />
            </Field>
            <div className="flex flex-wrap gap-2 md:justify-end">
              <Button
                type="button"
                className="h-11 rounded-full bg-[#111111] px-5 text-base font-bold text-white hover:bg-[#27272a]"
                onClick={applyFilters}
              >
                Apply
              </Button>
              <Button
                type="button"
                variant="outline"
                className="h-11 rounded-xl border-[#d4d4d8] bg-white px-5 text-base font-medium text-[#111111] hover:bg-[#f4f4f5]"
                onClick={clearFilters}
              >
                Clear
              </Button>
            </div>
          </div>
        </CardContent>
      </Card>

      {isLoading ? (
        <Card className="border-[#e5e7eb] bg-white shadow-xs">
          <CardContent className="p-5 text-sm leading-6 text-[#71717a]">
            Loading appointments.
          </CardContent>
        </Card>
      ) : appointments.length === 0 ? (
        <Card className="border-[#e5e7eb] bg-white shadow-xs">
          <CardContent className="flex min-h-[160px] items-center justify-center p-5 text-center text-sm leading-6 text-[#71717a]">
            No appointments found.
          </CardContent>
        </Card>
      ) : (
        <div className="grid gap-4">
          {appointments.map((appointment) => (
            <Card
              key={appointment.id}
              className="border-[#e5e7eb] bg-white shadow-xs transition-all hover:border-[#d4d4d8] hover:shadow-sm"
            >
              <CardContent className="p-5">
                <div className="grid gap-5 lg:grid-cols-[minmax(0,1fr)_auto] lg:items-start">
                  <div className="min-w-0 space-y-4">
                    <div className="flex flex-wrap items-start justify-between gap-3">
                      <div className="min-w-0 space-y-1">
                        <h2 className="break-words text-xl font-bold tracking-normal text-[#111111]">
                          {appointment.businessName}
                        </h2>
                        <p className="text-sm leading-6 text-[#71717a]">
                          {formatAppointmentTime(appointment.startsAtUtc)}
                        </p>
                      </div>
                      <Badge
                        variant="outline"
                        className={getStatusBadgeClass(appointment.status)}
                      >
                        {getStatusLabel(appointment.status)}
                      </Badge>
                    </div>

                    <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
                      <AppointmentDetail
                        icon={Scissors}
                        label="Service"
                        value={appointment.serviceName}
                      />
                      <AppointmentDetail
                        icon={UserRound}
                        label="Staff"
                        value={appointment.staffDisplayName}
                      />
                      <AppointmentDetail
                        icon={WalletCards}
                        label="Price"
                        value={formatPrice(
                          appointment.priceAmount,
                          appointment.currencyCode
                        )}
                      />
                    </div>
                  </div>

                  <div className="flex flex-wrap justify-start gap-2 lg:justify-end">
                    {appointment.status === "Completed" && appointment.hasReview ? (
                      <Badge
                        variant="outline"
                        className="h-9 border-[#cfe7c7] bg-[#f4fbf1] px-3 text-[#4f9d3a]"
                      >
                        Review submitted
                      </Badge>
                    ) : null}
                    {canShowReview(appointment) ? (
                      <Button
                        type="button"
                        variant="outline"
                        className="h-9 rounded-xl border-[#d4d4d8] bg-white px-3 font-medium text-[#111111] hover:bg-[#f4f4f5]"
                        onClick={() => openReviewDialog(appointment)}
                      >
                        <MessageSquare data-icon="inline-start" className="size-4" />
                        Review
                      </Button>
                    ) : null}
                    {canShowCancel(appointment) ? (
                      <Button
                        type="button"
                        variant="outline"
                        className="h-9 rounded-xl border-[#d4d4d8] bg-white px-3 font-medium text-[#111111] hover:bg-[#f4f4f5]"
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
              </CardContent>
            </Card>
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
                className="min-h-28 w-full resize-y rounded-lg border border-[#d4d4d8] bg-white px-3 py-2 text-sm outline-none transition-colors placeholder:text-[#71717a] focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50"
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
    </div>
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

function MetricCard({
  icon: Icon,
  label,
  value,
}: {
  icon: typeof CalendarDays
  label: string
  value: number
}) {
  return (
    <Card className="border-[#e5e7eb] bg-white shadow-xs">
      <CardContent className="flex items-center justify-between gap-4 p-4">
        <div>
          <p className="text-sm font-medium text-[#71717a]">{label}</p>
          <p className="mt-1 text-3xl font-bold tracking-normal text-[#111111]">
            {value}
          </p>
        </div>
        <div className="flex size-11 items-center justify-center rounded-lg border border-[#cfe7c7] bg-[#f4fbf1] text-[#4f9d3a]">
          <Icon className="size-5" aria-hidden="true" />
        </div>
      </CardContent>
    </Card>
  )
}

function AppointmentDetail({
  icon: Icon,
  label,
  value,
}: {
  icon: typeof CalendarDays
  label: string
  value: string
}) {
  return (
    <div className="flex min-w-0 items-start gap-3 rounded-lg border border-[#e5e7eb] bg-[#fafafa] p-3">
      <Icon className="mt-0.5 size-4 shrink-0 text-[#4f9d3a]" aria-hidden="true" />
      <div className="min-w-0">
        <p className="text-xs font-medium text-[#71717a]">{label}</p>
        <p className="break-words text-sm font-semibold text-[#111111]">
          {value || "Not set"}
        </p>
      </div>
    </div>
  )
}

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

function getStatusLabel(status: string) {
  return appointmentStatusOptions.find((option) => option.value === status)?.label ?? status
}

function getStatusBadgeClass(status: string) {
  return cn(
    "px-3 py-1 text-sm",
    status === "Approved" &&
      "border-[#cfe7c7] bg-[#f4fbf1] text-[#4f9d3a]",
    status === "Pending" &&
      "border-[#f4d58d] bg-[#fff8e7] text-[#9a6400]",
    status === "Completed" &&
      "border-[#a9d8d2] bg-[#eaf8f6] text-[#0f766e]",
    ["Cancelled", "Rejected", "Expired"].includes(status) &&
      "border-[#e5e7eb] bg-[#fafafa] text-[#71717a]",
    status === "NoShow" &&
      "border-destructive/30 bg-destructive/5 text-destructive"
  )
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
      <Label htmlFor={id} className="text-sm font-medium text-[#3f3f46]">
        {label}
      </Label>
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

function formatPrice(amount: number, currencyCode: string) {
  try {
    return new Intl.NumberFormat("en", {
      style: "currency",
      currency: currencyCode,
      maximumFractionDigits: 2,
    }).format(amount)
  } catch {
    return `${amount} ${currencyCode}`
  }
}
