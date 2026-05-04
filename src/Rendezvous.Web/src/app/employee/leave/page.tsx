"use client"

import { EmployeeAvailabilityExceptionsPanel } from "@/components/dashboard/availability-exceptions-panel"
import {
  hasActiveMembership,
  ProtectedPage,
} from "@/components/layout/protected-page"

export default function EmployeeLeavePage() {
  return (
    <ProtectedPage
      title="My leave"
      description="Manage your staff leave records."
      authorize={(user) => hasActiveMembership(user, "Employee")}
    >
      {({ user }) => (
        <EmployeeAvailabilityExceptionsPanel
          memberships={user.businessMemberships}
        />
      )}
    </ProtectedPage>
  )
}
