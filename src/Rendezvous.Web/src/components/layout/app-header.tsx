"use client"

import Image from "next/image"
import Link from "next/link"

import { AppNavigation } from "@/components/layout/app-navigation"

type AppHeaderProps = {
  showDiscoverLink?: boolean
  showGuestLinks?: boolean
  logoutRedirectTo?: string
}

export function AppHeader({
  showDiscoverLink = true,
  showGuestLinks = true,
  logoutRedirectTo,
}: AppHeaderProps) {
  return (
    <header className="border-b border-[#e5e7eb] bg-white">
      <div className="mx-auto flex w-full max-w-[1220px] flex-col gap-4 px-4 py-5 sm:px-6 lg:flex-row lg:items-center lg:justify-between lg:px-8">
        <Link
          href="/"
          className="inline-flex items-center gap-3 text-3xl font-bold leading-none tracking-normal text-[#111111]"
        >
          <Image
            src="/rendezvous-logo.png"
            alt=""
            width={40}
            height={40}
            className="size-10 object-contain"
            priority
          />
          <span>Rendezvous</span>
        </Link>

        <AppNavigation
          showDiscoverLink={showDiscoverLink}
          showGuestLinks={showGuestLinks}
          logoutRedirectTo={logoutRedirectTo}
        />
      </div>
    </header>
  )
}
