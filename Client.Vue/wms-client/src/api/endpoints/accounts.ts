import { http } from '../http'
import type { LoginRequest, LoginResponse, UserDto } from '@/types/auth'

export const accountsApi = {
  login: (body: LoginRequest) =>
    http.post<LoginResponse>('/accounts/login', body).then((r) => r.data),

  me: () => http.get<UserDto>('/accounts/me').then((r) => r.data),
}
