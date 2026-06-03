<script setup lang="ts">
import { watch } from 'vue'
import { useForm } from 'vee-validate'
import { toTypedSchema } from '@vee-validate/zod'
import * as z from 'zod'
import Dialog from 'primevue/dialog'
import Password from 'primevue/password'
import Button from 'primevue/button'
import Message from 'primevue/message'
import { useToast } from 'primevue/usetoast'
import { useChangePassword } from './useUsers'
import { serverErrorText } from './server-error'
import type { UserDto } from '@/types/auth'

const visible = defineModel<boolean>('visible', { default: false })
const props = defineProps<{ user: UserDto | null }>()

const toast = useToast()
const changePassword = useChangePassword()

const schema = toTypedSchema(
  z
    .object({
      newPassword: z.string().min(6, 'Password must be at least 6 characters'),
      confirmPassword: z.string().min(1, 'Confirm the password'),
    })
    .refine((v) => v.newPassword === v.confirmPassword, {
      message: 'Passwords do not match',
      path: ['confirmPassword'],
    }),
)

const { handleSubmit, defineField, errors, resetForm, setErrors } = useForm({
  validationSchema: schema,
})
const [newPassword, newPasswordAttrs] = defineField('newPassword')
const [confirmPassword, confirmPasswordAttrs] = defineField('confirmPassword')

// Reset the form each time the dialog opens.
watch(visible, (open) => {
  if (open) resetForm()
})

const onSubmit = handleSubmit((values) => {
  if (!props.user) return

  changePassword.mutate(
    { id: props.user.id, body: { newPassword: values.newPassword } },
    {
      onSuccess: () => {
        toast.add({
          severity: 'success',
          summary: 'Password changed',
          detail: `Updated password for ${props.user?.userName}.`,
          life: 3000,
        })
        visible.value = false
      },
      onError: (err) => {
        setErrors({ newPassword: serverErrorText(err) })
      },
    },
  )
})
</script>

<template>
  <Dialog
    v-model:visible="visible"
    modal
    header="Change password"
    :style="{ width: '26rem' }"
  >
    <form v-if="user" class="flex flex-col gap-4" novalidate @submit="onSubmit">
      <div class="text-sm text-surface-600 rounded-lg bg-surface-50 p-3">
        <div class="font-medium text-surface-900">
          {{ user.firstName }} {{ user.lastName }}
        </div>
        <div>{{ user.userName }} · {{ user.email }}</div>
      </div>

      <Message v-if="errors.newPassword" severity="error" :closable="false">
        {{ errors.newPassword }}
      </Message>

      <div class="flex flex-col gap-1">
        <label for="newPassword" class="text-sm font-medium text-surface-700">
          New password
        </label>
        <Password
          input-id="newPassword"
          v-model="newPassword"
          v-bind="newPasswordAttrs"
          :feedback="false"
          toggle-mask
          fluid
          autocomplete="new-password"
          :invalid="!!errors.newPassword"
        />
        <small v-if="errors.newPassword" class="text-red-500">{{ errors.newPassword }}</small>
      </div>

      <div class="flex flex-col gap-1">
        <label for="confirmPassword" class="text-sm font-medium text-surface-700">
          Confirm password
        </label>
        <Password
          input-id="confirmPassword"
          v-model="confirmPassword"
          v-bind="confirmPasswordAttrs"
          :feedback="false"
          toggle-mask
          fluid
          autocomplete="new-password"
          :invalid="!!errors.confirmPassword"
        />
        <small v-if="errors.confirmPassword" class="text-red-500">
          {{ errors.confirmPassword }}
        </small>
      </div>

      <div class="flex gap-2 justify-end pt-1">
        <Button
          type="button"
          label="Cancel"
          severity="secondary"
          text
          @click="visible = false"
        />
        <Button
          type="submit"
          label="Change password"
          icon="pi pi-key"
          :loading="changePassword.isPending.value"
        />
      </div>
    </form>
  </Dialog>
</template>
