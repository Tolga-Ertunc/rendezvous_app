"use client"

import { useEffect, useMemo, useState } from "react"
import { useParams } from "next/navigation"

import { PublicBusinessDetailView } from "@/components/public/public-business-components"
import {
  Card,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import { ApiError } from "@/lib/api-client"
import { getPublicBusiness } from "@/lib/public-api"
import type { PublicBusinessDetail } from "@/lib/public-api"

export default function BusinessDetailPage() {
  const params = useParams<{ id: string }>()
  const businessId = useMemo(() => params.id, [params.id])
  const [business, setBusiness] = useState<PublicBusinessDetail | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState("")

  useEffect(() => {
    let isMounted = true

    async function loadBusiness() {
      setIsLoading(true)
      setError("")

      try {
        const nextBusiness = await getPublicBusiness(businessId)

        if (isMounted) {
          setBusiness(nextBusiness)
        }
      } catch (caughtError) {
        if (!isMounted) {
          return
        }

        if (caughtError instanceof ApiError && caughtError.status === 404) {
          setError("This business is not available.")
        } else {
          setError("Business details could not be loaded.")
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
  }, [businessId])

  if (isLoading) {
    return (
      <main className="min-h-svh bg-[#fbfbfa] px-8 py-10">
        <Card className="mx-auto w-full max-w-xl">
          <CardHeader>
            <CardTitle>Loading business</CardTitle>
            <CardDescription>
              Fetching public business details and services.
            </CardDescription>
          </CardHeader>
        </Card>
      </main>
    )
  }

  if (error || !business) {
    return (
      <main className="min-h-svh bg-[#fbfbfa] px-8 py-10">
        <Card className="mx-auto w-full max-w-xl">
          <CardHeader>
            <CardTitle>Business unavailable</CardTitle>
            <CardDescription>{error}</CardDescription>
          </CardHeader>
        </Card>
      </main>
    )
  }

  return <PublicBusinessDetailView business={business} />
}
