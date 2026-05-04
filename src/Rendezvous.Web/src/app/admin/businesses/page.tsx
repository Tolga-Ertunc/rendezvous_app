"use client"

import { useEffect, useState } from "react"
import { Search } from "lucide-react"

import { BusinessList } from "@/components/dashboard/business-components"
import { ProtectedPage } from "@/components/layout/protected-page"
import { Button } from "@/components/ui/button"
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { getAdminBusinesses } from "@/lib/auth-api"
import type { OwnerBusiness } from "@/lib/auth-api"

export default function AdminBusinessesPage() {
  return (
    <ProtectedPage
      title="Admin businesses"
      description="Review and filter businesses."
      authorize={(user) => user.roles.includes("Admin")}
    >
      {() => <AdminBusinessesContent />}
    </ProtectedPage>
  )
}

function AdminBusinessesContent() {
  const [businesses, setBusinesses] = useState<OwnerBusiness[]>([])
  const [search, setSearch] = useState("")
  const [status, setStatus] = useState("all")
  const [isLoading, setIsLoading] = useState(true)

  useEffect(() => {
    let isMounted = true

    async function loadBusinesses() {
      const nextBusinesses = await getAdminBusinesses()
      if (isMounted) {
        setBusinesses(nextBusinesses)
        setIsLoading(false)
      }
    }

    loadBusinesses()

    return () => {
      isMounted = false
    }
  }, [])

  async function handleFilter() {
    setIsLoading(true)
    setBusinesses(
      await getAdminBusinesses({
        search,
        status: status === "all" ? "" : status,
      })
    )
    setIsLoading(false)
  }

  return (
    <div className="grid gap-4">
      <Card>
        <CardHeader>
          <CardTitle>Filters</CardTitle>
          <CardDescription>Search by name and status.</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="grid gap-2 md:grid-cols-[minmax(0,1fr)_190px_auto]">
            <Input
              placeholder="Search businesses"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
            />
            <Select value={status} onValueChange={setStatus}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All statuses</SelectItem>
                <SelectItem value="PendingApproval">Pending approval</SelectItem>
                <SelectItem value="Approved">Approved</SelectItem>
                <SelectItem value="Suspended">Suspended</SelectItem>
                <SelectItem value="Rejected">Rejected</SelectItem>
              </SelectContent>
            </Select>
            <Button type="button" disabled={isLoading} onClick={handleFilter}>
              <Search data-icon="inline-start" className="size-4" />
              {isLoading ? "Loading" : "Apply"}
            </Button>
          </div>
        </CardContent>
      </Card>
      <BusinessList
        title="Businesses"
        description="Admin visibility across all business statuses."
        businesses={businesses}
        emptyText={isLoading ? "Loading businesses." : "No businesses found."}
        detailHref={(businessId) => `/admin/businesses/${businessId}`}
      />
    </div>
  )
}
