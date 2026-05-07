"use client"

import { useEffect, useState } from "react"
import type React from "react"
import Image from "next/image"
import {
  ArrowDown,
  ArrowUp,
  CalendarDays,
  Clock,
  ImageIcon,
  MapPin,
  Save,
  Star,
  Trash2,
  Upload,
  UsersRound,
  X,
} from "lucide-react"

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Checkbox } from "@/components/ui/checkbox"
import { OwnerAvailabilityExceptionsPanel } from "@/components/dashboard/availability-exceptions-panel"
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import {
  activateOwnerService,
  activateOwnerStaff,
  cancelOwnerAppointment,
  createOwnerServiceCategory,
  createOwnerService,
  deactivateOwnerService,
  deactivateOwnerStaff,
  deleteOwnerServiceCategory,
  deleteOwnerBusinessPhoto,
  getOwnerAppointments,
  getOwnerBusinessWorkingHours,
  getOwnerStaffWorkingHours,
  reorderOwnerBusinessPhotos,
  updateOwnerBusinessWorkingHours,
  updateOwnerBusinessProfile,
  updateOwnerService,
  updateOwnerStaff,
  updateOwnerStaffWorkingHours,
  uploadOwnerBusinessPhoto,
} from "@/lib/auth-api"
import { ApiError } from "@/lib/api-client"
import type {
  BusinessDetail,
  BusinessPhoto,
  BusinessService,
  BusinessServiceCategory,
  BusinessStaffMember,
  OwnerAppointment,
  OwnerBusinessProfileRequest,
  WorkingHour,
} from "@/lib/auth-api"

const dayLabels = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"]

export function OwnerManagementPanels({
  business,
  onChanged,
}: {
  business: BusinessDetail
  onChanged: () => Promise<void>
}) {
  return (
    <div className="grid gap-4">
      <OwnerServicesPanel business={business} onChanged={onChanged} />
      <OwnerStaffPanel business={business} onChanged={onChanged} />
      <OwnerBusinessHoursPanel businessId={business.id} />
      <OwnerStaffHoursPanel business={business} />
      <OwnerAvailabilityExceptionsPanel business={business} />
      <OwnerAppointmentsPanel businessId={business.id} />
    </div>
  )
}

export function OwnerBusinessProfilePanel({
  business,
  onChanged,
}: {
  business: BusinessDetail
  onChanged: () => Promise<void>
}) {
  const [draft, setDraft] = useState<OwnerBusinessProfileRequest>(() =>
    toProfileDraft(business)
  )
  const [photoAltText, setPhotoAltText] = useState("")
  const [actingId, setActingId] = useState("")
  const [message, setMessage] = useState("")
  const [error, setError] = useState("")
  const photos = [...business.photos].sort(
    (left, right) => left.sortOrder - right.sortOrder
  )

  async function handleSaveProfile() {
    setActingId("profile")
    setMessage("")
    setError("")

    try {
      await updateOwnerBusinessProfile(business.id, draft)
      setMessage("Business profile updated.")
      await onChanged()
    } catch {
      setError("Business profile could not be updated.")
    } finally {
      setActingId("")
    }
  }

  async function handleUploadPhoto(event: React.ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0]
    if (!file) {
      return
    }

    setActingId("photo")
    setMessage("")
    setError("")

    try {
      await uploadOwnerBusinessPhoto(business.id, file, photoAltText)
      setPhotoAltText("")
      event.target.value = ""
      setMessage("Photo uploaded.")
      await onChanged()
    } catch {
      setError("Photo could not be uploaded.")
    } finally {
      setActingId("")
    }
  }

  async function handleDeletePhoto(photoId: string) {
    setActingId(photoId)
    setMessage("")
    setError("")

    try {
      await deleteOwnerBusinessPhoto(business.id, photoId)
      setMessage("Photo deleted.")
      await onChanged()
    } catch {
      setError("Photo could not be deleted.")
    } finally {
      setActingId("")
    }
  }

  async function handleMovePhoto(photoId: string, direction: -1 | 1) {
    const currentIndex = photos.findIndex((photo) => photo.id === photoId)
    const nextIndex = currentIndex + direction
    if (currentIndex < 0 || nextIndex < 0 || nextIndex >= photos.length) {
      return
    }

    const orderedPhotoIds = photos.map((photo) => photo.id)
    ;[orderedPhotoIds[currentIndex], orderedPhotoIds[nextIndex]] = [
      orderedPhotoIds[nextIndex],
      orderedPhotoIds[currentIndex],
    ]

    setActingId(photoId)
    setMessage("")
    setError("")

    try {
      await reorderOwnerBusinessPhotos(business.id, orderedPhotoIds)
      setMessage("Photo order updated.")
      await onChanged()
    } catch {
      setError("Photo order could not be updated.")
    } finally {
      setActingId("")
    }
  }

  return (
    <section className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_360px]">
      <div className="space-y-6 rounded-2xl border border-[#e5e7eb] bg-white p-8 shadow-[0_8px_28px_rgba(17,17,17,0.04)]">
        <div className="space-y-2">
          <p className="text-sm font-semibold text-[#635bff]">Public profile</p>
          <h2 className="text-4xl font-extrabold tracking-normal text-[#111111]">
            Business page
          </h2>
          <p className="max-w-2xl text-base leading-7 text-[#71717a]">
            These fields feed the public business profile immediately after
            saving.
          </p>
        </div>
        <PanelMessages message={message} error={error} />

        <div className="grid gap-5 md:grid-cols-2">
          <Field label="Business name">
            <Input
              value={draft.name}
              onChange={(event) =>
                setDraft({ ...draft, name: event.target.value })
              }
            />
          </Field>
          <Field label="Timezone">
            <Input
              value={draft.timeZoneId}
              onChange={(event) =>
                setDraft({ ...draft, timeZoneId: event.target.value })
              }
            />
          </Field>
          <Field label="Address line">
            <Input
              value={draft.addressLine}
              onChange={(event) =>
                setDraft({ ...draft, addressLine: event.target.value })
              }
            />
          </Field>
          <Field label="District">
            <Input
              value={draft.district}
              onChange={(event) =>
                setDraft({ ...draft, district: event.target.value })
              }
            />
          </Field>
          <Field label="City">
            <Input
              value={draft.city}
              onChange={(event) =>
                setDraft({ ...draft, city: event.target.value })
              }
            />
          </Field>
          <Field label="Country">
            <Input
              value={draft.country}
              onChange={(event) =>
                setDraft({ ...draft, country: event.target.value })
              }
            />
          </Field>
        </div>

        <Field label="Description">
          <textarea
            value={draft.description}
            maxLength={1200}
            className="min-h-32 w-full rounded-lg border border-input bg-background px-3 py-2 text-sm outline-none transition-[color,box-shadow] focus-visible:ring-3 focus-visible:ring-ring/35"
            onChange={(event) =>
              setDraft({ ...draft, description: event.target.value })
            }
          />
        </Field>

        <div className="grid gap-3 md:grid-cols-2">
          <ProfileFlag
            label="Instant Confirmation"
            checked={draft.supportsInstantConfirmation}
            onCheckedChange={(checked) =>
              setDraft({ ...draft, supportsInstantConfirmation: checked })
            }
          />
          <ProfileFlag
            label="Pay by app"
            checked={draft.supportsPayByApp}
            onCheckedChange={(checked) =>
              setDraft({ ...draft, supportsPayByApp: checked })
            }
          />
          <ProfileFlag
            label="Pet-friendly"
            checked={draft.isPetFriendly}
            onCheckedChange={(checked) =>
              setDraft({ ...draft, isPetFriendly: checked })
            }
          />
          <ProfileFlag
            label="Kid-friendly"
            checked={draft.isKidFriendly}
            onCheckedChange={(checked) =>
              setDraft({ ...draft, isKidFriendly: checked })
            }
          />
          <ProfileFlag
            label="Near public transport"
            checked={draft.isNearPublicTransport}
            onCheckedChange={(checked) =>
              setDraft({ ...draft, isNearPublicTransport: checked })
            }
          />
          <ProfileFlag
            label="Organic products only"
            checked={draft.usesOrganicProducts}
            onCheckedChange={(checked) =>
              setDraft({ ...draft, usesOrganicProducts: checked })
            }
          />
          <ProfileFlag
            label="Vegan products only"
            checked={draft.usesVeganProducts}
            onCheckedChange={(checked) =>
              setDraft({ ...draft, usesVeganProducts: checked })
            }
          />
          <ProfileFlag
            label="Environmentally friendly"
            checked={draft.isEnvironmentallyFriendly}
            onCheckedChange={(checked) =>
              setDraft({ ...draft, isEnvironmentallyFriendly: checked })
            }
          />
        </div>

        <Button
          type="button"
          className="h-12 rounded-full bg-[#111111] px-8 text-base font-bold text-white hover:bg-[#27272a]"
          disabled={actingId === "profile"}
          onClick={handleSaveProfile}
        >
          <Save data-icon="inline-start" className="size-4" />
          Save profile
        </Button>
      </div>

      <aside className="space-y-5">
        <div className="rounded-2xl border border-[#e5e7eb] bg-white p-6 shadow-[0_8px_28px_rgba(17,17,17,0.04)]">
          <div className="space-y-2">
            <h3 className="text-3xl font-extrabold text-[#111111]">
              {business.name}
            </h3>
            <p className="flex items-center gap-2 text-sm text-[#71717a]">
              <MapPin className="size-4" aria-hidden="true" />
              {formatOwnerAddress(business)}
            </p>
            <p className="flex items-center gap-2 text-sm font-semibold text-[#635bff]">
              <Star className="size-4 fill-[#f6b73c] text-[#f6b73c]" />
              {business.reviewSummary.averageRating.toFixed(1)} (
              {business.reviewSummary.reviewCount})
            </p>
          </div>
        </div>

        <div className="rounded-2xl border border-[#e5e7eb] bg-white p-6 shadow-[0_8px_28px_rgba(17,17,17,0.04)]">
          <div className="mb-5 space-y-2">
            <h3 className="text-2xl font-bold text-[#111111]">Photos</h3>
            <p className="text-sm leading-6 text-[#71717a]">
              Upload up to 4 JPEG, PNG, or WebP photos. The first photo becomes
              the public hero image.
            </p>
          </div>
          <div className="space-y-3">
            <Field label="Alt text">
              <Input
                value={photoAltText}
                placeholder="Salon interior"
                onChange={(event) => setPhotoAltText(event.target.value)}
              />
            </Field>
            <label className="flex h-12 cursor-pointer items-center justify-center gap-2 rounded-full border border-[#d4d4d8] bg-white text-sm font-bold text-[#111111] hover:bg-[#f4f4f5]">
              <Upload className="size-4" aria-hidden="true" />
              Upload photo
              <input
                type="file"
                accept="image/jpeg,image/png,image/webp"
                className="hidden"
                disabled={actingId === "photo" || photos.length >= 4}
                onChange={handleUploadPhoto}
              />
            </label>
          </div>

          <div className="mt-5 grid gap-3">
            {photos.length === 0 ? (
              <div className="flex h-32 items-center justify-center rounded-lg bg-[#eef0f2] text-[#71717a]">
                <ImageIcon className="size-6" aria-hidden="true" />
              </div>
            ) : (
              photos.map((photo, index) => (
                <PhotoManagerItem
                  key={photo.id}
                  photo={photo}
                  index={index}
                  count={photos.length}
                  disabled={actingId === photo.id}
                  onMove={handleMovePhoto}
                  onDelete={handleDeletePhoto}
                />
              ))
            )}
          </div>
        </div>
      </aside>
    </section>
  )
}

export function OwnerServicesPanel({
  business,
  onChanged,
}: {
  business: BusinessDetail
  onChanged: () => Promise<void>
}) {
  const serviceCategories = getServiceCategories(business)
  const [drafts, setDrafts] = useState<Record<string, ServiceDraft>>({})
  const [newService, setNewService] = useState<ServiceDraft>({
    name: "",
    categoryName: "Featured",
    durationMinutes: 30,
    basePriceAmount: 0,
    currencyCode: "TRY",
    isActive: true,
  })
  const [newCategoryName, setNewCategoryName] = useState("")
  const [actingId, setActingId] = useState("")
  const [message, setMessage] = useState("")
  const [error, setError] = useState("")
  const suggestedCategories = ["Hair", "Beard", "Hair Color", "Facial Care", "Add Ons"]

  async function handleCreateCategory(name = newCategoryName) {
    const trimmedName = name.trim()
    if (!trimmedName) {
      setError("Category name is required.")
      return
    }

    setActingId(`category:${trimmedName}`)
    setMessage("")
    setError("")

    try {
      await createOwnerServiceCategory(business.id, { name: trimmedName })
      setNewCategoryName("")
      setNewService({ ...newService, categoryName: trimmedName })
      setMessage("Category created.")
      await onChanged()
    } catch (caughtError) {
      setError(getApiErrorMessage(caughtError, "Category could not be created."))
    } finally {
      setActingId("")
    }
  }

  async function handleDeleteCategory(category: BusinessServiceCategory) {
    setActingId(`category:${category.id}`)
    setMessage("")
    setError("")

    try {
      await deleteOwnerServiceCategory(business.id, category.id)
      if (newService.categoryName === category.name) {
        setNewService({ ...newService, categoryName: "Featured" })
      }
      setMessage("Category deleted.")
      await onChanged()
    } catch (caughtError) {
      setError(getApiErrorMessage(caughtError, "Category could not be deleted."))
    } finally {
      setActingId("")
    }
  }

  async function handleCreate() {
    setActingId("new")
    setMessage("")
    setError("")

    try {
      await createOwnerService(business.id, normalizeServiceDraft(newService))
      setNewService({
        name: "",
        categoryName: "Featured",
        durationMinutes: 30,
        basePriceAmount: 0,
        currencyCode: "TRY",
        isActive: true,
      })
      setMessage("Service created.")
      await onChanged()
    } catch (caughtError) {
      setError(getApiErrorMessage(caughtError, "Service could not be created."))
    } finally {
      setActingId("")
    }
  }

  async function handleUpdate(service: BusinessService) {
    setActingId(service.id)
    setMessage("")
    setError("")

    try {
      await updateOwnerService(
        business.id,
        service.id,
        normalizeServiceDraft(drafts[service.id] ?? toServiceDraft(service))
      )
      setMessage("Service updated.")
      await onChanged()
    } catch (caughtError) {
      setError(getApiErrorMessage(caughtError, "Service could not be updated."))
    } finally {
      setActingId("")
    }
  }

  async function handleToggle(service: BusinessService) {
    setActingId(service.id)
    setMessage("")
    setError("")

    try {
      if (service.isActive) {
        await deactivateOwnerService(business.id, service.id)
      } else {
        await activateOwnerService(business.id, service.id)
      }
      setMessage("Service status updated.")
      await onChanged()
    } catch (caughtError) {
      setError(getApiErrorMessage(caughtError, "Service status could not be updated."))
    } finally {
      setActingId("")
    }
  }

  return (
    <section className="space-y-6 rounded-2xl border border-[#e5e7eb] bg-white p-8 shadow-[0_8px_28px_rgba(17,17,17,0.04)]">
      <div className="space-y-2">
        <p className="text-sm font-semibold text-[#635bff]">Catalog</p>
        <h2 className="text-4xl font-extrabold tracking-normal text-[#111111]">
          Services
        </h2>
        <p className="max-w-2xl text-base leading-7 text-[#71717a]">
          Service edits affect new appointment requests only; existing
          appointments keep their price snapshot.
        </p>
      </div>
      <div className="space-y-5">
        <PanelMessages message={message} error={error} />
        <div className="space-y-4 rounded-xl border border-[#e5e7eb] bg-[#fbfbfa] p-5">
          <div className="flex flex-wrap items-center gap-3">
            {serviceCategories.map((category) => (
              <div
                key={category.id}
                className="flex items-center gap-2 rounded-full border border-[#e5e7eb] bg-white px-4 py-2 text-sm font-semibold text-[#111111]"
              >
                <span>{category.name}</span>
                {category.isSystem ? (
                  <span className="text-xs font-medium text-[#71717a]">System</span>
                ) : (
                  <button
                    type="button"
                    className="text-[#71717a] transition hover:text-[#111111]"
                    disabled={actingId === `category:${category.id}`}
                    onClick={() => handleDeleteCategory(category)}
                    aria-label={`Delete ${category.name}`}
                  >
                    <X className="size-4" aria-hidden="true" />
                  </button>
                )}
              </div>
            ))}
          </div>
          <div className="grid gap-3 md:grid-cols-[minmax(0,1fr)_auto] md:items-end">
            <Field label="New category">
              <Input
                value={newCategoryName}
                placeholder="Hair, Beard, Facial Care"
                onChange={(event) => setNewCategoryName(event.target.value)}
              />
            </Field>
            <Button
              type="button"
              variant="outline"
              className="rounded-full px-5"
              disabled={actingId.startsWith("category:")}
              onClick={() => handleCreateCategory()}
            >
              Add category
            </Button>
          </div>
          <div className="flex flex-wrap gap-2">
            {suggestedCategories.map((name) => {
              const exists = serviceCategories.some(
                (category) => category.name.toLowerCase() === name.toLowerCase()
              )

              return (
                <Button
                  key={name}
                  type="button"
                  size="sm"
                  variant="outline"
                  className="rounded-full"
                  disabled={exists || actingId === `category:${name}`}
                  onClick={() => handleCreateCategory(name)}
                >
                  {name}
                </Button>
              )
            })}
          </div>
        </div>

        <div className="grid gap-3 rounded-xl border border-[#e5e7eb] bg-[#fbfbfa] p-5 md:grid-cols-[minmax(0,1fr)_220px_110px_120px_80px_auto] md:items-end">
          <Field label="Name">
            <Input
              value={newService.name}
              onChange={(event) =>
                setNewService({ ...newService, name: event.target.value })
              }
            />
          </Field>
          <Field label="Category">
            <ServiceCategorySelect
              categories={serviceCategories}
              value={newService.categoryName}
              onChange={(categoryName) =>
                setNewService({
                  ...newService,
                  categoryName,
                })
              }
            />
          </Field>
          <Field label="Minutes">
            <Input
              type="number"
              min={1}
              value={newService.durationMinutes}
              onChange={(event) =>
                setNewService({
                  ...newService,
                  durationMinutes: Number(event.target.value),
                })
              }
            />
          </Field>
          <Field label="Price">
            <Input
              type="number"
              min={0}
              value={newService.basePriceAmount}
              onChange={(event) =>
                setNewService({
                  ...newService,
                  basePriceAmount: Number(event.target.value),
                })
              }
            />
          </Field>
          <Field label="Currency">
            <Input
              value={newService.currencyCode}
              maxLength={3}
              onChange={(event) =>
                setNewService({
                  ...newService,
                  currencyCode: event.target.value.toUpperCase(),
                })
              }
            />
          </Field>
          <Button
            type="button"
            className="rounded-full bg-[#111111] px-5 text-white hover:bg-[#27272a]"
            disabled={actingId === "new"}
            onClick={handleCreate}
          >
            <Save data-icon="inline-start" className="size-4" />
            Add
          </Button>
        </div>

        <div className="grid gap-4">
          {business.services.map((service) => {
            const draft = drafts[service.id] ?? toServiceDraft(service)

            return (
              <div
                key={service.id}
                className="grid gap-3 rounded-xl border border-[#e5e7eb] bg-white p-5 md:grid-cols-[minmax(0,1fr)_220px_110px_120px_80px_auto] md:items-end"
              >
                <Field label="Name">
                  <Input
                    value={draft.name}
                    onChange={(event) =>
                      setDrafts({
                        ...drafts,
                        [service.id]: { ...draft, name: event.target.value },
                      })
                    }
                  />
                </Field>
                <Field label="Category">
                  <ServiceCategorySelect
                    categories={serviceCategories}
                    value={draft.categoryName}
                    onChange={(categoryName) =>
                      setDrafts({
                        ...drafts,
                        [service.id]: {
                          ...draft,
                          categoryName,
                        },
                      })
                    }
                  />
                </Field>
                <Field label="Minutes">
                  <Input
                    type="number"
                    min={1}
                    value={draft.durationMinutes}
                    onChange={(event) =>
                      setDrafts({
                        ...drafts,
                        [service.id]: {
                          ...draft,
                          durationMinutes: Number(event.target.value),
                        },
                      })
                    }
                  />
                </Field>
                <Field label="Price">
                  <Input
                    type="number"
                    min={0}
                    value={draft.basePriceAmount}
                    onChange={(event) =>
                      setDrafts({
                        ...drafts,
                        [service.id]: {
                          ...draft,
                          basePriceAmount: Number(event.target.value),
                        },
                      })
                    }
                  />
                </Field>
                <Field label="Currency">
                  <Input
                    value={draft.currencyCode}
                    maxLength={3}
                    onChange={(event) =>
                      setDrafts({
                        ...drafts,
                        [service.id]: {
                          ...draft,
                          currencyCode: event.target.value.toUpperCase(),
                        },
                      })
                    }
                  />
                </Field>
                <div className="flex flex-wrap gap-2">
                  <Button
                    type="button"
                    size="sm"
                    className="rounded-full bg-[#111111] px-4 text-white hover:bg-[#27272a]"
                    disabled={actingId === service.id}
                    onClick={() => handleUpdate(service)}
                  >
                    Save
                  </Button>
                  <Button
                    type="button"
                    size="sm"
                    variant="outline"
                    className="rounded-full"
                    disabled={actingId === service.id}
                    onClick={() => handleToggle(service)}
                  >
                    {service.isActive ? "Deactivate" : "Activate"}
                  </Button>
                </div>
              </div>
            )
          })}
        </div>
      </div>
    </section>
  )
}

export function OwnerStaffPanel({
  business,
  onChanged,
}: {
  business: BusinessDetail
  onChanged: () => Promise<void>
}) {
  const [drafts, setDrafts] = useState<Record<string, string>>({})
  const [actingId, setActingId] = useState("")
  const [message, setMessage] = useState("")
  const [error, setError] = useState("")

  async function handleUpdate(staff: BusinessStaffMember) {
    setActingId(staff.id)
    setMessage("")
    setError("")

    try {
      await updateOwnerStaff(business.id, staff.id, {
        displayName: drafts[staff.id] ?? staff.displayName,
        isActive: staff.isActive,
      })
      setMessage("Staff member updated.")
      await onChanged()
    } catch {
      setError("Staff member could not be updated.")
    } finally {
      setActingId("")
    }
  }

  async function handleToggle(staff: BusinessStaffMember) {
    setActingId(staff.id)
    setMessage("")
    setError("")

    try {
      if (staff.isActive) {
        await deactivateOwnerStaff(business.id, staff.id)
      } else {
        await activateOwnerStaff(business.id, staff.id)
      }
      setMessage("Staff status updated.")
      await onChanged()
    } catch {
      setError("Staff status could not be updated.")
    } finally {
      setActingId("")
    }
  }

  return (
    <Card>
      <CardHeader>
        <div className="flex items-center gap-2">
          <UsersRound className="size-4 text-primary" aria-hidden="true" />
          <CardTitle>Manage staff</CardTitle>
        </div>
        <CardDescription>
          Update staff display names and active state. Invitations are a later
          workflow.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <PanelMessages message={message} error={error} />
        <div className="grid gap-3">
          {business.staffMembers.map((staff) => (
            <div
              key={staff.id}
              className="grid gap-3 rounded-lg border border-border bg-background p-3 md:grid-cols-[minmax(0,1fr)_110px_auto] md:items-end"
            >
              <Field label="Display name">
                <Input
                  value={drafts[staff.id] ?? staff.displayName}
                  onChange={(event) =>
                    setDrafts({ ...drafts, [staff.id]: event.target.value })
                  }
                />
              </Field>
              <div>
                <Badge variant={staff.isActive ? "default" : "outline"}>
                  {staff.isActive ? "Active" : "Inactive"}
                </Badge>
              </div>
              <div className="flex flex-wrap gap-2">
                <Button
                  type="button"
                  size="sm"
                  disabled={actingId === staff.id}
                  onClick={() => handleUpdate(staff)}
                >
                  Save
                </Button>
                <Button
                  type="button"
                  size="sm"
                  variant="outline"
                  disabled={actingId === staff.id}
                  onClick={() => handleToggle(staff)}
                >
                  {staff.isActive ? "Deactivate" : "Activate"}
                </Button>
              </div>
            </div>
          ))}
        </div>
      </CardContent>
    </Card>
  )
}

export function OwnerBusinessHoursPanel({ businessId }: { businessId: string }) {
  const [hours, setHours] = useState<WorkingHour[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [message, setMessage] = useState("")
  const [error, setError] = useState("")

  useEffect(() => {
    let isMounted = true

    async function loadHours() {
      try {
        const nextHours = await getOwnerBusinessWorkingHours(businessId)
        if (isMounted) {
          setHours(nextHours)
        }
      } catch {
        if (isMounted) {
          setError("Business working hours could not be loaded.")
        }
      } finally {
        if (isMounted) {
          setIsLoading(false)
        }
      }
    }

    loadHours()

    return () => {
      isMounted = false
    }
  }, [businessId])

  async function handleSave() {
    setMessage("")
    setError("")

    try {
      const nextHours = await updateOwnerBusinessWorkingHours(businessId, hours)
      setHours(nextHours)
      setMessage("Business working hours updated.")
    } catch {
      setError("Business working hours could not be updated.")
    }
  }

  return (
    <WorkingHoursCard
      title="Business working hours"
      description="MVP supports one interval per day. Multiple intervals and breaks are a future scheduling upgrade."
      hours={hours}
      isLoading={isLoading}
      message={message}
      error={error}
      onChange={setHours}
      onSave={handleSave}
    />
  )
}

export function OwnerStaffHoursPanel({ business }: { business: BusinessDetail }) {
  const firstStaffId = business.staffMembers[0]?.id ?? ""
  const [selectedStaffId, setSelectedStaffId] = useState(firstStaffId)
  const [hours, setHours] = useState<WorkingHour[]>([])
  const [isLoading, setIsLoading] = useState(false)
  const [message, setMessage] = useState("")
  const [error, setError] = useState("")

  useEffect(() => {
    if (!selectedStaffId) {
      return
    }

    let isMounted = true

    async function loadHours() {
      setIsLoading(true)
      setMessage("")
      setError("")

      try {
        const nextHours = await getOwnerStaffWorkingHours(
          business.id,
          selectedStaffId
        )
        if (isMounted) {
          setHours(nextHours)
        }
      } catch {
        if (isMounted) {
          setError("Staff working hours could not be loaded.")
        }
      } finally {
        if (isMounted) {
          setIsLoading(false)
        }
      }
    }

    loadHours()

    return () => {
      isMounted = false
    }
  }, [business.id, selectedStaffId])

  async function handleSave() {
    if (!selectedStaffId) {
      return
    }

    setMessage("")
    setError("")

    try {
      const nextHours = await updateOwnerStaffWorkingHours(
        business.id,
        selectedStaffId,
        hours
      )
      setHours(nextHours)
      setMessage("Staff working hours updated.")
    } catch {
      setError("Staff working hours could not be updated.")
    }
  }

  return (
    <Card>
      <CardHeader>
        <div className="flex items-center gap-2">
          <Clock className="size-4 text-primary" aria-hidden="true" />
          <CardTitle>Staff working hours</CardTitle>
        </div>
        <CardDescription>
          Select one staff member and edit one daily interval.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {business.staffMembers.length === 0 ? (
          <p className="text-sm leading-6 text-muted-foreground">
            No staff members are available.
          </p>
        ) : (
          <>
            <Field label="Staff member">
              <Select
                value={selectedStaffId}
                onValueChange={setSelectedStaffId}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Select staff" />
                </SelectTrigger>
                <SelectContent>
                  {business.staffMembers.map((staff) => (
                    <SelectItem key={staff.id} value={staff.id}>
                      {staff.displayName}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </Field>
            <WorkingHoursEditor
              hours={hours}
              isLoading={isLoading}
              message={message}
              error={error}
              onChange={setHours}
              onSave={handleSave}
            />
          </>
        )}
      </CardContent>
    </Card>
  )
}

export function OwnerAppointmentsPanel({ businessId }: { businessId: string }) {
  const [appointments, setAppointments] = useState<OwnerAppointment[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [actingId, setActingId] = useState("")
  const [message, setMessage] = useState("")
  const [error, setError] = useState("")

  useEffect(() => {
    let isMounted = true

    async function loadAppointments() {
      setIsLoading(true)
      setError("")

      try {
        const nextAppointments = await getOwnerAppointments(businessId)
        if (isMounted) {
          setAppointments(nextAppointments)
        }
      } catch {
        if (isMounted) {
          setError("Approved appointments could not be loaded.")
        }
      } finally {
        if (isMounted) {
          setIsLoading(false)
        }
      }
    }

    loadAppointments()

    return () => {
      isMounted = false
    }
  }, [businessId])

  async function refreshAppointments() {
    setIsLoading(true)
    const nextAppointments = await getOwnerAppointments(businessId)
    setAppointments(nextAppointments)
    setIsLoading(false)
  }

  async function handleCancel(appointmentId: string) {
    setActingId(appointmentId)
    setMessage("")
    setError("")

    try {
      await cancelOwnerAppointment(businessId, appointmentId)
      setMessage("Appointment cancelled.")
      await refreshAppointments()
    } catch {
      setError("Appointment could not be cancelled.")
    } finally {
      setActingId("")
    }
  }

  return (
    <Card>
      <CardHeader>
        <div className="flex items-center gap-2">
          <CalendarDays className="size-4 text-primary" aria-hidden="true" />
          <CardTitle>Approved appointments</CardTitle>
        </div>
        <CardDescription>
          Owner can cancel approved appointments until one hour before start.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <PanelMessages message={message} error={error} />
        {isLoading ? (
          <p className="text-sm leading-6 text-muted-foreground">
            Loading approved appointments.
          </p>
        ) : appointments.length === 0 ? (
          <p className="text-sm leading-6 text-muted-foreground">
            No approved upcoming appointments.
          </p>
        ) : (
          <div className="grid gap-3">
            {appointments.map((appointment) => (
              <div
                key={appointment.id}
                className="grid gap-3 rounded-lg border border-border bg-background p-3 lg:grid-cols-[minmax(0,1fr)_auto] lg:items-center"
              >
                <div className="min-w-0 space-y-2">
                  <div className="flex flex-wrap items-center gap-2">
                    <p className="font-medium text-foreground">
                      {appointment.serviceName}
                    </p>
                    <Badge variant="outline">{appointment.status}</Badge>
                  </div>
                  <div className="grid gap-1 text-sm text-muted-foreground sm:grid-cols-2">
                    <p>{formatAppointmentTime(appointment.startsAtUtc)}</p>
                    <p>Staff: {appointment.staffDisplayName}</p>
                    <p>Customer: {appointment.customerPublicNumber}</p>
                    <p>
                      Price: {appointment.priceAmount}{" "}
                      {appointment.currencyCode}
                    </p>
                  </div>
                </div>
                <Button
                  type="button"
                  size="sm"
                  variant="outline"
                  disabled={actingId === appointment.id}
                  onClick={() => handleCancel(appointment.id)}
                >
                  <X data-icon="inline-start" className="size-4" />
                  {actingId === appointment.id ? "Cancelling" : "Cancel"}
                </Button>
              </div>
            ))}
          </div>
        )}
      </CardContent>
    </Card>
  )
}

function WorkingHoursCard({
  title,
  description,
  hours,
  isLoading,
  message,
  error,
  onChange,
  onSave,
}: {
  title: string
  description: string
  hours: WorkingHour[]
  isLoading: boolean
  message: string
  error: string
  onChange: (hours: WorkingHour[]) => void
  onSave: () => void
}) {
  return (
    <Card>
      <CardHeader>
        <div className="flex items-center gap-2">
          <Clock className="size-4 text-primary" aria-hidden="true" />
          <CardTitle>{title}</CardTitle>
        </div>
        <CardDescription>{description}</CardDescription>
      </CardHeader>
      <CardContent>
        <WorkingHoursEditor
          hours={hours}
          isLoading={isLoading}
          message={message}
          error={error}
          onChange={onChange}
          onSave={onSave}
        />
      </CardContent>
    </Card>
  )
}

function WorkingHoursEditor({
  hours,
  isLoading,
  message,
  error,
  onChange,
  onSave,
}: {
  hours: WorkingHour[]
  isLoading: boolean
  message: string
  error: string
  onChange: (hours: WorkingHour[]) => void
  onSave: () => void
}) {
  function updateDay(dayOfWeek: number, patch: Partial<WorkingHour>) {
    onChange(
      hours.map((hour) =>
        hour.dayOfWeek === dayOfWeek ? { ...hour, ...patch } : hour
      )
    )
  }

  if (isLoading) {
    return (
      <p className="text-sm leading-6 text-muted-foreground">
        Loading working hours.
      </p>
    )
  }

  return (
    <div className="space-y-4">
      <PanelMessages message={message} error={error} />
      <div className="grid gap-3">
        {hours.map((hour) => (
          <div
            key={hour.dayOfWeek}
            className="grid gap-3 rounded-lg border border-border bg-background p-3 md:grid-cols-[80px_100px_1fr_1fr] md:items-center"
          >
            <p className="text-sm font-medium text-foreground">
              {dayLabels[hour.dayOfWeek]}
            </p>
            <label className="flex items-center gap-2 text-sm text-muted-foreground">
              <Checkbox
                checked={hour.isClosed}
                onCheckedChange={(checked) =>
                  updateDay(hour.dayOfWeek, {
                    isClosed: checked === true,
                    opensAt: checked === true ? null : hour.opensAt ?? "09:00",
                    closesAt: checked === true
                      ? null
                      : hour.closesAt ?? "18:00",
                  })
                }
              />
              Closed
            </label>
            <Input
              type="time"
              disabled={hour.isClosed}
              value={hour.opensAt ?? ""}
              onChange={(event) =>
                updateDay(hour.dayOfWeek, { opensAt: event.target.value })
              }
            />
            <Input
              type="time"
              disabled={hour.isClosed}
              value={hour.closesAt ?? ""}
              onChange={(event) =>
                updateDay(hour.dayOfWeek, { closesAt: event.target.value })
              }
            />
          </div>
        ))}
      </div>
      <Button type="button" onClick={onSave}>
        <Save data-icon="inline-start" className="size-4" />
        Save hours
      </Button>
    </div>
  )
}

function PanelMessages({ message, error }: { message: string; error: string }) {
  return (
    <>
      {message ? (
        <Alert>
          <AlertTitle>Updated</AlertTitle>
          <AlertDescription>{message}</AlertDescription>
        </Alert>
      ) : null}
      {error ? (
        <Alert className="border-destructive/30 bg-destructive/5 text-destructive">
          <AlertTitle>Action failed</AlertTitle>
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      ) : null}
    </>
  )
}

function ProfileFlag({
  label,
  checked,
  onCheckedChange,
}: {
  label: string
  checked: boolean
  onCheckedChange: (checked: boolean) => void
}) {
  return (
    <label className="flex items-center gap-3 rounded-xl border border-[#e5e7eb] bg-white px-4 py-3 text-sm font-medium text-[#111111]">
      <Checkbox
        checked={checked}
        onCheckedChange={(nextChecked) => onCheckedChange(nextChecked === true)}
      />
      {label}
    </label>
  )
}

function PhotoManagerItem({
  photo,
  index,
  count,
  disabled,
  onMove,
  onDelete,
}: {
  photo: BusinessPhoto
  index: number
  count: number
  disabled: boolean
  onMove: (photoId: string, direction: -1 | 1) => void
  onDelete: (photoId: string) => void
}) {
  return (
    <div className="grid grid-cols-[88px_minmax(0,1fr)] gap-3 rounded-xl border border-[#e5e7eb] bg-white p-3">
      <div className="relative h-24 overflow-hidden rounded-lg bg-[#eef0f2]">
        <Image
          src={photo.imageUrl}
          alt={photo.altText || "Business photo"}
          fill
          sizes="88px"
          className="object-cover"
          unoptimized
        />
      </div>
      <div className="min-w-0 space-y-3">
        <div>
          <p className="truncate text-sm font-semibold text-[#111111]">
            {index === 0 ? "Hero photo" : `Photo ${index + 1}`}
          </p>
          <p className="text-xs text-[#71717a]">
            {formatBytes(photo.fileSizeBytes)}
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button
            type="button"
            size="icon-sm"
            variant="outline"
            disabled={disabled || index === 0}
            onClick={() => onMove(photo.id, -1)}
            aria-label="Move photo up"
          >
            <ArrowUp className="size-4" aria-hidden="true" />
          </Button>
          <Button
            type="button"
            size="icon-sm"
            variant="outline"
            disabled={disabled || index === count - 1}
            onClick={() => onMove(photo.id, 1)}
            aria-label="Move photo down"
          >
            <ArrowDown className="size-4" aria-hidden="true" />
          </Button>
          <Button
            type="button"
            size="icon-sm"
            variant="outline"
            disabled={disabled}
            onClick={() => onDelete(photo.id)}
            aria-label="Delete photo"
          >
            <Trash2 className="size-4" aria-hidden="true" />
          </Button>
        </div>
      </div>
    </div>
  )
}

function Field({
  label,
  children,
}: {
  label: string
  children: React.ReactNode
}) {
  return (
    <div className="space-y-2">
      <Label>{label}</Label>
      {children}
    </div>
  )
}

function toProfileDraft(business: BusinessDetail): OwnerBusinessProfileRequest {
  return {
    name: business.name,
    timeZoneId: business.timeZoneId,
    addressLine: business.addressLine,
    district: business.district,
    city: business.city,
    country: business.country,
    description: business.description,
    supportsInstantConfirmation: business.supportsInstantConfirmation,
    supportsPayByApp: business.supportsPayByApp,
    isPetFriendly: business.isPetFriendly,
    isKidFriendly: business.isKidFriendly,
    isNearPublicTransport: business.isNearPublicTransport,
    usesOrganicProducts: business.usesOrganicProducts,
    usesVeganProducts: business.usesVeganProducts,
    isEnvironmentallyFriendly: business.isEnvironmentallyFriendly,
  }
}

function formatOwnerAddress(business: BusinessDetail) {
  return [business.addressLine, business.district, business.city]
    .filter(Boolean)
    .join(", ") || "No address yet"
}

function formatBytes(value: number) {
  if (value < 1024) {
    return `${value} B`
  }

  return `${(value / 1024 / 1024).toFixed(1)} MB`
}

function ServiceCategorySelect({
  categories,
  value,
  onChange,
}: {
  categories: BusinessServiceCategory[]
  value: string
  onChange: (value: string) => void
}) {
  return (
    <Select value={value} onValueChange={onChange}>
      <SelectTrigger>
        <SelectValue placeholder="Select category" />
      </SelectTrigger>
      <SelectContent>
        {categories.map((category) => (
          <SelectItem key={category.id} value={category.name}>
            {category.name}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  )
}

function getServiceCategories(business: BusinessDetail) {
  const categories =
    business.serviceCategories?.length > 0
      ? business.serviceCategories
      : [
          {
            id: "featured",
            name: "Featured",
            sortOrder: 0,
            isSystem: true,
          },
        ]

  return [...categories].sort((left, right) => {
    if (left.sortOrder !== right.sortOrder) {
      return left.sortOrder - right.sortOrder
    }

    return left.name.localeCompare(right.name)
  })
}

function getApiErrorMessage(error: unknown, fallback: string) {
  if (!(error instanceof ApiError)) {
    return fallback
  }

  if (
    typeof error.body === "object" &&
    error.body !== null &&
    "message" in error.body
  ) {
    const message = (error.body as { message?: unknown }).message
    if (typeof message === "string" && message.trim()) {
      return message
    }
  }

  return fallback
}

function toServiceDraft(service: BusinessService): ServiceDraft {
  return {
    name: service.name,
    categoryName: service.categoryName,
    durationMinutes: service.durationMinutes,
    basePriceAmount: service.basePriceAmount,
    currencyCode: service.currencyCode,
    isActive: service.isActive,
  }
}

function normalizeServiceDraft(draft: ServiceDraft): ServiceDraft {
  return {
    name: draft.name,
    categoryName: draft.categoryName.trim() || "Featured",
    durationMinutes: Number(draft.durationMinutes),
    basePriceAmount: Number(draft.basePriceAmount),
    currencyCode: draft.currencyCode,
    isActive: draft.isActive,
  }
}

function formatAppointmentTime(value: string) {
  return new Intl.DateTimeFormat("en", {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(new Date(value))
}

type ServiceDraft = {
  name: string
  categoryName: string
  durationMinutes: number
  basePriceAmount: number
  currencyCode: string
  isActive: boolean
}
