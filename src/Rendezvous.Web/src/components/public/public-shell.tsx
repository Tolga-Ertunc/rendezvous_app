"use client"

import type { ReactNode } from "react"
import Link from "next/link"
import { CalendarDays, Compass, UserRound } from "lucide-react"

import { buttonVariants } from "@/components/ui/button"
import { cn } from "@/lib/utils"

type PublicShellProps = {
  children: ReactNode
}

export function PublicShell({ children }: PublicShellProps) {
  return (
    <main className="min-h-svh bg-white text-[#111111]">
      <header className="border-b border-[#e5e7eb] bg-white">
        <div className="mx-auto flex w-full max-w-[1220px] flex-col gap-4 px-4 py-5 sm:px-6 lg:flex-row lg:items-center lg:justify-between lg:px-8">
          <div className="space-y-1">
            <Link
              href="/"
              className="inline-flex text-3xl font-bold leading-none tracking-normal text-[#111111]"
            >
              Rendezvous
            </Link>
            <p className="text-base text-[#71717a]">Find a business</p>
          </div>

          <nav className="flex flex-wrap items-center gap-3">
            <Link
              href="/"
              className={cn(
                buttonVariants({ variant: "outline", size: "lg" }),
                "h-11 rounded-xl border-[#cfe7c7] bg-[#f4fbf1] px-5 text-base font-medium text-[#4f9d3a] hover:bg-[#eef8ea] hover:text-[#4f9d3a]"
              )}
            >
              <Compass className="mr-2 size-5" aria-hidden="true" />
              Discover
            </Link>
            <Link
              href="/profile"
              className={cn(
                buttonVariants({ variant: "outline", size: "lg" }),
                "h-11 rounded-xl border-[#d4d4d8] bg-white px-5 text-base font-medium text-[#111111] hover:bg-[#f4f4f5]"
              )}
            >
              <UserRound className="mr-2 size-5" aria-hidden="true" />
              Profile
            </Link>
            <Link
              href="/appointments"
              className={cn(
                buttonVariants({ variant: "outline", size: "lg" }),
                "h-11 rounded-xl border-[#d4d4d8] bg-white px-5 text-base font-medium text-[#111111] hover:bg-[#f4f4f5]"
              )}
            >
              <CalendarDays className="mr-2 size-5" aria-hidden="true" />
              My appointments
            </Link>
          </nav>
        </div>
      </header>
      <div className="mx-auto flex w-full max-w-[1220px] flex-col gap-8 px-4 py-8 sm:px-6 lg:px-8">
        {children}
      </div>
    </main>
  )
}
