import type { ReactNode } from "react"

import { AppHeader } from "@/components/layout/app-header"
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
  children,
  actions,
  variant = "default",
}: DashboardShellProps) {
  const isProfile = variant === "profile"

  return (
    <main
      className={cn(
        "min-h-svh text-[#111111]",
        isProfile
          ? "bg-[#f7f8f7]"
          : "bg-[linear-gradient(180deg,oklch(0.99_0_0),oklch(0.965_0.01_220))]"
      )}
    >
      <AppHeader showGuestLinks={false} logoutRedirectTo="/" />
      <div className="mx-auto flex w-full max-w-[1220px] flex-col gap-6 px-4 py-8 sm:px-6 lg:px-8">
        <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
          <h1
            className={cn(
              isProfile
                ? "text-3xl font-bold tracking-normal text-[#111111]"
                : "text-2xl font-semibold text-foreground"
            )}
          >
            {title}
          </h1>
          {actions ? (
            <div className="flex shrink-0 flex-wrap gap-2">{actions}</div>
          ) : null}
        </div>
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
