<script setup lang="ts">
import { ref } from 'vue'
import { useConfirm } from 'primevue/useconfirm'
import { useToast } from 'primevue/usetoast'
import Column from 'primevue/column'
import Button from 'primevue/button'
import Tag from 'primevue/tag'
import DataTableWrapper from '@/components/common/DataTableWrapper.vue'
import RefreshButton from '@/components/common/RefreshButton.vue'
import CreateUserDialog from './CreateUserDialog.vue'
import ChangePasswordDialog from './ChangePasswordDialog.vue'
import { useUsers, useDeleteUser } from './useUsers'
import { serverErrorText } from './server-error'
import { useAuthStore } from '@/stores/auth'
import type { UserDto } from '@/types/auth'

const auth = useAuthStore()
const confirm = useConfirm()
const toast = useToast()

const { data, isFetching, refetch } = useUsers()
const deleteUser = useDeleteUser()

const createVisible = ref(false)
const passwordVisible = ref(false)
const selectedUser = ref<UserDto | null>(null)

function openChangePassword(user: UserDto) {
  selectedUser.value = user
  passwordVisible.value = true
}

function confirmDelete(user: UserDto) {
  confirm.require({
    message: `Delete the account for ${user.userName}? This cannot be undone.`,
    header: 'Delete account',
    icon: 'pi pi-exclamation-triangle',
    rejectProps: { label: 'Cancel', severity: 'secondary', text: true },
    acceptProps: { label: 'Delete', severity: 'danger' },
    accept: () => {
      deleteUser.mutate(user.id, {
        onSuccess: () => {
          toast.add({
            severity: 'success',
            summary: 'Account deleted',
            detail: user.userName,
            life: 3000,
          })
        },
        onError: (err) => {
          toast.add({
            severity: 'error',
            summary: 'Delete failed',
            detail: serverErrorText(err),
            life: 5000,
          })
        },
      })
    },
  })
}

type TagSeverity = 'success' | 'info' | 'warn' | 'secondary' | 'danger' | 'contrast'

const roleSeverity: Record<string, TagSeverity> = {
  Admin: 'danger',
  Manager: 'info',
  Worker: 'secondary',
}
</script>

<template>
  <section class="p-6 flex flex-col gap-4" style="max-width: 1400px">
    <div class="flex items-center justify-between gap-4">
      <div>
        <div class="flex items-center gap-3">
          <h1 class="text-2xl font-semibold text-surface-900">Admin Panel</h1>
          <RefreshButton :loading="isFetching" @click="() => refetch()" />
        </div>
        <p class="text-sm text-surface-500 mt-1">Manage user accounts and access.</p>
      </div>

      <Button
        label="Create account"
        icon="pi pi-user-plus"
        @click="createVisible = true"
      />
    </div>

    <DataTableWrapper :items="data ?? []" :loading="isFetching" :paginate="false">
      <Column header="Name">
        <template #body="{ data: row }: { data: UserDto }">
          <div class="font-medium text-surface-900">
            {{ row.firstName }} {{ row.lastName }}
          </div>
          <div class="text-sm text-surface-500">{{ row.userName }}</div>
        </template>
      </Column>
      <Column field="email" header="Email" />
      <Column header="Role" style="width: 16rem">
        <template #body="{ data: row }: { data: UserDto }">
          <div class="flex flex-wrap gap-1">
            <Tag
              v-for="r in row.roles"
              :key="r"
              :value="r"
              :severity="roleSeverity[r] ?? 'secondary'"
            />
            <span v-if="!row.roles.length" class="text-surface-400 text-sm">—</span>
          </div>
        </template>
      </Column>
      <Column header="" style="width: 19rem">
        <template #body="{ data: row }: { data: UserDto }">
          <div class="flex gap-2">
            <Button
              label="Change password"
              icon="pi pi-key"
              size="small"
              severity="secondary"
              outlined
              @click="openChangePassword(row)"
            />
            <Button
              icon="pi pi-trash"
              size="small"
              severity="danger"
              outlined
              aria-label="Delete account"
              :disabled="row.id === auth.user?.id"
              :title="row.id === auth.user?.id ? 'You cannot delete your own account' : 'Delete account'"
              @click="confirmDelete(row)"
            />
          </div>
        </template>
      </Column>
    </DataTableWrapper>

    <CreateUserDialog v-model:visible="createVisible" />
    <ChangePasswordDialog v-model:visible="passwordVisible" :user="selectedUser" />
  </section>
</template>
