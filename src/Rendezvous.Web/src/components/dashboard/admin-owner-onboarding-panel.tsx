"use client"

import { useCallback, useEffect, useState } from "react"
import { Check, ClipboardList, X } from "lucide-react"

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
  approveAdminOwnerOnboardingRequest,
  getAdminOwnerOnboardingRequests,
  rejectAdminOwnerOnboardingRequest,
} from "@/lib/auth-api"
import type { AdminOwnerOnboardingRequest } from "@/lib/auth-api"

type StatusFilter = "All" | "Pending" | "Approved" | "Rejected"

export function AdminOwnerOnboardingPanel() {
  const [requests, setRequests] = useState<AdminOwnerOnboardingRequest[]>([])
  const [status, setStatus] = useState<StatusFilter>("Pending")
  const [notes, setNotes] = useState<Record<string, string>>({})
  const [isLoading, setIsLoading] = useState(true)
  const [busyRequestId, setBusyRequestId] = useState<string | null>(null)
  const [error, setError] = useState("")

  const loadRequests = useCallback(async (nextStatus = status) => {
    setIsLoading(true)
    setError("")

    try {
      setRequests(
        await getAdminOwnerOnboardingRequests({
          status: nextStatus === "All" ? undefined : nextStatus,
        })
      )
    } catch {
      setError("Owner applications could not be loaded.")
    } finally {
      setIsLoading(false)
    }
  }, [status])

  useEffect(() => {
    void Promise.resolve().then(() => loadRequests(status))
  }, [loadRequests, status])

  async function handleApprove(requestId: string) {
    await reviewRequest(requestId, "approve")
  }

  async function handleReject(requestId: string) {
    await reviewRequest(requestId, "reject")
  }

  async function reviewRequest(requestId: string, action: "approve" | "reject") {
    setBusyRequestId(requestId)
    setError("")

    try {
      if (action === "approve") {
        await approveAdminOwnerOnboardingRequest(requestId, notes[requestId])
      } else {
        await rejectAdminOwnerOnboardingRequest(requestId, notes[requestId])
      }

      await loadRequests()
    } catch {
      setError("Application review failed.")
    } finally {
      setBusyRequestId(null)
    }
  }

  return (
    <Card>
      <CardHeader>
        <div className="flex items-center gap-2">
          <ClipboardList className="size-4 text-primary" aria-hidden="true" />
          <CardTitle>Owner applications</CardTitle>
        </div>
        <CardDescription>
          Review business owner applications and provision approved businesses.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="max-w-xs space-y-2">
          <Label>Status</Label>
          <Select
            value={status}
            onValueChange={(value) => setStatus(value as StatusFilter)}
          >
            <SelectTrigger>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="Pending">Pending</SelectItem>
              <SelectItem value="Approved">Approved</SelectItem>
              <SelectItem value="Rejected">Rejected</SelectItem>
              <SelectItem value="All">All</SelectItem>
            </SelectContent>
          </Select>
        </div>

        {error ? (
          <Alert className="border-destructive/30 bg-destructive/5 text-destructive">
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        ) : null}

        {isLoading ? (
          <p className="text-sm leading-6 text-muted-foreground">
            Loading applications.
          </p>
        ) : requests.length === 0 ? (
          <p className="text-sm leading-6 text-muted-foreground">
            No applications matched the current filter.
          </p>
        ) : (
          <div className="grid gap-3">
            {requests.map((request) => (
              <div
                key={request.id}
                className="rounded-lg border border-border bg-background p-4"
              >
                <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_18rem]">
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <p className="break-words font-medium text-foreground">
                        {request.businessName}
                      </p>
                      <Badge variant="outline">{request.status}</Badge>
                    </div>
                    <dl className="mt-3 grid gap-2 text-sm text-muted-foreground sm:grid-cols-2">
                      <div>
                        <dt>Requester</dt>
                        <dd className="font-medium text-foreground">
                          {request.requesterFullName || "Name not set"}
                        </dd>
                        <dd className="break-all text-xs">
                          {request.requesterEmail}
                        </dd>
                      </div>
                      <div>
                        <dt>Public number</dt>
                        <dd className="font-medium text-foreground">
                          {request.requesterPublicNumber}
                        </dd>
                      </div>
                      <div>
                        <dt>Business type</dt>
                        <dd className="font-medium text-foreground">
                          {request.businessType}
                        </dd>
                      </div>
                    </dl>
                    {request.adminNote ? (
                      <p className="mt-3 text-sm text-muted-foreground">
                        Admin note: {request.adminNote}
                      </p>
                    ) : null}
                  </div>

                  <div className="space-y-3">
                    <div className="space-y-2">
                      <Label htmlFor={`admin-note-${request.id}`}>
                        Admin note
                      </Label>
                      <Input
                        id={`admin-note-${request.id}`}
                        value={notes[request.id] ?? ""}
                        onChange={(event) =>
                          setNotes((current) => ({
                            ...current,
                            [request.id]: event.target.value,
                          }))
                        }
                        placeholder="Optional"
                        disabled={request.status !== "Pending"}
                      />
                    </div>
                    {request.status === "Pending" ? (
                      <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-1 xl:grid-cols-2">
                        <Button
                          type="button"
                          onClick={() => handleApprove(request.id)}
                          disabled={busyRequestId === request.id}
                        >
                          <Check data-icon="inline-start" className="size-4" />
                          Approve
                        </Button>
                        <Button
                          type="button"
                          variant="outline"
                          onClick={() => handleReject(request.id)}
                          disabled={busyRequestId === request.id}
                        >
                          <X data-icon="inline-start" className="size-4" />
                          Reject
                        </Button>
                      </div>
                    ) : null}
                  </div>
                </div>
              </div>
            ))}
          </div>
        )}
      </CardContent>
    </Card>
  )
}
