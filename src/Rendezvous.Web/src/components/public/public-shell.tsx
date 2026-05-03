"use client"

import type { ReactNode } from "react"
import { useSyncExternalStore } from "react"
import Link from "next/link"
import { CalendarDays, LayoutDashboard, LogIn, UserPlus } from "lucide-react"

import { buttonVariants } from "@/components/ui/button"
import { getAccessToken } from "@/lib/auth-storage"
import { cn } from "@/lib/utils"

type PublicShellProps = {
  title: string
  description: string
  children: ReactNode
  actions?: ReactNode
}

export function PublicShell({
  title,
  description,
  children,
  actions,
}: PublicShellProps) {
  const isSignedIn = useSyncExternalStore(
    subscribeToAuthStorage,
    getAuthStorageSnapshot,
    getServerAuthStorageSnapshot
  )

  return (
    <main className="min-h-svh bg-[linear-gradient(180deg,oklch(0.99_0_0),oklch(0.965_0.01_220))] px-4 py-6 sm:px-6 lg:px-8">
      <div className="mx-auto flex w-full max-w-6xl flex-col gap-6">
        <header className="flex flex-col gap-4 border-b border-border pb-6 sm:flex-row sm:items-center sm:justify-between">
          <div className="space-y-2">
            <Link
              href="/"
              className="inline-flex items-center gap-2 text-sm font-medium text-primary"
            >
              <CalendarDays className="size-4" aria-hidden="true" />
              Rendezvous
            </Link>
            <div className="space-y-1">
              <h1 className="text-2xl font-semibold text-foreground">
                {title}
              </h1>
              <p className="max-w-2xl text-sm leading-6 text-muted-foreground">
                {description}
              </p>
            </div>
          </div>
          <div className="flex shrink-0 flex-wrap gap-2">
            {actions}
            {isSignedIn ? (
              <Link
                href="/dashboard"
                className={cn(buttonVariants({ variant: "outline" }))}
              >
                <LayoutDashboard data-icon="inline-start" className="size-4" />
                Dashboard
              </Link>
            ) : (
              <>
                <Link
                  href="/login"
                  className={cn(buttonVariants({ variant: "outline" }))}
                >
                  <LogIn data-icon="inline-start" className="size-4" />
                  Sign in
                </Link>
                <Link
                  href="/register"
                  className={cn(buttonVariants({ variant: "default" }))}
                >
                  <UserPlus data-icon="inline-start" className="size-4" />
                  Sign up
                </Link>
              </>
            )}
          </div>
        </header>
        {children}
      </div>
    </main>
  )
}

function subscribeToAuthStorage(onStoreChange: () => void) {
  window.addEventListener("storage", onStoreChange)

  return () => window.removeEventListener("storage", onStoreChange)
}

function getAuthStorageSnapshot() {
  return Boolean(getAccessToken())
}

function getServerAuthStorageSnapshot() {
  return false
}
