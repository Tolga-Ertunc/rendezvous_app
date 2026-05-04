"use client"

import { AdminUsersPanel } from "@/components/dashboard/admin-users-panel"
import { ProtectedPage } from "@/components/layout/protected-page"

export default function AdminUsersPage() {
  return (
    <ProtectedPage
      title="Admin users"
      description="Read-only user lookup."
      authorize={(user) => user.roles.includes("Admin")}
    >
      {() => <AdminUsersPanel />}
    </ProtectedPage>
  )
}
