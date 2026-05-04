"use client"

import { useState } from "react"
import { LogOut } from "lucide-react"

import { Button } from "@/components/ui/button"
import { logout } from "@/lib/auth-api"
import { clearAuthTokens } from "@/lib/auth-storage"

type SignOutButtonProps = {
  redirectTo?: string
}

export function SignOutButton({ redirectTo }: SignOutButtonProps) {
  const [isLoggingOut, setIsLoggingOut] = useState(false)

  async function handleLogout() {
    setIsLoggingOut(true)

    try {
      await logout()
    } finally {
      clearAuthTokens()
      setIsLoggingOut(false)
      window.location.assign(redirectTo ?? "/")
    }
  }

  return (
    <Button
      type="button"
      variant="outline"
      onClick={handleLogout}
      disabled={isLoggingOut}
    >
      <LogOut data-icon="inline-start" className="size-4" />
      {isLoggingOut ? "Signing out" : "Sign out"}
    </Button>
  )
}
