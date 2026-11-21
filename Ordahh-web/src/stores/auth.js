import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { API_URL } from '../api'

const TOKEN_KEY = 'ordahh.token'
const EXPIRY_KEY = 'ordahh.expiry'

export const useAuthStore = defineStore('auth', () => {
    // start from whatever was saved last time (null if nothing)
    const token = ref(localStorage.getItem(TOKEN_KEY))
    const expiry = ref(localStorage.getItem(EXPIRY_KEY))

    // a token only counts if it exists AND hasn't expired
    const isLoggedIn = computed(() =>
        !!token.value &&
        !!expiry.value &&
        new Date(expiry.value).getTime() > Date.now()
    )

    async function login(username, password) {
        const response = await fetch(`${API_URL}/api/auth/login`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ username, password }),
        })

        if (response.status === 401) throw new Error('Invalid username or password.')
        if (response.status === 429) throw new Error('Too many attempts. Try again in a minute.')
        if (!response.ok) throw new Error('Could not log in. Please try again.')

        const data = await response.json()
        token.value = data.token
        expiry.value = data.expiryTime
        localStorage.setItem(TOKEN_KEY, data.token)
        localStorage.setItem(EXPIRY_KEY, data.expiryTime)
    }

    function logout() {
        token.value = null
        expiry.value = null
        localStorage.removeItem(TOKEN_KEY)
        localStorage.removeItem(EXPIRY_KEY)
    }

    return { token, isLoggedIn, login, logout }
})