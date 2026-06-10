"use client"

import { useRef, useState } from "react"
import { Camera, Loader2 } from "lucide-react"

import { ApiError } from "@/lib/api-client"
import { Badge } from "@/components/ui/badge"
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar"
import { Alert, AlertDescription } from "@/components/ui/alert"
import { Button } from "@/components/ui/button"
import { Card, CardContent } from "@/components/ui/card"
import { Separator } from "@/components/ui/separator"
import { uploadProfilePhoto, type CurrentUser } from "@/lib/auth-api"
import { cn } from "@/lib/utils"

export function AccountCard({
  user,
  onUserChange,
}: {
  user: CurrentUser
  onUserChange?: (user: CurrentUser) => void
}) {
  const fileInputRef = useRef<HTMLInputElement>(null)
  const [isUploading, setIsUploading] = useState(false)
  const [uploadError, setUploadError] = useState("")
  const displayName = user.fullName.trim()
  const isOwner = hasActiveMembership(user, "Owner")
  const isEmployee = hasActiveMembership(user, "Employee")
  const workspaceAccess = [
    isOwner ? "Owner workspace" : null,
    isEmployee ? "Employee workspace" : null,
  ].filter(Boolean)

  async function handlePhotoChange(file: File | null) {
    if (!file) {
      return
    }

    setIsUploading(true)
    setUploadError("")

    try {
      const updatedUser = await uploadProfilePhoto(file)
      onUserChange?.(updatedUser)
    } catch (error) {
      setUploadError(getUploadErrorMessage(error))
    } finally {
      setIsUploading(false)
      if (fileInputRef.current) {
        fileInputRef.current.value = ""
      }
    }
  }

  return (
    <Card className="border-[#e5e7eb] bg-white shadow-sm">
      <CardContent className="p-6">
        <div className="grid grid-cols-[auto_minmax(0,1fr)_auto] items-start gap-6">
          <div className="group relative size-20 shrink-0">
            <Avatar className="size-20 border border-[#cfe7c7] bg-[#f4fbf1]">
              {user.profilePhotoUrl ? (
                <AvatarImage
                  src={user.profilePhotoUrl}
                  alt={`${displayName || user.email} profile photo`}
                  className="object-cover object-center"
                />
              ) : null}
              <AvatarFallback className="bg-[#f4fbf1] text-xl font-bold text-[#4f9d3a]">
                {getAvatarInitials(user)}
              </AvatarFallback>
            </Avatar>
            <Button
              type="button"
              variant="secondary"
              size="icon"
              className="absolute inset-0 size-20 rounded-full border border-[#111111]/10 bg-[#111111]/70 text-white opacity-0 transition-opacity hover:bg-[#111111]/80 hover:text-white focus-visible:opacity-100 group-hover:opacity-100"
              disabled={isUploading}
              aria-label="Change profile photo"
              onClick={() => fileInputRef.current?.click()}
            >
              {isUploading ? (
                <Loader2 className="size-5 animate-spin" aria-hidden="true" />
              ) : (
                <Camera className="size-5" aria-hidden="true" />
              )}
            </Button>
            <input
              ref={fileInputRef}
              type="file"
              accept="image/jpeg,image/png,image/webp"
              className="sr-only"
              disabled={isUploading}
              onChange={(event) => {
                void handlePhotoChange(event.target.files?.[0] ?? null)
              }}
            />
          </div>

          <div className="min-w-0 space-y-3">
            <div className="min-w-0 space-y-1">
              <h2
                className={cn(
                  "truncate text-3xl font-bold tracking-normal",
                  displayName ? "text-[#111111]" : "text-[#71717a]"
                )}
              >
                {displayName || "Name not set"}
              </h2>
              <p className="break-all text-base text-[#71717a]">{user.email}</p>
            </div>
            <div className="inline-flex max-w-full rounded-full border border-[#e5e7eb] bg-[#fafafa] px-3 py-1 font-mono text-xs font-medium text-[#3f3f46]">
              <span className="truncate">Public number #{user.publicNumber}</span>
            </div>
          </div>

          <div className="flex max-w-[360px] flex-wrap justify-end gap-2">
            {user.roles.map((role) => (
              <RoleBadge key={role} label={role} tone={getRoleTone(role)} />
            ))}
            {isOwner ? <RoleBadge label="Owner workspace" tone="owner" /> : null}
            {isEmployee ? (
              <RoleBadge label="Employee workspace" tone="employee" />
            ) : null}
          </div>
        </div>

        {uploadError ? (
          <Alert className="mt-5 border-[#fecaca] bg-[#fef2f2] text-[#991b1b]">
            <AlertDescription>{uploadError}</AlertDescription>
          </Alert>
        ) : null}

        <Separator className="my-6" />

        <div className="grid grid-cols-3 gap-5 text-sm">
          <MetadataItem
            label="Account identity"
            value={displayName ? displayName : "Name not set"}
          />
          <MetadataItem
            label="Global roles"
            value={user.roles.length > 0 ? user.roles.join(", ") : "No global roles"}
          />
          <MetadataItem
            label="Workspace access"
            value={
              workspaceAccess.length > 0
                ? workspaceAccess.join(", ")
                : "No active workspace"
            }
          />
        </div>
      </CardContent>
    </Card>
  )
}

function MetadataItem({ label, value }: { label: string; value: string }) {
  return (
    <div className="min-w-0 space-y-1">
      <p className="text-xs font-medium uppercase tracking-normal text-[#71717a]">
        {label}
      </p>
      <p className="truncate font-semibold text-[#111111]">{value}</p>
    </div>
  )
}

function RoleBadge({
  label,
  tone,
}: {
  label: string
  tone: "user" | "admin" | "owner" | "employee" | "default"
}) {
  return (
    <Badge
      variant="outline"
      className={cn(
        "rounded-full px-3 py-1 text-xs font-semibold",
        tone === "admin" &&
          "border-[#111111] bg-[#111111] text-white hover:bg-[#111111]",
        tone === "owner" && "border-[#a9d8d2] bg-[#eaf8f6] text-[#0f766e]",
        tone === "employee" && "border-[#cfe7c7] bg-[#f4fbf1] text-[#4f9d3a]",
        tone === "user" && "border-[#e5e7eb] bg-[#f8faf9] text-[#3f3f46]",
        tone === "default" && "border-[#e5e7eb] bg-white text-[#3f3f46]"
      )}
    >
      {label}
    </Badge>
  )
}

function getRoleTone(role: string) {
  if (role === "Admin") {
    return "admin"
  }

  if (role === "User") {
    return "user"
  }

  return "default"
}

function hasActiveMembership(user: CurrentUser, role: "Owner" | "Employee") {
  return user.businessMemberships.some(
    (membership) => membership.role === role && membership.status === "Active"
  )
}

function getAvatarInitials(user: CurrentUser) {
  const nameParts = user.fullName.trim().split(/\s+/).filter(Boolean)

  if (nameParts.length >= 2) {
    return `${nameParts[0][0]}${nameParts[nameParts.length - 1][0]}`.toUpperCase()
  }

  if (nameParts.length === 1) {
    return nameParts[0].slice(0, 2).toUpperCase()
  }

  const emailName = user.email.split("@")[0]

  return (emailName[0] ?? "U").toUpperCase()
}

function getUploadErrorMessage(error: unknown) {
  if (error instanceof ApiError && isApiErrorBody(error.body)) {
    return error.body.message
  }

  return "Profile photo could not be uploaded."
}

function isApiErrorBody(body: unknown): body is { message: string } {
  return (
    typeof body === "object" &&
    body !== null &&
    "message" in body &&
    typeof body.message === "string"
  )
}
