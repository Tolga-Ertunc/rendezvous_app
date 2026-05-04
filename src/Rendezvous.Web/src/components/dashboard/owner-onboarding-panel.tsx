"use client"

import { FormEvent, useState } from "react"
import { Building2 } from "lucide-react"

import { Alert, AlertDescription } from "@/components/ui/alert"
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
import { ApiError } from "@/lib/api-client"
import { createOwnerBusiness } from "@/lib/auth-api"
import type { BusinessDetail } from "@/lib/auth-api"

type OwnerOnboardingPanelProps = {
  hasOwnerBusiness: boolean
  onCreated: (business: BusinessDetail) => void
}

export function OwnerOnboardingPanel({
  hasOwnerBusiness,
  onCreated,
}: OwnerOnboardingPanelProps) {
  const [name, setName] = useState("")
  const [staffDisplayName, setStaffDisplayName] = useState("")
  const [message, setMessage] = useState("")
  const [error, setError] = useState("")
  const [isSubmitting, setIsSubmitting] = useState(false)

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setMessage("")
    setError("")
    setIsSubmitting(true)

    try {
      const business = await createOwnerBusiness({
        name,
        type: 1,
        ownerStaffDisplayName: staffDisplayName || undefined,
      })
      setName("")
      setStaffDisplayName("")
      setMessage("Business created as pending approval.")
      onCreated(business)
    } catch (caughtError) {
      if (caughtError instanceof ApiError && caughtError.status === 400) {
        setError("Business name is required.")
      } else {
        setError("Business could not be created. Try again.")
      }
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <Card>
      <CardHeader>
        <div className="flex items-center gap-2">
          <Building2 className="size-4 text-primary" aria-hidden="true" />
          <CardTitle>Business onboarding</CardTitle>
        </div>
        <CardDescription>
          {hasOwnerBusiness
            ? "Create another business under this account."
            : "Create another business under this owner account. New businesses start as pending approval."}
        </CardDescription>
      </CardHeader>
      <CardContent>
        <form className="grid gap-4" onSubmit={handleSubmit}>
          <div className="grid gap-2 md:grid-cols-[minmax(0,1fr)_minmax(0,1fr)]">
            <div className="space-y-2">
              <Label htmlFor="business-name">Business name</Label>
              <Input
                id="business-name"
                value={name}
                onChange={(event) => setName(event.target.value)}
                placeholder="Neighborhood Barber"
                required
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="owner-staff-name">Owner staff display name</Label>
              <Input
                id="owner-staff-name"
                value={staffDisplayName}
                onChange={(event) => setStaffDisplayName(event.target.value)}
                placeholder="Optional"
              />
            </div>
          </div>

          <div className="space-y-2">
            <Label htmlFor="business-type">Business type</Label>
            <Select value="1" disabled>
              <SelectTrigger id="business-type">
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

          <Button type="submit" disabled={isSubmitting}>
            <Building2 data-icon="inline-start" className="size-4" />
            {isSubmitting ? "Creating business" : "Create business"}
          </Button>
        </form>
      </CardContent>
    </Card>
  )
}
