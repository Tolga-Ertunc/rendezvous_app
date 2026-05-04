"use client"

import { EmployeeAppointmentRequestsPanel } from "@/components/dashboard/employee-appointment-requests-panel"
import {
  hasActiveMembership,
  ProtectedPage,
} from "@/components/layout/protected-page"

export default function EmployeeRequestsPage() {
  return (
    <ProtectedPage
      title="Employee requests"
      description="Manage appointment requests assigned to you."
      authorize={(user) => hasActiveMembership(user, "Employee")}
    >
      {() => <EmployeeAppointmentRequestsPanel />}
    </ProtectedPage>
  )
}
