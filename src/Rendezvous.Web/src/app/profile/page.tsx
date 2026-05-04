"use client"

import { AccountCard } from "@/components/dashboard/account-card"
import { ProtectedPage } from "@/components/layout/protected-page"

export default function ProfilePage() {
  return (
    <ProtectedPage title="Profile" description="Your account details.">
      {({ user }) => <AccountCard user={user} />}
    </ProtectedPage>
  )
}
