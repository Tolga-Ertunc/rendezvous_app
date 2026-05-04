"use client"

import { useEffect } from "react"
import { useParams, useRouter } from "next/navigation"

export default function LegacyAdminBusinessDetailRoute() {
  const params = useParams<{ id: string }>()
  const router = useRouter()

  useEffect(() => {
    router.replace(`/admin/businesses/${params.id}`)
  }, [params.id, router])

  return null
}
