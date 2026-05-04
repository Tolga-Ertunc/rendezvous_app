"use client"

import { useEffect, useState } from "react"
import type React from "react"
import { CalendarDays, Clock, ListChecks, Save, UsersRound, X } from "lucide-react"

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
  createOwnerService,
  deactivateOwnerService,
  deactivateOwnerStaff,
  getOwnerAppointments,
  getOwnerBusinessWorkingHours,
  getOwnerStaffWorkingHours,
  updateOwnerBusinessWorkingHours,
  updateOwnerService,
  updateOwnerStaff,
  updateOwnerStaffWorkingHours,
} from "@/lib/auth-api"
import type {
  BusinessDetail,
  BusinessService,
  BusinessStaffMember,
  OwnerAppointment,
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

export function OwnerServicesPanel({
  business,
  onChanged,
}: {
  business: BusinessDetail
  onChanged: () => Promise<void>
}) {
  const [drafts, setDrafts] = useState<Record<string, ServiceDraft>>({})
  const [newService, setNewService] = useState<ServiceDraft>({
    name: "",
    durationMinutes: 30,
    basePriceAmount: 0,
    currencyCode: "TRY",
    isActive: true,
  })
  const [actingId, setActingId] = useState("")
  const [message, setMessage] = useState("")
  const [error, setError] = useState("")

  async function handleCreate() {
    setActingId("new")
    setMessage("")
    setError("")

    try {
      await createOwnerService(business.id, normalizeServiceDraft(newService))
      setNewService({
        name: "",
        durationMinutes: 30,
        basePriceAmount: 0,
        currencyCode: "TRY",
        isActive: true,
      })
      setMessage("Service created.")
      await onChanged()
    } catch {
      setError("Service could not be created.")
    } finally {
      setActingId("")
    }
  }

  async function handleUpdate(serviceId: string) {
    setActingId(serviceId)
    setMessage("")
    setError("")

    try {
      await updateOwnerService(
        business.id,
        serviceId,
        normalizeServiceDraft(drafts[serviceId])
      )
      setMessage("Service updated.")
      await onChanged()
    } catch {
      setError("Service could not be updated.")
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
    } catch {
      setError("Service status could not be updated.")
    } finally {
      setActingId("")
    }
  }

  return (
    <Card>
      <CardHeader>
        <div className="flex items-center gap-2">
          <ListChecks className="size-4 text-primary" aria-hidden="true" />
          <CardTitle>Manage services</CardTitle>
        </div>
        <CardDescription>
          Service edits affect new appointment requests only; existing
          appointments keep their price snapshot.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <PanelMessages message={message} error={error} />
        <div className="grid gap-3 rounded-lg border border-border bg-background p-3 md:grid-cols-[minmax(0,1fr)_120px_140px_90px_auto] md:items-end">
          <Field label="Name">
            <Input
              value={newService.name}
              onChange={(event) =>
                setNewService({ ...newService, name: event.target.value })
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
            disabled={actingId === "new"}
            onClick={handleCreate}
          >
            <Save data-icon="inline-start" className="size-4" />
            Add
          </Button>
        </div>

        <div className="grid gap-3">
          {business.services.map((service) => {
            const draft = drafts[service.id] ?? toServiceDraft(service)

            return (
              <div
                key={service.id}
                className="grid gap-3 rounded-lg border border-border bg-background p-3 md:grid-cols-[minmax(0,1fr)_120px_140px_90px_auto] md:items-end"
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
                    disabled={actingId === service.id}
                    onClick={() => handleUpdate(service.id)}
                  >
                    Save
                  </Button>
                  <Button
                    type="button"
                    size="sm"
                    variant="outline"
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
      </CardContent>
    </Card>
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

function toServiceDraft(service: BusinessService): ServiceDraft {
  return {
    name: service.name,
    durationMinutes: service.durationMinutes,
    basePriceAmount: service.basePriceAmount,
    currencyCode: service.currencyCode,
    isActive: service.isActive,
  }
}

function normalizeServiceDraft(draft: ServiceDraft): ServiceDraft {
  return {
    name: draft.name,
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
  durationMinutes: number
  basePriceAmount: number
  currencyCode: string
  isActive: boolean
}
