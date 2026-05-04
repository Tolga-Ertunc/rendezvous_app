"use client"

import type { ComponentType } from "react"
import Link from "next/link"
import {
  BriefcaseBusiness,
  CalendarDays,
  ShieldCheck,
  UserRound,
  UsersRound,
} from "lucide-react"

import { AccountCard } from "@/components/dashboard/account-card"
import { ProtectedPage, hasActiveMembership } from "@/components/layout/protected-page"
import { buttonVariants } from "@/components/ui/button"
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import type { CurrentUser } from "@/lib/auth-api"
import { cn } from "@/lib/utils"

export default function DashboardPage() {
  return (
    <ProtectedPage
      title="Dashboard"
      description="Open the right workspace for your account."
    >
      {({ user }) => <DashboardLanding user={user} />}
    </ProtectedPage>
  )
}

function DashboardLanding({ user }: { user: CurrentUser }) {
  const isEmployee = hasActiveMembership(user, "Employee")
  const isOwner = hasActiveMembership(user, "Owner")
  const isAdmin = user.roles.includes("Admin")

  return (
    <div className="grid gap-4">
      <AccountCard user={user} />
      <div className="grid gap-4 md:grid-cols-2">
        <ActionCard
          title="My appointments"
          description="Review your appointment requests and bookings."
          href="/appointments"
          icon={CalendarDays}
        />
        <ActionCard
          title="Profile"
          description="Review your account details."
          href="/profile"
          icon={UserRound}
        />
        {isEmployee ? (
          <ActionCard
            title="Employee Panel"
            description="Manage requests, appointments, and leave."
            href="/employee/requests"
            icon={UsersRound}
          />
        ) : null}
        {isOwner ? (
          <ActionCard
            title="Owner Panel"
            description="Manage businesses, staff, and scheduling."
            href="/owner"
            icon={BriefcaseBusiness}
          />
        ) : null}
        {isAdmin ? (
          <ActionCard
            title="Admin Panel"
            description="Review businesses and users."
            href="/admin/businesses"
            icon={ShieldCheck}
          />
        ) : null}
      </div>
    </div>
  )
}

function ActionCard({
  title,
  description,
  href,
  icon: Icon,
}: {
  title: string
  description: string
  href: string
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
