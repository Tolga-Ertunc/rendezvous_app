"use client"

import { AppNavigation } from "@/components/layout/app-navigation"

type AuthHeaderActionsProps = {
  showDiscoverLink?: boolean
  showDashboardLink?: boolean
  showGuestLinks?: boolean
  logoutRedirectTo?: string
}

export function AuthHeaderActions(props: AuthHeaderActionsProps) {
  return <AppNavigation {...props} />
}
