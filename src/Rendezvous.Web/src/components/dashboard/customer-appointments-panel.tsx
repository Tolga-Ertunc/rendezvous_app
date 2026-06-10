"use client"

import { useEffect, useState } from "react"
import type React from "react"
import {
  CalendarDays,
  CheckCircle2,
  ChevronLeft,
  ChevronRight,
  CircleDashed,
  Clock,
  MessageSquare,
  Star,
  X,
} from "lucide-react"
import type { LucideIcon } from "lucide-react"

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert"
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent } from "@/components/ui/card"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"
import { Label } from "@/components/ui/label"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table"
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs"
import { ApiError } from "@/lib/api-client"
import {
  cancelCustomerAppointment,
  createCustomerAppointmentReview,
  getCustomerAppointments,
} from "@/lib/auth-api"
import type {
  CustomerAppointment,
  CustomerAppointmentListParams,
  CustomerAppointmentsResponse,
} from "@/lib/auth-api"
import { cn } from "@/lib/utils"

type AppointmentView = NonNullable<CustomerAppointmentListParams["view"]>
type AppointmentSort = NonNullable<CustomerAppointmentListParams["sort"]>

const pageSize = 10
const businessNameMaxLength = 32

const emptyAppointmentsResponse: CustomerAppointmentsResponse = {
  items: [],
  summary: {
    total: 0,
    pending: 0,
    completed: 0,
  },
  page: {
    page: 1,
    pageSize,
    totalItems: 0,
    totalPages: 0,
  },
}

export function CustomerAppointmentsPanel() {
  const [appointmentsResponse, setAppointmentsResponse] =
    useState<CustomerAppointmentsResponse>(emptyAppointmentsResponse)
  const [view, setView] = useState<AppointmentView>("all")
  const [sort, setSort] = useState<AppointmentSort>("date_desc")
  const [page, setPage] = useState(1)
  const [isLoading, setIsLoading] = useState(true)
  const [actingAppointmentId, setActingAppointmentId] = useState("")
  const [reviewingAppointment, setReviewingAppointment] =
    useState<CustomerAppointment | null>(null)
  const [reviewRating, setReviewRating] = useState(5)
  const [reviewComment, setReviewComment] = useState("")
  const [reviewError, setReviewError] = useState("")
  const [isSubmittingReview, setIsSubmittingReview] = useState(false)
  const [message, setMessage] = useState("")
  const [error, setError] = useState("")

  useEffect(() => {
    let isMounted = true

    async function loadAppointments() {
      setIsLoading(true)
      setError("")

      try {
        const nextResponse = await getCustomerAppointments({
          view,
          page,
          pageSize,
          sort,
        })

        if (isMounted) {
          setAppointmentsResponse(nextResponse)

          if (nextResponse.page.page > 0 && nextResponse.page.page !== page) {
            setPage(nextResponse.page.page)
          }
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
  }, [page, sort, view])

  const appointments = appointmentsResponse.items
  const pagination = appointmentsResponse.page

  async function refreshAppointments(nextPage = page) {
    setIsLoading(true)
    setError("")

    try {
      const nextResponse = await getCustomerAppointments({
        view,
        page: nextPage,
        pageSize,
        sort,
      })

      setAppointmentsResponse(nextResponse)

      if (nextResponse.page.page > 0 && nextResponse.page.page !== nextPage) {
        setPage(nextResponse.page.page)
      }
    } catch {
      setError("Appointments could not be loaded.")
    } finally {
      setIsLoading(false)
    }
  }

  function handleViewChange(nextView: string) {
    setView(nextView as AppointmentView)
    setPage(1)
    setMessage("")
    setError("")
  }

  function handleSortChange(nextSort: string) {
    setSort(nextSort as AppointmentSort)
    setPage(1)
    setMessage("")
    setError("")
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
        <Alert className="border-[#d6ead5] bg-[#f6fbf5] text-[#255d20]">
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

      <div className="grid gap-4 md:grid-cols-3">
        <MetricCard
          icon={CalendarDays}
          label="Total"
          value={appointmentsResponse.summary.total}
        />
        <MetricCard
          icon={CircleDashed}
          label="Pending"
          value={appointmentsResponse.summary.pending}
        />
        <MetricCard
          icon={CheckCircle2}
          label="Completed"
          tone="completed"
          value={appointmentsResponse.summary.completed}
        />
      </div>

      <Card
        className={cn(
          "overflow-hidden border-[#e5e7eb] bg-white shadow-xs",
          pagination.totalItems <= pageSize
            ? "mx-auto w-fit max-w-full"
            : "w-full"
        )}
      >
        <CardContent className="p-0">
          <div className="flex flex-col gap-4 border-b border-[#e5e7eb] p-4 sm:flex-row sm:items-center sm:justify-between">
            <Tabs value={view} onValueChange={handleViewChange}>
              <TabsList className="border-[#e5e7eb] bg-white shadow-none">
                {appointmentViewOptions.map((option) => (
                  <TabsTrigger
                    key={option.value}
                    value={option.value}
                    className="data-[state=active]:bg-[#111111] data-[state=active]:text-white"
                  >
                    {option.label}
                  </TabsTrigger>
                ))}
              </TabsList>
            </Tabs>

            <Select value={sort} onValueChange={handleSortChange}>
              <SelectTrigger className="h-9 w-full rounded-lg border-[#d4d4d8] bg-white sm:w-[190px]">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="date_desc">Date newest first</SelectItem>
                <SelectItem value="date_asc">Date oldest first</SelectItem>
              </SelectContent>
            </Select>
          </div>

          <Table className="min-w-[1120px] table-fixed [&_[data-slot=table-cell]]:px-2 [&_[data-slot=table-head]]:px-2">
            <colgroup>
              <col className="w-[145px]" />
              <col className="w-[300px]" />
              <col className="w-[190px]" />
              <col className="w-[145px]" />
              <col className="w-[95px]" />
              <col className="w-[120px]" />
              <col className="w-[125px]" />
            </colgroup>
            <TableHeader>
              <TableRow className="hover:bg-transparent">
                <TableHead className="text-center">Date & time</TableHead>
                <TableHead className="text-center">Business</TableHead>
                <TableHead className="text-center">Service</TableHead>
                <TableHead className="text-center">Staff</TableHead>
                <TableHead className="pr-3 text-center">Price</TableHead>
                <TableHead className="pl-3 text-center">Status</TableHead>
                <TableHead className="pr-6">
                  <span className="ml-auto mr-6 block w-[96px] text-center">
                    Actions
                  </span>
                </TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {isLoading ? (
                <TableRow>
                  <TableCell
                    colSpan={7}
                    className="h-28 text-center text-sm text-[#71717a]"
                  >
                    Loading appointments.
                  </TableCell>
                </TableRow>
              ) : appointments.length === 0 ? (
                <TableRow>
                  <TableCell
                    colSpan={7}
                    className="h-32 text-center text-sm text-[#71717a]"
                  >
                    No appointments found.
                  </TableCell>
                </TableRow>
              ) : (
                appointments.map((appointment) => (
                  <TableRow key={appointment.id}>
                    <TableCell className="text-center">
                      <DateTimeCell startsAtUtc={appointment.startsAtUtc} />
                    </TableCell>
                    <TableCell>
                      <BusinessCell appointment={appointment} />
                    </TableCell>
                    <TableCell className="text-center font-medium text-[#111111]">
                      <span
                        className="block truncate"
                        title={appointment.serviceName || "Not set"}
                      >
                        {appointment.serviceName || "Not set"}
                      </span>
                    </TableCell>
                    <TableCell className="text-center text-[#3f3f46]">
                      <span
                        className="block truncate"
                        title={appointment.staffDisplayName || "Not set"}
                      >
                        {appointment.staffDisplayName || "Not set"}
                      </span>
                    </TableCell>
                    <TableCell className="pr-3 text-center font-semibold text-[#111111]">
                      {formatPrice(
                        appointment.priceAmount,
                        appointment.currencyCode
                      )}
                    </TableCell>
                    <TableCell className="pl-3 text-center">
                      <Badge className={getStatusBadgeClass(appointment.status)}>
                        {getStatusLabel(appointment.status)}
                      </Badge>
                    </TableCell>
                    <TableCell className="pr-6">
                      <AppointmentActions
                        appointment={appointment}
                        actingAppointmentId={actingAppointmentId}
                        onCancel={handleCancel}
                        onReview={openReviewDialog}
                      />
                    </TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>

          {pagination.totalItems > pageSize ? (
            <PaginationFooter
              pagination={pagination}
              onPageChange={(nextPage) => setPage(nextPage)}
            />
          ) : null}
        </CardContent>
      </Card>

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
                    className="rounded-md p-1 text-muted-foreground transition-colors hover:text-[#eab308] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                    onClick={() => setReviewRating(rating)}
                  >
                    <Star
                      className={
                        rating <= reviewRating
                          ? "size-6 fill-[#eab308] text-[#eab308]"
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

const appointmentViewOptions: { value: AppointmentView; label: string }[] = [
  { value: "all", label: "All" },
  { value: "upcoming", label: "Upcoming" },
  { value: "completed", label: "Completed" },
]

function MetricCard({
  icon: Icon,
  label,
  value,
  tone = "neutral",
}: {
  icon: LucideIcon
  label: string
  value: number
  tone?: "neutral" | "completed"
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
        <div
          className={cn(
            "flex size-11 items-center justify-center rounded-lg border",
            tone === "completed"
              ? "border-[#d6ead5] bg-[#f6fbf5] text-[#255d20]"
              : "border-[#e5e7eb] bg-[#fafafa] text-[#111111]"
          )}
        >
          <Icon className="size-5" aria-hidden="true" />
        </div>
      </CardContent>
    </Card>
  )
}

function DateTimeCell({ startsAtUtc }: { startsAtUtc: string }) {
  return (
    <div className="grid gap-1.5 whitespace-nowrap">
      <div className="flex items-center justify-center gap-2 font-medium text-[#111111]">
        <CalendarDays className="size-4 text-[#3f3f46]" aria-hidden="true" />
        {formatAppointmentDate(startsAtUtc)}
      </div>
      <div className="flex items-center justify-center gap-2 text-[#71717a]">
        <Clock className="size-4 text-[#71717a]" aria-hidden="true" />
        {formatAppointmentTime(startsAtUtc)}
      </div>
    </div>
  )
}

function BusinessCell({ appointment }: { appointment: CustomerAppointment }) {
  const photo = appointment.businessMainPhoto

  return (
    <div className="mx-auto flex w-[250px] max-w-full items-center justify-start gap-3">
      <Avatar className="size-12 shrink-0 rounded-md border border-[#e5e7eb] bg-[#fafafa]">
        {photo ? (
          <AvatarImage
            src={photo.imageUrl}
            alt={photo.altText || appointment.businessName}
            className="rounded-md"
          />
        ) : (
          <AvatarFallback className="rounded-md bg-[#f4f4f5] text-[#111111]">
            {getBusinessInitial(appointment.businessName)}
          </AvatarFallback>
        )}
      </Avatar>
      <div className="min-w-0 flex-1">
        <p
          className="truncate font-semibold text-[#111111]"
          title={appointment.businessName}
        >
          {truncateBusinessName(appointment.businessName)}
        </p>
      </div>
    </div>
  )
}

function AppointmentActions({
  appointment,
  actingAppointmentId,
  onCancel,
  onReview,
}: {
  appointment: CustomerAppointment
  actingAppointmentId: string
  onCancel: (appointmentId: string) => void
  onReview: (appointment: CustomerAppointment) => void
}) {
  if (appointment.canReview) {
    return (
      <div className="grid grid-cols-[96px_24px] items-center justify-end gap-2">
        <Button
          type="button"
          variant="outline"
          className="h-9 w-[96px] rounded-lg border-[#d4d4d8] bg-white px-2 font-medium text-[#111111] hover:bg-[#f4f4f5]"
          onClick={() => onReview(appointment)}
        >
          <MessageSquare data-icon="inline-start" className="size-4" />
          Review
        </Button>
        <span aria-hidden="true" />
      </div>
    )
  }

  if (appointment.hasReview && appointment.reviewRating !== null) {
    return (
      <div className="grid grid-cols-[96px_24px] items-center justify-end gap-2">
        <Button
          type="button"
          variant="outline"
          disabled
          className="h-9 w-[96px] rounded-lg border-[#d4d4d8] bg-[#f4f4f5] px-2 text-[#71717a]"
        >
          Reviewed
        </Button>
        <div className="grid justify-items-center gap-0.5 text-xs font-bold text-[#111111]">
          <Star
            className="size-4 fill-[#eab308] text-[#eab308]"
            aria-hidden="true"
          />
          <span>{formatRating(appointment.reviewRating)}</span>
        </div>
      </div>
    )
  }

  if (appointment.canCancel) {
    return (
      <div className="grid grid-cols-[96px_24px] items-center justify-end gap-2">
        <Button
          type="button"
          variant="outline"
          className="h-9 w-[96px] rounded-lg border-[#d4d4d8] bg-white px-2 font-medium text-[#111111] hover:bg-[#f4f4f5]"
          disabled={actingAppointmentId === appointment.id}
          onClick={() => onCancel(appointment.id)}
        >
          <X data-icon="inline-start" className="size-4" />
          {actingAppointmentId === appointment.id ? "Cancelling" : "Cancel"}
        </Button>
        <span aria-hidden="true" />
      </div>
    )
  }

  return (
    <div className="grid grid-cols-[96px_24px] items-center justify-end gap-2">
      <span className="text-center text-[#a1a1aa]">-</span>
      <span aria-hidden="true" />
    </div>
  )
}

function PaginationFooter({
  pagination,
  onPageChange,
}: {
  pagination: CustomerAppointmentsResponse["page"]
  onPageChange: (page: number) => void
}) {
  const firstItem = (pagination.page - 1) * pagination.pageSize + 1
  const lastItem = Math.min(
    pagination.page * pagination.pageSize,
    pagination.totalItems
  )
  const visiblePages = getVisiblePages(pagination.page, pagination.totalPages)

  return (
    <div className="flex flex-col gap-3 border-t border-[#e5e7eb] p-4 text-sm text-[#71717a] sm:flex-row sm:items-center sm:justify-between">
      <p>
        Showing {firstItem} to {lastItem} of {pagination.totalItems} appointments
      </p>
      <div className="flex items-center justify-end gap-2">
        <Button
          type="button"
          variant="outline"
          size="icon"
          className="border-[#d4d4d8] bg-white text-[#111111] hover:bg-[#f4f4f5]"
          disabled={pagination.page <= 1}
          onClick={() => onPageChange(pagination.page - 1)}
        >
          <ChevronLeft className="size-4" aria-hidden="true" />
          <span className="sr-only">Previous page</span>
        </Button>
        {visiblePages.map((visiblePage) => (
          <Button
            key={visiblePage}
            type="button"
            variant={visiblePage === pagination.page ? "default" : "outline"}
            size="icon"
            className={cn(
              visiblePage === pagination.page
                ? "bg-[#111111] text-white hover:bg-[#27272a]"
                : "border-[#d4d4d8] bg-white text-[#111111] hover:bg-[#f4f4f5]"
            )}
            onClick={() => onPageChange(visiblePage)}
          >
            {visiblePage}
          </Button>
        ))}
        <Button
          type="button"
          variant="outline"
          size="icon"
          className="border-[#d4d4d8] bg-white text-[#111111] hover:bg-[#f4f4f5]"
          disabled={pagination.page >= pagination.totalPages}
          onClick={() => onPageChange(pagination.page + 1)}
        >
          <ChevronRight className="size-4" aria-hidden="true" />
          <span className="sr-only">Next page</span>
        </Button>
      </div>
    </div>
  )
}

function getVisiblePages(currentPage: number, totalPages: number) {
  if (totalPages <= 5) {
    return Array.from({ length: totalPages }, (_, index) => index + 1)
  }

  if (currentPage <= 3) {
    return [1, 2, 3, 4, 5]
  }

  if (currentPage >= totalPages - 2) {
    return Array.from({ length: 5 }, (_, index) => totalPages - 4 + index)
  }

  return Array.from({ length: 5 }, (_, index) => currentPage - 2 + index)
}

function getStatusLabel(status: string) {
  return appointmentStatusLabels[status] ?? status
}

const appointmentStatusLabels: Record<string, string> = {
  Pending: "Pending",
  Approved: "Approved",
  Rejected: "Rejected",
  Cancelled: "Cancelled",
  Completed: "Completed",
  NoShow: "No show",
  Expired: "Expired",
}

function getStatusBadgeClass(status: string) {
  return cn(
    "border-transparent px-2.5 py-1 text-xs font-semibold",
    status === "Approved" && "bg-[#2f6d22] text-white",
    status === "Completed" && "bg-[#111111] text-white",
    status === "Cancelled" && "bg-[#b42318] text-white",
    status === "Pending" && "bg-[#e5e7eb] text-[#3f3f46]",
    ["Rejected", "Expired", "NoShow"].includes(status) &&
      "bg-[#f4f4f5] text-[#71717a]"
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

function formatAppointmentDate(value: string) {
  return new Intl.DateTimeFormat("en", {
    dateStyle: "medium",
  }).format(new Date(value))
}

function formatAppointmentTime(value: string) {
  return new Intl.DateTimeFormat("en", {
    timeStyle: "short",
  }).format(new Date(value))
}

function formatPrice(amount: number, currencyCode: string) {
  try {
    return new Intl.NumberFormat("tr-TR", {
      style: "currency",
      currency: currencyCode,
      maximumFractionDigits: Number.isInteger(amount) ? 0 : 2,
    }).format(amount)
  } catch {
    return `${amount} ${currencyCode}`
  }
}

function formatRating(value: number) {
  return Number.isInteger(value) ? value.toString() : value.toFixed(1)
}

function truncateBusinessName(value: string) {
  return value.length > businessNameMaxLength
    ? `${value.slice(0, businessNameMaxLength - 1)}...`
    : value
}

function getBusinessInitial(value: string) {
  return value.trim().charAt(0).toUpperCase() || "B"
}
