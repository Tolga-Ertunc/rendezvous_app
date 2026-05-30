"use client"

import type { ComponentType } from "react"
import { useEffect, useMemo, useState, useSyncExternalStore } from "react"
import Link from "next/link"
import { usePathname } from "next/navigation"
import {
  BriefcaseBusiness,
  CalendarDays,
  Compass,
  LogIn,
  ShieldCheck,
  UserPlus,
  UserRound,
  UsersRound,
} from "lucide-react"

import { SignOutButton } from "@/components/auth/sign-out-button"
import { NotificationButton } from "@/components/notifications/notification-button"
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
  logoutRedirectTo?: string
}

const headerButtonClass =
  "h-11 rounded-xl border-[#d4d4d8] bg-white px-5 text-base font-medium text-[#111111] hover:bg-[#f4f4f5] hover:text-[#111111] focus:bg-[#f4f4f5] focus:text-[#111111]"

const activeHeaderButtonClass =
  "border-[#cfe7c7] bg-[#f4fbf1] text-[#4f9d3a] hover:bg-[#eef8ea] hover:text-[#4f9d3a] focus:bg-[#eef8ea] focus:text-[#4f9d3a] data-[active]:bg-[#f4fbf1] data-[active]:text-[#4f9d3a] data-[active=true]:bg-[#f4fbf1] data-[active=true]:text-[#4f9d3a]"

const menuTriggerOpenClass =
  "data-[state=open]:bg-[#f4f4f5] data-[state=open]:text-[#111111]"

export function AppNavigation({
  showDiscoverLink = true,
  showGuestLinks = true,
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
    <div className="flex flex-1 flex-wrap items-center justify-start gap-3 lg:justify-end">
      <NavigationMenu className="max-w-full flex-none justify-start lg:justify-end">
        <NavigationMenuList className="gap-3">
          {showDiscoverLink ? (
            <NavigationLink
              href="/"
              label="Discover"
              icon={Compass}
              active={pathname === "/" || pathname.startsWith("/businesses")}
            />
          ) : null}
          {user ? (
            <>
              <NavigationLink
                href="/profile"
                label="Profile"
                icon={UserRound}
                active={isActiveGroup(pathname, "/profile")}
              />
              <NavigationLink
                href="/appointments"
                label="My appointments"
                icon={CalendarDays}
                active={pathname === "/appointments"}
              />
              {isEmployee ? (
                <NavigationMenuItem>
                  <NavigationMenuTrigger
                    className={cn(
                      buttonVariants({ variant: "outline", size: "lg" }),
                      headerButtonClass,
                      menuTriggerOpenClass,
                      isActiveGroup(pathname, "/employee") &&
                        activeHeaderButtonClass
                    )}
                  >
                    <UsersRound className="mr-2 size-5" aria-hidden="true" />
                    Employee Panel
                  </NavigationMenuTrigger>
                  <NavigationMenuContent className="right-0 w-[230px] p-2">
                    <div className="grid gap-1">
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
                      buttonVariants({ variant: "outline", size: "lg" }),
                      headerButtonClass,
                      menuTriggerOpenClass,
                      isActiveGroup(pathname, "/owner") &&
                        activeHeaderButtonClass
                    )}
                  >
                    <BriefcaseBusiness
                      className="mr-2 size-5"
                      aria-hidden="true"
                    />
                    Owner Panel
                  </NavigationMenuTrigger>
                  <NavigationMenuContent className="right-0 w-[260px] p-2">
                    <div className="grid gap-1">
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
                      buttonVariants({ variant: "outline", size: "lg" }),
                      headerButtonClass,
                      menuTriggerOpenClass,
                      isActiveGroup(pathname, "/admin") &&
                        activeHeaderButtonClass
                    )}
                  >
                    <ShieldCheck className="mr-2 size-5" aria-hidden="true" />
                    Admin Panel
                  </NavigationMenuTrigger>
                  <NavigationMenuContent className="right-0 w-[220px] p-2">
                    <div className="grid gap-1">
                      <MenuPanelLink href="/admin/businesses" title="Businesses" />
                      <MenuPanelLink href="/admin/users" title="Users" />
                      <MenuPanelLink
                        href="/admin/owner-applications"
                        title="Owner applications"
                      />
                    </div>
                  </NavigationMenuContent>
                </NavigationMenuItem>
              ) : null}
            </>
          ) : null}
        </NavigationMenuList>
      </NavigationMenu>

      {user ? (
        <>
          <NotificationButton className="h-11 rounded-xl border-[#d4d4d8] bg-white px-4 text-[#111111] hover:bg-[#f4f4f5] hover:text-[#111111]" />
          <SignOutButton
            redirectTo={logoutRedirectTo}
            className={headerButtonClass}
          />
        </>
      ) : showGuestLinks ? (
        <>
          <Link
            href="/login"
            className={cn(
              buttonVariants({ variant: "outline", size: "lg" }),
              headerButtonClass
            )}
          >
            <LogIn className="mr-2 size-5" aria-hidden="true" />
            Sign in
          </Link>
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
      <NavigationMenuLink
        asChild
        active={active}
        className={cn(
          buttonVariants({ variant: "outline", size: "lg" }),
          headerButtonClass,
          active && activeHeaderButtonClass
        )}
      >
        <Link href={href}>
          <Icon className="mr-2 size-5" />
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
      <Link
        href={href}
        className={cn(
          "w-full justify-start rounded-md px-3 py-2 text-sm font-medium text-[#3f3f46] hover:bg-[#f4f4f5] hover:text-[#111111]",
          pathname === href && "bg-[#f4fbf1] text-[#4f9d3a]"
        )}
      >
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
