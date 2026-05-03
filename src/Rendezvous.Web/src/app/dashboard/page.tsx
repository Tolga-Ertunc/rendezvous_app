"use client"

import { useEffect, useMemo, useState } from "react"
import { LogOut, ShieldCheck, UserRound } from "lucide-react"
import { useRouter } from "next/navigation"

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs"
import { Input } from "@/components/ui/input"
import { AdminUsersPanel } from "@/components/dashboard/admin-users-panel"
import { BusinessList } from "@/components/dashboard/business-components"
import { CustomerAppointmentsPanel } from "@/components/dashboard/customer-appointments-panel"
import { DashboardShell } from "@/components/dashboard/dashboard-shell"
import { EmployeeAppointmentRequestsPanel } from "@/components/dashboard/employee-appointment-requests-panel"
import { OwnerOnboardingPanel } from "@/components/dashboard/owner-onboarding-panel"
import {
  getAdminBusinesses,
  getCurrentUser,
  getOwnerBusinesses,
  logout,
} from "@/lib/auth-api"
import type { CurrentUser, OwnerBusiness } from "@/lib/auth-api"
import { clearAuthTokens, getAccessToken } from "@/lib/auth-storage"

export default function DashboardPage() {
  const router = useRouter()
  const [user, setUser] = useState<CurrentUser | null>(null)
  const [ownerBusinesses, setOwnerBusinesses] = useState<OwnerBusiness[]>([])
  const [adminBusinesses, setAdminBusinesses] = useState<OwnerBusiness[]>([])
  const [adminBusinessSearch, setAdminBusinessSearch] = useState("")
  const [adminBusinessStatus, setAdminBusinessStatus] = useState("")
  const [isLoading, setIsLoading] = useState(true)
  const [isFilteringAdminBusinesses, setIsFilteringAdminBusinesses] =
    useState(false)
  const [isLoggingOut, setIsLoggingOut] = useState(false)
  const [error, setError] = useState("")

  const isOwner = useMemo(
    () =>
      user?.businessMemberships.some(
        (membership) =>
          membership.role === "Owner" && membership.status === "Active"
      ) ?? false,
    [user]
  )
  const isEmployee = useMemo(
    () =>
      user?.businessMemberships.some(
        (membership) =>
          membership.role === "Employee" && membership.status === "Active"
      ) ?? false,
    [user]
  )
  const isAdmin = useMemo(
    () => user?.roles.includes("Admin") ?? false,
    [user]
  )

  useEffect(() => {
    if (!getAccessToken()) {
      router.replace("/login")
      return
    }

    let isMounted = true

    async function loadDashboard() {
      setIsLoading(true)
      setError("")

      try {
        const currentUser = await getCurrentUser()
        const isCurrentUserOwner = currentUser.businessMemberships.some(
          (membership) =>
            membership.role === "Owner" && membership.status === "Active"
        )
        const isCurrentUserAdmin = currentUser.roles.includes("Admin")
        const [ownedBusinesses, administratedBusinesses] = await Promise.all([
          isCurrentUserOwner ? getOwnerBusinesses() : Promise.resolve([]),
          isCurrentUserAdmin ? getAdminBusinesses() : Promise.resolve([]),
        ])

        if (!isMounted) {
          return
        }

        setUser(currentUser)
        setOwnerBusinesses(ownedBusinesses)
        setAdminBusinesses(administratedBusinesses)
      } catch {
        clearAuthTokens()

        if (isMounted) {
          setError("Your session could not be loaded. Please sign in again.")
        }
      } finally {
        if (isMounted) {
          setIsLoading(false)
        }
      }
    }

    loadDashboard()

    return () => {
      isMounted = false
    }
  }, [router])

  async function handleLogout() {
    setIsLoggingOut(true)

    try {
      await logout()
    } finally {
      clearAuthTokens()
      router.replace("/login")
    }
  }

  async function handleBusinessCreated() {
    const [currentUser, ownedBusinesses] = await Promise.all([
      getCurrentUser(),
      getOwnerBusinesses(),
    ])

    setUser(currentUser)
    setOwnerBusinesses(ownedBusinesses)
  }

  if (isLoading) {
    return (
      <DashboardShell
        title="Dashboard"
        description="Checking your session and business access."
      >
        <Card className="mx-auto w-full max-w-xl">
          <CardHeader>
            <CardTitle>Loading workspace</CardTitle>
            <CardDescription>
              Checking your session and business access.
            </CardDescription>
          </CardHeader>
        </Card>
      </DashboardShell>
    )
  }

  if (error || !user) {
    return (
      <DashboardShell
        title="Dashboard"
        description="Your current session could not be loaded."
      >
        <Card className="mx-auto w-full max-w-xl">
          <CardHeader>
            <CardTitle>Session unavailable</CardTitle>
            <CardDescription>{error}</CardDescription>
          </CardHeader>
          <CardContent>
            <Button type="button" onClick={() => router.replace("/login")}>
              Return to sign in
            </Button>
          </CardContent>
        </Card>
      </DashboardShell>
    )
  }

  return (
    <DashboardShell
      title="Dashboard"
      description="Review account access and open read-only business management views."
      actions={
        <Button
          type="button"
          variant="outline"
          onClick={handleLogout}
          disabled={isLoggingOut}
        >
          <LogOut data-icon="inline-start" className="size-4" />
          {isLoggingOut ? "Signing out" : "Sign out"}
        </Button>
      }
    >
      <Tabs defaultValue="account">
        <TabsList>
          <TabsTrigger value="account">Account</TabsTrigger>
          <TabsTrigger value="appointments">My appointments</TabsTrigger>
          {isEmployee ? (
            <TabsTrigger value="employee-requests">
              Employee requests
            </TabsTrigger>
          ) : null}
          {isOwner ? (
            <TabsTrigger value="owner-businesses">Owner businesses</TabsTrigger>
          ) : null}
          {isAdmin ? (
            <TabsTrigger value="admin-businesses">Admin businesses</TabsTrigger>
          ) : null}
        </TabsList>

        <TabsContent value="account">
          <div className="grid gap-4">
            <div className="grid gap-4 lg:grid-cols-[minmax(0,0.9fr)_minmax(0,1.1fr)]">
              <AccountCard user={user} />
              <MembershipCard user={user} />
            </div>
            <OwnerOnboardingPanel
              hasOwnerBusiness={isOwner}
              onCreated={handleBusinessCreated}
            />
          </div>
        </TabsContent>

        <TabsContent value="appointments">
          <CustomerAppointmentsPanel />
        </TabsContent>

        {isEmployee ? (
          <TabsContent value="employee-requests">
            <EmployeeAppointmentRequestsPanel />
          </TabsContent>
        ) : null}

        {isOwner ? (
          <TabsContent value="owner-businesses">
            <BusinessList
              title="Owner businesses"
              description="Businesses managed through active owner memberships."
              businesses={ownerBusinesses}
              emptyText="Owner access is active, but no businesses were returned."
              detailHref={(businessId) =>
                `/dashboard/owner/businesses/${businessId}`
              }
            />
          </TabsContent>
        ) : null}

        {isAdmin ? (
          <TabsContent value="admin-businesses">
            <div className="grid gap-4">
              <Card>
                <CardHeader>
                  <CardTitle>Admin business filters</CardTitle>
                  <CardDescription>
                    Filter businesses by name and status.
                  </CardDescription>
                </CardHeader>
                <CardContent>
                  <div className="grid gap-2 md:grid-cols-[minmax(0,1fr)_180px_auto]">
                    <Input
                      placeholder="Search businesses"
                      value={adminBusinessSearch}
                      onChange={(event) =>
                        setAdminBusinessSearch(event.target.value)
                      }
                    />
                    <select
                      className="h-10 rounded-md border border-input bg-transparent px-3 text-sm outline-none focus-visible:ring-3 focus-visible:ring-ring/35"
                      value={adminBusinessStatus}
                      onChange={(event) =>
                        setAdminBusinessStatus(event.target.value)
                      }
                    >
                      <option value="">All statuses</option>
                      <option value="PendingApproval">Pending approval</option>
                      <option value="Approved">Approved</option>
                      <option value="Suspended">Suspended</option>
                      <option value="Rejected">Rejected</option>
                    </select>
                    <Button
                      type="button"
                      disabled={isFilteringAdminBusinesses}
                      onClick={async () => {
                        setIsFilteringAdminBusinesses(true)
                        try {
                          setAdminBusinesses(
                            await getAdminBusinesses({
                              search: adminBusinessSearch,
                              status: adminBusinessStatus,
                            })
                          )
                        } finally {
                          setIsFilteringAdminBusinesses(false)
                        }
                      }}
                    >
                      {isFilteringAdminBusinesses ? "Filtering" : "Apply"}
                    </Button>
                  </div>
                </CardContent>
              </Card>
              <BusinessList
                title="Admin businesses"
                description="Read-only visibility across all businesses in the system."
                businesses={adminBusinesses}
                emptyText="No businesses are available for admin review."
                detailHref={(businessId) =>
                  `/dashboard/admin/businesses/${businessId}`
                }
              />
              <AdminUsersPanel />
            </div>
          </TabsContent>
        ) : null}
      </Tabs>
    </DashboardShell>
  )
}

function AccountCard({ user }: { user: CurrentUser }) {
  return (
    <Card>
      <CardHeader>
        <div className="flex items-center gap-2">
          <UserRound className="size-4 text-primary" aria-hidden="true" />
          <CardTitle>Account</CardTitle>
        </div>
        <CardDescription>
          Identity and global roles returned by the auth API.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-5">
        <dl className="grid gap-4 text-sm">
          <div className="space-y-1">
            <dt className="text-muted-foreground">Email</dt>
            <dd className="break-all font-medium text-foreground">
              {user.email}
            </dd>
          </div>
          <div className="space-y-1">
            <dt className="text-muted-foreground">Public number</dt>
            <dd className="font-medium text-foreground">{user.publicNumber}</dd>
          </div>
          <div className="space-y-2">
            <dt className="text-muted-foreground">Global roles</dt>
            <dd className="flex flex-wrap gap-2">
              {user.roles.map((role) => (
                <Badge key={role}>{role}</Badge>
              ))}
            </dd>
          </div>
        </dl>
      </CardContent>
    </Card>
  )
}

function MembershipCard({ user }: { user: CurrentUser }) {
  return (
    <Card>
      <CardHeader>
        <div className="flex items-center gap-2">
          <ShieldCheck className="size-4 text-primary" aria-hidden="true" />
          <CardTitle>Business access</CardTitle>
        </div>
        <CardDescription>
          Owner and employee access come from active business memberships. Admin
          access uses separate read-only admin routes.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-5">
        {user.businessMemberships.length > 0 ? (
          <div className="space-y-3">
            {user.businessMemberships.map((membership) => (
              <div
                key={membership.businessId}
                className="rounded-lg border border-border bg-background p-3"
              >
                <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
                  <div className="min-w-0">
                    <p className="truncate text-sm font-medium text-foreground">
                      {membership.businessName}
                    </p>
                    <p className="mt-1 break-all text-xs text-muted-foreground">
                      {membership.businessId}
                    </p>
                  </div>
                  <div className="flex shrink-0 flex-wrap gap-2">
                    <Badge>{membership.role}</Badge>
                    <Badge variant="outline">{membership.status}</Badge>
                  </div>
                </div>
              </div>
            ))}
          </div>
        ) : (
          <Alert>
            <AlertTitle>No business membership</AlertTitle>
            <AlertDescription>
              This account has no active business-level membership.
            </AlertDescription>
          </Alert>
        )}
      </CardContent>
    </Card>
  )
}
