import { apiRequest } from "@/lib/api-client"

export type PublicBusiness = {
  id: string
  name: string
  type: string
  timeZoneId: string
  address: PublicBusinessAddress
  services: PublicBusinessSummaryService[]
  workingHours: PublicBusinessWorkingHour[]
  photos: PublicBusinessPhoto[]
  reviewSummary: PublicBusinessReviewSummary
  additionalInformation: string[]
}

export type PublicBusinessSummaryService = {
  id: string
  name: string
  durationMinutes: number
  currencyCode: string
}

export type PublicBusinessService = PublicBusinessSummaryService & {
  categoryName: string
  description: string
  basePriceAmount: number
}

export type PublicBusinessDetail = Omit<PublicBusiness, "services"> & {
  address: PublicBusinessAddress
  description: string
  services: PublicBusinessService[]
  workingHours: PublicBusinessWorkingHour[]
  staffMembers: PublicBusinessStaffMember[]
  photos: PublicBusinessPhoto[]
  reviewSummary: PublicBusinessReviewSummary
  reviews: PublicBusinessReview[]
  additionalInformation: string[]
}

export type PublicBusinessAddress = {
  addressLine: string
  district: string
  city: string
  country: string
}

export type PublicBusinessWorkingHour = {
  dayOfWeek: string
  opensAt: string
  closesAt: string
}

export type PublicBusinessStaffMember = {
  id: string
  displayName: string
}

export type PublicBusinessPhoto = {
  id: string
  imageUrl: string
  altText: string
  sortOrder: number
}

export type PublicBusinessReviewSummary = {
  averageRating: number
  reviewCount: number
}

export type PublicBusinessReview = {
  id: string
  customerName: string
  customerInitial: string
  rating: number
  comment: string
  createdAtUtc: string
}

export function getPublicBusinesses(params?: { search?: string; type?: string }) {
  const searchParams = new URLSearchParams()

  if (params?.search?.trim()) {
    searchParams.set("search", params.search.trim())
  }

  if (params?.type?.trim()) {
    searchParams.set("type", params.type.trim())
  }

  const query = searchParams.toString()
  return apiRequest<PublicBusiness[]>(
    `/public/businesses${query ? `?${query}` : ""}`
  )
}

export function getPublicBusiness(businessId: string) {
  return apiRequest<PublicBusinessDetail>(`/public/businesses/${businessId}`)
}
