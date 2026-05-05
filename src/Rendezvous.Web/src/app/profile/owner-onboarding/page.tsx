"use client"

import { OwnerOnboardingRequestPanel } from "@/components/dashboard/owner-onboarding-request-panel"
import { ProtectedPage } from "@/components/layout/protected-page"

export default function ProfileOwnerOnboardingPage() {
  return (
    <ProtectedPage
      title="Owner application"
      description="Apply to open a business on Rendezvous."
    >
      {() => <OwnerOnboardingRequestPanel />}
    </ProtectedPage>
  )
}
