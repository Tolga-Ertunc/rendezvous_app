import Link from "next/link"
import { ArrowRight, Building2, ListChecks, UsersRound } from "lucide-react"

import { Badge } from "@/components/ui/badge"
import { buttonVariants } from "@/components/ui/button"
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import type { BusinessDetail, OwnerBusiness } from "@/lib/auth-api"
import { cn } from "@/lib/utils"

type BusinessListProps = {
  title: string
  description: string
  businesses: OwnerBusiness[]
  emptyText: string
  detailHref: (businessId: string) => string
}

export function BusinessList({
  title,
  description,
  businesses,
  emptyText,
  detailHref,
}: BusinessListProps) {
  return (
    <Card>
      <CardHeader>
        <div className="flex items-center gap-2">
          <Building2 className="size-4 text-primary" aria-hidden="true" />
          <CardTitle>{title}</CardTitle>
        </div>
        <CardDescription>{description}</CardDescription>
      </CardHeader>
      <CardContent>
        {businesses.length > 0 ? (
          <div className="grid gap-3">
            {businesses.map((business) => (
              <div
                key={business.id}
                className="rounded-lg border border-border bg-background p-3"
              >
                <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                  <div className="min-w-0">
                    <p className="truncate text-sm font-medium text-foreground">
                      {business.name}
                    </p>
                    <p className="mt-1 text-xs text-muted-foreground">
                      {business.type} - {business.timeZoneId}
                    </p>
                  </div>
                  <div className="flex shrink-0 flex-wrap items-center gap-2">
                    <Badge variant="outline">{business.status}</Badge>
                    <Link
                      href={detailHref(business.id)}
                      className={cn(buttonVariants({ size: "sm", variant: "outline" }))}
                    >
                      View
                      <ArrowRight data-icon="inline-end" className="size-4" />
                    </Link>
                  </div>
                </div>
              </div>
            ))}
          </div>
        ) : (
          <p className="text-sm leading-6 text-muted-foreground">{emptyText}</p>
        )}
      </CardContent>
    </Card>
  )
}

export function BusinessDetailView({
  business,
  eyebrow,
}: {
  business: BusinessDetail
  eyebrow: string
}) {
  return (
    <div className="grid gap-4">
      <Card>
        <CardHeader>
          <div className="flex items-center gap-2">
            <Building2 className="size-4 text-primary" aria-hidden="true" />
            <CardTitle>{business.name}</CardTitle>
          </div>
          <CardDescription>{eyebrow}</CardDescription>
        </CardHeader>
        <CardContent>
          <dl className="grid gap-4 text-sm sm:grid-cols-3">
            <div className="space-y-1">
              <dt className="text-muted-foreground">Type</dt>
              <dd className="font-medium text-foreground">{business.type}</dd>
            </div>
            <div className="space-y-1">
              <dt className="text-muted-foreground">Status</dt>
              <dd>
                <Badge variant="outline">{business.status}</Badge>
              </dd>
            </div>
            <div className="space-y-1">
              <dt className="text-muted-foreground">Timezone</dt>
              <dd className="font-medium text-foreground">
                {business.timeZoneId}
              </dd>
            </div>
          </dl>
          {business.owner || business.serviceCount !== undefined ? (
            <dl className="mt-5 grid gap-4 border-t border-border pt-4 text-sm sm:grid-cols-4">
              {business.owner ? (
                <div className="space-y-1">
                  <dt className="text-muted-foreground">Owner</dt>
                  <dd className="break-all font-medium text-foreground">
                    {business.owner.fullName || "Name not set"}
                  </dd>
                  <dd className="text-xs text-muted-foreground">
                    {business.owner.email} · {business.owner.publicNumber}
                  </dd>
                </div>
              ) : null}
              <div className="space-y-1">
                <dt className="text-muted-foreground">Services</dt>
                <dd className="font-medium text-foreground">
                  {business.serviceCount ?? business.services.length}
                </dd>
              </div>
              <div className="space-y-1">
                <dt className="text-muted-foreground">Staff</dt>
                <dd className="font-medium text-foreground">
                  {business.staffCount ?? business.staffMembers.length}
                </dd>
              </div>
              <div className="space-y-1">
                <dt className="text-muted-foreground">Appointments</dt>
                <dd className="font-medium text-foreground">
                  {business.appointmentCount ?? "-"}
                </dd>
              </div>
            </dl>
          ) : null}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <div className="flex items-center gap-2">
            <UsersRound className="size-4 text-primary" aria-hidden="true" />
            <CardTitle>Staff</CardTitle>
          </div>
          <CardDescription>
            Read-only staff list for this business.
          </CardDescription>
        </CardHeader>
        <CardContent>
          {business.staffMembers.length > 0 ? (
            <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
              {business.staffMembers.map((staffMember) => (
                <div
                  key={staffMember.id}
                  className="rounded-lg border border-border bg-background p-3"
                >
                  <div className="flex items-center justify-between gap-3">
                    <div className="min-w-0">
                      <p className="truncate text-sm font-medium text-foreground">
                        {staffMember.displayName}
                      </p>
                      <p className="mt-1 break-all text-xs text-muted-foreground">
                        {staffMember.id}
                      </p>
                    </div>
                    <Badge variant={staffMember.isActive ? "default" : "outline"}>
                      {staffMember.isActive ? "Active" : "Inactive"}
                    </Badge>
                  </div>
                </div>
              ))}
            </div>
          ) : (
            <p className="text-sm leading-6 text-muted-foreground">
              No staff members are defined for this business.
            </p>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <div className="flex items-center gap-2">
            <ListChecks className="size-4 text-primary" aria-hidden="true" />
            <CardTitle>Services</CardTitle>
          </div>
          <CardDescription>
            Read-only service list for this business.
          </CardDescription>
        </CardHeader>
        <CardContent>
          {business.services.length > 0 ? (
            <div className="overflow-hidden rounded-lg border border-border">
              <div className="hidden grid-cols-[minmax(0,1fr)_110px_130px_90px] gap-3 border-b border-border bg-muted/45 px-3 py-2 text-xs font-medium text-muted-foreground md:grid">
                <div>Name</div>
                <div>Duration</div>
                <div>Price</div>
                <div>Status</div>
              </div>
              <div className="divide-y divide-border">
                {business.services.map((service) => (
                  <div
                    key={service.id}
                    className="grid gap-2 px-3 py-3 text-sm md:grid-cols-[minmax(0,1fr)_110px_130px_90px] md:items-center md:gap-3"
                  >
                    <div className="min-w-0">
                      <p className="truncate font-medium text-foreground">
                        {service.name}
                      </p>
                      <p className="mt-1 break-all text-xs text-muted-foreground md:hidden">
                        {service.id}
                      </p>
                    </div>
                    <div className="text-muted-foreground">
                      {service.durationMinutes} min
                    </div>
                    <div className="font-medium text-foreground">
                      {service.basePriceAmount} {service.currencyCode}
                    </div>
                    <div>
                      <Badge variant={service.isActive ? "default" : "outline"}>
                        {service.isActive ? "Active" : "Inactive"}
                      </Badge>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          ) : (
            <p className="text-sm leading-6 text-muted-foreground">
              No services are defined for this business.
            </p>
          )}
        </CardContent>
      </Card>
    </div>
  )
}
