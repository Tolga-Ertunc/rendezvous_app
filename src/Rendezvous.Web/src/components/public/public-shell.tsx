"use client"

import type { ReactNode } from "react"

import { AppHeader } from "@/components/layout/app-header"

type PublicShellProps = {
  children: ReactNode
}

export function PublicShell({ children }: PublicShellProps) {
  return (
    <main className="min-h-svh bg-white text-[#111111]">
      <AppHeader />
      <div className="mx-auto flex w-full max-w-[1220px] flex-col gap-8 px-4 py-8 sm:px-6 lg:px-8">
        {children}
      </div>
    </main>
  )
}
