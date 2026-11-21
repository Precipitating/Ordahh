<script setup>
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'

const auth = useAuthStore()
const router = useRouter()

const username = ref('')
const password = ref('')
const submitting = ref(false)
const error = ref('')

async function submit() {
          if (submitting.value) return

          submitting.value = true
          error.value = ''

          try {
            await auth.login(username.value, password.value)
            router.push('/staff')
          } catch (e) {
            // network failures arrive as a TypeError with a browser-specific message
            error.value = e instanceof TypeError
              ? 'Could not reach the server.'
              : e.message
            password.value = ''
          } finally {
            submitting.value = false
          }
}
</script>

<template>
    <main>
        <h1>Staff login</h1>

        <form @submit.prevent="submit">
            <label>
                Username
                <input v-model="username" type="text" autocomplete="username" required />
            </label>

            <label>
                Password
                <input v-model="password" type="password" autocomplete="current-password" required />
            </label>

            <p v-if="error" class="error">{{ error }}</p>

            <button type="submit" :disabled="submitting">
                {{ submitting ? 'Logging in...' : 'Log in' }}
            </button>
        </form>
    </main>
</template>

<style scoped>
    main {
        max-width: 360px;
        margin: 3rem auto;
        padding: 1rem;
    }

    form {
        display: flex;
        flex-direction: column;
        gap: 1rem;
    }

    label {
        display: flex;
        flex-direction: column;
        gap: 0.25rem;
        font-weight: 600;
    }

    input {
        padding: 0.6rem;
        font-size: 1rem;
        border: 1px solid #bbb;
        border-radius: 6px;
    }

    button {
        min-height: 48px;
        font-size: 1rem;
        font-weight: 600;
        color: #fff;
        background: #1565c0;
        border: none;
        border-radius: 8px;
        cursor: pointer;
    }

        button:disabled {
            opacity: 0.5;
            cursor: not-allowed;
        }

    .error {
        color: #b00020;
        margin: 0;
    }
</style>