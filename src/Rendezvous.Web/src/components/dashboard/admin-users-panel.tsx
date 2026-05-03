"use client"

import { useState } from "react"
import { ShieldCheck, Search } from "lucide-react"

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
import { getAdminUser, getAdminUsers } from "@/lib/auth-api"
import type { AdminUser, AdminUserDetail } from "@/lib/auth-api"

export function AdminUsersPanel() {
  const [users, setUsers] = useState<AdminUser[]>([])
  const [selectedUser, setSelectedUser] = useState<AdminUserDetail | null>(null)
  const [search, setSearch] = useState("")
  const [isLoading, setIsLoading] = useState(false)
  const [hasLoaded, setHasLoaded] = useState(false)
  const [error, setError] = useState("")

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

  return (
    <Card>
      <CardHeader>
        <div className="flex items-center gap-2">
          <ShieldCheck className="size-4 text-primary" aria-hidden="true" />
          <CardTitle>Admin users</CardTitle>
        </div>
        <CardDescription>
          Read-only user lookup by email or public number.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="flex flex-col gap-2 sm:flex-row">
          <Input
            placeholder="Search users"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
          />
          <Button type="button" onClick={handleSearch} disabled={isLoading}>
            <Search data-icon="inline-start" className="size-4" />
            {isLoading ? "Searching" : "Search"}
          </Button>
        </div>
        {error ? (
          <p className="text-sm text-destructive">{error}</p>
        ) : !hasLoaded ? (
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
                    <p className="break-all text-sm font-medium text-foreground">
                      {user.email}
                    </p>
                    <p className="mt-1 text-xs text-muted-foreground">
                      Public number: {user.publicNumber}
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
        {selectedUser ? (
          <div className="rounded-lg border border-border bg-background p-3">
            <p className="text-sm font-medium text-foreground">
              {selectedUser.email}
            </p>
            <p className="mt-1 text-xs text-muted-foreground">
              User id: {selectedUser.id}
            </p>
            <div className="mt-3 grid gap-2">
              {selectedUser.businessMemberships.length > 0 ? (
                selectedUser.businessMemberships.map((membership) => (
                  <div
                    key={membership.businessId}
                    className="flex flex-wrap items-center justify-between gap-2 rounded-md border border-border p-2 text-sm"
                  >
                    <span className="font-medium text-foreground">
                      {membership.businessName}
                    </span>
                    <span className="flex gap-2">
                      <Badge>{membership.role}</Badge>
                      <Badge variant="outline">{membership.status}</Badge>
                    </span>
                  </div>
                ))
              ) : (
                <p className="text-sm text-muted-foreground">
                  No business memberships.
                </p>
              )}
            </div>
          </div>
        ) : null}
      </CardContent>
    </Card>
  )
}
