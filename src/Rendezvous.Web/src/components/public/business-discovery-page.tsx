"use client"

import type { FormEvent } from "react"
import { useEffect, useMemo, useState } from "react"
import Image from "next/image"
import Link from "next/link"
import {
  Building2,
  ChevronRight,
  ImageIcon,
  MapPin,
  Search,
  SlidersHorizontal,
  Star,
} from "lucide-react"

import { PublicBookingFlow } from "@/components/public/public-booking-flow"
import { PublicShell } from "@/components/public/public-shell"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent } from "@/components/ui/card"
import { Checkbox } from "@/components/ui/checkbox"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from "@/components/ui/popover"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { Separator } from "@/components/ui/separator"
import { getPublicBusiness, getPublicBusinesses } from "@/lib/public-api"
import type {
  PublicBusiness,
  PublicBusinessDetail,
  PublicBusinessWorkingHour,
} from "@/lib/public-api"
import { cn } from "@/lib/utils"

const additionalInformationOrder = [
  "Instant Confirmation",
  "Pay by app",
  "Pet-friendly",
  "Kid-friendly",
  "Near public transport",
  "Organic products only",
  "Vegan products only",
  "Environmentally friendly",
]

type SortMode = "popular" | "name"

export function BusinessDiscoveryPage() {
  const [businesses, setBusinesses] = useState<PublicBusiness[]>([])
  const [searchDraft, setSearchDraft] = useState("")
  const [searchQuery, setSearchQuery] = useState("")
  const [sortMode, setSortMode] = useState<SortMode>("popular")
  const [filterOpen, setFilterOpen] = useState(false)
  const [draftFilters, setDraftFilters] = useState<string[]>([])
  const [appliedFilters, setAppliedFilters] = useState<string[]>([])
  const [bookingBusiness, setBookingBusiness] =
    useState<PublicBusinessDetail | null>(null)
  const [bookingBusinessId, setBookingBusinessId] = useState("")
  const [bookingError, setBookingError] = useState("")
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState("")

  useEffect(() => {
    let isMounted = true

    async function loadBusinesses() {
      setIsLoading(true)
      setError("")

      try {
        const nextBusinesses = await getPublicBusinesses()

        if (isMounted) {
          setBusinesses(nextBusinesses)
        }
      } catch {
        if (isMounted) {
          setError("Businesses could not be loaded.")
        }
      } finally {
        if (isMounted) {
          setIsLoading(false)
        }
      }
    }

    loadBusinesses()

    return () => {
      isMounted = false
    }
  }, [])

  const filterOptions = useMemo(() => {
    const values = new Set(
      businesses.flatMap((business) => business.additionalInformation)
    )

    return additionalInformationOrder.filter((item) => values.has(item))
  }, [businesses])

  const visibleBusinesses = useMemo(() => {
    const normalizedSearch = searchQuery.trim().toLowerCase()

    return businesses
      .filter((business) => {
        const matchesSearch =
          !normalizedSearch ||
          business.name.toLowerCase().includes(normalizedSearch) ||
          business.services.some((service) =>
            service.name.toLowerCase().includes(normalizedSearch)
          )
        const matchesFilters = appliedFilters.every((filter) =>
          business.additionalInformation.includes(filter)
        )

        return matchesSearch && matchesFilters
      })
      .sort((left, right) => {
        if (sortMode === "name") {
          return left.name.localeCompare(right.name)
        }

        return (
          right.reviewSummary.reviewCount - left.reviewSummary.reviewCount ||
          left.name.localeCompare(right.name)
        )
      })
  }, [appliedFilters, businesses, searchQuery, sortMode])

  function handleSearch(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSearchQuery(searchDraft)
  }

  function handleFilterToggle(filter: string, checked: boolean) {
    setDraftFilters((current) =>
      checked
        ? [...current, filter]
        : current.filter((candidate) => candidate !== filter)
    )
  }

  function openFilters(nextOpen: boolean) {
    setFilterOpen(nextOpen)

    if (nextOpen) {
      setDraftFilters(appliedFilters)
    }
  }

  function clearFilters() {
    setDraftFilters([])
  }

  function saveFilters() {
    setAppliedFilters(draftFilters)
    setFilterOpen(false)
  }

  function clearSearch() {
    setSearchDraft("")
    setSearchQuery("")
  }

  async function openBookingFlow(businessId: string) {
    setBookingBusinessId(businessId)
    setBookingError("")

    try {
      setBookingBusiness(await getPublicBusiness(businessId))
    } catch {
      setBookingError("Booking flow could not be opened.")
      setBookingBusiness(null)
    } finally {
      setBookingBusinessId("")
    }
  }

  return (
    <PublicShell>
      <section className="rounded-lg border border-[#e5e7eb] bg-white p-4 shadow-xs">
        <form
          className="grid gap-3 md:grid-cols-[minmax(0,1fr)_156px]"
          onSubmit={handleSearch}
        >
          <div className="relative">
            <Search
              className="pointer-events-none absolute left-5 top-1/2 size-5 -translate-y-1/2 text-[#111111]"
              aria-hidden="true"
            />
            <Input
              className="h-14 rounded-lg border-[#d4d4d8] pl-14 pr-4 text-base shadow-none placeholder:text-[#71717a]"
              placeholder="Search businesses or services"
              value={searchDraft}
              onChange={(event) => setSearchDraft(event.target.value)}
            />
          </div>
          <Button
            type="submit"
            className="h-14 rounded-full bg-[#111111] px-8 text-base font-bold text-white hover:bg-[#27272a]"
            disabled={isLoading}
          >
            Search
          </Button>
        </form>
      </section>

      <section className="space-y-4">
        <div className="flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
          <div>
            <h1 className="text-3xl font-bold tracking-normal text-[#111111]">
              Active businesses
            </h1>
            <p className="mt-1 text-base text-[#71717a]">
              {visibleBusinesses.length}{" "}
              {visibleBusinesses.length === 1 ? "business" : "businesses"}{" "}
              available
            </p>
          </div>

          <div className="flex flex-wrap items-center gap-3">
            <Popover open={filterOpen} onOpenChange={openFilters}>
              <PopoverTrigger asChild>
                <Button
                  type="button"
                  variant="outline"
                  className="h-11 rounded-lg border-[#d4d4d8] bg-white px-4 text-base font-medium"
                >
                  <SlidersHorizontal className="mr-2 size-5" aria-hidden="true" />
                  Filters
                  {appliedFilters.length > 0 ? (
                    <Badge className="ml-1 border-transparent bg-[#111111] px-2 text-white">
                      {appliedFilters.length}
                    </Badge>
                  ) : null}
                </Button>
              </PopoverTrigger>
              <PopoverContent align="end" className="w-80 p-0">
                <div className="p-4">
                  <h2 className="text-base font-semibold text-[#111111]">
                    Filters
                  </h2>
                  <p className="mt-1 text-sm text-[#71717a]">
                    Match every selected business detail.
                  </p>
                </div>
                <Separator />
                <div className="grid max-h-80 gap-3 overflow-auto p-4">
                  {filterOptions.length > 0 ? (
                    filterOptions.map((filter) => (
                      <Label
                        key={filter}
                        className="flex cursor-pointer items-center gap-3 text-sm font-medium text-[#111111]"
                      >
                        <Checkbox
                          checked={draftFilters.includes(filter)}
                          onCheckedChange={(checked) =>
                            handleFilterToggle(filter, checked === true)
                          }
                        />
                        {filter}
                      </Label>
                    ))
                  ) : (
                    <p className="text-sm text-[#71717a]">
                      No additional information filters are available.
                    </p>
                  )}
                </div>
                <Separator />
                <div className="flex items-center justify-between gap-3 p-4">
                  <Button
                    type="button"
                    variant="ghost"
                    className="text-[#111111]"
                    onClick={clearFilters}
                  >
                    Clear
                  </Button>
                  <Button
                    type="button"
                    className="rounded-full bg-[#111111] px-5 text-white hover:bg-[#27272a]"
                    onClick={saveFilters}
                  >
                    Save
                  </Button>
                </div>
              </PopoverContent>
            </Popover>

            <Select
              value={sortMode}
              onValueChange={(value) => setSortMode(value as SortMode)}
            >
              <SelectTrigger className="h-11 w-[218px] rounded-lg border-[#d4d4d8] bg-white px-4 text-base shadow-none">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="popular">Sort by Popular</SelectItem>
                <SelectItem value="name">Sort by Name</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </div>

        {bookingError ? (
          <p className="rounded-lg border border-[#fecaca] bg-[#fef2f2] px-4 py-3 text-sm text-[#991b1b]">
            {bookingError}
          </p>
        ) : null}

        {isLoading ? (
          <BusinessListSkeleton />
        ) : error ? (
          <Card className="border-[#e5e7eb] bg-white shadow-xs">
            <CardContent className="p-8">
              <h2 className="text-xl font-semibold text-[#111111]">
                Businesses unavailable
              </h2>
              <p className="mt-2 text-sm text-[#71717a]">{error}</p>
            </CardContent>
          </Card>
        ) : visibleBusinesses.length === 0 ? (
          <EmptyBusinessesState onClearSearch={clearSearch} />
        ) : (
          <div className="overflow-hidden rounded-lg border border-[#e5e7eb] bg-white">
            {visibleBusinesses.map((business) => (
              <BusinessListItem
                key={business.id}
                business={business}
                isBookingLoading={bookingBusinessId === business.id}
                onBookNow={() => openBookingFlow(business.id)}
              />
            ))}
          </div>
        )}
      </section>

      {bookingBusiness ? (
        <PublicBookingFlow
          open={Boolean(bookingBusiness)}
          business={bookingBusiness}
          initialServiceId={null}
          onOpenChange={(open) => {
            if (!open) {
              setBookingBusiness(null)
            }
          }}
        />
      ) : null}
    </PublicShell>
  )
}

function BusinessListItem({
  business,
  isBookingLoading,
  onBookNow,
}: {
  business: PublicBusiness
  isBookingLoading: boolean
  onBookNow: () => void
}) {
  const status = getOpenStatus(business)
  const heroPhoto = business.photos.find((photo) => photo.imageUrl.trim())

  return (
    <article className="grid gap-5 border-b border-[#e5e7eb] p-4 last:border-b-0 lg:grid-cols-[356px_minmax(0,1fr)_270px] lg:p-2 lg:pr-4">
      <Link
        href={`/businesses/${business.id}`}
        className="relative block aspect-[16/9] self-center overflow-hidden rounded-lg bg-[#f1f2f5] lg:h-40 lg:aspect-auto"
        aria-label={`${business.name} details`}
      >
        {heroPhoto ? (
          <Image
            src={heroPhoto.imageUrl}
            alt={heroPhoto.altText || business.name}
            fill
            sizes="356px"
            className="object-cover"
            unoptimized
          />
        ) : (
          <div className="flex size-full flex-col items-center justify-center gap-2 text-[#71717a]">
            <ImageIcon className="size-8" aria-hidden="true" />
            <span className="text-sm font-medium">No image</span>
          </div>
        )}
      </Link>

      <div className="min-w-0 space-y-3 py-1 lg:py-5">
        <div className="min-w-0 space-y-1">
          <Link
            href={`/businesses/${business.id}`}
            className="block truncate text-2xl font-bold tracking-normal text-[#111111] hover:text-[#635bff]"
          >
            {business.name}
          </Link>
          <div className="flex flex-wrap items-center gap-x-2 gap-y-1 text-base">
            <span className="font-semibold text-[#111111]">
              {formatRating(business.reviewSummary.averageRating)}
            </span>
            <StarRating rating={business.reviewSummary.averageRating} />
            <span className="font-semibold text-[#4f9d3a]">
              ({formatReviewCount(business.reviewSummary.reviewCount)})
            </span>
          </div>
        </div>

        <div className="flex flex-wrap items-center gap-x-2 gap-y-1 text-sm text-[#3f3f46]">
          <OpenStatusInline status={status} />
          <span className="text-[#71717a]">·</span>
          <span>{business.type}</span>
        </div>

        {formatAddress(business.address) ? (
          <div className="flex items-start gap-2 text-sm text-[#3f3f46]">
            <MapPin className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
            <span className="line-clamp-1">{formatAddress(business.address)}</span>
          </div>
        ) : null}

        <div className="flex flex-wrap gap-2">
          {business.services.slice(0, 5).map((service) => (
            <Badge
              key={service.id}
              variant="outline"
              className="rounded-md border-[#e5e7eb] bg-white px-3 py-1 text-xs font-medium text-[#3f3f46]"
            >
              {service.name}
            </Badge>
          ))}
        </div>
      </div>

      <div className="flex w-full max-w-[280px] flex-col gap-3 justify-self-start py-1 lg:justify-self-end lg:py-5">
        <Button
          type="button"
          className="h-12 rounded-full bg-[#111111] text-base font-bold text-white hover:bg-[#27272a]"
          onClick={onBookNow}
          disabled={isBookingLoading}
        >
          {isBookingLoading ? "Loading" : "Book now"}
        </Button>
        <Link
          href={`/businesses/${business.id}#services`}
          className="inline-flex h-9 w-full items-center justify-center gap-1 text-sm font-semibold text-[#4f9d3a] hover:underline"
        >
          Services
          <ChevronRight className="size-4" aria-hidden="true" />
        </Link>
        <Link
          href={`/businesses/${business.id}`}
          className="inline-flex h-9 w-full items-center justify-center gap-1 text-sm font-semibold text-[#3f3f46] hover:text-[#4f9d3a]"
        >
          View details
        </Link>
      </div>
    </article>
  )
}

function BusinessListSkeleton() {
  return (
    <div className="overflow-hidden rounded-lg border border-[#e5e7eb] bg-white">
      {Array.from({ length: 2 }, (_, index) => (
        <div
          key={index}
          className="grid gap-5 border-b border-[#e5e7eb] p-4 last:border-b-0 lg:grid-cols-[356px_minmax(0,1fr)_270px] lg:p-2 lg:pr-4"
        >
          <div className="h-48 self-center rounded-lg bg-[#f1f2f5] lg:h-40" />
          <div className="space-y-3 py-5">
            <div className="h-7 w-56 rounded bg-[#f1f2f5]" />
            <div className="h-5 w-44 rounded bg-[#f1f2f5]" />
            <div className="h-5 w-72 rounded bg-[#f1f2f5]" />
            <div className="flex gap-2">
              <div className="h-7 w-20 rounded bg-[#f1f2f5]" />
              <div className="h-7 w-24 rounded bg-[#f1f2f5]" />
            </div>
          </div>
          <div className="flex w-full max-w-[280px] flex-col justify-center justify-self-start gap-3 py-5 lg:justify-self-end">
            <div className="h-12 rounded-full bg-[#f1f2f5]" />
            <div className="mx-auto h-5 w-24 rounded bg-[#f1f2f5]" />
          </div>
        </div>
      ))}
    </div>
  )
}

function EmptyBusinessesState({ onClearSearch }: { onClearSearch: () => void }) {
  return (
    <Card className="border-[#e5e7eb] bg-white shadow-xs">
      <CardContent className="flex flex-col items-center px-6 py-12 text-center">
        <div className="flex size-12 items-center justify-center rounded-full bg-[#f4f4f5] text-[#71717a]">
          <Building2 className="size-6" aria-hidden="true" />
        </div>
        <h2 className="mt-4 text-xl font-semibold text-[#111111]">
          No businesses found
        </h2>
        <p className="mt-2 max-w-md text-sm leading-6 text-[#71717a]">
          Try a different business or service search, or clear the current
          filters.
        </p>
        <Button
          type="button"
          variant="outline"
          className="mt-5 rounded-full border-[#d4d4d8] bg-white px-5"
          onClick={onClearSearch}
        >
          Clear search
        </Button>
      </CardContent>
    </Card>
  )
}

function OpenStatusInline({ status }: { status: OpenStatus }) {
  return (
    <span>
      <span
        className={cn(
          "font-semibold",
          status.isOpen ? "text-[#4f9d3a]" : "text-[#dc2626]"
        )}
      >
        {status.isOpen ? "Open" : "Closed"}
      </span>
      {status.detail ? <span className="text-[#71717a]"> {status.detail}</span> : null}
    </span>
  )
}

function StarRating({ rating }: { rating: number }) {
  return (
    <span className="inline-flex items-center gap-0.5" aria-label={`${rating} stars`}>
      {Array.from({ length: 5 }, (_, index) => {
        const fillPercent = Math.max(0, Math.min(1, rating - index)) * 100

        return (
          <span key={index} className="relative inline-flex size-4">
            <Star
              className="absolute inset-0 size-4 text-[#d4d4d8]"
              fill="currentColor"
              strokeWidth={0}
              aria-hidden="true"
            />
            <span
              className="absolute inset-0 overflow-hidden text-[#f6b73c]"
              style={{ width: `${fillPercent}%` }}
            >
              <Star
                className="size-4"
                fill="currentColor"
                strokeWidth={0}
                aria-hidden="true"
              />
            </span>
          </span>
        )
      })}
    </span>
  )
}

type OpenStatus = {
  isOpen: boolean
  detail: string
  todayName: string
}

function getOpenStatus(business: {
  timeZoneId: string
  workingHours: PublicBusinessWorkingHour[]
}): OpenStatus {
  const now = getBusinessNow(business.timeZoneId)
  const todayHour = business.workingHours.find(
    (workingHour) => workingHour.dayOfWeek === now.dayName
  )

  if (!todayHour) {
    return { isOpen: false, detail: "", todayName: now.dayName }
  }

  const opensAt = parseMinutes(todayHour.opensAt)
  const closesAt = parseMinutes(todayHour.closesAt)

  if (now.minutes >= opensAt && now.minutes < closesAt) {
    return {
      isOpen: true,
      detail: `until ${formatClockTime(todayHour.closesAt)}`,
      todayName: now.dayName,
    }
  }

  return { isOpen: false, detail: "", todayName: now.dayName }
}

function getBusinessNow(timeZoneId: string) {
  const parts = new Intl.DateTimeFormat("en-US", {
    timeZone: timeZoneId,
    weekday: "long",
    hour: "2-digit",
    minute: "2-digit",
    hour12: false,
  }).formatToParts(new Date())
  const dayName = parts.find((part) => part.type === "weekday")?.value ?? "Monday"
  const hour = Number(parts.find((part) => part.type === "hour")?.value ?? "0")
  const minute = Number(parts.find((part) => part.type === "minute")?.value ?? "0")

  return { dayName, minutes: hour * 60 + minute }
}

function formatAddress(address: PublicBusiness["address"]) {
  return [address.addressLine, address.district, address.city]
    .filter(Boolean)
    .join(", ")
}

function formatClockTime(value: string) {
  const [hourText, minuteText] = value.split(":")
  const hour = Number(hourText)
  const minute = Number(minuteText)
  const normalizedHour = hour % 12 || 12
  const suffix = hour >= 12 ? "PM" : "AM"

  return `${normalizedHour}:${String(minute).padStart(2, "0")} ${suffix}`
}

function parseMinutes(value: string) {
  const [hourText, minuteText] = value.split(":")

  return Number(hourText) * 60 + Number(minuteText)
}

function formatRating(rating: number) {
  return rating > 0 ? rating.toFixed(1) : "0.0"
}

function formatReviewCount(reviewCount: number) {
  if (reviewCount >= 10000) {
    return "10,000+"
  }

  return new Intl.NumberFormat("en-US").format(reviewCount)
}
