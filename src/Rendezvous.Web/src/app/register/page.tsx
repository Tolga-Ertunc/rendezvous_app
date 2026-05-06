"use client"

import { FormEvent, Suspense, useEffect, useState } from "react"
import {
  CalendarDays,
  Check,
  MailCheck,
  RotateCcw,
  UserPlus,
} from "lucide-react"
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
import { Checkbox } from "@/components/ui/checkbox"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { ApiError } from "@/lib/api-client"
import {
  checkEmailAvailability,
  confirmEmail,
  register,
  resendConfirmationCode,
} from "@/lib/auth-api"
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
  const searchParams = useSearchParams()
  const isBookingReason = searchParams.get("reason") === "booking"
  const [email, setEmail] = useState("")
  const [password, setPassword] = useState("")
  const [confirmPassword, setConfirmPassword] = useState("")
  const [emailError, setEmailError] = useState("")
  const [error, setError] = useState("")
  const [acceptedTerms, setAcceptedTerms] = useState(false)
  const [termsError, setTermsError] = useState(false)
  const [isCheckingEmail, setIsCheckingEmail] = useState(false)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [pendingEmail, setPendingEmail] = useState("")
  const passwordRules = getPasswordRules(password)

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError("")
    setEmailError("")
    setTermsError(false)

    if (password !== confirmPassword) {
      setError("Passwords do not match.")
      return
    }

    if (!acceptedTerms) {
      setTermsError(true)
      return
    }

    setIsSubmitting(true)

    try {
      await register(email, password, confirmPassword)
      clearAuthTokens()
      setPendingEmail(email.trim())
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
        <AuthHeaderActions showDiscoverLink={false} showGuestLinks={false} />
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
              {pendingEmail ? "Confirm your email" : "Create your account"}
            </h1>
          </div>
        </div>

        {pendingEmail ? (
          <ConfirmRegistrationStep email={pendingEmail} />
        ) : (
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

              <div
                data-invalid={termsError ? true : undefined}
                className={cn(
                  "flex gap-3 rounded-lg border border-border bg-background p-3 transition-colors",
                  termsError && "border-destructive bg-destructive/5"
                )}
              >
                <Checkbox
                  id="terms"
                  checked={acceptedTerms}
                  aria-invalid={termsError}
                  onCheckedChange={(checked) => {
                    setAcceptedTerms(checked === true)
                    if (checked === true) {
                      setTermsError(false)
                    }
                  }}
                />
                <div className="grid gap-1.5 leading-none">
                  <Label
                    htmlFor="terms"
                    className={cn(
                      "text-sm font-medium",
                      termsError && "text-destructive"
                    )}
                  >
                    Accept terms and conditions
                  </Label>
                  <p
                    className={cn(
                      "text-sm leading-5 text-muted-foreground",
                      termsError && "text-destructive"
                    )}
                  >
                    By clicking this checkbox, you agree to the terms.
                  </p>
                </div>
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
                {isSubmitting ? "Sending code" : "Create account"}
              </Button>
              </form>
            </CardContent>
          </Card>
        )}

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

function ConfirmRegistrationStep({ email }: { email: string }) {
  const router = useRouter()
  const [code, setCode] = useState("")
  const [error, setError] = useState("")
  const [message, setMessage] = useState("")
  const [cooldownUntil, setCooldownUntil] = useState<number>(
    () => Date.now() + 60000
  )
  const [now, setNow] = useState(() => Date.now())
  const [isConfirming, setIsConfirming] = useState(false)
  const [isResending, setIsResending] = useState(false)
  const secondsRemaining = Math.max(
    0,
    Math.ceil((cooldownUntil - now) / 1000)
  )

  useEffect(() => {
    const interval = window.setInterval(() => setNow(Date.now()), 1000)

    return () => window.clearInterval(interval)
  }, [])

  async function handleConfirm(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError("")
    setMessage("")

    if (code.trim().length !== 6) {
      setError("Enter the 6-digit code from your email.")
      return
    }

    setIsConfirming(true)

    try {
      await confirmEmail(email, code.trim())
      router.push("/login?confirmed=1")
    } catch (caughtError) {
      if (caughtError instanceof ApiError && caughtError.status === 400) {
        setError("The confirmation code is invalid or expired.")
      } else if (caughtError instanceof ApiError && caughtError.status === 409) {
        setError("An account with this email already exists.")
      } else {
        setError("Email confirmation failed. Try again.")
      }
    } finally {
      setIsConfirming(false)
    }
  }

  async function handleResend() {
    setError("")
    setMessage("")
    setIsResending(true)

    try {
      const response = await resendConfirmationCode(email)
      setCooldownUntil(new Date(response.resendAvailableAtUtc).getTime())
      setMessage("A new confirmation code was sent.")
    } catch (caughtError) {
      if (caughtError instanceof ApiError && caughtError.status === 429) {
        setCooldownUntil(Date.now() + 60000)
        setError("Please wait before requesting a new code.")
      } else {
        setError("Could not resend the confirmation code.")
      }
    } finally {
      setIsResending(false)
    }
  }

  return (
    <Card className="bg-white/90 shadow-sm backdrop-blur">
      <CardHeader>
        <CardTitle>Enter your code</CardTitle>
        <CardDescription>
          We sent a 6-digit confirmation code to your email address.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <form className="space-y-4" onSubmit={handleConfirm}>
          <div className="space-y-2">
            <Label htmlFor="code">Confirmation code</Label>
            <Input
              id="code"
              name="code"
              inputMode="numeric"
              autoComplete="one-time-code"
              maxLength={6}
              placeholder="123456"
              value={code}
              onChange={(event) =>
                setCode(event.target.value.replace(/\D/g, "").slice(0, 6))
              }
              required
            />
          </div>

          {message ? (
            <Alert className="border-emerald-200 bg-emerald-50 text-emerald-800">
              <AlertDescription>{message}</AlertDescription>
            </Alert>
          ) : null}

          {error ? (
            <Alert className="border-destructive/30 bg-destructive/5 text-destructive">
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          ) : null}

          <Button
            className="w-full"
            type="submit"
            size="lg"
            disabled={isConfirming}
          >
            <MailCheck data-icon="inline-start" className="size-4" />
            {isConfirming ? "Confirming" : "Confirm email"}
          </Button>

          <Button
            className="w-full"
            type="button"
            variant="outline"
            disabled={isResending || secondsRemaining > 0}
            onClick={handleResend}
          >
            <RotateCcw data-icon="inline-start" className="size-4" />
            {secondsRemaining > 0
              ? `Resend code in ${secondsRemaining}s`
              : "Resend code"}
          </Button>
        </form>
      </CardContent>
    </Card>
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
