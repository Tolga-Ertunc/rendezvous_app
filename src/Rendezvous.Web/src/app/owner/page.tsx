"use client"

import { useEffect, useState } from "react"
import { useRouter } from "next/navigation"

import { BusinessList } from "@/components/dashboard/business-components"
import {
  hasActiveMembership,
  ProtectedPage,
} from "@/components/layout/protected-page"
import { getOwnerBusinesses } from "@/lib/auth-api"
import type { OwnerBusiness } from "@/lib/auth-api"

export default function OwnerPage() {
  return (
    <ProtectedPage
      title="Owner Panel"
      description="Select a business to manage."
      authorize={(user) => hasActiveMembership(user, "Owner")}
    >
      {() => <OwnerBusinessSelector />}
    </ProtectedPage>
  )
}

function OwnerBusinessSelector() {
  const router = useRouter()
  const [businesses, setBusinesses] = useState<OwnerBusiness[]>([])
  const [isLoading, setIsLoading] = useState(true)

  useEffect(() => {
    let isMounted = true

    async function loadBusinesses() {
      setIsLoading(true)
      const nextBusinesses = await getOwnerBusinesses()

      if (!isMounted) {
        return
      }

      if (nextBusinesses.length === 1) {
        router.replace(`/owner/businesses/${nextBusinesses[0].id}/overview`)
        return
      }

      setBusinesses(nextBusinesses)
      setIsLoading(false)
    }

    loadBusinesses()

    return () => {
      isMounted = false
    }
  }, [router])

  if (isLoading) {
    return (
      <p className="text-sm leading-6 text-muted-foreground">
        Loading businesses.
      </p>
    )
  }

  return (
    <BusinessList
      title="Owner businesses"
      description="Businesses managed through active owner memberships."
      businesses={businesses}
      emptyText="No owner businesses were returned."
      detailHref={(businessId) => `/owner/businesses/${businessId}/overview`}
    />
  )
}
