"use client"

import Link from "next/link"
import { Building2 } from "lucide-react"

import { AccountCard } from "@/components/dashboard/account-card"
import { ProtectedPage } from "@/components/layout/protected-page"
import { buttonVariants } from "@/components/ui/button"
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import { cn } from "@/lib/utils"

export default function ProfilePage() {
  return (
    <ProtectedPage title="Profile" description="Your account details.">
      {({ user }) => (
        <div className="grid gap-4">
          <AccountCard user={user} />
          <Card>
            <CardHeader>
              <div className="flex items-center gap-2">
                <Building2 className="size-4 text-primary" aria-hidden="true" />
                <CardTitle>Owner application</CardTitle>
              </div>
              <CardDescription>
                Request approval to open and manage a business.
              </CardDescription>
            </CardHeader>
            <CardContent>
              <Link
                href="/profile/owner-onboarding"
                className={cn(buttonVariants({ variant: "outline" }))}
              >
                Open application
              </Link>
            </CardContent>
          </Card>
        </div>
      )}
    </ProtectedPage>
  )
}
