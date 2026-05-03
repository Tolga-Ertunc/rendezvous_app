"use client"

import { useState } from "react"
import { ShieldCheck } from "lucide-react"

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
import {
  approveAdminBusiness,
  rejectAdminBusiness,
  suspendAdminBusiness,
} from "@/lib/auth-api"

type AdminBusinessActionsPanelProps = {
  businessId: string
  initialStatus: string
}

export function AdminBusinessActionsPanel({
  businessId,
  initialStatus,
}: AdminBusinessActionsPanelProps) {
  const [status, setStatus] = useState(initialStatus)
  const [isActing, setIsActing] = useState(false)
  const [message, setMessage] = useState("")
  const [error, setError] = useState("")

  async function changeStatus(nextStatus: "Approved" | "Suspended" | "Rejected") {
    setIsActing(true)
    setMessage("")
    setError("")

    try {
      const result =
        nextStatus === "Approved"
          ? await approveAdminBusiness(businessId)
          : nextStatus === "Suspended"
            ? await suspendAdminBusiness(businessId)
            : await rejectAdminBusiness(businessId)

      setStatus(result.status)
      setMessage(`Business status changed to ${result.status}.`)
    } catch {
      setError("Business status could not be changed.")
    } finally {
      setIsActing(false)
    }
  }

  return (
    <Card>
      <CardHeader>
        <div className="flex items-center gap-2">
          <ShieldCheck className="size-4 text-primary" aria-hidden="true" />
          <CardTitle>Admin status</CardTitle>
        </div>
        <CardDescription>
          Change business visibility through admin-only routes.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="flex flex-wrap items-center gap-2">
          <span className="text-sm text-muted-foreground">Current status</span>
          <Badge variant="outline">{status}</Badge>
        </div>

        {message ? (
          <Alert>
            <AlertTitle>Updated</AlertTitle>
            <AlertDescription>{message}</AlertDescription>
          </Alert>
        ) : null}

        {error ? (
          <Alert className="border-destructive/30 bg-destructive/5 text-destructive">
            <AlertTitle>Status update failed</AlertTitle>
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        ) : null}

        <div className="flex flex-wrap gap-2">
          <Button
            type="button"
            disabled={isActing || status === "Approved"}
            onClick={() => changeStatus("Approved")}
          >
            Approve
          </Button>
          <Button
            type="button"
            variant="outline"
            disabled={isActing || status === "Suspended"}
            onClick={() => changeStatus("Suspended")}
          >
            Suspend
          </Button>
          <Button
            type="button"
            variant="outline"
            disabled={isActing || status === "Rejected"}
            onClick={() => changeStatus("Rejected")}
          >
            Reject
          </Button>
        </div>
      </CardContent>
    </Card>
  )
}
