import { apiRequest } from "@/lib/api-client"

export type AvailableStaff = {
  staffMemberId: string
  displayName: string
  profilePhotoUrl: string | null
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

export type StylePreview = {
  previewId: string
  originalImageUrl: string
  generatedImageUrl: string
  imageUrl: string
  isPlaceholder: boolean
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
  stylePreviewId?: string
}) {
  return apiRequest<AppointmentRequest>("/booking/appointment-requests", {
    method: "POST",
    body: JSON.stringify(input),
  })
}

export function generateStylePreview(input: {
  businessId: string
  serviceId: string
  staffMemberId: string
  image: File
  prompt: string
}) {
  const formData = new FormData()
  formData.append("businessId", input.businessId)
  formData.append("serviceId", input.serviceId)
  formData.append("staffMemberId", input.staffMemberId)
  formData.append("image", input.image)
  formData.append("prompt", input.prompt)

  return apiRequest<StylePreview>("/booking/style-previews", {
    method: "POST",
    body: formData,
  })
}
