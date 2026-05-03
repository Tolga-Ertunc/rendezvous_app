import Link from "next/link"
import { ArrowLeft, ArrowRight, Building2, Clock, ListChecks } from "lucide-react"

import { BookingAvailabilityPanel } from "@/components/public/booking-availability-panel"
import { Badge } from "@/components/ui/badge"
import { buttonVariants } from "@/components/ui/button"
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import type {
  PublicBusiness,
  PublicBusinessDetail,
} from "@/lib/public-api"
import { cn } from "@/lib/utils"

type PublicBusinessListProps = {
  businesses: PublicBusiness[]
}

export function PublicBusinessList({ businesses }: PublicBusinessListProps) {
  if (businesses.length === 0) {
    return (
      <Card className="mx-auto w-full max-w-xl">
        <CardHeader>
          <CardTitle>No businesses yet</CardTitle>
          <CardDescription>
            Approved businesses will appear here when they are available.
          </CardDescription>
        </CardHeader>
      </Card>
    )
  }

  return (
    <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
      {businesses.map((business) => (
        <Card key={business.id} className="min-w-0">
          <CardHeader>
            <div className="flex items-start justify-between gap-3">
              <div className="min-w-0 space-y-2">
                <div className="flex items-center gap-2">
                  <Building2
                    className="size-4 shrink-0 text-primary"
                    aria-hidden="true"
                  />
                  <CardTitle className="truncate">{business.name}</CardTitle>
                </div>
                <CardDescription>
                  {business.type} - {business.timeZoneId}
                </CardDescription>
              </div>
              <Badge variant="outline">{business.type}</Badge>
            </div>
          </CardHeader>
          <CardContent>
            <Link
              href={`/businesses/${business.id}`}
              className={cn(buttonVariants({ variant: "outline" }), "w-full")}
            >
              View services
              <ArrowRight data-icon="inline-end" className="size-4" />
            </Link>
          </CardContent>
        </Card>
      ))}
    </div>
  )
}

export function PublicBusinessDetailView({
  business,
}: {
  business: PublicBusinessDetail
}) {
  return (
    <div className="grid gap-4">
      <Card>
        <CardHeader>
          <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
            <div className="min-w-0 space-y-2">
              <div className="flex items-center gap-2">
                <Building2
                  className="size-4 shrink-0 text-primary"
                  aria-hidden="true"
                />
                <CardTitle className="truncate">{business.name}</CardTitle>
              </div>
              <CardDescription>
                {business.type} - {business.timeZoneId}
              </CardDescription>
            </div>
            <Link
              href="/businesses"
              className={cn(buttonVariants({ variant: "outline", size: "sm" }))}
            >
              <ArrowLeft data-icon="inline-start" className="size-4" />
              Businesses
            </Link>
          </div>
        </CardHeader>
      </Card>

      <BookingAvailabilityPanel
        businessId={business.id}
        services={business.services}
      />

      <Card>
        <CardHeader>
          <div className="flex items-center gap-2">
            <ListChecks className="size-4 text-primary" aria-hidden="true" />
            <CardTitle>Services</CardTitle>
          </div>
          <CardDescription>
            Active services and current base prices.
          </CardDescription>
        </CardHeader>
        <CardContent>
          {business.services.length > 0 ? (
            <div className="overflow-hidden rounded-lg border border-border">
              <div className="hidden grid-cols-[minmax(0,1fr)_120px_130px] gap-3 border-b border-border bg-muted/45 px-3 py-2 text-xs font-medium text-muted-foreground md:grid">
                <div>Name</div>
                <div>Duration</div>
                <div>Price</div>
              </div>
              <div className="divide-y divide-border">
                {business.services.map((service) => (
                  <div
                    key={service.id}
                    className="grid gap-2 px-3 py-3 text-sm md:grid-cols-[minmax(0,1fr)_120px_130px] md:items-center md:gap-3"
                  >
                    <div className="min-w-0">
                      <p className="truncate font-medium text-foreground">
                        {service.name}
                      </p>
                    </div>
                    <div className="flex items-center gap-1.5 text-muted-foreground">
                      <Clock className="size-3.5" aria-hidden="true" />
                      {service.durationMinutes} min
                    </div>
                    <div className="font-medium text-foreground">
                      {service.basePriceAmount} {service.currencyCode}
                    </div>
                  </div>
                ))}
              </div>
            </div>
          ) : (
            <p className="text-sm leading-6 text-muted-foreground">
              This business does not have active public services yet.
            </p>
          )}
        </CardContent>
      </Card>
    </div>
  )
}
