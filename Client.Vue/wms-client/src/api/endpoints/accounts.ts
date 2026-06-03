import { http } from '../http'
import type {
  ChangePasswordRequest,
  LoginRequest,
  LoginResponse,
  RegisterRequest,
  UserDto,
} from '@/types/auth'

export const accountsApi = {
  login: (body: LoginRequest) =>
    http.post<LoginResponse>('/accounts/login', body).then((r) => r.data),

  me: () => http.get<UserDto>('/accounts/me').then((r) => r.data),

  list: () => http.get<UserDto[]>('/accounts').then((r) => r.data),

  register: (body: RegisterRequest) =>
    http.post<UserDto>('/accounts/register', body).then((r) => r.data),

  changePassword: (id: string, body: ChangePasswordRequest) =>
    http.put<void>(`/accounts/${id}/password`, body).then(() => undefined),

  remove: (id: string) => http.delete<void>(`/accounts/${id}`).then(() => undefined),
}
