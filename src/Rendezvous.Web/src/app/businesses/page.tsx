"use client"

import { useEffect, useState } from "react"

import { PublicBusinessList } from "@/components/public/public-business-components"
import { PublicShell } from "@/components/public/public-shell"
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { getPublicBusinesses } from "@/lib/public-api"
import type { PublicBusiness } from "@/lib/public-api"

export default function BusinessesPage() {
  const [businesses, setBusinesses] = useState<PublicBusiness[]>([])
  const [search, setSearch] = useState("")
  const [type, setType] = useState("")
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState("")

  useEffect(() => {
    let isMounted = true

    async function loadBusinesses() {
      setIsLoading(true)
      setError("")

      try {
        const nextBusinesses = await getPublicBusinesses()

        if (isMounted) {
          setBusinesses(nextBusinesses)
        }
      } catch {
        if (isMounted) {
          setError("Businesses could not be loaded.")
        }
      } finally {
        if (isMounted) {
          setIsLoading(false)
        }
      }
    }

    loadBusinesses()

    return () => {
      isMounted = false
    }
  }, [])

  async function handleSearch() {
    setIsLoading(true)
    setError("")

    try {
      setBusinesses(await getPublicBusinesses({ search, type }))
    } catch {
      setError("Businesses could not be loaded.")
    } finally {
      setIsLoading(false)
    }
  }

  return (
    <PublicShell
      title="Businesses"
      description="Browse approved businesses and review their active services."
    >
      <Card>
        <CardHeader>
          <CardTitle>Find a business</CardTitle>
          <CardDescription>
            Search approved businesses by name and type.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="grid gap-2 md:grid-cols-[minmax(0,1fr)_180px_auto]">
            <Input
              placeholder="Search businesses"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
            />
            <select
              className="h-10 rounded-md border border-input bg-transparent px-3 text-sm outline-none focus-visible:ring-3 focus-visible:ring-ring/35"
              value={type}
              onChange={(event) => setType(event.target.value)}
            >
              <option value="">All types</option>
              <option value="Barber">Barber</option>
            </select>
            <Button type="button" onClick={handleSearch} disabled={isLoading}>
              Search
            </Button>
          </div>
        </CardContent>
      </Card>
      {isLoading ? (
        <Card className="mx-auto w-full max-w-xl">
          <CardHeader>
            <CardTitle>Loading businesses</CardTitle>
            <CardDescription>
              Fetching approved businesses from the public API.
            </CardDescription>
          </CardHeader>
        </Card>
      ) : error ? (
        <Card className="mx-auto w-full max-w-xl">
          <CardHeader>
            <CardTitle>Businesses unavailable</CardTitle>
            <CardDescription>{error}</CardDescription>
          </CardHeader>
        </Card>
      ) : (
        <PublicBusinessList businesses={businesses} />
      )}
    </PublicShell>
  )
}
