"use client"

import type { ComponentType } from "react"
import { useEffect, useMemo, useState, useSyncExternalStore } from "react"
import Link from "next/link"
import { usePathname } from "next/navigation"
import {
  BriefcaseBusiness,
  Building2,
  CalendarDays,
  LayoutDashboard,
  ShieldCheck,
  UserRound,
  UsersRound,
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

type AppNavigationProps = {
  showDiscoverLink?: boolean
  showGuestLinks?: boolean
  showDashboardLink?: boolean
  logoutRedirectTo?: string
}

export function AppNavigation({
  showDiscoverLink = true,
  showGuestLinks = true,
  showDashboardLink = true,
  logoutRedirectTo,
}: AppNavigationProps) {
  const pathname = usePathname()
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
        }
      }
    }

    loadUser()

    return () => {
      isMounted = false
    }
  }, [hasToken])

  const isEmployee = useMemo(
    () =>
      user?.businessMemberships.some(
        (membership) =>
          membership.role === "Employee" && membership.status === "Active"
      ) ?? false,
    [user]
  )
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
      return
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
    <div className="flex flex-1 flex-wrap items-center justify-end gap-2">
      <NavigationMenu className="max-w-full justify-end">
        <NavigationMenuList>
          {showDiscoverLink ? (
            <NavigationLink
              href="/"
              label="Discover"
              icon={Building2}
              active={pathname === "/" || pathname.startsWith("/businesses")}
            />
          ) : null}
          {user ? (
            <>
              <NavigationLink
                href="/profile"
                label="Profile"
                icon={UserRound}
                active={pathname === "/profile"}
              />
              <NavigationLink
                href="/appointments"
                label="My appointments"
                icon={CalendarDays}
                active={pathname === "/appointments"}
              />
              {showDashboardLink ? (
                <NavigationLink
                  href="/dashboard"
                  label="Dashboard"
                  icon={LayoutDashboard}
                  active={pathname === "/dashboard"}
                />
              ) : null}
              {isEmployee ? (
                <NavigationMenuItem>
                  <NavigationMenuTrigger
                    className={cn(
                      isActiveGroup(pathname, "/employee") &&
                        "bg-primary/10 text-primary"
                    )}
                  >
                    <UsersRound className="mr-2 size-4" aria-hidden="true" />
                    Employee Panel
                  </NavigationMenuTrigger>
                  <NavigationMenuContent>
                    <div className="grid w-[230px] gap-1">
                      <MenuPanelLink href="/employee/requests" title="Requests" />
                      <MenuPanelLink
                        href="/employee/appointments"
                        title="Appointments"
                      />
                      <MenuPanelLink href="/employee/leave" title="My leave" />
                    </div>
                  </NavigationMenuContent>
                </NavigationMenuItem>
              ) : null}
              {isOwner ? (
                <NavigationMenuItem>
                  <NavigationMenuTrigger
                    className={cn(
                      isActiveGroup(pathname, "/owner") &&
                        "bg-primary/10 text-primary"
                    )}
                  >
                    <BriefcaseBusiness
                      className="mr-2 size-4"
                      aria-hidden="true"
                    />
                    Owner Panel
                  </NavigationMenuTrigger>
                  <NavigationMenuContent>
                    <div className="grid w-[260px] gap-1">
                      <MenuPanelLink href={ownerHref} title="Open owner panel" />
                      <MenuPanelLink
                        href="/owner/create-business"
                        title="Create business"
                      />
                      <MenuPanelLink
                        href="/owner/invitations"
                        title="Invitations"
                      />
                    </div>
                  </NavigationMenuContent>
                </NavigationMenuItem>
              ) : null}
              {isAdmin ? (
                <NavigationMenuItem>
                  <NavigationMenuTrigger
                    className={cn(
                      isActiveGroup(pathname, "/admin") &&
                        "bg-primary/10 text-primary"
                    )}
                  >
                    <ShieldCheck className="mr-2 size-4" aria-hidden="true" />
                    Admin Panel
                  </NavigationMenuTrigger>
                  <NavigationMenuContent>
                    <div className="grid w-[220px] gap-1">
                      <MenuPanelLink href="/admin/businesses" title="Businesses" />
                      <MenuPanelLink href="/admin/users" title="Users" />
                    </div>
                  </NavigationMenuContent>
                </NavigationMenuItem>
              ) : null}
            </>
          ) : null}
        </NavigationMenuList>
      </NavigationMenu>

      {user ? (
        <SignOutButton redirectTo={logoutRedirectTo} />
      ) : showGuestLinks ? (
        <>
          <Link
            href="/login"
            className={cn(buttonVariants({ variant: "outline" }))}
          >
            Sign in
          </Link>
          <Link href="/register" className={cn(buttonVariants())}>
            Sign up
          </Link>
        </>
      ) : null}
    </div>
  )
}

function NavigationLink({
  href,
  label,
  icon: Icon,
  active,
}: {
  href: string
  label: string
  icon: ComponentType<{ className?: string }>
  active: boolean
}) {
  return (
    <NavigationMenuItem>
      <NavigationMenuLink asChild active={active}>
        <Link href={href}>
          <Icon className="mr-2 size-4" />
          {label}
        </Link>
      </NavigationMenuLink>
    </NavigationMenuItem>
  )
}

function MenuPanelLink({ href, title }: { href: string; title: string }) {
  const pathname = usePathname()

  return (
    <NavigationMenuLink asChild active={pathname === href}>
      <Link href={href} className="w-full justify-start">
        {title}
      </Link>
    </NavigationMenuLink>
  )
}

function isActiveGroup(pathname: string, prefix: string) {
  return pathname === prefix || pathname.startsWith(`${prefix}/`)
}

function getAuthStorageSnapshot() {
  return Boolean(getAccessToken())
}

function getServerAuthStorageSnapshot() {
  return false
}
