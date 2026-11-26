export const API_URL = 'http://localhost:5276'

export async function readError(response, fallback = 'Something went wrong.') {
    const contentType = response.headers.get('content-type') ?? ''
    if (contentType.includes('json')) {
        const problem = await response.json()
        const messages = Object.values(problem.errors ?? {}).flat().join(' ')
        return messages || problem.title || fallback
    }
    return (await response.text()) || fallback
}

export class SessionExpiredError extends Error {
    constructor() {
        super('Session expired')
        this.name = 'SessionExpiredError'
    }
}