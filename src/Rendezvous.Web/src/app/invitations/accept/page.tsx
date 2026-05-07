"use client"

import { FormEvent, useState } from "react"
import { MailCheck } from "lucide-react"
import Link from "next/link"
import { useRouter } from "next/navigation"

import { AuthHeaderActions } from "@/components/auth/auth-header-actions"
import { Alert, AlertDescription } from "@/components/ui/alert"
import { Button, buttonVariants } from "@/components/ui/button"
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
import { acceptBusinessInvitation } from "@/lib/auth-api"
import { getAccessToken } from "@/lib/auth-storage"
import { cn } from "@/lib/utils"

export default function AcceptInvitationPage() {
  const router = useRouter()
  const [token, setToken] = useState("")
  const [message, setMessage] = useState("")
  const [error, setError] = useState("")
  const [isSubmitting, setIsSubmitting] = useState(false)

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setMessage("")
    setError("")

    if (!getAccessToken()) {
      router.push("/login")
      return
    }

    setIsSubmitting(true)

    try {
      const result = await acceptBusinessInvitation(token)
      setMessage(`Invitation accepted for ${result.businessName}.`)
      setToken("")
    } catch (caughtError) {
      if (caughtError instanceof ApiError && caughtError.status === 403) {
        setError("This invitation belongs to a different email address.")
      } else {
        setError("Invitation could not be accepted.")
      }
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <main className="flex min-h-svh items-center justify-center bg-[linear-gradient(180deg,oklch(0.99_0_0),oklch(0.96_0.01_220))] px-4 py-10">
      <div className="fixed right-4 top-4 z-10 flex flex-wrap justify-end gap-2">
        <AuthHeaderActions showDiscoverLink={false} showGuestLinks={false} />
      </div>
      <section className="w-full max-w-[520px] space-y-6">
        <Card className="shadow-sm">
          <CardHeader>
            <div className="flex items-center gap-2">
              <MailCheck className="size-4 text-primary" aria-hidden="true" />
              <CardTitle>Accept business invitation</CardTitle>
            </div>
            <CardDescription>
              Sign in with the invited email address, then paste the one-time
              acceptance token.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <form className="space-y-4" onSubmit={handleSubmit}>
              <div className="space-y-2">
                <Label htmlFor="token">Acceptance token</Label>
                <Input
                  id="token"
                  value={token}
                  onChange={(event) => setToken(event.target.value)}
                  placeholder="Paste token"
                  required
                />
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
                <MailCheck data-icon="inline-start" className="size-4" />
                {isSubmitting ? "Accepting" : "Accept invitation"}
              </Button>
            </form>
          </CardContent>
        </Card>

        <div className="flex justify-center gap-2">
          <Link
            href="/profile"
            className={cn(buttonVariants({ variant: "link" }))}
          >
            Profile
          </Link>
          <Link href="/login" className={cn(buttonVariants({ variant: "link" }))}>
            Sign in
          </Link>
        </div>
      </section>
    </main>
  )
}
