import { apiRequest } from "@/lib/api-client"
import { getRefreshToken, setAuthTokens } from "@/lib/auth-storage"

export type AuthenticatedUser = {
  id: string
  publicNumber: number
  email: string
  firstName: string
  lastName: string
  fullName: string
  roles: string[]
}

export type BusinessMembership = {
  businessId: string
  businessName: string
  role: string
  status: string
}

export type CurrentUser = AuthenticatedUser & {
  businessMemberships: BusinessMembership[]
}

export type AuthTokenResponse = {
  accessToken: string
  accessTokenExpiresAtUtc: string
  refreshToken: string
  user: AuthenticatedUser
}

export type EmailAvailability = {
  email: string
  isAvailable: boolean
}

export type PendingEmailRegistration = {
  email: string
  codeExpiresAtUtc: string
  resendAvailableAtUtc: string
}

export type OwnerBusiness = {
  id: string
  name: string
  type: string
  status: string
  timeZoneId: string
}

export type BusinessService = {
  id: string
  name: string
  categoryName: string
  description: string
  durationMinutes: number
  basePriceAmount: number
  currencyCode: string
  isActive: boolean
}

export type BusinessServiceCategory = {
  id: string
  name: string
  sortOrder: number
  isSystem: boolean
}

export type BusinessStaffMember = {
  id: string
  displayName: string
  email: string
  isActive: boolean
}

export type BusinessDetail = OwnerBusiness & {
  addressLine: string
  district: string
  city: string
  country: string
  description: string
  supportsInstantConfirmation: boolean
  supportsPayByApp: boolean
  isPetFriendly: boolean
  isKidFriendly: boolean
  isNearPublicTransport: boolean
  usesOrganicProducts: boolean
  usesVeganProducts: boolean
  isEnvironmentallyFriendly: boolean
  owner?: {
    id: string
    publicNumber: number
    email: string
    firstName: string
    lastName: string
    fullName: string
  } | null
  serviceCount?: number
  staffCount?: number
  appointmentCount?: number
  serviceCategories: BusinessServiceCategory[]
  services: BusinessService[]
  staffMembers: BusinessStaffMember[]
  photos: BusinessPhoto[]
  reviewSummary: BusinessReviewSummary
  reviews: BusinessReview[]
}

export type BusinessPhoto = {
  id: string
  imageUrl: string
  altText: string
  sortOrder: number
  contentType: string
  fileSizeBytes: number
}

export type BusinessReviewSummary = {
  averageRating: number
  reviewCount: number
}

export type BusinessReview = {
  id: string
  customerName: string
  customerInitial: string
  rating: number
  comment: string
  createdAtUtc: string
}

export type OwnerAppointmentRequest = {
  id: string
  status: string
  startsAtUtc: string
  endsAtUtc: string
  serviceName: string
  staffDisplayName: string
  customerFullName: string
  priceAmount: number
  currencyCode: string
}

export type EmployeeAppointmentRequest = OwnerAppointmentRequest & {
  businessId: string
  businessName: string
}

export type OwnerAppointmentRequestDecision = {
  id: string
  status: string
  autoRejectedCount: number
}

export type CustomerAppointment = {
  id: string
  status: string
  startsAtUtc: string
  endsAtUtc: string
  businessName: string
  serviceName: string
  staffDisplayName: string
  priceAmount: number
  currencyCode: string
  hasReview: boolean
}

export type CustomerAppointmentDecision = {
  id: string
  status: string
}

export type CustomerAppointmentReviewRequest = {
  rating: number
  comment: string
}

export type CustomerAppointmentReview = {
  id: string
  appointmentId: string
  businessId: string
  customerName: string
  customerInitial: string
  rating: number
  comment: string
  createdAtUtc: string
}

export type EmployeeAppointment = {
  id: string
  status: string
  startsAtUtc: string
  endsAtUtc: string
  businessId: string
  businessName: string
  serviceName: string
  staffDisplayName: string
  customerFullName: string
  priceAmount: number
  currencyCode: string
}

export type OwnerAppointment = {
  id: string
  status: string
  startsAtUtc: string
  endsAtUtc: string
  serviceName: string
  staffDisplayName: string
  customerFullName: string
  priceAmount: number
  currencyCode: string
}

export type AppointmentDecision = {
  id: string
  status: string
}

export type AppointmentFilters = {
  status?: string
  fromUtc?: string
  toUtc?: string
}

export type AdminBusinessStatus = {
  id: string
  status: string
}

export type WorkingHour = {
  dayOfWeek: number
  isClosed: boolean
  opensAt: string | null
  closesAt: string | null
}

export type AvailabilityException = {
  id: string
  businessId: string
  staffMemberId: string | null
  staffDisplayName: string | null
  type: "BusinessClosed" | "Holiday" | "StaffLeave"
  date: string
  isFullDay: boolean
  startsAt: string | null
  endsAt: string | null
  note: string | null
  createdAtUtc: string
}

export type AvailabilityExceptionRequest = {
  businessId?: string
  staffMemberId?: string | null
  type: "BusinessClosed" | "Holiday" | "StaffLeave"
  date: string
  isFullDay: boolean
  startsAt?: string | null
  endsAt?: string | null
  note?: string | null
  cancelConflictingAppointments?: boolean
}

export type AvailabilityExceptionConflict = {
  message: string
  appointmentCount: number
  appointments: {
    id: string
    status: string
    startsAtUtc: string
    endsAtUtc: string
    serviceName: string
    staffDisplayName: string
  }[]
}

export type OwnerServiceRequest = {
  name: string
  categoryName: string
  description: string
  durationMinutes: number
  basePriceAmount: number
  currencyCode: string
  isActive: boolean
}

export type OwnerServiceCategoryRequest = {
  name: string
}

export type OwnerBusinessProfileRequest = {
  name: string
  timeZoneId: string
  addressLine: string
  district: string
  city: string
  country: string
  description: string
  supportsInstantConfirmation: boolean
  supportsPayByApp: boolean
  isPetFriendly: boolean
  isKidFriendly: boolean
  isNearPublicTransport: boolean
  usesOrganicProducts: boolean
  usesVeganProducts: boolean
  isEnvironmentallyFriendly: boolean
}

export type CreateOwnerBusinessRequest = {
  name: string
  type: number
}

export type OwnerBusinessInvitation = {
  id: string
  email: string
  role: string
  status: string
  createdAtUtc: string
  expiresAtUtc: string
  acceptedAtUtc: string | null
  acceptanceToken: string | null
}

export type AcceptedBusinessInvitation = {
  businessId: string
  businessName: string
  role: string
}

export type AdminUser = {
  id: string
  publicNumber: number
  email: string
  firstName: string
  lastName: string
  fullName: string
  isSuspended: boolean
  roles: string[]
}

export type AdminUserDetail = AdminUser & {
  businessMemberships: BusinessMembership[]
}

export type OwnerOnboardingRequest = {
  id: string
  requesterUserId: string
  businessName: string
  businessType: string
  status: string
  adminNote: string | null
  createdBusinessId: string | null
  createdAtUtc: string
  reviewedAtUtc: string | null
}

export type AdminOwnerOnboardingRequest = OwnerOnboardingRequest & {
  requesterEmail: string
  requesterPublicNumber: number
  requesterFirstName: string
  requesterLastName: string
  requesterFullName: string
}

export type NotificationItem = {
  id: string
  title: string
  message: string
  linkUrl: string | null
  type: string
  createdAtUtc: string
  readAtUtc: string | null
}

export type NotificationsResponse = {
  unreadCount: number
  notifications: NotificationItem[]
}

export async function login(email: string, password: string) {
  const response = await apiRequest<AuthTokenResponse>("/auth/login", {
    method: "POST",
    body: JSON.stringify({ email, password }),
    skipAuthRefresh: true,
  })

  setAuthTokens(response.accessToken, response.refreshToken)

  return response
}

export async function register(
  firstName: string,
  lastName: string,
  email: string,
  password: string,
  confirmPassword: string
) {
  return apiRequest<PendingEmailRegistration>("/auth/register", {
    method: "POST",
    body: JSON.stringify({
      firstName,
      lastName,
      email,
      password,
      confirmPassword,
    }),
    skipAuthRefresh: true,
  })
}

export function confirmEmail(email: string, code: string) {
  return apiRequest<void>("/auth/confirm-email", {
    method: "POST",
    body: JSON.stringify({ email, code }),
    skipAuthRefresh: true,
    ignoreNoContent: true,
  })
}

export function resendConfirmationCode(email: string) {
  return apiRequest<PendingEmailRegistration>(
    "/auth/resend-confirmation-code",
    {
      method: "POST",
      body: JSON.stringify({ email }),
      skipAuthRefresh: true,
    }
  )
}

export function checkEmailAvailability(email: string) {
  const searchParams = new URLSearchParams({ email })

  return apiRequest<EmailAvailability>(
    `/auth/email-availability?${searchParams.toString()}`,
    { skipAuthRefresh: true }
  )
}

export async function refreshSession(refreshToken: string) {
  const response = await apiRequest<AuthTokenResponse>("/auth/refresh", {
    method: "POST",
    body: JSON.stringify({ refreshToken }),
    skipAuthRefresh: true,
  })

  setAuthTokens(response.accessToken, response.refreshToken)

  return response
}

export async function logout() {
  const refreshToken = getRefreshToken()

  if (!refreshToken) {
    return
  }

  await apiRequest<void>("/auth/logout", {
    method: "POST",
    body: JSON.stringify({ refreshToken }),
    skipAuthRefresh: true,
    ignoreNoContent: true,
  })
}

export function getCurrentUser() {
  return apiRequest<CurrentUser>("/auth/me")
}

export function getOwnerBusinesses() {
  return apiRequest<OwnerBusiness[]>("/owner/businesses")
}

export function getOwnerBusiness(businessId: string) {
  return apiRequest<BusinessDetail>(`/owner/businesses/${businessId}`)
}

export function createOwnerBusiness(request: CreateOwnerBusinessRequest) {
  return apiRequest<BusinessDetail>("/owner/businesses", {
    method: "POST",
    body: JSON.stringify(request),
  })
}

export function updateOwnerBusinessProfile(
  businessId: string,
  request: OwnerBusinessProfileRequest
) {
  return apiRequest<BusinessDetail>(`/owner/businesses/${businessId}/profile`, {
    method: "PUT",
    body: JSON.stringify(request),
  })
}

export function getOwnerBusinessInvitations(businessId: string) {
  return apiRequest<OwnerBusinessInvitation[]>(
    `/owner/businesses/${businessId}/invitations`
  )
}

export function createOwnerBusinessInvitation(
  businessId: string,
  request: { email: string }
) {
  return apiRequest<OwnerBusinessInvitation>(
    `/owner/businesses/${businessId}/invitations`,
    {
      method: "POST",
      body: JSON.stringify(request),
    }
  )
}

export function acceptBusinessInvitation(token: string) {
  return apiRequest<AcceptedBusinessInvitation>("/business-invitations/accept", {
    method: "POST",
    body: JSON.stringify({ token }),
  })
}

export function getAdminBusinesses(params?: {
  search?: string
  status?: string
  type?: string
}) {
  const query = buildQuery(params)
  return apiRequest<OwnerBusiness[]>(`/admin/businesses${query}`)
}

export function getAdminBusiness(businessId: string) {
  return apiRequest<BusinessDetail>(`/admin/businesses/${businessId}`)
}

export function approveAdminBusiness(businessId: string) {
  return apiRequest<AdminBusinessStatus>(
    `/admin/businesses/${businessId}/approve`,
    { method: "POST" }
  )
}

export function suspendAdminBusiness(businessId: string) {
  return apiRequest<AdminBusinessStatus>(
    `/admin/businesses/${businessId}/suspend`,
    { method: "POST" }
  )
}

export function rejectAdminBusiness(businessId: string) {
  return apiRequest<AdminBusinessStatus>(
    `/admin/businesses/${businessId}/reject`,
    { method: "POST" }
  )
}

export function createOwnerService(
  businessId: string,
  request: OwnerServiceRequest
) {
  return apiRequest<BusinessService>(`/owner/businesses/${businessId}/services`, {
    method: "POST",
    body: JSON.stringify(request),
  })
}

export function createOwnerServiceCategory(
  businessId: string,
  request: OwnerServiceCategoryRequest
) {
  return apiRequest<BusinessServiceCategory>(
    `/owner/businesses/${businessId}/service-categories`,
    {
      method: "POST",
      body: JSON.stringify(request),
    }
  )
}

export function updateOwnerServiceCategory(
  businessId: string,
  categoryId: string,
  request: OwnerServiceCategoryRequest
) {
  return apiRequest<BusinessServiceCategory>(
    `/owner/businesses/${businessId}/service-categories/${categoryId}`,
    {
      method: "PUT",
      body: JSON.stringify(request),
    }
  )
}

export function deleteOwnerServiceCategory(
  businessId: string,
  categoryId: string
) {
  return apiRequest<void>(
    `/owner/businesses/${businessId}/service-categories/${categoryId}`,
    { method: "DELETE", ignoreNoContent: true }
  )
}

export function updateOwnerService(
  businessId: string,
  serviceId: string,
  request: OwnerServiceRequest
) {
  return apiRequest<BusinessService>(
    `/owner/businesses/${businessId}/services/${serviceId}`,
    {
      method: "PUT",
      body: JSON.stringify(request),
    }
  )
}

export function activateOwnerService(businessId: string, serviceId: string) {
  return apiRequest<BusinessService>(
    `/owner/businesses/${businessId}/services/${serviceId}/activate`,
    { method: "POST" }
  )
}

export function deactivateOwnerService(businessId: string, serviceId: string) {
  return apiRequest<BusinessService>(
    `/owner/businesses/${businessId}/services/${serviceId}/deactivate`,
    { method: "POST" }
  )
}

export function uploadOwnerBusinessPhoto(
  businessId: string,
  file: File,
  altText: string
) {
  const formData = new FormData()
  formData.append("file", file)
  formData.append("altText", altText)

  return apiRequest<BusinessPhoto>(`/owner/businesses/${businessId}/photos`, {
    method: "POST",
    body: formData,
  })
}

export function deleteOwnerBusinessPhoto(
  businessId: string,
  photoId: string
) {
  return apiRequest<void>(
    `/owner/businesses/${businessId}/photos/${photoId}`,
    {
      method: "DELETE",
      ignoreNoContent: true,
    }
  )
}

export function reorderOwnerBusinessPhotos(
  businessId: string,
  photoIds: string[]
) {
  return apiRequest<BusinessPhoto[]>(
    `/owner/businesses/${businessId}/photos/order`,
    {
      method: "PUT",
      body: JSON.stringify({ photoIds }),
    }
  )
}

export function activateOwnerStaff(businessId: string, staffMemberId: string) {
  return apiRequest<BusinessStaffMember>(
    `/owner/businesses/${businessId}/staff/${staffMemberId}/activate`,
    { method: "POST" }
  )
}

export function deactivateOwnerStaff(businessId: string, staffMemberId: string) {
  return apiRequest<BusinessStaffMember>(
    `/owner/businesses/${businessId}/staff/${staffMemberId}/deactivate`,
    { method: "POST" }
  )
}

export function getOwnerBusinessWorkingHours(businessId: string) {
  return apiRequest<WorkingHour[]>(
    `/owner/businesses/${businessId}/working-hours`
  )
}

export function updateOwnerBusinessWorkingHours(
  businessId: string,
  workingHours: WorkingHour[]
) {
  return apiRequest<WorkingHour[]>(
    `/owner/businesses/${businessId}/working-hours`,
    {
      method: "PUT",
      body: JSON.stringify(workingHours),
    }
  )
}

export function getOwnerStaffWorkingHours(
  businessId: string,
  staffMemberId: string
) {
  return apiRequest<WorkingHour[]>(
    `/owner/businesses/${businessId}/staff/${staffMemberId}/working-hours`
  )
}

export function getOwnerAvailabilityExceptions(businessId: string) {
  return apiRequest<AvailabilityException[]>(
    `/owner/businesses/${businessId}/availability-exceptions`
  )
}

export function createOwnerAvailabilityException(
  businessId: string,
  request: AvailabilityExceptionRequest
) {
  return apiRequest<AvailabilityException>(
    `/owner/businesses/${businessId}/availability-exceptions`,
    {
      method: "POST",
      body: JSON.stringify(request),
    }
  )
}

export function updateOwnerAvailabilityException(
  businessId: string,
  exceptionId: string,
  request: AvailabilityExceptionRequest
) {
  return apiRequest<AvailabilityException>(
    `/owner/businesses/${businessId}/availability-exceptions/${exceptionId}`,
    {
      method: "PUT",
      body: JSON.stringify(request),
    }
  )
}

export function deleteOwnerAvailabilityException(
  businessId: string,
  exceptionId: string
) {
  return apiRequest<void>(
    `/owner/businesses/${businessId}/availability-exceptions/${exceptionId}`,
    { method: "DELETE", ignoreNoContent: true }
  )
}

export function updateOwnerStaffWorkingHours(
  businessId: string,
  staffMemberId: string,
  workingHours: WorkingHour[]
) {
  return apiRequest<WorkingHour[]>(
    `/owner/businesses/${businessId}/staff/${staffMemberId}/working-hours`,
    {
      method: "PUT",
      body: JSON.stringify(workingHours),
    }
  )
}

export function getOwnerAppointmentRequests(businessId: string) {
  return apiRequest<OwnerAppointmentRequest[]>(
    `/owner/businesses/${businessId}/appointment-requests`
  )
}

export function approveOwnerAppointmentRequest(
  businessId: string,
  appointmentId: string
) {
  return apiRequest<OwnerAppointmentRequestDecision>(
    `/owner/businesses/${businessId}/appointment-requests/${appointmentId}/approve`,
    { method: "POST" }
  )
}

export function rejectOwnerAppointmentRequest(
  businessId: string,
  appointmentId: string
) {
  return apiRequest<OwnerAppointmentRequestDecision>(
    `/owner/businesses/${businessId}/appointment-requests/${appointmentId}/reject`,
    { method: "POST" }
  )
}

export function getEmployeeAppointmentRequests() {
  return apiRequest<EmployeeAppointmentRequest[]>(
    "/employee/appointment-requests"
  )
}

export function approveEmployeeAppointmentRequest(appointmentId: string) {
  return apiRequest<OwnerAppointmentRequestDecision>(
    `/employee/appointment-requests/${appointmentId}/approve`,
    { method: "POST" }
  )
}

export function rejectEmployeeAppointmentRequest(appointmentId: string) {
  return apiRequest<OwnerAppointmentRequestDecision>(
    `/employee/appointment-requests/${appointmentId}/reject`,
    { method: "POST" }
  )
}

export function getEmployeeAppointments(params?: AppointmentFilters) {
  const query = buildAppointmentQuery(params)
  return apiRequest<EmployeeAppointment[]>(`/employee/appointments${query}`)
}

export function cancelEmployeeAppointment(appointmentId: string) {
  return apiRequest<AppointmentDecision>(
    `/employee/appointments/${appointmentId}/cancel`,
    { method: "POST" }
  )
}

export function getEmployeeAvailabilityExceptions() {
  return apiRequest<AvailabilityException[]>("/employee/availability-exceptions")
}

export function createEmployeeAvailabilityException(
  request: AvailabilityExceptionRequest
) {
  return apiRequest<AvailabilityException>("/employee/availability-exceptions", {
    method: "POST",
    body: JSON.stringify(request),
  })
}

export function updateEmployeeAvailabilityException(
  exceptionId: string,
  request: AvailabilityExceptionRequest
) {
  return apiRequest<AvailabilityException>(
    `/employee/availability-exceptions/${exceptionId}`,
    {
      method: "PUT",
      body: JSON.stringify(request),
    }
  )
}

export function deleteEmployeeAvailabilityException(exceptionId: string) {
  return apiRequest<void>(
    `/employee/availability-exceptions/${exceptionId}`,
    { method: "DELETE", ignoreNoContent: true }
  )
}

export function completeEmployeeAppointment(appointmentId: string) {
  return apiRequest<AppointmentDecision>(
    `/employee/appointments/${appointmentId}/complete`,
    { method: "POST" }
  )
}

export function markEmployeeAppointmentNoShow(appointmentId: string) {
  return apiRequest<AppointmentDecision>(
    `/employee/appointments/${appointmentId}/no-show`,
    { method: "POST" }
  )
}

export function getOwnerAppointments(
  businessId: string,
  params?: AppointmentFilters
) {
  const query = buildAppointmentQuery(params)
  return apiRequest<OwnerAppointment[]>(
    `/owner/businesses/${businessId}/appointments${query}`
  )
}

export function cancelOwnerAppointment(
  businessId: string,
  appointmentId: string
) {
  return apiRequest<AppointmentDecision>(
    `/owner/businesses/${businessId}/appointments/${appointmentId}/cancel`,
    { method: "POST" }
  )
}

export function completeOwnerAppointment(
  businessId: string,
  appointmentId: string
) {
  return apiRequest<AppointmentDecision>(
    `/owner/businesses/${businessId}/appointments/${appointmentId}/complete`,
    { method: "POST" }
  )
}

export function markOwnerAppointmentNoShow(
  businessId: string,
  appointmentId: string
) {
  return apiRequest<AppointmentDecision>(
    `/owner/businesses/${businessId}/appointments/${appointmentId}/no-show`,
    { method: "POST" }
  )
}

export function getCustomerAppointments(params?: AppointmentFilters) {
  const query = buildAppointmentQuery(params)
  return apiRequest<CustomerAppointment[]>(`/customer/appointments${query}`)
}

export function cancelCustomerAppointment(appointmentId: string) {
  return apiRequest<CustomerAppointmentDecision>(
    `/customer/appointments/${appointmentId}/cancel`,
    { method: "POST" }
  )
}

export function createCustomerAppointmentReview(
  appointmentId: string,
  request: CustomerAppointmentReviewRequest
) {
  return apiRequest<CustomerAppointmentReview>(
    `/customer/appointments/${appointmentId}/review`,
    {
      method: "POST",
      body: JSON.stringify(request),
    }
  )
}

export function getAdminUsers(params?: { search?: string }) {
  const query = buildQuery(params)
  return apiRequest<AdminUser[]>(`/admin/users${query}`)
}

export function getAdminUser(userId: string) {
  return apiRequest<AdminUserDetail>(`/admin/users/${userId}`)
}

export function suspendAdminUser(userId: string) {
  return apiRequest<AdminUserDetail>(`/admin/users/${userId}/suspend`, {
    method: "POST",
  })
}

export function unsuspendAdminUser(userId: string) {
  return apiRequest<AdminUserDetail>(`/admin/users/${userId}/unsuspend`, {
    method: "POST",
  })
}

export function addAdminUserRole(userId: string, roleName: string) {
  return apiRequest<AdminUserDetail>(`/admin/users/${userId}/roles`, {
    method: "POST",
    body: JSON.stringify({ roleName }),
  })
}

export function removeAdminUserRole(userId: string, roleName: string) {
  return apiRequest<AdminUserDetail>(
    `/admin/users/${userId}/roles/${encodeURIComponent(roleName)}`,
    { method: "DELETE" }
  )
}

export function upsertAdminUserBusinessMembership(
  userId: string,
  request: {
    businessId: string
    role: "Owner" | "Employee"
    status: "Active" | "Suspended"
  }
) {
  return apiRequest<AdminUserDetail>(
    `/admin/users/${userId}/business-memberships`,
    {
      method: "POST",
      body: JSON.stringify({
        businessId: request.businessId,
        role: request.role === "Owner" ? 1 : 2,
        status: request.status === "Active" ? 1 : 2,
      }),
    }
  )
}

export function activateAdminUserBusinessMembership(
  userId: string,
  businessId: string
) {
  return apiRequest<AdminUserDetail>(
    `/admin/users/${userId}/business-memberships/${businessId}/activate`,
    { method: "POST" }
  )
}

export function suspendAdminUserBusinessMembership(
  userId: string,
  businessId: string
) {
  return apiRequest<AdminUserDetail>(
    `/admin/users/${userId}/business-memberships/${businessId}/suspend`,
    { method: "POST" }
  )
}

export function getMyOwnerOnboardingRequests() {
  return apiRequest<OwnerOnboardingRequest[]>("/owner-onboarding-requests/me")
}

export function createOwnerOnboardingRequest(request: {
  businessName: string
  businessType: number
}) {
  return apiRequest<OwnerOnboardingRequest>("/owner-onboarding-requests", {
    method: "POST",
    body: JSON.stringify(request),
  })
}

export function getAdminOwnerOnboardingRequests(params?: { status?: string }) {
  const query = buildQuery(params)
  return apiRequest<AdminOwnerOnboardingRequest[]>(
    `/admin/owner-onboarding-requests${query}`
  )
}

export function approveAdminOwnerOnboardingRequest(
  requestId: string,
  adminNote?: string
) {
  return apiRequest<OwnerOnboardingRequest>(
    `/admin/owner-onboarding-requests/${requestId}/approve`,
    {
      method: "POST",
      body: JSON.stringify({ adminNote }),
    }
  )
}

export function rejectAdminOwnerOnboardingRequest(
  requestId: string,
  adminNote?: string
) {
  return apiRequest<OwnerOnboardingRequest>(
    `/admin/owner-onboarding-requests/${requestId}/reject`,
    {
      method: "POST",
      body: JSON.stringify({ adminNote }),
    }
  )
}

export function getNotifications() {
  return apiRequest<NotificationsResponse>("/notifications")
}

export function markNotificationRead(notificationId: string) {
  return apiRequest<void>(`/notifications/${notificationId}/read`, {
    method: "POST",
    ignoreNoContent: true,
  })
}

export function markAllNotificationsRead() {
  return apiRequest<void>("/notifications/read-all", {
    method: "POST",
    ignoreNoContent: true,
  })
}

function buildQuery(params?: Record<string, string | undefined>) {
  const searchParams = new URLSearchParams()

  Object.entries(params ?? {}).forEach(([key, value]) => {
    if (value && value.trim()) {
      searchParams.set(key, value.trim())
    }
  })

  const query = searchParams.toString()
  return query ? `?${query}` : ""
}

function buildAppointmentQuery(params?: AppointmentFilters) {
  return buildQuery({
    status: params?.status,
    fromUtc: params?.fromUtc,
    toUtc: params?.toUtc,
  })
}
