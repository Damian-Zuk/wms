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

const demoEmail = 'demo@wms.local'
const demoPassword = '1wFWvd8zrS7!'
const copiedField = ref<'email' | 'password' | null>(null)

async function copyDemoValue(field: 'email' | 'password', value: string) {
  await navigator.clipboard.writeText(value)
  copiedField.value = field
  setTimeout(() => {
    if (copiedField.value === field) copiedField.value = null
  }, 1500)
}

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

      <div class="rounded-lg border border-blue-200 bg-blue-50 p-3 flex flex-col gap-2">
        <p class="text-md font-medium text-blue-700">Demo credentials</p>

        <div class="flex items-center justify-between gap-2">
          <code class="text-sm font-mono text-surface-800 truncate">{{ demoEmail }}</code>
          <Button
            :icon="copiedField === 'email' ? 'pi pi-check' : 'pi pi-copy'"
            text
            size="small"
            aria-label="Copy demo email"
            @click="copyDemoValue('email', demoEmail)"
          />
        </div>

        <div class="flex items-center justify-between gap-2">
          <code class="text-sm font-mono text-surface-800 truncate">{{ demoPassword }}</code>
          <Button
            :icon="copiedField === 'password' ? 'pi pi-check' : 'pi pi-copy'"
            text
            size="small"
            aria-label="Copy demo password"
            @click="copyDemoValue('password', demoPassword)"
          />
        </div>
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
