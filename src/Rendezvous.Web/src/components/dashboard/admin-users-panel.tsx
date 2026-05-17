"use client"

import { useEffect, useState } from "react"
import {
  BriefcaseBusiness,
  Plus,
  Search,
  ShieldCheck,
  UserCheck,
  UserX,
} from "lucide-react"

import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import {
  activateAdminUserBusinessMembership,
  addAdminUserRole,
  getAdminBusinesses,
  getAdminUser,
  getAdminUsers,
  removeAdminUserRole,
  suspendAdminUser,
  suspendAdminUserBusinessMembership,
  unsuspendAdminUser,
  upsertAdminUserBusinessMembership,
} from "@/lib/auth-api"
import type { AdminUser, AdminUserDetail, OwnerBusiness } from "@/lib/auth-api"

type BusinessMembershipRoleValue = "Owner" | "Employee"
type BusinessMembershipStatusValue = "Active" | "Suspended"

export function AdminUsersPanel() {
  const [users, setUsers] = useState<AdminUser[]>([])
  const [businesses, setBusinesses] = useState<OwnerBusiness[]>([])
  const [selectedUser, setSelectedUser] = useState<AdminUserDetail | null>(null)
  const [search, setSearch] = useState("")
  const [roleName, setRoleName] = useState("User")
  const [membershipBusinessId, setMembershipBusinessId] = useState("")
  const [membershipRole, setMembershipRole] =
    useState<BusinessMembershipRoleValue>("Employee")
  const [membershipStatus, setMembershipStatus] =
    useState<BusinessMembershipStatusValue>("Active")
  const [isLoading, setIsLoading] = useState(false)
  const [isMutating, setIsMutating] = useState(false)
  const [hasLoaded, setHasLoaded] = useState(false)
  const [error, setError] = useState("")

  useEffect(() => {
    async function loadBusinesses() {
      try {
        const nextBusinesses = await getAdminBusinesses()
        setBusinesses(nextBusinesses)
        if (nextBusinesses.length > 0) {
          setMembershipBusinessId(nextBusinesses[0].id)
        }
      } catch {
        setBusinesses([])
      }
    }

    loadBusinesses()
  }, [])

  async function handleSearch() {
    setIsLoading(true)
    setError("")

    try {
      const nextUsers = await getAdminUsers({ search })
      setUsers(nextUsers)
      setSelectedUser(null)
      setHasLoaded(true)
    } catch {
      setError("Users could not be loaded.")
    } finally {
      setIsLoading(false)
    }
  }

  async function handleLoadDetail(userId: string) {
    setIsLoading(true)
    setError("")

    try {
      setSelectedUser(await getAdminUser(userId))
    } catch {
      setError("User detail could not be loaded.")
    } finally {
      setIsLoading(false)
    }
  }

  async function handleSuspendToggle() {
    if (!selectedUser) {
      return
    }

    await mutateSelectedUser(() =>
      selectedUser.isSuspended
        ? unsuspendAdminUser(selectedUser.id)
        : suspendAdminUser(selectedUser.id)
    )
  }

  async function handleAddRole() {
    if (!selectedUser || !roleName.trim()) {
      return
    }

    await mutateSelectedUser(() => addAdminUserRole(selectedUser.id, roleName))
  }

  async function handleRemoveRole(nextRoleName: string) {
    if (!selectedUser) {
      return
    }

    await mutateSelectedUser(() =>
      removeAdminUserRole(selectedUser.id, nextRoleName)
    )
  }

  async function handleUpsertMembership() {
    if (!selectedUser || !membershipBusinessId) {
      return
    }

    await mutateSelectedUser(() =>
      upsertAdminUserBusinessMembership(selectedUser.id, {
        businessId: membershipBusinessId,
        role: membershipRole,
        status: membershipStatus,
      })
    )
  }

  async function handleMembershipStatus(
    businessId: string,
    status: BusinessMembershipStatusValue
  ) {
    if (!selectedUser) {
      return
    }

    await mutateSelectedUser(() =>
      status === "Active"
        ? activateAdminUserBusinessMembership(selectedUser.id, businessId)
        : suspendAdminUserBusinessMembership(selectedUser.id, businessId)
    )
  }

  async function mutateSelectedUser(
    mutation: () => Promise<AdminUserDetail>
  ) {
    setIsMutating(true)
    setError("")

    try {
      const nextUser = await mutation()
      setSelectedUser(nextUser)
      setUsers((current) =>
        current.map((user) =>
          user.id === nextUser.id
            ? {
                ...user,
                firstName: nextUser.firstName,
                lastName: nextUser.lastName,
                fullName: nextUser.fullName,
                email: nextUser.email,
                isSuspended: nextUser.isSuspended,
                roles: nextUser.roles,
              }
            : user
        )
      )
    } catch {
      setError("Admin user update failed.")
    } finally {
      setIsMutating(false)
    }
  }

  return (
    <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_24rem]">
      <Card>
        <CardHeader>
          <div className="flex items-center gap-2">
            <ShieldCheck className="size-4 text-primary" aria-hidden="true" />
            <CardTitle>Admin users</CardTitle>
          </div>
          <CardDescription>
            Search users, suspend accounts, manage global roles and business
            memberships.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex flex-col gap-2 sm:flex-row">
            <Input
              placeholder="Search by name, email, or public number"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
            />
            <Button type="button" onClick={handleSearch} disabled={isLoading}>
              <Search data-icon="inline-start" className="size-4" />
              {isLoading ? "Searching" : "Search"}
            </Button>
          </div>
          {error ? <p className="text-sm text-destructive">{error}</p> : null}
          {!hasLoaded ? (
            <p className="text-sm leading-6 text-muted-foreground">
              Run a search to load users.
            </p>
          ) : users.length === 0 ? (
            <p className="text-sm leading-6 text-muted-foreground">
              No users matched the current search.
            </p>
          ) : (
            <div className="grid gap-3">
              {users.map((user) => (
                <div
                  key={user.id}
                  className="rounded-lg border border-border bg-background p-3"
                >
                  <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                    <div className="min-w-0">
                      <div className="flex flex-wrap items-center gap-2">
                        <p className="break-all text-sm font-medium text-foreground">
                          {user.fullName || "Name not set"}
                        </p>
                        {user.isSuspended ? (
                          <Badge className="border-destructive/30 bg-destructive/10 text-destructive">
                            Suspended
                          </Badge>
                        ) : null}
                      </div>
                      <p className="mt-1 break-all text-xs text-muted-foreground">
                        {user.email} · Public number: {user.publicNumber}
                      </p>
                    </div>
                    <div className="flex flex-wrap gap-2">
                      {user.roles.map((role) => (
                        <Badge key={role} variant="outline">
                          {role}
                        </Badge>
                      ))}
                      <Button
                        type="button"
                        size="sm"
                        variant="outline"
                        onClick={() => handleLoadDetail(user.id)}
                      >
                        View detail
                      </Button>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>User management</CardTitle>
          <CardDescription>
            {selectedUser
              ? `${selectedUser.fullName || "Name not set"} · ${selectedUser.email}`
              : "Select a user to manage access."}
          </CardDescription>
        </CardHeader>
        <CardContent>
          {selectedUser ? (
            <div className="space-y-5">
              <div className="space-y-2">
                <div className="flex flex-wrap items-center gap-2">
                  <Badge
                    variant={selectedUser.isSuspended ? undefined : "outline"}
                    className={
                      selectedUser.isSuspended
                        ? "border-destructive/30 bg-destructive/10 text-destructive"
                        : undefined
                    }
                  >
                    {selectedUser.isSuspended ? "Suspended" : "Active"}
                  </Badge>
                  {selectedUser.roles.map((role) => (
                    <Badge key={role}>{role}</Badge>
                  ))}
                </div>
                <Button
                  type="button"
                  variant={selectedUser.isSuspended ? "outline" : "destructive"}
                  onClick={handleSuspendToggle}
                  disabled={isMutating}
                >
                  {selectedUser.isSuspended ? (
                    <UserCheck data-icon="inline-start" className="size-4" />
                  ) : (
                    <UserX data-icon="inline-start" className="size-4" />
                  )}
                  {selectedUser.isSuspended ? "Unsuspend user" : "Suspend user"}
                </Button>
              </div>

              <div className="space-y-3 rounded-lg border border-border p-3">
                <Label>Global roles</Label>
                <div className="grid gap-2 sm:grid-cols-[1fr_auto]">
                  <Select value={roleName} onValueChange={setRoleName}>
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="User">User</SelectItem>
                      <SelectItem value="Admin">Admin</SelectItem>
                    </SelectContent>
                  </Select>
                  <Button
                    type="button"
                    variant="outline"
                    onClick={handleAddRole}
                    disabled={isMutating}
                  >
                    <Plus data-icon="inline-start" className="size-4" />
                    Add
                  </Button>
                </div>
                <div className="grid gap-2">
                  {selectedUser.roles.map((role) => (
                    <div
                      key={role}
                      className="flex items-center justify-between gap-2 rounded-md border border-border p-2"
                    >
                      <Badge>{role}</Badge>
                      <Button
                        type="button"
                        size="sm"
                        variant="outline"
                        onClick={() => handleRemoveRole(role)}
                        disabled={isMutating}
                      >
                        Remove
                      </Button>
                    </div>
                  ))}
                </div>
              </div>

              <div className="space-y-3 rounded-lg border border-border p-3">
                <div className="flex items-center gap-2">
                  <BriefcaseBusiness
                    className="size-4 text-primary"
                    aria-hidden="true"
                  />
                  <Label>Business membership</Label>
                </div>
                <Select
                  value={membershipBusinessId}
                  onValueChange={setMembershipBusinessId}
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Business" />
                  </SelectTrigger>
                  <SelectContent>
                    {businesses.map((business) => (
                      <SelectItem key={business.id} value={business.id}>
                        {business.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <div className="grid gap-2 sm:grid-cols-2">
                  <Select
                    value={membershipRole}
                    onValueChange={(value) =>
                      setMembershipRole(value as BusinessMembershipRoleValue)
                    }
                  >
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="Owner">Owner</SelectItem>
                      <SelectItem value="Employee">Employee</SelectItem>
                    </SelectContent>
                  </Select>
                  <Select
                    value={membershipStatus}
                    onValueChange={(value) =>
                      setMembershipStatus(value as BusinessMembershipStatusValue)
                    }
                  >
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="Active">Active</SelectItem>
                      <SelectItem value="Suspended">Suspended</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
                <Button
                  type="button"
                  onClick={handleUpsertMembership}
                  disabled={isMutating || !membershipBusinessId}
                >
                  Save membership
                </Button>
              </div>

              <div className="space-y-3">
                <Label>Current memberships</Label>
                {selectedUser.businessMemberships.length > 0 ? (
                  selectedUser.businessMemberships.map((membership) => (
                    <div
                      key={membership.businessId}
                      className="space-y-3 rounded-md border border-border p-3 text-sm"
                    >
                      <div className="flex flex-wrap items-center justify-between gap-2">
                        <span className="font-medium text-foreground">
                          {membership.businessName}
                        </span>
                        <span className="flex gap-2">
                          <Badge>{membership.role}</Badge>
                          <Badge variant="outline">{membership.status}</Badge>
                        </span>
                      </div>
                      <div className="flex flex-wrap gap-2">
                        <Button
                          type="button"
                          size="sm"
                          variant="outline"
                          onClick={() =>
                            handleMembershipStatus(
                              membership.businessId,
                              "Active"
                            )
                          }
                          disabled={isMutating || membership.status === "Active"}
                        >
                          Activate
                        </Button>
                        <Button
                          type="button"
                          size="sm"
                          variant="outline"
                          onClick={() =>
                            handleMembershipStatus(
                              membership.businessId,
                              "Suspended"
                            )
                          }
                          disabled={
                            isMutating || membership.status === "Suspended"
                          }
                        >
                          Suspend
                        </Button>
                      </div>
                    </div>
                  ))
                ) : (
                  <p className="text-sm text-muted-foreground">
                    No business memberships.
                  </p>
                )}
              </div>
            </div>
          ) : (
            <p className="text-sm leading-6 text-muted-foreground">
              Search and select a user first.
            </p>
          )}
        </CardContent>
      </Card>
    </div>
  )
}
