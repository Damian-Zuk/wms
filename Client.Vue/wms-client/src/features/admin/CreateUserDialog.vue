<script setup lang="ts">
import { ref, watch } from 'vue'
import { useForm } from 'vee-validate'
import { toTypedSchema } from '@vee-validate/zod'
import * as z from 'zod'
import Dialog from 'primevue/dialog'
import InputText from 'primevue/inputtext'
import Password from 'primevue/password'
import Select from 'primevue/select'
import Button from 'primevue/button'
import Message from 'primevue/message'
import { useToast } from 'primevue/usetoast'
import { useCreateUser } from './useUsers'
import { serverErrorText } from './server-error'
import type { Role } from '@/types/auth'

const visible = defineModel<boolean>('visible', { default: false })

const toast = useToast()
const createUser = useCreateUser()

const roleOptions: { label: string; value: Role }[] = [
  { label: 'Admin', value: 'Admin' },
  { label: 'Manager', value: 'Manager' },
  { label: 'Worker', value: 'Worker' },
]

const schema = toTypedSchema(
  z.object({
    firstName: z.string().min(1, 'First name is required'),
    lastName: z.string().min(1, 'Last name is required'),
    userName: z.string().min(1, 'Username is required'),
    email: z.string().min(1, 'Email is required').email('Enter a valid email'),
    password: z.string().min(6, 'Password must be at least 6 characters'),
    role: z.enum(['Admin', 'Manager', 'Worker']),
  }),
)

const { handleSubmit, defineField, errors, resetForm } = useForm({
  validationSchema: schema,
  initialValues: { role: 'Worker' as Role },
})

const [firstName, firstNameAttrs] = defineField('firstName')
const [lastName, lastNameAttrs] = defineField('lastName')
const [userName, userNameAttrs] = defineField('userName')
const [email, emailAttrs] = defineField('email')
const [password, passwordAttrs] = defineField('password')
const [role] = defineField('role')

const serverErrorMessage = ref<string | null>(null)

// Reset the form each time the dialog opens.
watch(visible, (open) => {
  if (open) {
    resetForm({ values: { role: 'Worker' as Role } })
    serverErrorMessage.value = null
  }
})

const onSubmit = handleSubmit((values) => {
  serverErrorMessage.value = null
  createUser.mutate(values, {
    onSuccess: (user) => {
      toast.add({
        severity: 'success',
        summary: 'Account created',
        detail: `${user.userName} (${user.roles.join(', ')})`,
        life: 3000,
      })
      visible.value = false
    },
    onError: (err) => {
      serverErrorMessage.value = serverErrorText(err)
    },
  })
})
</script>

<template>
  <Dialog
    v-model:visible="visible"
    modal
    header="Create account"
    :style="{ width: '30rem' }"
  >
    <form class="flex flex-col gap-4" novalidate @submit="onSubmit">
      <Message v-if="serverErrorMessage" severity="error" :closable="false">
        {{ serverErrorMessage }}
      </Message>

      <div class="grid grid-cols-2 gap-3">
        <div class="flex flex-col gap-1">
          <label for="firstName" class="text-sm font-medium text-surface-700">
            First name
          </label>
          <InputText
            id="firstName"
            v-model="firstName"
            v-bind="firstNameAttrs"
            fluid
            :invalid="!!errors.firstName"
          />
          <small v-if="errors.firstName" class="text-red-500">{{ errors.firstName }}</small>
        </div>

        <div class="flex flex-col gap-1">
          <label for="lastName" class="text-sm font-medium text-surface-700">
            Last name
          </label>
          <InputText
            id="lastName"
            v-model="lastName"
            v-bind="lastNameAttrs"
            fluid
            :invalid="!!errors.lastName"
          />
          <small v-if="errors.lastName" class="text-red-500">{{ errors.lastName }}</small>
        </div>
      </div>

      <div class="flex flex-col gap-1">
        <label for="userName" class="text-sm font-medium text-surface-700">Username</label>
        <InputText
          id="userName"
          v-model="userName"
          v-bind="userNameAttrs"
          fluid
          autocomplete="off"
          :invalid="!!errors.userName"
        />
        <small v-if="errors.userName" class="text-red-500">{{ errors.userName }}</small>
      </div>

      <div class="flex flex-col gap-1">
        <label for="email" class="text-sm font-medium text-surface-700">Email</label>
        <InputText
          id="email"
          v-model="email"
          v-bind="emailAttrs"
          type="email"
          fluid
          autocomplete="off"
          :invalid="!!errors.email"
        />
        <small v-if="errors.email" class="text-red-500">{{ errors.email }}</small>
      </div>

      <div class="flex flex-col gap-1">
        <label for="password" class="text-sm font-medium text-surface-700">Password</label>
        <Password
          input-id="password"
          v-model="password"
          v-bind="passwordAttrs"
          :feedback="false"
          toggle-mask
          fluid
          autocomplete="new-password"
          :invalid="!!errors.password"
        />
        <small v-if="errors.password" class="text-red-500">{{ errors.password }}</small>
      </div>

      <div class="flex flex-col gap-1">
        <label for="role" class="text-sm font-medium text-surface-700">Role</label>
        <Select
          input-id="role"
          v-model="role"
          :options="roleOptions"
          option-label="label"
          option-value="value"
          fluid
        />
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
          label="Create account"
          icon="pi pi-user-plus"
          :loading="createUser.isPending.value"
        />
      </div>
    </form>
  </Dialog>
</template>
