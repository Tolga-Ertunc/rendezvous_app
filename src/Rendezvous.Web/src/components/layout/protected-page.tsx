"use client"

import type { ReactNode } from "react"
import { useEffect, useRef, useState } from "react"
import { useRouter } from "next/navigation"

import { Button } from "@/components/ui/button"
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import { DashboardShell } from "@/components/dashboard/dashboard-shell"
import { getCurrentUser } from "@/lib/auth-api"
import type { CurrentUser } from "@/lib/auth-api"
import { clearAuthTokens, getAccessToken } from "@/lib/auth-storage"

type ProtectedPageProps = {
  title: string
  description: string
  authorize?: (user: CurrentUser) => boolean
  children: (context: { user: CurrentUser }) => ReactNode
}

export function ProtectedPage({
  title,
  description,
  authorize,
  children,
}: ProtectedPageProps) {
  const router = useRouter()
  const [user, setUser] = useState<CurrentUser | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState("")
  const authorizeRef = useRef(authorize)

  useEffect(() => {
    authorizeRef.current = authorize
  }, [authorize])

  useEffect(() => {
    if (!getAccessToken()) {
      router.replace("/")
      return
    }

    let isMounted = true

    async function loadUser() {
      setIsLoading(true)
      setError("")

      try {
        const currentUser = await getCurrentUser()

        if (authorizeRef.current && !authorizeRef.current(currentUser)) {
          router.replace("/")
          return
        }

        if (isMounted) {
          setUser(currentUser)
        }
      } catch {
        clearAuthTokens()
        if (isMounted) {
          setError("Your session could not be loaded.")
        }
      } finally {
        if (isMounted) {
          setIsLoading(false)
        }
      }
    }

    loadUser()

    return () => {
      isMounted = false
    }
  }, [router])

  if (isLoading) {
    return (
      <DashboardShell title={title} description={description}>
        <Card className="mx-auto w-full max-w-xl">
          <CardHeader>
            <CardTitle>Loading</CardTitle>
            <CardDescription>Checking your session.</CardDescription>
          </CardHeader>
        </Card>
      </DashboardShell>
    )
  }

  if (error || !user) {
    return (
      <DashboardShell title={title} description={description}>
        <Card className="mx-auto w-full max-w-xl">
          <CardHeader>
            <CardTitle>Session unavailable</CardTitle>
            <CardDescription>{error}</CardDescription>
          </CardHeader>
          <CardContent>
            <Button type="button" onClick={() => router.replace("/")}>
              Return home
            </Button>
          </CardContent>
        </Card>
      </DashboardShell>
    )
  }

  return (
    <DashboardShell title={title} description={description}>
      {children({ user })}
    </DashboardShell>
  )
}

export function hasActiveMembership(user: CurrentUser, role: "Owner" | "Employee") {
  return user.businessMemberships.some(
    (membership) => membership.role === role && membership.status === "Active"
  )
}
