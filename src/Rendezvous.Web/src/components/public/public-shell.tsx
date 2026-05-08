"use client"

import type { ReactNode } from "react"
import { useEffect, useMemo, useState, useSyncExternalStore } from "react"
import Image from "next/image"
import Link from "next/link"
import {
  BriefcaseBusiness,
  CalendarDays,
  Compass,
  LogIn,
  ShieldCheck,
  UserPlus,
  UserRound,
} from "lucide-react"

import { SignOutButton } from "@/components/auth/sign-out-button"
import { buttonVariants } from "@/components/ui/button"
import {
  NavigationMenu,
  NavigationMenuContent,
  NavigationMenuItem,
  NavigationMenuLink,
  NavigationMenuList,
  NavigationMenuTrigger,
} from "@/components/ui/navigation-menu"
import { getCurrentUser, getOwnerBusinesses } from "@/lib/auth-api"
import type { CurrentUser, OwnerBusiness } from "@/lib/auth-api"
import {
  getAccessToken,
  subscribeToAuthTokenChanges,
} from "@/lib/auth-storage"
import { cn } from "@/lib/utils"

type PublicShellProps = {
  children: ReactNode
}

export function PublicShell({ children }: PublicShellProps) {
  const hasToken = useSyncExternalStore(
    subscribeToAuthTokenChanges,
    getAuthStorageSnapshot,
    getServerAuthStorageSnapshot
  )
  const [user, setUser] = useState<CurrentUser | null>(null)
  const [ownerBusinesses, setOwnerBusinesses] = useState<OwnerBusiness[]>([])

  useEffect(() => {
    let isMounted = true

    async function loadUser() {
      if (!hasToken) {
        if (isMounted) {
          setUser(null)
          setOwnerBusinesses([])
        }
        return
      }

      try {
        const nextUser = await getCurrentUser()
        if (isMounted) {
          setUser(nextUser)
        }
      } catch {
        if (isMounted) {
          setUser(null)
          setOwnerBusinesses([])
        }
      }
    }

    loadUser()

    return () => {
      isMounted = false
    }
  }, [hasToken])

  const isOwner = useMemo(
    () =>
      user?.businessMemberships.some(
        (membership) =>
          membership.role === "Owner" && membership.status === "Active"
      ) ?? false,
    [user]
  )
  const isAdmin = user?.roles.includes("Admin") ?? false

  useEffect(() => {
    if (!isOwner) {
      const resetOwnerBusinesses = window.setTimeout(() => {
        setOwnerBusinesses([])
      }, 0)

      return () => window.clearTimeout(resetOwnerBusinesses)
    }

    let isMounted = true

    async function loadOwnerBusinesses() {
      try {
        const nextBusinesses = await getOwnerBusinesses()
        if (isMounted) {
          setOwnerBusinesses(nextBusinesses)
        }
      } catch {
        if (isMounted) {
          setOwnerBusinesses([])
        }
      }
    }

    loadOwnerBusinesses()

    return () => {
      isMounted = false
    }
  }, [isOwner])

  const ownerHref =
    ownerBusinesses.length === 1
      ? `/owner/businesses/${ownerBusinesses[0].id}/overview`
      : "/owner"

  return (
    <main className="min-h-svh bg-white text-[#111111]">
      <header className="border-b border-[#e5e7eb] bg-white">
        <div className="mx-auto flex w-full max-w-[1220px] flex-col gap-4 px-4 py-5 sm:px-6 lg:flex-row lg:items-center lg:justify-between lg:px-8">
          <div className="space-y-1">
            <Link
              href="/"
              className="inline-flex items-center gap-3 text-3xl font-bold leading-none tracking-normal text-[#111111]"
            >
              <Image
                src="/rendezvous-logo.png"
                alt=""
                width={40}
                height={40}
                className="size-10 object-contain"
                priority
              />
              <span>Rendezvous</span>
            </Link>
            <p className="pl-[52px] text-base text-[#71717a]">Find a business</p>
          </div>

          <nav className="flex flex-wrap items-center gap-3">
            <Link
              href="/"
              className={cn(
                buttonVariants({ variant: "outline", size: "lg" }),
                "h-11 rounded-xl border-[#cfe7c7] bg-[#f4fbf1] px-5 text-base font-medium text-[#4f9d3a] hover:bg-[#eef8ea] hover:text-[#4f9d3a]"
              )}
            >
              <Compass className="mr-2 size-5" aria-hidden="true" />
              Discover
            </Link>
            {user ? (
              <>
                <PublicNavLink href="/profile" icon={UserRound}>
                  Profile
                </PublicNavLink>
                <PublicNavLink href="/appointments" icon={CalendarDays}>
                  My appointments
                </PublicNavLink>
                {isOwner ? (
                  <OwnerPanelMenu ownerHref={ownerHref} />
                ) : null}
                {isAdmin ? (
                  <AdminPanelMenu />
                ) : null}
                <SignOutButton className="h-11 rounded-xl border-[#d4d4d8] bg-white px-5 text-base font-medium text-[#111111] hover:bg-[#f4f4f5]" />
              </>
            ) : (
              <>
                <PublicNavLink href="/login" icon={LogIn}>
                  Sign in
                </PublicNavLink>
                <Link
                  href="/register"
                  className={cn(
                    buttonVariants({ size: "lg" }),
                    "h-11 rounded-full bg-[#111111] px-5 text-base font-bold text-white hover:bg-[#27272a]"
                  )}
                >
                  <UserPlus className="mr-2 size-5" aria-hidden="true" />
                  Sign up
                </Link>
              </>
            )}
          </nav>
        </div>
      </header>
      <div className="mx-auto flex w-full max-w-[1220px] flex-col gap-8 px-4 py-8 sm:px-6 lg:px-8">
        {children}
      </div>
    </main>
  )
}

function PublicNavLink({
  href,
  icon: Icon,
  children,
}: {
  href: string
  icon: typeof UserRound
  children: ReactNode
}) {
  return (
    <Link
      href={href}
      className={cn(
        buttonVariants({ variant: "outline", size: "lg" }),
        "h-11 rounded-xl border-[#d4d4d8] bg-white px-5 text-base font-medium text-[#111111] hover:bg-[#f4f4f5]"
      )}
    >
      <Icon className="mr-2 size-5" aria-hidden="true" />
      {children}
    </Link>
  )
}

function OwnerPanelMenu({ ownerHref }: { ownerHref: string }) {
  return (
    <NavigationMenu className="flex-none">
      <NavigationMenuList className="gap-0">
        <NavigationMenuItem>
          <NavigationMenuTrigger className="h-11 rounded-xl border border-[#d4d4d8] bg-white px-5 text-base font-medium text-[#111111] hover:bg-[#f4f4f5] hover:text-[#111111] focus:bg-[#f4f4f5] focus:text-[#111111] data-[state=open]:bg-[#f4f4f5] data-[state=open]:text-[#111111]">
            <BriefcaseBusiness className="mr-2 size-5" aria-hidden="true" />
            Owner Panel
          </NavigationMenuTrigger>
          <NavigationMenuContent className="right-0 w-[240px] p-2">
            <div className="grid gap-1">
              <PanelMenuLink href={ownerHref}>
                Open owner panel
              </PanelMenuLink>
              <PanelMenuLink href="/owner/create-business">
                Create business
              </PanelMenuLink>
              <PanelMenuLink href="/owner/invitations">
                Invitations
              </PanelMenuLink>
            </div>
          </NavigationMenuContent>
        </NavigationMenuItem>
      </NavigationMenuList>
    </NavigationMenu>
  )
}

function PanelMenuLink({
  href,
  children,
}: {
  href: string
  children: ReactNode
}) {
  return (
    <NavigationMenuLink asChild>
      <Link
        href={href}
        className="w-full justify-start rounded-md px-3 py-2 text-sm font-medium text-[#3f3f46] hover:bg-[#f4f4f5] hover:text-[#111111]"
      >
        {children}
      </Link>
    </NavigationMenuLink>
  )
}

function AdminPanelMenu() {
  return (
    <NavigationMenu className="flex-none">
      <NavigationMenuList className="gap-0">
        <NavigationMenuItem>
          <NavigationMenuTrigger className="h-11 rounded-xl border border-[#d4d4d8] bg-white px-5 text-base font-medium text-[#111111] hover:bg-[#f4f4f5] hover:text-[#111111] focus:bg-[#f4f4f5] focus:text-[#111111] data-[state=open]:bg-[#f4f4f5] data-[state=open]:text-[#111111]">
            <ShieldCheck className="mr-2 size-5" aria-hidden="true" />
            Admin Panel
          </NavigationMenuTrigger>
          <NavigationMenuContent className="right-0 w-[220px] p-2">
            <div className="grid gap-1">
              <PanelMenuLink href="/admin/businesses">Businesses</PanelMenuLink>
              <PanelMenuLink href="/admin/users">Users</PanelMenuLink>
              <PanelMenuLink href="/admin/owner-applications">
                Owner applications
              </PanelMenuLink>
            </div>
          </NavigationMenuContent>
        </NavigationMenuItem>
      </NavigationMenuList>
    </NavigationMenu>
  )
}

function getAuthStorageSnapshot() {
  return Boolean(getAccessToken())
}

function getServerAuthStorageSnapshot() {
  return false
}
