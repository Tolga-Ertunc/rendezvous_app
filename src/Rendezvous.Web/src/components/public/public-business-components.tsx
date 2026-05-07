"use client"

import { useEffect, useMemo, useState } from "react"
import Image from "next/image"
import Link from "next/link"
import {
  ArrowLeft,
  Baby,
  Bus,
  Check,
  ChevronDown,
  Clock,
  CreditCard,
  Heart,
  Leaf,
  MapPin,
  PawPrint,
  Share2,
  Sparkles,
  Star,
} from "lucide-react"

import { Avatar, AvatarFallback } from "@/components/ui/avatar"
import { Badge } from "@/components/ui/badge"
import { Button, buttonVariants } from "@/components/ui/button"
import { Separator } from "@/components/ui/separator"
import type {
  PublicBusiness,
  PublicBusinessDetail,
  PublicBusinessReview,
  PublicBusinessService,
  PublicBusinessWorkingHour,
} from "@/lib/public-api"
import { cn } from "@/lib/utils"

const sectionTabs = [
  { id: "photos", label: "Photos" },
  { id: "services", label: "Services" },
  { id: "team", label: "Team" },
  { id: "reviews", label: "Reviews" },
  { id: "about", label: "About" },
]

const dayOrder = [
  "Monday",
  "Tuesday",
  "Wednesday",
  "Thursday",
  "Friday",
  "Saturday",
  "Sunday",
]

type PublicBusinessListProps = {
  businesses: PublicBusiness[]
}

export function PublicBusinessList({ businesses }: PublicBusinessListProps) {
  if (businesses.length === 0) {
    return (
      <p className="rounded-lg border border-border bg-background px-4 py-3 text-sm text-muted-foreground">
        No active businesses are available.
      </p>
    )
  }

  return (
    <div className="grid gap-3 md:grid-cols-2">
      {businesses.map((business) => (
        <div
          key={business.id}
          className="rounded-lg border border-border bg-background p-5"
        >
          <div className="flex items-start justify-between gap-3">
            <div className="min-w-0 space-y-2">
              <h2 className="truncate text-lg font-semibold text-foreground">
                {business.name}
              </h2>
              <p className="text-sm text-muted-foreground">
                {business.type} - {business.timeZoneId}
              </p>
            </div>
            <Badge variant="outline">{business.type}</Badge>
          </div>
          <div className="mt-4 flex flex-wrap gap-2">
            {business.services.map((service) => (
              <Badge
                key={service.id}
                variant="secondary"
                className="max-w-full truncate"
              >
                {service.name} - {service.durationMinutes} min
              </Badge>
            ))}
          </div>
          <Link
            href={`/businesses/${business.id}`}
            className={cn(buttonVariants({ variant: "outline" }), "mt-5 w-full")}
          >
            View business
          </Link>
        </div>
      ))}
    </div>
  )
}

export function PublicBusinessDetailView({
  business,
}: {
  business: PublicBusinessDetail
}) {
  const [activeSection, setActiveSection] = useState("services")
  const [activeCategory, setActiveCategory] = useState("Featured")
  const [expandedReviewIds, setExpandedReviewIds] = useState<Set<string>>(
    () => new Set()
  )
  const status = useMemo(() => getOpenStatus(business), [business])
  const categories = useMemo(() => getServiceCategories(business.services), [
    business.services,
  ])
  const selectedCategory = categories.includes(activeCategory)
    ? activeCategory
    : categories[0] ?? "Featured"
  const visibleServices = useMemo(
    () =>
      selectedCategory === "Featured"
        ? business.services.slice(0, 4)
        : business.services.filter(
            (service) => service.categoryName === selectedCategory
          ),
    [business.services, selectedCategory]
  )
  const orderedWorkingHours = useMemo(
    () => orderWorkingHours(business.workingHours),
    [business.workingHours]
  )

  useEffect(() => {
    function updateActiveSection() {
      const pageBottom =
        window.scrollY + window.innerHeight >= document.documentElement.scrollHeight - 8

      if (pageBottom) {
        setActiveSection("about")
        return
      }

      const sections = sectionTabs
        .map((section) => document.getElementById(section.id))
        .filter((section): section is HTMLElement => section !== null)
      const currentSection = sections
        .filter((section) => section.getBoundingClientRect().top <= 120)
        .at(-1)

      if (currentSection?.id) {
        setActiveSection(currentSection.id)
      }
    }

    updateActiveSection()
    window.addEventListener("scroll", updateActiveSection, { passive: true })

    return () => window.removeEventListener("scroll", updateActiveSection)
  }, [])

  function scrollToSection(sectionId: string) {
    setActiveSection(sectionId)
    document
      .getElementById(sectionId)
      ?.scrollIntoView({ behavior: "smooth", block: "start" })
  }

  function toggleReview(reviewId: string) {
    setExpandedReviewIds((current) => {
      const next = new Set(current)

      if (next.has(reviewId)) {
        next.delete(reviewId)
      } else {
        next.add(reviewId)
      }

      return next
    })
  }

  return (
    <main className="min-h-svh min-w-[1180px] bg-[#fbfbfa] text-[#111111]">
      <div className="mx-auto w-full max-w-[1220px] px-8 pb-20 pt-8">
        <HeroSection business={business} />

        <section className="mt-9 space-y-4">
          <p className="text-sm text-[#71717a]">
            Home <span className="mx-2">·</span> {business.type}s{" "}
            <span className="mx-2">·</span> {business.address.city || "Istanbul"}{" "}
            <span className="mx-2">·</span> {business.address.district || "Local"}{" "}
            <span className="mx-2">·</span> {business.name}
          </p>
          <div className="flex items-start justify-between gap-8">
            <div className="min-w-0 space-y-3">
              <h1 className="break-words text-5xl font-extrabold leading-tight tracking-normal">
                {business.name}
              </h1>
              <div className="flex flex-wrap items-center gap-x-3 gap-y-2 text-lg">
                <span className="font-bold">
                  {formatRating(business.reviewSummary.averageRating)}
                </span>
                <StarRating
                  rating={business.reviewSummary.averageRating}
                  sizeClassName="size-5"
                />
                <span className="font-semibold text-[#635bff]">
                  ({formatReviewCount(business.reviewSummary.reviewCount)})
                </span>
                <span className="font-bold">·</span>
                <OpenStatusInline status={status} />
                <span className="font-bold">·</span>
                <span className="text-[#71717a]">
                  {formatAddress(business.address)}
                </span>
                <span className="font-semibold text-[#635bff]">
                  Get directions
                </span>
              </div>
            </div>
          </div>
        </section>
      </div>

      <div className="sticky top-0 z-30 border-b border-[#e5e7eb] bg-[#fbfbfa]/95 backdrop-blur">
        <div className="mx-auto flex w-full max-w-[1220px] gap-7 overflow-x-auto px-8">
          {sectionTabs.map((section) => (
            <button
              key={section.id}
              type="button"
              className={cn(
                "relative h-14 shrink-0 text-base font-semibold text-[#71717a] transition-colors",
                activeSection === section.id && "text-[#111111]"
              )}
              onClick={() => scrollToSection(section.id)}
            >
              {section.label}
              <span
                className={cn(
                  "absolute bottom-0 left-0 h-[3px] w-full bg-transparent",
                  activeSection === section.id && "bg-[#111111]"
                )}
              />
            </button>
          ))}
        </div>
      </div>

      <div className="mx-auto grid w-full max-w-[1220px] grid-cols-[minmax(0,1fr)_420px] gap-14 px-8 py-10">
        <div className="min-w-0 space-y-20">
          <ServicesSection
            categories={categories}
            activeCategory={selectedCategory}
            onCategoryChange={setActiveCategory}
            services={visibleServices}
          />
          <TeamSection business={business} />
          <ReviewsSection
            business={business}
            expandedReviewIds={expandedReviewIds}
            onToggleReview={toggleReview}
          />
          <AboutSection
            business={business}
            workingHours={orderedWorkingHours}
            todayName={status.todayName}
          />
        </div>

        <aside className="sticky top-24 h-fit">
          <BookingPanel
            business={business}
            status={status}
            workingHours={orderedWorkingHours}
          />
        </aside>
      </div>
    </main>
  )
}

function HeroSection({ business }: { business: PublicBusinessDetail }) {
  const photos = business.photos.filter((photo) => photo.imageUrl.trim())
  const mainPhoto = photos[0]
  const secondaryPhotos = photos.slice(1, 3)
  const photoCount = Math.max(photos.length, 4)

  return (
    <section id="photos" className="scroll-mt-24">
      <div className="relative grid h-[430px] grid-cols-[2fr_1fr] gap-5 overflow-hidden rounded-lg">
        <Link
          href="/"
          className="absolute left-5 top-5 z-10 inline-flex size-12 items-center justify-center rounded-full bg-white text-[#111111] shadow-sm ring-1 ring-black/5"
          aria-label="Back"
        >
          <ArrowLeft className="size-5" aria-hidden="true" />
        </Link>
        <div className="absolute right-5 top-5 z-10 flex gap-3">
          <button
            type="button"
            className="inline-flex size-12 items-center justify-center rounded-full bg-white text-[#111111] shadow-sm ring-1 ring-black/5"
            aria-label="Share"
          >
            <Share2 className="size-5" aria-hidden="true" />
          </button>
          <button
            type="button"
            className="inline-flex size-12 items-center justify-center rounded-full bg-white text-[#111111] shadow-sm ring-1 ring-black/5"
            aria-label="Favorite"
          >
            <Heart className="size-5" aria-hidden="true" />
          </button>
        </div>
        <PhotoSurface photo={mainPhoto} businessName={business.name} isLarge />
        <div className="grid grid-rows-2 gap-5">
          <PhotoSurface photo={secondaryPhotos[0]} businessName={business.name} />
          <div className="relative overflow-hidden rounded-lg">
            <PhotoSurface photo={secondaryPhotos[1]} businessName={business.name} />
            <span className="absolute bottom-5 right-5 rounded-full bg-black/80 px-4 py-2 text-sm font-semibold text-white">
              1/{photoCount}
            </span>
          </div>
        </div>
      </div>
    </section>
  )
}

function PhotoSurface({
  photo,
  businessName,
  isLarge = false,
}: {
  photo?: { imageUrl: string; altText: string }
  businessName: string
  isLarge?: boolean
}) {
  if (photo?.imageUrl) {
    return (
      <div className="relative size-full overflow-hidden rounded-lg">
        <Image
          src={photo.imageUrl}
          alt={photo.altText || businessName}
          fill
          sizes={isLarge ? "790px" : "390px"}
          className="object-cover"
          unoptimized
        />
      </div>
    )
  }

  return (
    <div
      className={cn(
        "relative size-full overflow-hidden rounded-lg bg-[#e9ecef]",
        isLarge ? "min-h-[430px]" : "min-h-[205px]"
      )}
      aria-label={businessName}
    >
      <div className="absolute inset-x-10 bottom-12 h-28 rounded-t-lg bg-[#cfd4da]" />
      <div className="absolute bottom-12 left-20 h-36 w-28 rounded-t-full bg-[#b8bec7]" />
      <div className="absolute bottom-12 right-24 h-32 w-24 rounded-t-full bg-[#d7dbe0]" />
      <div className="absolute inset-x-0 bottom-0 h-14 bg-[#dfe3e7]" />
    </div>
  )
}

function ServicesSection({
  categories,
  activeCategory,
  onCategoryChange,
  services,
}: {
  categories: string[]
  activeCategory: string
  onCategoryChange: (category: string) => void
  services: PublicBusinessService[]
}) {
  return (
    <section id="services" className="scroll-mt-24 space-y-8">
      <h2 className="text-4xl font-bold tracking-normal">Services</h2>
      <div className="flex items-center gap-8 overflow-x-auto pb-1">
        {categories.map((category) => (
          <button
            key={category}
            type="button"
            className={cn(
              "h-12 shrink-0 whitespace-nowrap text-xl font-bold transition-colors",
              activeCategory === category
                ? "rounded-full bg-[#111111] px-7 text-white"
                : "text-[#111111] hover:text-[#635bff]"
            )}
            onClick={() => onCategoryChange(category)}
          >
            {category}
          </button>
        ))}
      </div>
      <div className="grid gap-5">
        {services.map((service) => (
          <div
            key={service.id}
            className="flex min-h-[154px] items-center justify-between gap-8 rounded-lg border border-[#e5e7eb] bg-white px-8 py-7"
          >
            <div className="min-w-0 space-y-3">
              <h3 className="truncate text-2xl font-medium">
                {service.name}
              </h3>
              <p className="text-xl text-[#71717a]">
                {formatDuration(service.durationMinutes)}
              </p>
              <p className="text-xl font-medium">
                from{" "}
                {formatCurrency(service.basePriceAmount, service.currencyCode)}
              </p>
            </div>
            <Button
              type="button"
              variant="outline"
              className="h-[52px] min-w-[104px] rounded-full border-[#d4d4d8] bg-white px-7 text-xl font-medium text-[#111111] hover:bg-[#f4f4f5]"
            >
              Book
            </Button>
          </div>
        ))}
      </div>
      <Button
        type="button"
        variant="outline"
        className="h-14 rounded-full border-[#d4d4d8] bg-white px-8 text-lg font-bold"
      >
        See all
      </Button>
    </section>
  )
}

function BookingPanel({
  business,
  status,
  workingHours,
}: {
  business: PublicBusinessDetail
  status: OpenStatus
  workingHours: PublicBusinessWorkingHour[]
}) {
  return (
    <div className="overflow-hidden rounded-2xl border border-[#e5e7eb] bg-white shadow-[0_8px_28px_rgba(17,17,17,0.06)]">
      <div className="space-y-8 p-8">
        <div className="space-y-5">
          <h2 className="break-words text-5xl font-extrabold leading-[0.98] tracking-normal">
            {business.name}
          </h2>
          <div className="flex items-center gap-3">
            <span className="text-3xl font-bold">
              {formatRating(business.reviewSummary.averageRating)}
            </span>
            <StarRating
              rating={business.reviewSummary.averageRating}
              sizeClassName="size-8"
            />
            <span className="text-2xl font-bold text-[#635bff]">
              ({formatReviewCount(business.reviewSummary.reviewCount)})
            </span>
          </div>
        </div>
        <Button
          type="button"
          className="h-16 w-full rounded-full bg-[#111111] text-xl font-bold text-white hover:bg-[#27272a]"
        >
          Book now
        </Button>
      </div>
      <Separator />
      <div className="space-y-8 p-8">
        <div className="space-y-6">
          <div className="flex items-start gap-4">
            <Clock className="mt-0.5 size-7 shrink-0" aria-hidden="true" />
            <div className="min-w-0 flex-1">
              <div className="flex items-center justify-between gap-4 text-xl">
                <OpenStatusInline status={status} />
                <ChevronDown className="size-5 shrink-0" aria-hidden="true" />
              </div>
            </div>
          </div>
          <OpeningTimesList
            workingHours={workingHours}
            todayName={status.todayName}
            isLarge
          />
        </div>
        <div className="flex items-start gap-4">
          <MapPin className="mt-1 size-7 shrink-0" aria-hidden="true" />
          <div className="space-y-1 text-xl leading-7">
            <p>{formatAddress(business.address)}</p>
            <p className="font-semibold text-[#635bff]">Get directions</p>
          </div>
        </div>
      </div>
    </div>
  )
}

function TeamSection({ business }: { business: PublicBusinessDetail }) {
  const rating = business.reviewSummary.averageRating || 5

  return (
    <section id="team" className="scroll-mt-24 space-y-10">
      <h2 className="text-4xl font-bold tracking-normal">Team</h2>
      <div className="grid grid-cols-4 gap-x-10 gap-y-14">
        {business.staffMembers.map((staffMember, index) => (
          <div key={staffMember.id} className="space-y-4 text-center">
            <div className="relative mx-auto size-40 rounded-full border border-[#e5e7eb] bg-[#f1f2f5]">
              <Avatar className="size-full bg-[#f1f2f5]">
                <AvatarFallback className="text-5xl text-[#635bff]">
                  {getInitial(staffMember.displayName)}
                </AvatarFallback>
              </Avatar>
              <span className="absolute -bottom-4 left-1/2 inline-flex -translate-x-1/2 items-center gap-1 rounded-full border border-[#ddd8ff] bg-white px-4 py-2 text-xl font-bold shadow-sm">
                <Star className="size-5 fill-[#f6b73c] text-[#f6b73c]" />
                {(rating - (index % 2 === 0 ? 0 : 0.1)).toFixed(1)}
              </span>
            </div>
            <div className="pt-4">
              <h3 className="text-2xl font-medium">{staffMember.displayName}</h3>
              <p className="mt-1 text-xl text-[#71717a]">
                {index === 0 ? "Head Barber" : "Senior Barber"}
              </p>
            </div>
          </div>
        ))}
      </div>
      <Button
        type="button"
        variant="outline"
        className="h-14 rounded-full border-[#d4d4d8] bg-white px-8 text-lg font-bold"
      >
        See all
      </Button>
    </section>
  )
}

function ReviewsSection({
  business,
  expandedReviewIds,
  onToggleReview,
}: {
  business: PublicBusinessDetail
  expandedReviewIds: Set<string>
  onToggleReview: (reviewId: string) => void
}) {
  return (
    <section id="reviews" className="scroll-mt-24 space-y-10">
      <div className="space-y-5">
        <h2 className="text-4xl font-bold tracking-normal">Reviews</h2>
        <StarRating
          rating={business.reviewSummary.averageRating}
          sizeClassName="size-10"
        />
        <p className="text-2xl font-bold">
          {formatRating(business.reviewSummary.averageRating)}{" "}
          <span className="text-[#635bff]">
            ({formatReviewCount(business.reviewSummary.reviewCount)})
          </span>
        </p>
      </div>
      <div className="grid grid-cols-2 gap-x-16 gap-y-16">
        {business.reviews.map((review) => (
          <ReviewItem
            key={review.id}
            review={review}
            isExpanded={expandedReviewIds.has(review.id)}
            onToggle={() => onToggleReview(review.id)}
          />
        ))}
      </div>
      <Button
        type="button"
        variant="outline"
        className="h-14 rounded-full border-[#d4d4d8] bg-white px-8 text-lg font-bold"
      >
        See all
      </Button>
    </section>
  )
}

function ReviewItem({
  review,
  isExpanded,
  onToggle,
}: {
  review: PublicBusinessReview
  isExpanded: boolean
  onToggle: () => void
}) {
  const isLong = review.comment.length > 130

  return (
    <article className="space-y-5">
      <div className="flex items-center gap-4">
        <Avatar className="size-20 bg-[#efefff]">
          <AvatarFallback className="text-3xl text-[#635bff]">
            {review.customerInitial || getInitial(review.customerName)}
          </AvatarFallback>
        </Avatar>
        <div>
          <h3 className="text-2xl font-medium">{review.customerName}</h3>
          <p className="text-lg text-[#71717a]">
            {formatReviewDate(review.createdAtUtc)}
          </p>
        </div>
      </div>
      <StarRating rating={review.rating} sizeClassName="size-5" />
      <p
        className={cn(
          "text-xl leading-8",
          !isExpanded && "line-clamp-2"
        )}
      >
        {review.comment}
      </p>
      {isLong ? (
        <button
          type="button"
          className="text-base font-semibold text-[#635bff]"
          onClick={onToggle}
        >
          {isExpanded ? "Show less" : "Read more"}
        </button>
      ) : null}
    </article>
  )
}

function AboutSection({
  business,
  workingHours,
  todayName,
}: {
  business: PublicBusinessDetail
  workingHours: PublicBusinessWorkingHour[]
  todayName: string
}) {
  return (
    <section id="about" className="scroll-mt-24 space-y-10 pb-24">
      <h2 className="text-4xl font-bold tracking-normal">About</h2>
      {business.description ? (
        <p className="max-w-3xl text-xl leading-8 text-[#3f3f46]">
          {business.description}
        </p>
      ) : null}
      <div className="grid grid-cols-2 gap-16">
        <div className="space-y-7">
          <h3 className="text-3xl font-bold">Opening times</h3>
          <OpeningTimesList workingHours={workingHours} todayName={todayName} />
        </div>
        <div className="space-y-7">
          <h3 className="text-3xl font-bold">Additional information</h3>
          <div className="grid gap-5">
            {business.additionalInformation.map((item) => (
              <div key={item} className="flex items-center gap-4 text-xl">
                <AdditionalInformationIcon item={item} />
                <span>{item}</span>
              </div>
            ))}
          </div>
        </div>
      </div>
    </section>
  )
}

function OpeningTimesList({
  workingHours,
  todayName,
  isLarge = false,
}: {
  workingHours: PublicBusinessWorkingHour[]
  todayName: string
  isLarge?: boolean
}) {
  return (
    <div className={cn("space-y-5", isLarge && "space-y-6")}>
      {dayOrder.map((day) => {
        const workingHour = workingHours.find((hour) => hour.dayOfWeek === day)
        const isClosed = !workingHour
        const isToday = day === todayName

        return (
          <div
            key={day}
            className={cn(
              "grid grid-cols-[12px_minmax(0,1fr)_auto] items-center gap-3",
              isLarge ? "text-[22px]" : "text-lg",
              isClosed && "text-[#a1a1aa]"
            )}
          >
            <span
              className={cn(
                "size-3 rounded-full",
                isClosed ? "bg-[#d4d4d8]" : "bg-[#4f9d3a]"
              )}
            />
            <span className={cn(isToday && "font-bold")}>{day}</span>
            <span
              className={cn(
                "text-right tabular-nums",
                isToday && !isClosed && "font-bold"
              )}
            >
              {workingHour
                ? `${formatClockTime(workingHour.opensAt)} - ${formatClockTime(
                    workingHour.closesAt
                  )}`
                : "Closed"}
            </span>
          </div>
        )
      })}
    </div>
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

function StarRating({
  rating,
  sizeClassName,
}: {
  rating: number
  sizeClassName: string
}) {
  return (
    <span className="inline-flex items-center gap-1" aria-label={`${rating} stars`}>
      {Array.from({ length: 5 }, (_, index) => {
        const fillPercent = Math.max(0, Math.min(1, rating - index)) * 100

        return (
          <span key={index} className={cn("relative inline-flex", sizeClassName)}>
            <Star
              className={cn("absolute inset-0 text-[#d4d4d8]", sizeClassName)}
              fill="currentColor"
              strokeWidth={0}
              aria-hidden="true"
            />
            <span
              className="absolute inset-0 overflow-hidden text-[#f6b73c]"
              style={{ width: `${fillPercent}%` }}
            >
              <Star
                className={sizeClassName}
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

function AdditionalInformationIcon({ item }: { item: string }) {
  const className = "size-6 shrink-0"

  if (item === "Instant Confirmation") {
    return <Check className={className} aria-hidden="true" />
  }

  if (item === "Pay by app") {
    return <CreditCard className={className} aria-hidden="true" />
  }

  if (item === "Pet-friendly") {
    return <PawPrint className={className} aria-hidden="true" />
  }

  if (item === "Kid-friendly") {
    return <Baby className={className} aria-hidden="true" />
  }

  if (item === "Near public transport") {
    return <Bus className={className} aria-hidden="true" />
  }

  if (item.includes("Organic") || item.includes("Vegan")) {
    return <Leaf className={className} aria-hidden="true" />
  }

  return <Sparkles className={className} aria-hidden="true" />
}

type OpenStatus = {
  isOpen: boolean
  detail: string
  todayName: string
}

function getOpenStatus(business: PublicBusinessDetail): OpenStatus {
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

function getServiceCategories(services: PublicBusinessService[]) {
  return Array.from(
    new Set(["Featured", ...services.map((service) => service.categoryName)])
  ).filter(Boolean)
}

function orderWorkingHours(workingHours: PublicBusinessWorkingHour[]) {
  return [...workingHours].sort(
    (left, right) => dayOrder.indexOf(left.dayOfWeek) - dayOrder.indexOf(right.dayOfWeek)
  )
}

function formatAddress(address: PublicBusinessDetail["address"]) {
  return [address.addressLine, address.district, address.city]
    .filter(Boolean)
    .join(", ")
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

function formatReviewDate(value: string) {
  return new Intl.DateTimeFormat("en-US", {
    month: "short",
    day: "numeric",
    year: "numeric",
    hour: "numeric",
    minute: "2-digit",
  }).format(new Date(value))
}

function getInitial(value: string) {
  return value.trim().charAt(0).toUpperCase() || "R"
}
