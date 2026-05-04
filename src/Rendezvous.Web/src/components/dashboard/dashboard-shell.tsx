import type { ReactNode } from "react"
import { CalendarDays } from "lucide-react"

import { AuthHeaderActions } from "@/components/auth/auth-header-actions"
import { Button } from "@/components/ui/button"

type DashboardShellProps = {
  title: string
  description: string
  children: ReactNode
  actions?: ReactNode
}

export function DashboardShell({
  title,
  description,
  children,
  actions,
}: DashboardShellProps) {
  return (
    <main className="min-h-svh bg-[linear-gradient(180deg,oklch(0.99_0_0),oklch(0.965_0.01_220))] px-4 py-6 sm:px-6 lg:px-8">
      <div className="mx-auto flex w-full max-w-6xl flex-col gap-6">
        <header className="flex flex-col gap-4 border-b border-border pb-6 sm:flex-row sm:items-center sm:justify-between">
          <div className="space-y-2">
            <div className="flex items-center gap-2 text-sm font-medium text-primary">
              <CalendarDays className="size-4" aria-hidden="true" />
              Rendezvous
            </div>
            <div className="space-y-1">
              <h1 className="text-2xl font-semibold text-foreground">
                {title}
              </h1>
              <p className="max-w-2xl text-sm leading-6 text-muted-foreground">
                {description}
              </p>
            </div>
          </div>
          <div className="flex shrink-0 flex-wrap gap-2">
            {actions}
            <AuthHeaderActions
              showDashboardLink={false}
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
