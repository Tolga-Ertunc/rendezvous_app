"use client"

import { CustomerAppointmentsPanel } from "@/components/dashboard/customer-appointments-panel"
import { ProtectedPage } from "@/components/layout/protected-page"

export default function AppointmentsPage() {
  return (
    <ProtectedPage
      title="My appointments"
      description="Review your appointment requests and bookings."
    >
      {() => <CustomerAppointmentsPanel />}
    </ProtectedPage>
  )
}
