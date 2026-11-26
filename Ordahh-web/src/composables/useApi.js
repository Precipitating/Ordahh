import { useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import { API_URL } from '../api'

export function useApi() {
    const auth = useAuthStore()
    const router = useRouter()

    async function authFetch(path, options = {}) {
        const response = await fetch(`${API_URL}${path}`, {
            ...options,
            headers: {
                ...(options.headers ?? {}),
                Authorization: `Bearer ${auth.token}`,
            },
        })

        if (response.status === 401) {
            auth.logout()
            router.push('/login')
            throw new Error('Session expired')
        }
        return response
    }

    return { authFetch }
}