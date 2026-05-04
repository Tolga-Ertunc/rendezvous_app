"use client"

import { useSyncExternalStore } from "react"
import Link from "next/link"
import { LayoutDashboard, LogIn, UserPlus } from "lucide-react"

import { SignOutButton } from "@/components/auth/sign-out-button"
import { buttonVariants } from "@/components/ui/button"
import {
  getAccessToken,
  subscribeToAuthTokenChanges,
} from "@/lib/auth-storage"
import { cn } from "@/lib/utils"

type AuthHeaderActionsProps = {
  showDashboardLink?: boolean
  showGuestLinks?: boolean
  logoutRedirectTo?: string
}

export function AuthHeaderActions({
  showDashboardLink = true,
  showGuestLinks = true,
  logoutRedirectTo,
}: AuthHeaderActionsProps) {
  const isSignedIn = useSyncExternalStore(
    subscribeToAuthTokenChanges,
    getAuthStorageSnapshot,
    getServerAuthStorageSnapshot
  )

  if (isSignedIn) {
    return (
      <>
        {showDashboardLink ? (
          <Link
            href="/dashboard"
            className={cn(buttonVariants({ variant: "outline" }))}
          >
            <LayoutDashboard data-icon="inline-start" className="size-4" />
            Dashboard
          </Link>
        ) : null}
        <SignOutButton redirectTo={logoutRedirectTo} />
      </>
    )
  }

  if (!showGuestLinks) {
    return null
  }

  return (
    <>
      <Link href="/login" className={cn(buttonVariants({ variant: "outline" }))}>
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
  )
}

function getAuthStorageSnapshot() {
  return Boolean(getAccessToken())
}

function getServerAuthStorageSnapshot() {
  return false
}
