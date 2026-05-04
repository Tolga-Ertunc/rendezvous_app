"use client"

import { useEffect } from "react"
import { useParams, useRouter } from "next/navigation"

export default function OwnerBusinessRoute() {
  const params = useParams<{ id: string }>()
  const router = useRouter()

  useEffect(() => {
    router.replace(`/owner/businesses/${params.id}/overview`)
  }, [params.id, router])

  return null
}
