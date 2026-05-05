"use client"

import { FormEvent, useEffect, useState } from "react"
import { Building2 } from "lucide-react"

import { Alert, AlertDescription } from "@/components/ui/alert"
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
  createOwnerOnboardingRequest,
  getMyOwnerOnboardingRequests,
} from "@/lib/auth-api"
import type { OwnerOnboardingRequest } from "@/lib/auth-api"

export function OwnerOnboardingRequestPanel() {
  const [requests, setRequests] = useState<OwnerOnboardingRequest[]>([])
  const [businessName, setBusinessName] = useState("")
  const [ownerStaffDisplayName, setOwnerStaffDisplayName] = useState("")
  const [businessType, setBusinessType] = useState("1")
  const [message, setMessage] = useState("")
  const [error, setError] = useState("")
  const [isLoading, setIsLoading] = useState(true)
  const [isSubmitting, setIsSubmitting] = useState(false)

  useEffect(() => {
    loadRequests()
  }, [])

  async function loadRequests() {
    setIsLoading(true)
    try {
      setRequests(await getMyOwnerOnboardingRequests())
    } finally {
      setIsLoading(false)
    }
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setMessage("")
    setError("")
    setIsSubmitting(true)

    try {
      await createOwnerOnboardingRequest({
        businessName,
        businessType: Number(businessType),
        ownerStaffDisplayName: ownerStaffDisplayName || undefined,
      })
      setBusinessName("")
      setOwnerStaffDisplayName("")
      setMessage("Application submitted.")
      await loadRequests()
    } catch {
      setError("Application could not be submitted.")
    } finally {
      setIsSubmitting(false)
    }
  }

  const hasPendingRequest = requests.some((request) => request.status === "Pending")

  return (
    <div className="grid gap-4">
      <Card>
        <CardHeader>
          <div className="flex items-center gap-2">
            <Building2 className="size-4 text-primary" aria-hidden="true" />
            <CardTitle>Owner application</CardTitle>
          </div>
          <CardDescription>
            Apply to create a business owner account.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <form className="grid gap-4" onSubmit={handleSubmit}>
            <div className="grid gap-3 md:grid-cols-2">
              <div className="space-y-2">
                <Label htmlFor="owner-business-name">Business name</Label>
                <Input
                  id="owner-business-name"
                  value={businessName}
                  onChange={(event) => setBusinessName(event.target.value)}
                  placeholder="Neighborhood Barber"
                  required
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="owner-staff-display">Owner staff display name</Label>
                <Input
                  id="owner-staff-display"
                  value={ownerStaffDisplayName}
                  onChange={(event) =>
                    setOwnerStaffDisplayName(event.target.value)
                  }
                  placeholder="Optional"
                />
              </div>
            </div>
            <div className="space-y-2">
              <Label>Business type</Label>
              <Select value={businessType} onValueChange={setBusinessType}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="1">Barber</SelectItem>
                </SelectContent>
              </Select>
            </div>
            {error ? (
              <Alert className="border-destructive/30 bg-destructive/5 text-destructive">
                <AlertDescription>{error}</AlertDescription>
              </Alert>
            ) : null}
            {message ? (
              <Alert>
                <AlertDescription>{message}</AlertDescription>
              </Alert>
            ) : null}
            <Button type="submit" disabled={isSubmitting || hasPendingRequest}>
              <Building2 data-icon="inline-start" className="size-4" />
              {isSubmitting ? "Submitting" : "Submit application"}
            </Button>
          </form>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Applications</CardTitle>
          <CardDescription>Your owner onboarding history.</CardDescription>
        </CardHeader>
        <CardContent>
          {isLoading ? (
            <p className="text-sm leading-6 text-muted-foreground">
              Loading applications.
            </p>
          ) : requests.length === 0 ? (
            <p className="text-sm leading-6 text-muted-foreground">
              No applications yet.
            </p>
          ) : (
            <div className="grid gap-3">
              {requests.map((request) => (
                <div
                  key={request.id}
                  className="rounded-lg border border-border bg-background p-3"
                >
                  <div className="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
                    <div className="min-w-0">
                      <p className="font-medium text-foreground">
                        {request.businessName}
                      </p>
                      <p className="text-sm text-muted-foreground">
                        {request.businessType}
                      </p>
                      {request.adminNote ? (
                        <p className="mt-2 text-sm text-muted-foreground">
                          {request.adminNote}
                        </p>
                      ) : null}
                    </div>
                    <Badge variant="outline">{request.status}</Badge>
                  </div>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  )
}
