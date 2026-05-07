"use client"

import type { ComponentType } from "react"
import { useEffect, useState } from "react"
import Link from "next/link"
import { useParams, useRouter } from "next/navigation"
import {
  CalendarDays,
  Clock,
  Images,
  ListChecks,
  Settings,
  Store,
  UsersRound,
} from "lucide-react"

import { OwnerAvailabilityExceptionsPanel } from "@/components/dashboard/availability-exceptions-panel"
import { DashboardShell } from "@/components/dashboard/dashboard-shell"
import { OwnerAppointmentRequestsPanel } from "@/components/dashboard/owner-appointment-requests-panel"
import {
  OwnerAppointmentsPanel,
  OwnerBusinessHoursPanel,
  OwnerBusinessProfilePanel,
  OwnerServicesPanel,
  OwnerStaffHoursPanel,
  OwnerStaffPanel,
} from "@/components/dashboard/owner-management-panels"
import { Badge } from "@/components/ui/badge"
import { buttonVariants } from "@/components/ui/button"
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import { ApiError } from "@/lib/api-client"
import { getOwnerBusiness } from "@/lib/auth-api"
import type { BusinessDetail } from "@/lib/auth-api"
import { clearAuthTokens, getAccessToken } from "@/lib/auth-storage"
import { cn } from "@/lib/utils"

type OwnerBusinessSection =
  | "overview"
  | "profile"
  | "services"
  | "staff"
  | "hours"
  | "exceptions"
  | "appointments"

const sectionCopy = {
  overview: ["Overview", "Business summary and management links."],
  profile: ["Business profile", "Manage the public business page content."],
  services: ["Services", "Manage service names, categories, durations, and prices."],
  staff: ["Staff", "Manage staff display names and active state."],
  hours: ["Working hours", "Manage business and staff working hours."],
  exceptions: ["Scheduling exceptions", "Manage closures, holidays, and leave."],
  appointments: ["Appointments", "Review requests and approved appointments."],
} satisfies Record<OwnerBusinessSection, [string, string]>

export function OwnerBusinessSectionPage({
  section,
}: {
  section: OwnerBusinessSection
}) {
  const params = useParams<{ id: string }>()
  const router = useRouter()
  const [business, setBusiness] = useState<BusinessDetail | null>(null)
  const [isLoading, setIsLoading] = useState(true)

  async function refreshBusiness() {
    setBusiness(await getOwnerBusiness(params.id))
  }

  useEffect(() => {
    if (!getAccessToken()) {
      router.replace("/")
      return
    }

    let isMounted = true

    async function loadBusiness() {
      setIsLoading(true)

      try {
        const nextBusiness = await getOwnerBusiness(params.id)
        if (isMounted) {
          setBusiness(nextBusiness)
        }
      } catch (caughtError) {
        if (caughtError instanceof ApiError && caughtError.status === 401) {
          clearAuthTokens()
        }
        router.replace("/")
      } finally {
        if (isMounted) {
          setIsLoading(false)
        }
      }
    }

    loadBusiness()

    return () => {
      isMounted = false
    }
  }, [params.id, router])

  const [title, description] = sectionCopy[section]

  if (isLoading) {
    return (
      <DashboardShell title={title} description={description}>
        <Card className="mx-auto w-full max-w-xl">
          <CardHeader>
            <CardTitle>Loading business</CardTitle>
            <CardDescription>Checking owner access.</CardDescription>
          </CardHeader>
        </Card>
      </DashboardShell>
    )
  }

  if (!business) {
    return null
  }

  return (
    <DashboardShell title={title} description={description}>
      <div className="grid gap-4">
        <OwnerBusinessNav businessId={business.id} active={section} />
        {section === "overview" ? (
          <OwnerOverview business={business} />
        ) : section === "profile" ? (
          <OwnerBusinessProfilePanel
            business={business}
            onChanged={refreshBusiness}
          />
        ) : section === "services" ? (
          <OwnerServicesPanel business={business} onChanged={refreshBusiness} />
        ) : section === "staff" ? (
          <OwnerStaffPanel business={business} onChanged={refreshBusiness} />
        ) : section === "hours" ? (
          <div className="grid gap-4">
            <OwnerBusinessHoursPanel businessId={business.id} />
            <OwnerStaffHoursPanel business={business} />
          </div>
        ) : section === "exceptions" ? (
          <OwnerAvailabilityExceptionsPanel business={business} />
        ) : (
          <div className="grid gap-4">
            <OwnerAppointmentRequestsPanel businessId={business.id} />
            <OwnerAppointmentsPanel businessId={business.id} />
          </div>
        )}
      </div>
    </DashboardShell>
  )
}

function OwnerOverview({ business }: { business: BusinessDetail }) {
  return (
    <div className="grid gap-4">
      <Card>
        <CardHeader>
          <div className="flex items-center gap-2">
            <Settings className="size-4 text-primary" aria-hidden="true" />
            <CardTitle>{business.name}</CardTitle>
          </div>
          <CardDescription>Owner management summary.</CardDescription>
        </CardHeader>
        <CardContent>
          <dl className="grid gap-4 text-sm sm:grid-cols-4">
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
              <dt className="text-muted-foreground">Services</dt>
              <dd className="font-medium text-foreground">
                {business.services.length}
              </dd>
            </div>
            <div className="space-y-1">
              <dt className="text-muted-foreground">Staff</dt>
              <dd className="font-medium text-foreground">
                {business.staffMembers.length}
              </dd>
            </div>
          </dl>
        </CardContent>
      </Card>
      <div className="grid gap-3 md:grid-cols-3">
        <OverviewAction
          href={`/owner/businesses/${business.id}/profile`}
          title="Profile"
          description="Edit public page content."
          icon={Store}
        />
        <OverviewAction
          href={`/owner/businesses/${business.id}/services`}
          title="Services"
          description="Edit service catalog."
          icon={ListChecks}
        />
        <OverviewAction
          href={`/owner/businesses/${business.id}/staff`}
          title="Staff"
          description="Manage staff records."
          icon={UsersRound}
        />
        <OverviewAction
          href={`/owner/businesses/${business.id}/appointments`}
          title="Appointments"
          description="Review requests."
          icon={CalendarDays}
        />
      </div>
    </div>
  )
}

function OverviewAction({
  href,
  title,
  description,
  icon: Icon,
}: {
  href: string
  title: string
  description: string
  icon: ComponentType<{ className?: string }>
}) {
  return (
    <Card>
      <CardHeader>
        <div className="flex items-center gap-2">
          <Icon className="size-4 text-primary" />
          <CardTitle>{title}</CardTitle>
        </div>
        <CardDescription>{description}</CardDescription>
      </CardHeader>
      <CardContent>
        <Link href={href} className={cn(buttonVariants({ variant: "outline" }))}>
          Open
        </Link>
      </CardContent>
    </Card>
  )
}

function OwnerBusinessNav({
  businessId,
  active,
}: {
  businessId: string
  active: OwnerBusinessSection
}) {
  const links = [
    ["overview", "Overview", Settings],
    ["profile", "Profile", Store],
    ["services", "Services", ListChecks],
    ["staff", "Staff", UsersRound],
    ["hours", "Hours", Clock],
    ["exceptions", "Exceptions", CalendarDays],
    ["appointments", "Appointments", Images],
  ] as const

  return (
    <div className="border-b border-[#e5e7eb]">
      <div className="flex flex-wrap gap-7 overflow-x-auto">
        {links.map(([key, label, Icon]) => (
          <Link
            key={key}
            href={`/owner/businesses/${businessId}/${key}`}
            className={cn(
              "relative flex h-14 shrink-0 items-center gap-2 text-base font-semibold text-[#71717a]",
              active === key && "text-[#111111]"
            )}
          >
            <Icon className="size-4" aria-hidden="true" />
            {label}
            <span
              className={cn(
                "absolute bottom-0 left-0 h-[3px] w-full bg-transparent",
                active === key && "bg-[#111111]"
              )}
            />
          </Link>
        ))}
      </div>
    </div>
  )
}
