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
import { Badge } from "@/components/ui/badge"
import { buttonVariants } from "@/components/ui/button"
import {
  Card,
  CardContent,
  CardTitle,
} from "@/components/ui/card"
import type { CurrentUser } from "@/lib/auth-api"
import { cn } from "@/lib/utils"

export default function ProfilePage() {
  return (
    <ProtectedPage
      title="Profile"
      description="Manage your account identity and workspace access."
      shellVariant="profile"
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
    <div className="grid gap-8">
      <AccountCard user={user} />

      <section className="space-y-4">
        <div>
          <h2 className="text-xl font-bold tracking-normal text-[#111111]">
            Workspace shortcuts
          </h2>
          <p className="mt-1 text-sm leading-6 text-[#71717a]">
            Open the areas available for this account.
          </p>
        </div>

        <div className="grid grid-cols-3 gap-4">
          {isAdmin ? (
            <ActionCard
              title="Admin Panel"
              description="Review businesses, users, and owner applications."
              href="/admin/businesses"
              icon={ShieldCheck}
              actionLabel="Open admin panel"
              badge="Admin"
              tone="admin"
              primary
            />
          ) : null}
          {isOwner ? (
            <ActionCard
              title="Owner Panel"
              description="Manage businesses, staff, services, and scheduling."
              href="/owner"
              icon={BriefcaseBusiness}
              actionLabel="Open owner panel"
              badge="Owner"
              tone="owner"
              primary
            />
          ) : null}
          {isEmployee ? (
            <ActionCard
              title="Employee Panel"
              description="Manage requests, appointments, and leave."
              href="/employee/requests"
              icon={UsersRound}
              actionLabel="Open employee panel"
              badge="Employee"
              tone="employee"
            />
          ) : null}
          <ActionCard
            title="My appointments"
            description="Review appointment requests and bookings."
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
            description="Join a business as staff with a one-time invitation token."
            href="/invitations/accept"
            icon={MailCheck}
            actionLabel="Accept invitation"
          />
        </div>
      </section>
    </div>
  )
}

function ActionCard({
  title,
  description,
  href,
  icon: Icon,
  actionLabel = "Open",
  badge,
  tone = "default",
  primary = false,
}: {
  title: string
  description: string
  href: string
  icon: ComponentType<{ className?: string }>
  actionLabel?: string
  badge?: string
  tone?: "default" | "owner" | "admin" | "employee"
  primary?: boolean
}) {
  return (
    <Card className="group flex min-h-[218px] border-[#e5e7eb] bg-white shadow-xs transition-all hover:border-[#d4d4d8] hover:shadow-sm">
      <CardContent className="flex flex-1 flex-col p-5">
        <div className="flex items-start justify-between gap-4">
          <div
            className={cn(
              "flex size-11 items-center justify-center rounded-lg border",
              tone === "owner" && "border-[#a9d8d2] bg-[#eaf8f6] text-[#0f766e]",
              tone === "admin" && "border-[#111111] bg-[#111111] text-white",
              tone === "employee" &&
                "border-[#cfe7c7] bg-[#f4fbf1] text-[#4f9d3a]",
              tone === "default" && "border-[#e5e7eb] bg-[#fafafa] text-[#111111]"
            )}
          >
            <Icon className="size-5" aria-hidden="true" />
          </div>
          {badge ? (
            <Badge
              variant="outline"
              className={cn(
                "rounded-full px-2.5 py-1 text-xs font-semibold",
                tone === "owner" &&
                  "border-[#a9d8d2] bg-[#eaf8f6] text-[#0f766e]",
                tone === "admin" &&
                  "border-[#111111] bg-[#111111] text-white",
                tone === "employee" &&
                  "border-[#cfe7c7] bg-[#f4fbf1] text-[#4f9d3a]"
              )}
            >
              {badge}
            </Badge>
          ) : null}
        </div>

        <div className="mt-5 flex-1 space-y-2">
          <CardTitle className="text-lg font-bold tracking-normal text-[#111111]">
            {title}
          </CardTitle>
          <p className="text-sm leading-6 text-[#71717a]">{description}</p>
        </div>

        <Link
          href={href}
          className={cn(
            buttonVariants({ variant: primary ? "default" : "outline", size: "lg" }),
            "mt-5 h-10 w-fit rounded-full px-5 font-semibold",
            primary
              ? "bg-[#111111] text-white hover:bg-[#27272a]"
              : "border-[#d4d4d8] bg-white text-[#111111] hover:bg-[#f4f4f5] hover:text-[#111111]"
          )}
        >
          {actionLabel}
        </Link>
      </CardContent>
    </Card>
  )
}
