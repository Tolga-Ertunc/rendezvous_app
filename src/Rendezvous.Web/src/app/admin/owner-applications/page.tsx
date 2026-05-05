"use client"

import { AdminOwnerOnboardingPanel } from "@/components/dashboard/admin-owner-onboarding-panel"
import { ProtectedPage } from "@/components/layout/protected-page"

export default function AdminOwnerApplicationsPage() {
  return (
    <ProtectedPage
      title="Owner applications"
      description="Approve or reject business owner applications."
      authorize={(user) => user.roles.includes("Admin")}
    >
      {() => <AdminOwnerOnboardingPanel />}
    </ProtectedPage>
  )
}
