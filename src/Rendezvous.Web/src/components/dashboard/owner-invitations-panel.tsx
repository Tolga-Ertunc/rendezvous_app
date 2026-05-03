"use client"

import { FormEvent, useEffect, useState } from "react"
import { MailPlus } from "lucide-react"

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
import { ApiError } from "@/lib/api-client"
import {
  createOwnerBusinessInvitation,
  getOwnerBusinessInvitations,
} from "@/lib/auth-api"
import type { OwnerBusinessInvitation } from "@/lib/auth-api"

type OwnerInvitationsPanelProps = {
  businessId: string
}

export function OwnerInvitationsPanel({
  businessId,
}: OwnerInvitationsPanelProps) {
  const [invitations, setInvitations] = useState<OwnerBusinessInvitation[]>([])
  const [email, setEmail] = useState("")
  const [staffDisplayName, setStaffDisplayName] = useState("")
  const [latestToken, setLatestToken] = useState("")
  const [error, setError] = useState("")
  const [isLoading, setIsLoading] = useState(true)
  const [isSubmitting, setIsSubmitting] = useState(false)

  useEffect(() => {
    let isMounted = true

    async function loadInvitations() {
      setIsLoading(true)
      setError("")

      try {
        const nextInvitations = await getOwnerBusinessInvitations(businessId)
        if (isMounted) {
          setInvitations(nextInvitations)
        }
      } catch {
        if (isMounted) {
          setError("Invitations could not be loaded.")
        }
      } finally {
        if (isMounted) {
          setIsLoading(false)
        }
      }
    }

    loadInvitations()

    return () => {
      isMounted = false
    }
  }, [businessId])

  async function handleCreate(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError("")
    setLatestToken("")
    setIsSubmitting(true)

    try {
      const invitation = await createOwnerBusinessInvitation(businessId, {
        email,
        staffDisplayName,
      })
      setInvitations((current) => [invitation, ...current])
      setLatestToken(invitation.acceptanceToken ?? "")
      setEmail("")
      setStaffDisplayName("")
    } catch (caughtError) {
      if (caughtError instanceof ApiError && caughtError.status === 409) {
        setError("This email already has an active invitation.")
      } else {
        setError("Invitation could not be created.")
      }
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <Card>
      <CardHeader>
        <div className="flex items-center gap-2">
          <MailPlus className="size-4 text-primary" aria-hidden="true" />
          <CardTitle>Employee invitations</CardTitle>
        </div>
        <CardDescription>
          Invite an employee by email. Email delivery is not wired yet, so the
          one-time token is shown after creation.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-5">
        <form className="grid gap-4" onSubmit={handleCreate}>
          <div className="grid gap-2 md:grid-cols-[minmax(0,1fr)_minmax(0,1fr)]">
            <div className="space-y-2">
              <Label htmlFor="invite-email">Employee email</Label>
              <Input
                id="invite-email"
                type="email"
                value={email}
                onChange={(event) => setEmail(event.target.value)}
                placeholder="employee@example.com"
                required
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="invite-staff-name">Staff display name</Label>
              <Input
                id="invite-staff-name"
                value={staffDisplayName}
                onChange={(event) => setStaffDisplayName(event.target.value)}
                placeholder="Employee name"
                required
              />
            </div>
          </div>

          {error ? (
            <Alert className="border-destructive/30 bg-destructive/5 text-destructive">
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          ) : null}

          {latestToken ? (
            <Alert>
              <AlertDescription>
                New acceptance token:{" "}
                <span className="break-all font-mono text-xs">
                  {latestToken}
                </span>
              </AlertDescription>
            </Alert>
          ) : null}

          <Button type="submit" disabled={isSubmitting}>
            <MailPlus data-icon="inline-start" className="size-4" />
            {isSubmitting ? "Creating invitation" : "Create invitation"}
          </Button>
        </form>

        <div className="space-y-3">
          {isLoading ? (
            <p className="text-sm text-muted-foreground">
              Loading invitations.
            </p>
          ) : invitations.length > 0 ? (
            invitations.map((invitation) => (
              <div
                key={invitation.id}
                className="rounded-lg border border-border bg-background p-3"
              >
                <div className="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
                  <div className="min-w-0 space-y-1">
                    <p className="break-all text-sm font-medium text-foreground">
                      {invitation.email}
                    </p>
                    <p className="text-xs text-muted-foreground">
                      {invitation.staffDisplayName}
                    </p>
                    <p className="text-xs text-muted-foreground">
                      Expires {formatDate(invitation.expiresAtUtc)}
                    </p>
                  </div>
                  <div className="flex shrink-0 flex-wrap gap-2">
                    <Badge>{invitation.role}</Badge>
                    <Badge variant="outline">{invitation.status}</Badge>
                  </div>
                </div>
              </div>
            ))
          ) : (
            <Alert>
              <AlertDescription>No invitations have been created.</AlertDescription>
            </Alert>
          )}
        </div>
      </CardContent>
    </Card>
  )
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat("en", {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(new Date(value))
}
