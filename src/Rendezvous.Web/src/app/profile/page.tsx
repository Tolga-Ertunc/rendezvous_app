"use client"

import type { ComponentType } from "react"
import Link from "next/link"
import {
  BriefcaseBusiness,
  Building2,
  CalendarDays,
  MailCheck,
  ShieldCheck,
  UsersRound,
} from "lucide-react"

import { AccountCard } from "@/components/dashboard/account-card"
import {
  hasActiveMembership,
  ProtectedPage,
} from "@/components/layout/protected-page"
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

export default function ProfilePage() {
  return (
    <ProtectedPage
      title="Profile"
      description="Your account details and workspace shortcuts."
    >
      {({ user }) => <ProfileLanding user={user} />}
    </ProtectedPage>
  )
}

function ProfileLanding({ user }: { user: CurrentUser }) {
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
          title="Owner application"
          description="Request approval to open and manage a business."
          href="/profile/owner-onboarding"
          icon={Building2}
          actionLabel="Open application"
        />
        <ActionCard
          title="Accept invitation"
          description="Join a business as staff with your one-time invitation token."
          href="/invitations/accept"
          icon={MailCheck}
          actionLabel="Accept invitation"
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
  actionLabel = "Open",
}: {
  title: string
  description: string
  href: string
  icon: ComponentType<{ className?: string }>
  actionLabel?: string
}) {
  return (
    <Card>
      <CardHeader>
        <div className="flex items-center gap-2">
          <Icon className="size-4 text-primary" aria-hidden="true" />
          <CardTitle>{title}</CardTitle>
        </div>
        <CardDescription>{description}</CardDescription>
      </CardHeader>
      <CardContent>
        <Link href={href} className={cn(buttonVariants({ variant: "outline" }))}>
          {actionLabel}
        </Link>
      </CardContent>
    </Card>
  )
}
