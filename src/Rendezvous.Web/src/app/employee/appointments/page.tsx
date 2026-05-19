"use client"

import { EmployeeApprovedAppointmentsPanel } from "@/components/dashboard/employee-appointment-requests-panel"
import {
  hasActiveMembership,
  ProtectedPage,
} from "@/components/layout/protected-page"

export default function EmployeeAppointmentsPage() {
  return (
    <ProtectedPage
      title="Employee appointments"
      description="Review assigned appointments and history."
      authorize={(user) => hasActiveMembership(user, "Employee")}
    >
      {() => <EmployeeApprovedAppointmentsPanel />}
    </ProtectedPage>
  )
}
