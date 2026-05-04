"use client"

import { useEffect, useState } from "react"

import { OwnerInvitationsPanel } from "@/components/dashboard/owner-invitations-panel"
import {
  hasActiveMembership,
  ProtectedPage,
} from "@/components/layout/protected-page"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { getOwnerBusinesses } from "@/lib/auth-api"
import type { OwnerBusiness } from "@/lib/auth-api"

export default function OwnerInvitationsPage() {
  return (
    <ProtectedPage
      title="Invitations"
      description="Invite employees to a business."
      authorize={(user) => hasActiveMembership(user, "Owner")}
    >
      {() => <OwnerInvitationsContent />}
    </ProtectedPage>
  )
}

function OwnerInvitationsContent() {
  const [businesses, setBusinesses] = useState<OwnerBusiness[]>([])
  const [selectedBusinessId, setSelectedBusinessId] = useState("")
  const [isLoading, setIsLoading] = useState(true)

  useEffect(() => {
    let isMounted = true

    async function loadBusinesses() {
      setIsLoading(true)
      const nextBusinesses = await getOwnerBusinesses()

      if (!isMounted) {
        return
      }

      setBusinesses(nextBusinesses)
      setSelectedBusinessId(nextBusinesses[0]?.id ?? "")
      setIsLoading(false)
    }

    loadBusinesses()

    return () => {
      isMounted = false
    }
  }, [])

  if (isLoading) {
    return (
      <p className="text-sm leading-6 text-muted-foreground">
        Loading businesses.
      </p>
    )
  }

  if (businesses.length === 0 || !selectedBusinessId) {
    return (
      <p className="text-sm leading-6 text-muted-foreground">
        No owner businesses were returned.
      </p>
    )
  }

  return (
    <div className="grid gap-4">
      {businesses.length > 1 ? (
        <div className="max-w-sm">
          <Select
            value={selectedBusinessId}
            onValueChange={setSelectedBusinessId}
          >
            <SelectTrigger>
              <SelectValue placeholder="Select business" />
            </SelectTrigger>
            <SelectContent>
              {businesses.map((business) => (
                <SelectItem key={business.id} value={business.id}>
                  {business.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      ) : null}
      <OwnerInvitationsPanel businessId={selectedBusinessId} />
    </div>
  )
}
