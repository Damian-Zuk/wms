import { http } from '../http'

export const adminApi = {
  seed: () => http.post<void>('/admin/seed').then(() => undefined),

  truncate: () => http.post<void>('/admin/truncate').then(() => undefined),
}
