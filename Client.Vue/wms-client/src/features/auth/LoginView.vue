<script setup lang="ts">
import { ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useForm } from 'vee-validate'
import { toTypedSchema } from '@vee-validate/zod'
import * as z from 'zod'
import InputText from 'primevue/inputtext'
import Password from 'primevue/password'
import Button from 'primevue/button'
import Message from 'primevue/message'
import { useAuthStore } from '@/stores/auth'
import type { AppError } from '@/api/problem-details'

const auth = useAuthStore()
const router = useRouter()
const route = useRoute()

const schema = toTypedSchema(
  z.object({
    email: z.string().min(1, 'Email is required').email('Enter a valid email'),
    password: z.string().min(1, 'Password is required'),
  }),
)

const { handleSubmit, defineField, errors } = useForm({ validationSchema: schema })
const [email, emailAttrs] = defineField('email')
const [password, passwordAttrs] = defineField('password')

const submitting = ref(false)
const serverError = ref<string | null>(null)

const onSubmit = handleSubmit(async (values) => {
  submitting.value = true
  serverError.value = null
  try {
    await auth.login(values)
    const redirect = (route.query.redirect as string) || '/'
    router.push(redirect)
  } catch (err) {
    serverError.value = (err as AppError).message ?? 'Login failed'
  } finally {
    submitting.value = false
  }
})
</script>

<template>
  <div class="min-h-[calc(100svh-3.5rem)] flex items-center justify-center px-4">
    <form
      class="w-full max-w-sm flex flex-col gap-5 p-8 rounded-xl border border-surface-200 bg-white shadow-sm"
      novalidate
      @submit="onSubmit"
    >
      <div class="text-center">
        <h1 class="text-2xl font-semibold text-surface-900">Sign in</h1>
        <p class="text-sm text-surface-500 mt-1">Warehouse Management System</p>
      </div>

      <Message v-if="serverError" severity="error" :closable="false">
        {{ serverError }}
      </Message>

      <div class="flex flex-col gap-1">
        <label for="email" class="text-sm font-medium text-surface-700">Email</label>
        <InputText
          id="email"
          v-model="email"
          v-bind="emailAttrs"
          type="email"
          autocomplete="username"
          fluid
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
          autocomplete="current-password"
          :invalid="!!errors.password"
        />
        <small v-if="errors.password" class="text-red-500">{{ errors.password }}</small>
      </div>

      <Button
        type="submit"
        label="Sign in"
        icon="pi pi-sign-in"
        :loading="submitting"
        fluid
      />
    </form>
  </div>
</template>
