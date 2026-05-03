import { apiRequest } from "@/lib/api-client"

export type PublicBusiness = {
  id: string
  name: string
  type: string
  timeZoneId: string
  services: PublicBusinessSummaryService[]
}

export type PublicBusinessSummaryService = {
  id: string
  name: string
  durationMinutes: number
  currencyCode: string
}

export type PublicBusinessService = PublicBusinessSummaryService & {
  basePriceAmount: number
}

export type PublicBusinessDetail = Omit<PublicBusiness, "services"> & {
  services: PublicBusinessService[]
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
