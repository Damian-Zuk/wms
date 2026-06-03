export type Role = 'Admin' | 'Manager' | 'Worker'

export interface UserDto {
  id: string
  email: string
  userName: string
  firstName: string
  lastName: string
  roles: string[]
}

export interface LoginRequest {
  email: string
  password: string
}

export interface RegisterRequest {
  email: string
  userName: string
  password: string
  firstName: string
  lastName: string
  role: Role
}

export interface ChangePasswordRequest {
  newPassword: string
}

export interface LoginResponse {
  token: string
  /** ISO 8601 timestamp */
  expiresAt: string
  user: UserDto
}
