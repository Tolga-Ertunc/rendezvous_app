import { apiRequest } from "@/lib/api-client"

export type AvailableStaff = {
  staffMemberId: string
  displayName: string
}

export type AvailabilitySlot = {
  startsAtUtc: string
  endsAtUtc: string
  startsAtLocal: string
  endsAtLocal: string
  staffMembers: AvailableStaff[]
}

export type BookingAvailability = {
  date: string
  serviceId: string
  durationMinutes: number
  slots: AvailabilitySlot[]
}

export type AppointmentRequest = {
  id: string
  status: string
  startsAtUtc: string
  endsAtUtc: string
  priceAmount: number
  currencyCode: string
}

export function getBookingAvailability(
  businessId: string,
  serviceId: string,
  date: string
) {
  const searchParams = new URLSearchParams({ date })

  return apiRequest<BookingAvailability>(
    `/booking/businesses/${businessId}/services/${serviceId}/availability?${searchParams.toString()}`
  )
}

export function createAppointmentRequest(input: {
  businessId: string
  serviceId: string
  staffMemberId: string
  startsAtUtc: string
}) {
  return apiRequest<AppointmentRequest>("/booking/appointment-requests", {
    method: "POST",
    body: JSON.stringify(input),
  })
}
