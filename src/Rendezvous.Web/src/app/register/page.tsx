"use client"

import { FormEvent, Suspense, useState } from "react"
import { CalendarDays, Check, UserPlus } from "lucide-react"
import Link from "next/link"
import { useRouter, useSearchParams } from "next/navigation"

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
import { checkEmailAvailability, register } from "@/lib/auth-api"
import { clearAuthTokens } from "@/lib/auth-storage"
import { cn } from "@/lib/utils"

export default function RegisterPage() {
  return (
    <Suspense fallback={null}>
      <RegisterPageContent />
    </Suspense>
  )
}

function RegisterPageContent() {
  const router = useRouter()
  const searchParams = useSearchParams()
  const isBookingReason = searchParams.get("reason") === "booking"
  const [email, setEmail] = useState("")
  const [password, setPassword] = useState("")
  const [confirmPassword, setConfirmPassword] = useState("")
  const [emailError, setEmailError] = useState("")
  const [error, setError] = useState("")
  const [isCheckingEmail, setIsCheckingEmail] = useState(false)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const passwordRules = getPasswordRules(password)

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError("")
    setEmailError("")

    if (password !== confirmPassword) {
      setError("Passwords do not match.")
      return
    }

    setIsSubmitting(true)

    try {
      await register(email, password, confirmPassword)
      router.push("/")
    } catch (caughtError) {
      clearAuthTokens()

      if (caughtError instanceof ApiError && caughtError.status === 409) {
        setEmailError("An account with this email already exists.")
      } else if (caughtError instanceof ApiError && caughtError.status === 400) {
        setError("Registration failed. Check the email and password rules.")
      } else {
        setError("Registration failed. Check the API server and try again.")
      }
    } finally {
      setIsSubmitting(false)
    }
  }

  async function handleEmailBlur() {
    const nextEmail = email.trim()

    if (!nextEmail) {
      setEmailError("")
      return
    }

    setIsCheckingEmail(true)
    setEmailError("")

    try {
      const availability = await checkEmailAvailability(nextEmail)

      if (!availability.isAvailable) {
        setEmailError("An account with this email already exists.")
      }
    } catch {
      setEmailError("")
    } finally {
      setIsCheckingEmail(false)
    }
  }

  return (
    <main className="flex min-h-svh items-center justify-center bg-[radial-gradient(circle_at_top,oklch(0.94_0.04_183),transparent_34%),linear-gradient(180deg,oklch(0.99_0_0),oklch(0.96_0.01_220))] px-4 py-10">
      <div className="fixed right-4 top-4 z-10 flex flex-wrap justify-end gap-2">
        <AuthHeaderActions showGuestLinks={false} />
      </div>
      <section className="w-full max-w-[420px] space-y-6">
        <div className="space-y-3 text-center">
          <div className="mx-auto flex size-10 items-center justify-center rounded-lg border border-teal-200/70 bg-white/80 text-primary shadow-xs">
            <CalendarDays className="size-5" aria-hidden="true" />
          </div>
          <div className="space-y-1">
            <p className="text-sm font-medium text-muted-foreground">
              Rendezvous
            </p>
            <h1 className="text-2xl font-semibold text-foreground">
              Create your account
            </h1>
          </div>
        </div>

        <Card className="bg-white/90 shadow-sm backdrop-blur">
          <CardHeader>
            <CardTitle>Start booking</CardTitle>
            <CardDescription>
              {isBookingReason
                ? "Create a customer account before requesting this appointment."
                : "Create a customer account to request appointments."}
            </CardDescription>
          </CardHeader>
          <CardContent>
            <form className="space-y-4" onSubmit={handleSubmit}>
              <div className="space-y-2">
                <Label htmlFor="email">Email</Label>
                <Input
                  id="email"
                  name="email"
                  type="email"
                  autoComplete="email"
                  placeholder="user@example.com"
                  value={email}
                  onBlur={handleEmailBlur}
                  onChange={(event) => {
                    setEmail(event.target.value)
                    setEmailError("")
                  }}
                  aria-invalid={Boolean(emailError)}
                  aria-describedby={emailError ? "email-error" : undefined}
                  required
                />
                {isCheckingEmail ? (
                  <p className="text-xs text-muted-foreground">
                    Checking email availability.
                  </p>
                ) : null}
                {emailError ? (
                  <p id="email-error" className="text-xs text-destructive">
                    {emailError}
                  </p>
                ) : null}
              </div>
              <div className="space-y-2">
                <Label htmlFor="password">Password</Label>
                <Input
                  id="password"
                  name="password"
                  type="password"
                  autoComplete="new-password"
                  value={password}
                  onChange={(event) => setPassword(event.target.value)}
                  required
                />
                <div className="grid gap-1 text-xs text-muted-foreground">
                  {passwordRules.map((rule) => (
                    <div
                      key={rule.label}
                      className={cn(
                        "flex items-center gap-2",
                        rule.isMet ? "text-emerald-700" : "text-muted-foreground"
                      )}
                    >
                      <Check className="size-3.5" aria-hidden="true" />
                      <span>{rule.label}</span>
                    </div>
                  ))}
                </div>
              </div>
              <div className="space-y-2">
                <Label htmlFor="confirm-password">Confirm password</Label>
                <Input
                  id="confirm-password"
                  name="confirm-password"
                  type="password"
                  autoComplete="new-password"
                  value={confirmPassword}
                  onChange={(event) => setConfirmPassword(event.target.value)}
                  required
                />
              </div>

              {error ? (
                <Alert className="border-destructive/30 bg-destructive/5 text-destructive">
                  <AlertDescription>{error}</AlertDescription>
                </Alert>
              ) : null}

              <Button
                className="w-full"
                type="submit"
                size="lg"
                disabled={isSubmitting || isCheckingEmail || Boolean(emailError)}
              >
                <UserPlus data-icon="inline-start" className="size-4" />
                {isSubmitting ? "Creating account" : "Create account"}
              </Button>
            </form>
          </CardContent>
        </Card>

        <div className="flex justify-center gap-2">
          <Link href="/login" className={cn(buttonVariants({ variant: "link" }))}>
            Sign in
          </Link>
          <Link
            href="/businesses"
            className={cn(buttonVariants({ variant: "link" }))}
          >
            Browse businesses
          </Link>
        </div>
      </section>
    </main>
  )
}

function getPasswordRules(password: string) {
  return [
    {
      label: "Min 8 characters",
      isMet: password.length >= 8,
    },
    {
      label: "Uppercase letter",
      isMet: /[A-Z]/.test(password),
    },
    {
      label: "At least 1 number",
      isMet: /\d/.test(password),
    },
    {
      label: "At least 1 special character",
      isMet: /[^A-Za-z0-9]/.test(password),
    },
  ]
}
