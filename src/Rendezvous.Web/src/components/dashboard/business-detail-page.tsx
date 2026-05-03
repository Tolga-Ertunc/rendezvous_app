"use client"

import { useEffect, useMemo, useState } from "react"
import { useParams, useRouter } from "next/navigation"

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert"
import { Button } from "@/components/ui/button"
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import { AdminBusinessActionsPanel } from "@/components/dashboard/admin-business-actions-panel"
import { BusinessDetailView } from "@/components/dashboard/business-components"
import { BackButton, DashboardShell } from "@/components/dashboard/dashboard-shell"
import { OwnerAppointmentRequestsPanel } from "@/components/dashboard/owner-appointment-requests-panel"
import { OwnerInvitationsPanel } from "@/components/dashboard/owner-invitations-panel"
import { OwnerManagementPanels } from "@/components/dashboard/owner-management-panels"
import { ApiError } from "@/lib/api-client"
import { getAdminBusiness, getOwnerBusiness } from "@/lib/auth-api"
import type { BusinessDetail } from "@/lib/auth-api"
import { clearAuthTokens, getAccessToken } from "@/lib/auth-storage"

type BusinessDetailPageProps = {
  mode: "owner" | "admin"
}

const pageCopy = {
  owner: {
    title: "Owner business detail",
    description: "Review the business and services available to this owner account.",
    eyebrow: "Owner read-only management view.",
    backHref: "/dashboard",
  },
  admin: {
    title: "Admin business detail",
    description: "Review any business through the separate read-only admin route.",
    eyebrow: "Admin read-only system visibility.",
    backHref: "/dashboard",
  },
} satisfies Record<
  BusinessDetailPageProps["mode"],
  {
    title: string
    description: string
    eyebrow: string
    backHref: string
  }
>

export function BusinessDetailPage({ mode }: BusinessDetailPageProps) {
  const params = useParams<{ id: string }>()
  const router = useRouter()
  const [business, setBusiness] = useState<BusinessDetail | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState("")

  const copy = pageCopy[mode]
  const businessId = useMemo(() => params.id, [params.id])

  async function refreshBusiness() {
    const nextBusiness =
      mode === "owner"
        ? await getOwnerBusiness(businessId)
        : await getAdminBusiness(businessId)

    setBusiness(nextBusiness)
  }

  useEffect(() => {
    if (!getAccessToken()) {
      router.replace("/login")
      return
    }

    let isMounted = true

    async function loadBusiness() {
      setIsLoading(true)
      setError("")

      try {
        const nextBusiness =
          mode === "owner"
            ? await getOwnerBusiness(businessId)
            : await getAdminBusiness(businessId)

        if (isMounted) {
          setBusiness(nextBusiness)
        }
      } catch (caughtError) {
        if (caughtError instanceof ApiError && caughtError.status === 401) {
          clearAuthTokens()
          router.replace("/login")
          return
        }

        if (isMounted) {
          setError("This business view is not available for this account.")
        }
      } finally {
        if (isMounted) {
          setIsLoading(false)
        }
      }
    }

    loadBusiness()

    return () => {
      isMounted = false
    }
  }, [businessId, mode, router])

  if (isLoading) {
    return (
      <DashboardShell
        title={copy.title}
        description={copy.description}
        actions={<BackButton onClick={() => router.push(copy.backHref)} />}
      >
        <Card className="mx-auto w-full max-w-xl">
          <CardHeader>
            <CardTitle>Loading business</CardTitle>
            <CardDescription>
              Checking access and loading read-only business data.
            </CardDescription>
          </CardHeader>
        </Card>
      </DashboardShell>
    )
  }

  if (error || !business) {
    return (
      <DashboardShell
        title={copy.title}
        description={copy.description}
        actions={<BackButton onClick={() => router.push(copy.backHref)} />}
      >
        <Card className="mx-auto w-full max-w-xl">
          <CardHeader>
            <CardTitle>Business unavailable</CardTitle>
            <CardDescription>{error}</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <Alert>
              <AlertTitle>Read-only access was not granted</AlertTitle>
              <AlertDescription>
                This route only shows data when the current account has the
                matching owner or admin permission.
              </AlertDescription>
            </Alert>
            <Button type="button" onClick={() => router.push(copy.backHref)}>
              Return to dashboard
            </Button>
          </CardContent>
        </Card>
      </DashboardShell>
    )
  }

  return (
    <DashboardShell
      title={copy.title}
      description={copy.description}
      actions={<BackButton onClick={() => router.push(copy.backHref)} />}
    >
      <div className="grid gap-4">
        <BusinessDetailView business={business} eyebrow={copy.eyebrow} />
        {mode === "owner" ? (
          <>
            <OwnerAppointmentRequestsPanel businessId={business.id} />
            <OwnerManagementPanels
              business={business}
              onChanged={refreshBusiness}
            />
            <OwnerInvitationsPanel businessId={business.id} />
          </>
        ) : null}
        {mode === "admin" ? (
          <AdminBusinessActionsPanel
            businessId={business.id}
            initialStatus={business.status}
          />
        ) : null}
      </div>
    </DashboardShell>
  )
}
