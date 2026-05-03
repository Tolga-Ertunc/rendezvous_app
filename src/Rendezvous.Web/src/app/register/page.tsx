"use client"

import { FormEvent, useState } from "react"
import { CalendarDays, UserPlus } from "lucide-react"
import Link from "next/link"
import { useRouter } from "next/navigation"

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
import { register } from "@/lib/auth-api"
import { clearAuthTokens } from "@/lib/auth-storage"
import { cn } from "@/lib/utils"

export default function RegisterPage() {
  const router = useRouter()
  const [email, setEmail] = useState("")
  const [password, setPassword] = useState("")
  const [error, setError] = useState("")
  const [isSubmitting, setIsSubmitting] = useState(false)

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError("")
    setIsSubmitting(true)

    try {
      await register(email, password)
      router.push("/dashboard")
    } catch (caughtError) {
      clearAuthTokens()

      if (caughtError instanceof ApiError && caughtError.status === 409) {
        setError("An account with this email already exists.")
      } else if (caughtError instanceof ApiError && caughtError.status === 400) {
        setError("Registration failed. Check the email and password rules.")
      } else {
        setError("Registration failed. Check the API server and try again.")
      }
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <main className="flex min-h-svh items-center justify-center bg-[radial-gradient(circle_at_top,oklch(0.94_0.04_183),transparent_34%),linear-gradient(180deg,oklch(0.99_0_0),oklch(0.96_0.01_220))] px-4 py-10">
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
              Create a customer account, then book appointments or create a
              business from the dashboard.
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
                  onChange={(event) => setEmail(event.target.value)}
                  required
                />
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
                disabled={isSubmitting}
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
