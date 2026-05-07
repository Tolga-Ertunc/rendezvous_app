"use client"

import { useEffect, useMemo, useState } from "react"
import { useRouter } from "next/navigation"
import {
  ArrowLeft,
  Calendar,
  Check,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  Clock,
  Plus,
  ShoppingCart,
  UserRound,
  X,
} from "lucide-react"

import { Avatar, AvatarFallback } from "@/components/ui/avatar"
import { Button } from "@/components/ui/button"
import {
  Dialog,
  DialogContent,
  DialogTitle,
} from "@/components/ui/dialog"
import { Separator } from "@/components/ui/separator"
import { ApiError } from "@/lib/api-client"
import { clearAuthTokens, getAccessToken } from "@/lib/auth-storage"
import {
  createAppointmentRequest,
  getBookingAvailability,
} from "@/lib/booking-api"
import type {
  AppointmentRequest,
  AvailabilitySlot,
  BookingAvailability,
} from "@/lib/booking-api"
import type {
  PublicBusinessDetail,
  PublicBusinessService,
  PublicBusinessStaffMember,
} from "@/lib/public-api"
import { cn } from "@/lib/utils"

type BookingStep = "services" | "time" | "professional" | "confirm" | "success"

type PublicBookingFlowProps = {
  open: boolean
  business: PublicBusinessDetail
  initialServiceId: string | null
  onOpenChange: (open: boolean) => void
}

const stepItems: { id: Exclude<BookingStep, "success">; label: string }[] = [
  { id: "services", label: "Services" },
  { id: "time", label: "Select time" },
  { id: "professional", label: "Select professional" },
  { id: "confirm", label: "Confirm" },
]

export function PublicBookingFlow({
  open,
  business,
  initialServiceId,
  onOpenChange,
}: PublicBookingFlowProps) {
  const router = useRouter()
  const [step, setStep] = useState<BookingStep>("services")
  const [selectedServiceId, setSelectedServiceId] = useState("")
  const [activeCategory, setActiveCategory] = useState("Featured")
  const [selectedDate, setSelectedDate] = useState(() =>
    formatDateKey(getBusinessToday(business.timeZoneId))
  )
  const [selectedSlotKey, setSelectedSlotKey] = useState("")
  const [selectedStaffId, setSelectedStaffId] = useState("")
  const [availabilityByDate, setAvailabilityByDate] = useState<
    Record<string, BookingAvailability>
  >({})
  const [isLoadingAvailability, setIsLoadingAvailability] = useState(false)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [error, setError] = useState("")
  const [appointmentRequest, setAppointmentRequest] =
    useState<AppointmentRequest | null>(null)

  const today = useMemo(
    () => getBusinessToday(business.timeZoneId),
    [business.timeZoneId]
  )
  const dateOptions = useMemo(() => buildDateOptions(today, 14), [today])
  const categories = useMemo(
    () => getServiceCategories(business.services),
    [business.services]
  )
  const selectedService = useMemo(
    () =>
      business.services.find((service) => service.id === selectedServiceId) ??
      null,
    [business.services, selectedServiceId]
  )
  const visibleServices = useMemo(
    () =>
      activeCategory === "Featured"
        ? business.services.slice(0, 4)
        : business.services.filter(
            (service) => service.categoryName === activeCategory
          ),
    [activeCategory, business.services]
  )
  const availability = availabilityByDate[selectedDate] ?? null
  const selectedSlot = useMemo(
    () =>
      availability?.slots.find((slot) => getSlotKey(slot) === selectedSlotKey) ??
      null,
    [availability, selectedSlotKey]
  )
  const selectedStaff = useMemo(
    () =>
      selectedSlot?.staffMembers.find(
        (staffMember) => staffMember.staffMemberId === selectedStaffId
      ) ?? null,
    [selectedSlot, selectedStaffId]
  )
  const selectedDateOption =
    dateOptions.find((dateOption) => dateOption.key === selectedDate) ??
    dateOptions[0]
  const isClosedSelectedDate = isBusinessClosedOnDate(
    business,
    selectedDateOption
  )
  const canContinue =
    step === "services"
      ? Boolean(selectedService)
      : step === "time"
        ? Boolean(selectedSlot)
        : step === "professional"
          ? Boolean(selectedStaff)
          : step === "confirm"
            ? Boolean(selectedService && selectedSlot && selectedStaff)
            : false

  useEffect(() => {
    if (!open) {
      return
    }

    if (!getAccessToken()) {
      onOpenChange(false)
      router.push("/register?reason=booking")
      return
    }

    const initialService =
      business.services.find((service) => service.id === initialServiceId) ??
      null

    const resetTimeout = window.setTimeout(() => {
      setStep("services")
      setSelectedServiceId(initialService?.id ?? "")
      setActiveCategory(initialService?.categoryName ?? "Featured")
      setSelectedDate(formatDateKey(getBusinessToday(business.timeZoneId)))
      setSelectedSlotKey("")
      setSelectedStaffId("")
      setAvailabilityByDate({})
      setError("")
      setAppointmentRequest(null)
    }, 0)

    return () => window.clearTimeout(resetTimeout)
  }, [business.services, business.timeZoneId, initialServiceId, onOpenChange, open, router])

  useEffect(() => {
    if (!open || !selectedServiceId || step === "services" || step === "success") {
      return
    }

    if (isBusinessClosedOnDate(business, selectedDateOption)) {
      const resetTimeout = window.setTimeout(() => {
        setSelectedSlotKey("")
        setSelectedStaffId("")
      }, 0)

      return () => window.clearTimeout(resetTimeout)
    }

    let isMounted = true

    async function loadAvailability() {
      setIsLoadingAvailability(true)
      setError("")

      try {
        const nextAvailability = await getBookingAvailability(
          business.id,
          selectedServiceId,
          selectedDate
        )

        if (!isMounted) {
          return
        }

        setAvailabilityByDate((current) => ({
          ...current,
          [selectedDate]: nextAvailability,
        }))

        if (
          selectedSlotKey &&
          !nextAvailability.slots.some(
            (slot) => getSlotKey(slot) === selectedSlotKey
          )
        ) {
          setSelectedSlotKey("")
          setSelectedStaffId("")
        }
      } catch (caughtError) {
        if (!isMounted) {
          return
        }

        if (caughtError instanceof ApiError && caughtError.status === 401) {
          clearAuthTokens()
          onOpenChange(false)
          router.push("/register?reason=booking")
          return
        }

        setAvailabilityByDate((current) => {
          const next = { ...current }
          delete next[selectedDate]
          return next
        })
        setError("Available appointment times could not be loaded.")
      } finally {
        if (isMounted) {
          setIsLoadingAvailability(false)
        }
      }
    }

    loadAvailability()

    return () => {
      isMounted = false
    }
  }, [
    business,
    onOpenChange,
    open,
    router,
    selectedDate,
    selectedDateOption,
    selectedServiceId,
    selectedSlotKey,
    step,
  ])

  function handleBack() {
    setError("")

    if (step === "services") {
      onOpenChange(false)
    } else if (step === "time") {
      setStep("services")
    } else if (step === "professional") {
      setStep("time")
    } else if (step === "confirm") {
      setStep("professional")
    } else {
      onOpenChange(false)
    }
  }

  function handleServiceSelect(service: PublicBusinessService) {
    setSelectedServiceId(service.id)
    setActiveCategory(service.categoryName || "Featured")
    setSelectedSlotKey("")
    setSelectedStaffId("")
    setAvailabilityByDate({})
    setError("")
  }

  function handleDateSelect(dateKey: string) {
    setSelectedDate(dateKey)
    setSelectedSlotKey("")
    setSelectedStaffId("")
    setError("")
  }

  function handleSlotSelect(slot: AvailabilitySlot) {
    setSelectedSlotKey(getSlotKey(slot))
    setSelectedStaffId("")
    setError("")
  }

  function handleContinue() {
    if (!canContinue) {
      return
    }

    setError("")

    if (step === "services") {
      setStep("time")
    } else if (step === "time") {
      setStep("professional")
    } else if (step === "professional") {
      setStep("confirm")
    }
  }

  async function handleConfirm() {
    if (!selectedService || !selectedSlot || !selectedStaff) {
      return
    }

    setIsSubmitting(true)
    setError("")

    try {
      const created = await createAppointmentRequest({
        businessId: business.id,
        serviceId: selectedService.id,
        staffMemberId: selectedStaff.staffMemberId,
        startsAtUtc: selectedSlot.startsAtUtc,
      })

      setAppointmentRequest(created)
      setStep("success")
    } catch (caughtError) {
      if (caughtError instanceof ApiError && caughtError.status === 401) {
        clearAuthTokens()
        onOpenChange(false)
        router.push("/register?reason=booking")
        return
      }

      setError("Selected slot is no longer available. Choose another time.")
      setStep("time")
      setSelectedSlotKey("")
      setSelectedStaffId("")

      try {
        const refreshedAvailability = await getBookingAvailability(
          business.id,
          selectedService.id,
          selectedDate
        )

        setAvailabilityByDate((current) => ({
          ...current,
          [selectedDate]: refreshedAvailability,
        }))
      } catch {
        setAvailabilityByDate((current) => {
          const next = { ...current }
          delete next[selectedDate]
          return next
        })
      }
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent
        showCloseButton={false}
        className="fixed inset-0 !left-0 !top-0 z-50 !flex h-svh w-screen max-w-none !translate-x-0 !translate-y-0 flex-col gap-0 overflow-hidden rounded-none border-0 bg-white p-0 text-[#111111] shadow-none outline-none"
      >
        <DialogTitle className="sr-only">Book appointment</DialogTitle>
        <div className="absolute left-4 top-4 z-20 md:left-8 md:top-8">
          <Button
            type="button"
            variant="outline"
            size="icon-lg"
            className="size-14 rounded-full border-[#e5e7eb] bg-white text-[#111111] shadow-sm hover:bg-[#f4f4f5]"
            onClick={handleBack}
            aria-label="Back"
          >
            <ArrowLeft className="size-6" aria-hidden="true" />
          </Button>
        </div>
        <div className="absolute right-4 top-4 z-20 md:right-8 md:top-8">
          <Button
            type="button"
            variant="outline"
            size="icon-lg"
            className="size-14 rounded-full border-[#e5e7eb] bg-white text-[#111111] shadow-sm hover:bg-[#f4f4f5]"
            onClick={() => onOpenChange(false)}
            aria-label="Close"
          >
            <X className="size-6" aria-hidden="true" />
          </Button>
        </div>

        <div className="flex min-h-0 flex-1 overflow-y-auto px-5 pb-28 pt-24 md:px-10 md:pb-10 md:pt-20">
          <div
            className={cn(
              "mx-auto w-full",
              step === "success"
                ? "flex max-w-none justify-center"
                : "grid max-w-[1460px] gap-12 lg:grid-cols-[minmax(0,1fr)_430px]"
            )}
          >
            <section
              className={cn(
                "mx-auto w-full",
                step === "success" ? "max-w-[760px]" : "max-w-[820px] space-y-10"
              )}
            >
              {step !== "success" ? (
                <StepTrail activeStep={step} />
              ) : null}

              {step === "services" ? (
                <ServicesStep
                  categories={categories}
                  activeCategory={activeCategory}
                  services={visibleServices}
                  selectedServiceId={selectedServiceId}
                  onCategoryChange={setActiveCategory}
                  onServiceSelect={handleServiceSelect}
                />
              ) : step === "time" ? (
                <TimeStep
                  business={business}
                  selectedService={selectedService}
                  dateOptions={dateOptions}
                  selectedDate={selectedDate}
                  selectedDateOption={selectedDateOption}
                  selectedSlotKey={selectedSlotKey}
                  selectedStaffName={selectedStaff?.displayName ?? ""}
                  availability={availability}
                  isClosed={isClosedSelectedDate}
                  isLoading={isLoadingAvailability}
                  error={error}
                  onDateSelect={handleDateSelect}
                  onSlotSelect={handleSlotSelect}
                />
              ) : step === "professional" ? (
                <ProfessionalStep
                  business={business}
                  selectedService={selectedService}
                  selectedSlot={selectedSlot}
                  selectedStaffId={selectedStaffId}
                  onStaffSelect={setSelectedStaffId}
                />
              ) : step === "confirm" ? (
                <ConfirmStep
                  business={business}
                  selectedService={selectedService}
                  selectedDateOption={selectedDateOption}
                  selectedSlot={selectedSlot}
                  selectedStaff={selectedStaff}
                  error={error}
                />
              ) : (
                <SuccessStep
                  appointmentRequest={appointmentRequest}
                  selectedService={selectedService}
                  selectedStaff={selectedStaff}
                  onViewAppointments={() => router.push("/appointments")}
                />
              )}
            </section>

            <aside className="hidden lg:block">
              <BookingSummary
                business={business}
                selectedService={selectedService}
                selectedDateOption={selectedDateOption}
                selectedSlot={selectedSlot}
                selectedStaff={selectedStaff}
                canContinue={canContinue}
                step={step}
                isSubmitting={isSubmitting}
                onContinue={handleContinue}
                onConfirm={handleConfirm}
              />
            </aside>
          </div>
        </div>

        {step !== "success" ? (
          <MobileSummaryBar
            selectedService={selectedService}
            canContinue={canContinue}
            step={step}
            isSubmitting={isSubmitting}
            onContinue={handleContinue}
            onConfirm={handleConfirm}
          />
        ) : null}
      </DialogContent>
    </Dialog>
  )
}

function StepTrail({ activeStep }: { activeStep: BookingStep }) {
  const activeIndex = stepItems.findIndex((item) => item.id === activeStep)

  return (
    <nav className="flex flex-wrap items-center gap-3 text-base font-semibold">
      {stepItems.map((item, index) => (
        <div key={item.id} className="flex items-center gap-3">
          <span
            className={cn(
              index === activeIndex ? "text-[#111111]" : "text-[#a1a1aa]"
            )}
          >
            {item.label}
          </span>
          {index < stepItems.length - 1 ? (
            <ChevronRight
              className="size-5 text-[#a1a1aa]"
              aria-hidden="true"
            />
          ) : null}
        </div>
      ))}
    </nav>
  )
}

function ServicesStep({
  categories,
  activeCategory,
  services,
  selectedServiceId,
  onCategoryChange,
  onServiceSelect,
}: {
  categories: string[]
  activeCategory: string
  services: PublicBusinessService[]
  selectedServiceId: string
  onCategoryChange: (category: string) => void
  onServiceSelect: (service: PublicBusinessService) => void
}) {
  return (
    <div className="space-y-9">
      <h1 className="text-5xl font-extrabold leading-none tracking-normal md:text-6xl">
        Services
      </h1>
      <div className="flex gap-4 overflow-x-auto pb-1">
        {categories.map((category) => (
          <button
            key={category}
            type="button"
            className={cn(
              "h-12 shrink-0 whitespace-nowrap rounded-full px-6 text-base font-bold transition-colors",
              activeCategory === category
                ? "bg-[#111111] text-white"
                : "bg-white text-[#111111] hover:bg-[#f4f4f5]"
            )}
            onClick={() => onCategoryChange(category)}
          >
            {category}
          </button>
        ))}
      </div>
      <div className="space-y-4">
        <h2 className="text-3xl font-bold tracking-normal">{activeCategory}</h2>
        <div className="overflow-hidden">
          {services.map((service, index) => (
            <div key={service.id}>
              <button
                type="button"
                className={cn(
                  "flex w-full items-center justify-between gap-5 rounded-lg border border-transparent bg-white px-5 py-6 text-left transition-colors",
                  selectedServiceId === service.id &&
                    "border-[#4f9d3a] shadow-[0_0_0_1px_rgba(79,157,58,0.2)]"
                )}
                onClick={() => onServiceSelect(service)}
              >
                <div className="min-w-0 space-y-2">
                  <h3 className="truncate text-2xl font-semibold">
                    {service.name}
                  </h3>
                  <p className="text-lg text-[#71717a]">
                    {formatDuration(service.durationMinutes)}
                  </p>
                  {service.description ? (
                    <p className="line-clamp-2 max-w-2xl text-base leading-6 text-[#71717a]">
                      {service.description}
                    </p>
                  ) : null}
                  <p className="text-xl font-bold">
                    from {formatCurrency(service.basePriceAmount, service.currencyCode)}
                  </p>
                </div>
                <span
                  className={cn(
                    "inline-flex size-12 shrink-0 items-center justify-center rounded-full border text-[#111111] shadow-sm",
                    selectedServiceId === service.id
                      ? "border-[#4f9d3a] bg-[#4f9d3a] text-white"
                      : "border-[#e5e7eb] bg-white"
                  )}
                  aria-hidden="true"
                >
                  {selectedServiceId === service.id ? (
                    <Check className="size-6" />
                  ) : (
                    <Plus className="size-6" />
                  )}
                </span>
              </button>
              {index < services.length - 1 ? <Separator /> : null}
            </div>
          ))}
        </div>
      </div>
    </div>
  )
}

function TimeStep({
  business,
  selectedService,
  dateOptions,
  selectedDate,
  selectedDateOption,
  selectedSlotKey,
  selectedStaffName,
  availability,
  isClosed,
  isLoading,
  error,
  onDateSelect,
  onSlotSelect,
}: {
  business: PublicBusinessDetail
  selectedService: PublicBusinessService | null
  dateOptions: DateOption[]
  selectedDate: string
  selectedDateOption: DateOption
  selectedSlotKey: string
  selectedStaffName: string
  availability: BookingAvailability | null
  isClosed: boolean
  isLoading: boolean
  error: string
  onDateSelect: (dateKey: string) => void
  onSlotSelect: (slot: AvailabilitySlot) => void
}) {
  const slots = availability?.slots ?? []

  return (
    <div className="space-y-9">
      <h1 className="text-5xl font-extrabold leading-none tracking-normal md:text-6xl">
        Select time
      </h1>
      <div className="flex items-center justify-between gap-4">
        <button
          type="button"
          className="inline-flex h-12 items-center gap-3 rounded-full border border-[#e5e7eb] bg-white px-4 text-base font-semibold text-[#111111]"
        >
          <UserRound className="size-5" aria-hidden="true" />
          {selectedStaffName || "No preference"}
          <ChevronDown className="size-4" aria-hidden="true" />
        </button>
        <Button
          type="button"
          variant="outline"
          size="icon-lg"
          className="size-12 rounded-full border-[#e5e7eb] bg-white"
          aria-label="Open calendar"
        >
          <Calendar className="size-5" aria-hidden="true" />
        </Button>
      </div>
      <div className="space-y-6">
        <div className="flex items-center justify-between gap-4">
          <h2 className="text-2xl font-bold">
            {formatMonthLabel(selectedDateOption)}
          </h2>
          <div className="flex items-center gap-5">
            <ChevronLeft className="size-5 text-[#71717a]" aria-hidden="true" />
            <ChevronRight className="size-5 text-[#111111]" aria-hidden="true" />
          </div>
        </div>
        <div className="flex gap-6 overflow-x-auto pb-2">
          {dateOptions.map((dateOption) => {
            const isSelected = dateOption.key === selectedDate
            const isClosedDate = isBusinessClosedOnDate(business, dateOption)

            return (
              <button
                key={dateOption.key}
                type="button"
                disabled={isClosedDate}
                className={cn(
                  "grid shrink-0 justify-items-center gap-3 text-center transition-opacity disabled:cursor-not-allowed disabled:opacity-45"
                )}
                onClick={() => onDateSelect(dateOption.key)}
              >
                <span
                  className={cn(
                    "inline-flex size-20 items-center justify-center rounded-full border border-[#e5e7eb] bg-white text-3xl font-bold tabular-nums text-[#111111]",
                    isSelected && "border-[#635bff] bg-[#635bff] text-white",
                    isClosedDate && "text-[#a1a1aa] line-through"
                  )}
                >
                  {dateOption.day}
                </span>
                <span
                  className={cn(
                    "text-lg font-semibold",
                    isSelected ? "text-[#111111]" : "text-[#71717a]"
                  )}
                >
                  {dateOption.weekdayShort}
                </span>
              </button>
            )
          })}
        </div>
      </div>

      {error ? (
        <p className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm font-medium text-red-700">
          {error}
        </p>
      ) : null}

      {isClosed ? (
        <EmptyAvailability
          staffMembers={business.staffMembers}
          message={`${business.name} is closed on this date.`}
        />
      ) : isLoading ? (
        <p className="text-base text-[#71717a]">Loading available times.</p>
      ) : slots.length === 0 ? (
        <EmptyAvailability
          staffMembers={business.staffMembers}
          message={`${business.name} has no available times on this date.`}
        />
      ) : (
        <div className="grid gap-3">
          {slots.map((slot) => {
            const isSelected = getSlotKey(slot) === selectedSlotKey

            return (
              <button
                key={getSlotKey(slot)}
                type="button"
                className={cn(
                  "flex h-20 w-full items-center justify-between gap-4 rounded-lg border border-[#e5e7eb] bg-white px-6 text-left text-xl font-semibold transition-colors",
                  isSelected && "border-[#4f9d3a] shadow-[0_0_0_1px_rgba(79,157,58,0.2)]"
                )}
                onClick={() => onSlotSelect(slot)}
              >
                <span>{formatSlotTime(slot.startsAtLocal)}</span>
                <span className="text-lg font-bold">
                  {selectedService
                    ? formatCurrency(
                        selectedService.basePriceAmount,
                        selectedService.currencyCode
                      )
                    : ""}
                </span>
              </button>
            )
          })}
        </div>
      )}
    </div>
  )
}

function ProfessionalStep({
  business,
  selectedService,
  selectedSlot,
  selectedStaffId,
  onStaffSelect,
}: {
  business: PublicBusinessDetail
  selectedService: PublicBusinessService | null
  selectedSlot: AvailabilitySlot | null
  selectedStaffId: string
  onStaffSelect: (staffId: string) => void
}) {
  const availableStaff = selectedSlot?.staffMembers ?? []

  return (
    <div className="space-y-9">
      <h1 className="text-5xl font-extrabold leading-none tracking-normal md:text-6xl">
        Select professional
      </h1>
      {availableStaff.length === 0 ? (
        <EmptyAvailability
          staffMembers={business.staffMembers}
          message="No professional is available for this time."
        />
      ) : (
        <div className="grid gap-4">
          {availableStaff.map((staffMember) => {
            const isSelected = staffMember.staffMemberId === selectedStaffId

            return (
              <button
                key={staffMember.staffMemberId}
                type="button"
                className={cn(
                  "flex min-h-32 items-center justify-between gap-5 rounded-lg border border-[#e5e7eb] bg-white px-5 py-5 text-left transition-colors",
                  isSelected && "border-[#4f9d3a] shadow-[0_0_0_1px_rgba(79,157,58,0.2)]"
                )}
                onClick={() => onStaffSelect(staffMember.staffMemberId)}
              >
                <div className="flex min-w-0 items-center gap-5">
                  <Avatar className="size-20 bg-[#f1f2f5]">
                    <AvatarFallback className="text-2xl font-bold text-[#635bff]">
                      {getInitial(staffMember.displayName)}
                    </AvatarFallback>
                  </Avatar>
                  <div className="min-w-0 space-y-2">
                    <h2 className="truncate text-2xl font-bold">
                      {staffMember.displayName}
                    </h2>
                    <p className="text-lg font-semibold">
                      {selectedService
                        ? formatCurrency(
                            selectedService.basePriceAmount,
                            selectedService.currencyCode
                          )
                        : ""}
                    </p>
                  </div>
                </div>
                <span
                  className={cn(
                    "inline-flex h-12 shrink-0 items-center justify-center rounded-full border px-6 text-base font-bold",
                    isSelected
                      ? "border-[#4f9d3a] bg-[#4f9d3a] text-white"
                      : "border-[#d4d4d8] bg-white text-[#111111]"
                  )}
                >
                  {isSelected ? <Check className="size-5" /> : "Select"}
                </span>
              </button>
            )
          })}
        </div>
      )}
    </div>
  )
}

function ConfirmStep({
  business,
  selectedService,
  selectedDateOption,
  selectedSlot,
  selectedStaff,
  error,
}: {
  business: PublicBusinessDetail
  selectedService: PublicBusinessService | null
  selectedDateOption: DateOption
  selectedSlot: AvailabilitySlot | null
  selectedStaff: { staffMemberId: string; displayName: string } | null
  error: string
}) {
  return (
    <div className="space-y-9">
      <h1 className="text-5xl font-extrabold leading-none tracking-normal md:text-6xl">
        Confirm
      </h1>
      {error ? (
        <p className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm font-medium text-red-700">
          {error}
        </p>
      ) : null}
      <div className="space-y-6 text-xl">
        <SummaryRow label="Business" value={business.name} />
        <Separator />
        <SummaryRow label="Service" value={selectedService?.name ?? ""} />
        <SummaryRow
          label="Date"
          value={`${selectedDateOption.weekdayLong}, ${selectedDateOption.monthLong} ${selectedDateOption.day}`}
        />
        <SummaryRow
          label="Time"
          value={
            selectedSlot
              ? `${formatSlotTime(selectedSlot.startsAtLocal)} - ${formatSlotTime(
                  selectedSlot.endsAtLocal
                )}`
              : ""
          }
        />
        <SummaryRow label="Professional" value={selectedStaff?.displayName ?? ""} />
        <SummaryRow
          label="Duration"
          value={selectedService ? formatDuration(selectedService.durationMinutes) : ""}
        />
        <Separator />
        <SummaryRow
          label="Total"
          value={
            selectedService
              ? `from ${formatCurrency(
                  selectedService.basePriceAmount,
                  selectedService.currencyCode
                )}`
              : ""
          }
          strong
        />
      </div>
    </div>
  )
}

function SuccessStep({
  appointmentRequest,
  selectedService,
  selectedStaff,
  onViewAppointments,
}: {
  appointmentRequest: AppointmentRequest | null
  selectedService: PublicBusinessService | null
  selectedStaff: { staffMemberId: string; displayName: string } | null
  onViewAppointments: () => void
}) {
  return (
    <div className="flex min-h-[560px] flex-col items-center justify-center space-y-8 text-center">
      <span className="inline-flex size-20 items-center justify-center rounded-full bg-[#4f9d3a] text-white">
        <Check className="size-10" aria-hidden="true" />
      </span>
      <div className="space-y-3">
        <h1 className="text-5xl font-extrabold tracking-normal">
          Request sent
        </h1>
        <p className="max-w-xl text-lg leading-7 text-[#71717a]">
          Your appointment request is pending. The business will review it.
        </p>
      </div>
      <div className="w-full max-w-lg space-y-4 rounded-lg border border-[#e5e7eb] bg-white p-6 text-left">
        <SummaryRow label="Service" value={selectedService?.name ?? ""} />
        <SummaryRow label="Professional" value={selectedStaff?.displayName ?? ""} />
        <SummaryRow
          label="Status"
          value={appointmentRequest?.status ?? "Pending"}
          strong
        />
      </div>
      <Button
        type="button"
        className="h-14 rounded-full bg-[#111111] px-10 text-base font-bold text-white hover:bg-[#27272a]"
        onClick={onViewAppointments}
      >
        View appointments
      </Button>
    </div>
  )
}

function BookingSummary({
  business,
  selectedService,
  selectedDateOption,
  selectedSlot,
  selectedStaff,
  canContinue,
  step,
  isSubmitting,
  onContinue,
  onConfirm,
}: {
  business: PublicBusinessDetail
  selectedService: PublicBusinessService | null
  selectedDateOption: DateOption
  selectedSlot: AvailabilitySlot | null
  selectedStaff: { staffMemberId: string; displayName: string } | null
  canContinue: boolean
  step: BookingStep
  isSubmitting: boolean
  onContinue: () => void
  onConfirm: () => void
}) {
  if (step === "success") {
    return null
  }

  return (
    <div className="sticky top-20 flex min-h-[620px] flex-col rounded-2xl border border-[#e5e7eb] bg-white p-8 shadow-[0_8px_28px_rgba(17,17,17,0.04)]">
      <div className="space-y-7">
        <div className="space-y-2">
          <h2 className="text-2xl font-extrabold">{business.name}</h2>
          <p className="text-base leading-6 text-[#71717a]">
            {formatAddress(business.address)}
          </p>
        </div>
        {selectedService ? (
          <div className="space-y-2">
            <div className="flex items-start justify-between gap-4">
              <div>
                <p className="text-lg font-bold">{selectedService.name}</p>
                <p className="text-base text-[#71717a]">
                  {formatDuration(selectedService.durationMinutes)}
                  {selectedStaff ? ` with ${selectedStaff.displayName}` : ""}
                </p>
              </div>
              <p className="shrink-0 text-lg font-bold">
                from{" "}
                {formatCurrency(
                  selectedService.basePriceAmount,
                  selectedService.currencyCode
                )}
              </p>
            </div>
          </div>
        ) : null}
        {selectedSlot ? (
          <div className="space-y-3 text-lg">
            <p className="flex items-center gap-3">
              <Calendar className="size-5" aria-hidden="true" />
              {selectedDateOption.weekdayLong}, {selectedDateOption.monthLong}{" "}
              {selectedDateOption.day}
            </p>
            <p className="flex items-center gap-3">
              <Clock className="size-5" aria-hidden="true" />
              {formatSlotTime(selectedSlot.startsAtLocal)} -{" "}
              {formatSlotTime(selectedSlot.endsAtLocal)}
            </p>
          </div>
        ) : null}
        <Separator />
        <div className="flex items-center justify-between text-xl font-extrabold">
          <span>Total</span>
          <span>
            {selectedService
              ? `from ${formatCurrency(
                  selectedService.basePriceAmount,
                  selectedService.currencyCode
                )}`
              : "-"}
          </span>
        </div>
      </div>
      <div className="mt-auto pt-8">
        <Button
          type="button"
          className="h-16 w-full rounded-full bg-[#111111] text-xl font-bold text-white hover:bg-[#27272a] disabled:bg-[#a1a1aa]"
          disabled={!canContinue || isSubmitting}
          onClick={step === "confirm" ? onConfirm : onContinue}
        >
          {step === "confirm"
            ? isSubmitting
              ? "Booking"
              : "Book now"
            : "Continue"}
        </Button>
      </div>
    </div>
  )
}

function MobileSummaryBar({
  selectedService,
  canContinue,
  step,
  isSubmitting,
  onContinue,
  onConfirm,
}: {
  selectedService: PublicBusinessService | null
  canContinue: boolean
  step: BookingStep
  isSubmitting: boolean
  onContinue: () => void
  onConfirm: () => void
}) {
  return (
    <div className="fixed inset-x-0 bottom-0 z-30 border-t border-[#e5e7eb] bg-white px-5 py-4 shadow-[0_-8px_24px_rgba(17,17,17,0.08)] lg:hidden">
      <div className="mx-auto flex max-w-[820px] items-center justify-between gap-4">
        <div className="min-w-0">
          <p className="truncate text-lg font-extrabold">
            {selectedService
              ? `from ${formatCurrency(
                  selectedService.basePriceAmount,
                  selectedService.currencyCode
                )}`
              : "Select service"}
          </p>
          <p className="mt-1 flex items-center gap-2 truncate text-sm font-medium text-[#71717a]">
            <ShoppingCart className="size-4" aria-hidden="true" />
            {selectedService
              ? `1 service • ${formatDuration(selectedService.durationMinutes)}`
              : "No service selected"}
          </p>
        </div>
        <Button
          type="button"
          className="h-12 shrink-0 rounded-full bg-[#111111] px-7 text-base font-bold text-white hover:bg-[#27272a] disabled:bg-[#a1a1aa]"
          disabled={!canContinue || isSubmitting}
          onClick={step === "confirm" ? onConfirm : onContinue}
        >
          {step === "confirm" ? "Book now" : "Continue"}
        </Button>
      </div>
    </div>
  )
}

function EmptyAvailability({
  staffMembers,
  message,
}: {
  staffMembers: PublicBusinessStaffMember[]
  message: string
}) {
  const staffMember = staffMembers[0]

  return (
    <div className="flex min-h-[340px] flex-col items-center justify-center rounded-lg border border-[#e5e7eb] bg-white px-6 text-center">
      <Avatar className="size-16 bg-[#f1f2f5]">
        <AvatarFallback className="text-xl font-bold text-[#635bff]">
          {staffMember ? getInitial(staffMember.displayName) : "R"}
        </AvatarFallback>
      </Avatar>
      <p className="mt-6 max-w-md text-2xl font-bold">{message}</p>
    </div>
  )
}

function SummaryRow({
  label,
  value,
  strong = false,
}: {
  label: string
  value: string
  strong?: boolean
}) {
  return (
    <div className="flex items-start justify-between gap-6">
      <span className="text-[#71717a]">{label}</span>
      <span
        className={cn(
          "max-w-[60%] text-right text-[#111111]",
          strong ? "font-extrabold" : "font-semibold"
        )}
      >
        {value || "-"}
      </span>
    </div>
  )
}

type DateOption = {
  key: string
  day: number
  weekdayShort: string
  weekdayLong: string
  monthLong: string
  monthShort: string
  monthNumber: number
  year: number
}

function getBusinessToday(timeZoneId: string) {
  const parts = new Intl.DateTimeFormat("en-US", {
    timeZone: timeZoneId,
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).formatToParts(new Date())

  const year = Number(parts.find((part) => part.type === "year")?.value ?? "0")
  const month = Number(parts.find((part) => part.type === "month")?.value ?? "1")
  const day = Number(parts.find((part) => part.type === "day")?.value ?? "1")

  return new Date(Date.UTC(year, month - 1, day, 12))
}

function buildDateOptions(startDate: Date, count: number): DateOption[] {
  return Array.from({ length: count }, (_, offset) => {
    const date = new Date(startDate)
    date.setUTCDate(startDate.getUTCDate() + offset)

    return {
      key: formatDateKey(date),
      day: date.getUTCDate(),
      weekdayShort: new Intl.DateTimeFormat("en-US", {
        weekday: "short",
        timeZone: "UTC",
      }).format(date),
      weekdayLong: new Intl.DateTimeFormat("en-US", {
        weekday: "long",
        timeZone: "UTC",
      }).format(date),
      monthLong: new Intl.DateTimeFormat("en-US", {
        month: "long",
        timeZone: "UTC",
      }).format(date),
      monthShort: new Intl.DateTimeFormat("en-US", {
        month: "short",
        timeZone: "UTC",
      }).format(date),
      monthNumber: date.getUTCMonth() + 1,
      year: date.getUTCFullYear(),
    }
  })
}

function formatDateKey(date: Date) {
  return `${date.getUTCFullYear()}-${String(date.getUTCMonth() + 1).padStart(
    2,
    "0"
  )}-${String(date.getUTCDate()).padStart(2, "0")}`
}

function isBusinessClosedOnDate(
  business: PublicBusinessDetail,
  dateOption: DateOption
) {
  return !business.workingHours.some(
    (workingHour) => workingHour.dayOfWeek === dateOption.weekdayLong
  )
}

function getServiceCategories(services: PublicBusinessService[]) {
  return Array.from(
    new Set(["Featured", ...services.map((service) => service.categoryName)])
  ).filter(Boolean)
}

function getSlotKey(slot: AvailabilitySlot) {
  return `${slot.startsAtUtc}-${slot.endsAtUtc}`
}

function formatMonthLabel(dateOption: DateOption) {
  return `${dateOption.monthLong} ${dateOption.year}`
}

function formatDuration(durationMinutes: number) {
  if (durationMinutes < 60) {
    return `${durationMinutes} min`
  }

  const hours = Math.floor(durationMinutes / 60)
  const minutes = durationMinutes % 60

  return minutes === 0 ? `${hours} hr` : `${hours} hr, ${minutes} min`
}

function formatCurrency(amount: number, currencyCode: string) {
  return new Intl.NumberFormat("tr-TR", {
    style: "currency",
    currency: currencyCode,
    maximumFractionDigits: 0,
  }).format(amount)
}

function formatSlotTime(value: string) {
  const [hourText, minuteText] = value.split(":")
  const hour = Number(hourText)
  const minute = Number(minuteText)
  const normalizedHour = hour % 12 || 12
  const suffix = hour >= 12 ? "PM" : "AM"

  return `${normalizedHour}:${String(minute).padStart(2, "0")} ${suffix}`
}

function formatAddress(address: PublicBusinessDetail["address"]) {
  return [address.addressLine, address.district, address.city]
    .filter(Boolean)
    .join(", ")
}

function getInitial(value: string) {
  return value.trim().charAt(0).toUpperCase() || "R"
}
