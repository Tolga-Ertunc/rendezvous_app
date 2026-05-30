import type { ReactNode } from "react"
import { CalendarDays } from "lucide-react"

import { AuthHeaderActions } from "@/components/auth/auth-header-actions"
import { Button } from "@/components/ui/button"
import { cn } from "@/lib/utils"

type DashboardShellProps = {
  title: string
  description: string
  children: ReactNode
  actions?: ReactNode
  variant?: "default" | "profile"
}

export function DashboardShell({
  title,
  description,
  children,
  actions,
  variant = "default",
}: DashboardShellProps) {
  const isProfile = variant === "profile"

  return (
    <main
      className={cn(
        "min-h-svh px-4 py-6 sm:px-6 lg:px-8",
        isProfile
          ? "bg-[#f7f8f7]"
          : "bg-[linear-gradient(180deg,oklch(0.99_0_0),oklch(0.965_0.01_220))]"
      )}
    >
      <div className="mx-auto flex w-full max-w-6xl flex-col gap-6">
        <header
          className={cn(
            "flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between",
            isProfile
              ? "rounded-lg border border-[#e5e7eb] bg-white px-5 py-4 shadow-xs"
              : "border-b border-border pb-6"
          )}
        >
          <div className="space-y-2">
            <div
              className={cn(
                "flex items-center gap-2 text-sm",
                isProfile
                  ? "font-semibold text-[#4f9d3a]"
                  : "font-medium text-primary"
              )}
            >
              <CalendarDays className="size-4" aria-hidden="true" />
              Rendezvous
            </div>
            <div className="space-y-1">
              <h1
                className={cn(
                  isProfile
                    ? "text-3xl font-bold tracking-normal text-[#111111]"
                    : "text-2xl font-semibold text-foreground"
                )}
              >
                {title}
              </h1>
              <p
                className={cn(
                  "max-w-2xl text-sm leading-6",
                  isProfile ? "text-[#71717a]" : "text-muted-foreground"
                )}
              >
                {description}
              </p>
            </div>
          </div>
          <div className="flex shrink-0 flex-wrap gap-2">
            {actions}
            <AuthHeaderActions
              showGuestLinks={false}
              logoutRedirectTo="/"
            />
          </div>
        </header>
        {children}
      </div>
    </main>
  )
}

export function BackButton({ onClick }: { onClick: () => void }) {
  return (
    <Button type="button" variant="outline" onClick={onClick}>
      Back
    </Button>
  )
}
