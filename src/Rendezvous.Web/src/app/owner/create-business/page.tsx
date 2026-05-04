"use client"

import { useRouter } from "next/navigation"

import { OwnerOnboardingPanel } from "@/components/dashboard/owner-onboarding-panel"
import {
  hasActiveMembership,
  ProtectedPage,
} from "@/components/layout/protected-page"

export default function OwnerCreateBusinessPage() {
  const router = useRouter()

  return (
    <ProtectedPage
      title="Create business"
      description="Create another business under this owner account."
      authorize={(user) => hasActiveMembership(user, "Owner")}
    >
      {() => (
        <OwnerOnboardingPanel
          hasOwnerBusiness
          onCreated={(business) =>
            router.push(`/owner/businesses/${business.id}/overview`)
          }
        />
      )}
    </ProtectedPage>
  )
}
