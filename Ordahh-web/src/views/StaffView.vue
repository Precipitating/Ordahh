<script setup>
import { ref, onMounted, onUnmounted, computed } from 'vue'
import * as signalR from '@microsoft/signalr'
import { API_URL } from '../api'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'
const orders = ref([])
const loading = ref(true)
const error = ref('')
const auth = useAuthStore()
const router = useRouter()
const connection = new signalR.HubConnectionBuilder()
        .withUrl(`${API_URL}/hubs/orders`, {
            accessTokenFactory: () => auth.token
        })
        .withAutomaticReconnect()
        .build()

const showClosed = ref(false)
const now = ref(Date.now())
let clockTimer
const rtf = new Intl.RelativeTimeFormat('en', { numeric: 'auto', style: 'short' })

const transitions = {
        Received: ['Preparing', 'Cancelled'],
        Preparing: ['Completed'],
        Completed: [],
        Cancelled: [],
}
const updatingId = ref(null)
const statusError = ref('')

const actionLabels = {
        Preparing: 'Start preparing',
        Completed: 'Complete',
        Cancelled: 'Cancel',
}

const columns = [
        { status: 'Received', title: 'New', closed: false },
        { status: 'Preparing', title: 'In progress', closed: false },
        { status: 'Completed', title: 'Completed', closed: true },
        { status: 'Cancelled', title: 'Cancelled', closed: true },
]


// ------------------------------------------------------------------------------------------- //

function logout() {
    auth.logout()
    router.push('/login')
}


// the closed columns only appear when the checkbox is ticked
const visibleColumns = computed(() =>
        columns.filter(c => showClosed.value || !c.closed)
)

// one bucket of orders per status
const ordersByStatus = computed(() => {
        const groups = {}
        for (const c of columns) groups[c.status] = []
        for (const o of orders.value) {
            if (groups[o.status]) groups[o.status].push(o)
        }
        // active columns: oldest first (longest wait on top); closed: newest first
        for (const c of columns) {
            if (!c.closed) groups[c.status].reverse()
        }
        return groups
})

// convert an ISO timestamp into a human-friendly relative time string
function timeAgo(iso) {
        const then = new Date(iso)
        const minutes = Math.floor((now.value - then.getTime()) / 60000)

        if (minutes < 1) return 'just now'
        if (minutes < 60) return rtf.format(-minutes, 'minute')

        const hours = Math.floor(minutes / 60)
        if (hours < 24) return rtf.format(-hours, 'hour')

        const days = Math.floor(hours / 24)
        if (days < 7) return rtf.format(-days, 'day')

        const options = { month: 'short', day: 'numeric' }
        if (then.getFullYear() !== new Date(now.value).getFullYear()) {
            options.year = 'numeric'
        }
        return then.toLocaleDateString('en', options)
}

onMounted(() => {
        // keep the "now" value current so relative times stay fresh
        clockTimer = setInterval(() => {
            now.value = Date.now()
        }, 30000)

        connection.onreconnecting(() => {
            console.log('Reconnecting to SignalR hub...')
        })

        connection.onreconnected(() => {
            console.log('Reconnected to SignalR hub')
            fetchSite() // catch up on anything missed while disconnected
        })

        connection.onclose(() => {
            console.log('Disconnected from SignalR hub')
        })

        connection.on('OrderCreated', (order) => {
            const exists = orders.value.some(o => o.id === order.id)
            if (exists) return

            orders.value.unshift(order) // newest first
        })

        // replace an order in the list when its status changes
        // NOTE: this string must match the event name your controller sends
        connection.on('StatusUpdated', (updated) => {
            const index = orders.value.findIndex(o => o.id === updated.id)
            if (index !== -1) orders.value[index] = updated
        })

        fetchSite() // initial load, independent of the connection

        connection.start()
            .then(() => console.log('Connected to SignalR hub'))
            .catch(err => console.error('Error connecting to SignalR hub:', err))
})

onUnmounted(() => {
        clearInterval(clockTimer)
        connection.stop()
})

// fetch the list of orders from the server
async function fetchSite() {
        try {
            const response = await fetch(`${API_URL}/api/orders`, {
                headers: { 'Authorization': `Bearer ${auth.token}` }
            })
            if (response.status === 401) {
                logout()
            }
            if (!response.ok) throw new Error(`Server returned ${response.status}`)
            orders.value = await response.json()
            error.value = ''
        } catch (e) {
            error.value = 'Could not load orders.'
            console.error(e)
        } finally {
            loading.value = false
        }
}

async function changeStatus(order, newStatus) {
        if (updatingId.value !== null) return
        if (newStatus === 'Cancelled' && !confirm(`Cancel order #${order.id}?`)) return

        updatingId.value = order.id
        statusError.value = ''

        try {
            const response = await fetch(`${API_URL}/api/orders/${order.id}/status`, {
                method: 'PATCH',
                headers: { 'Content-Type': 'application/json', 'Authorization': `Bearer ${auth.token}` },
                body: JSON.stringify({ status: newStatus }),
            })

            if (response.status === 401) {
                logout()
                return
            }   

            if (!response.ok) {
                // a 409 arrives as plain text; a 404 has an empty body
                statusError.value = (await response.text()) || 'Could not update the order.'
                return
            }

            const updated = await response.json()
            const index = orders.value.findIndex(o => o.id === updated.id)
            if (index !== -1) orders.value[index] = updated
        } catch (e) {
            statusError.value = 'Could not reach the server.'
            console.error(e)
        } finally {
            updatingId.value = null
        }
}
</script>

<template>
    <main>
        <div class="toolbar">
            <label>
                <input type="checkbox" v-model="showClosed" />
                Show completed and cancelled
            </label>

            <button v-if="auth.isLoggedIn" class="logout" @click="logout">Log out</button>
        </div>

        <p v-if="statusError" class="error">{{ statusError }}</p>

        <p v-if="loading">Loading...</p>
        <p v-else-if="error">{{ error }}</p>
        <p v-else-if="orders.length === 0">No orders yet.</p>

        <div v-if="!loading" class="board">
            <section v-for="col in visibleColumns" :key="col.status" class="column">
                <h2>
                    {{ col.title }}
                    <span class="count">{{ ordersByStatus[col.status].length }}</span>
                </h2>

                <p v-if="ordersByStatus[col.status].length === 0" class="empty">Nothing here</p>

                <section v-for="order in ordersByStatus[col.status]"
                         :key="order.id"
                         class="order"
                         :class="`status-${order.status}`">
                    <h3>
                        Order #{{ order.id }}
                        <span class="status">{{ order.status }}</span>
                    </h3>
                    <p class="time">{{ timeAgo(order.createdAt) }}</p>

                    <ul>
                        <li v-for="line in order.items" :key="line.menuItemId">
                            {{ line.quantity }}x {{ line.name }}
                        </li>
                    </ul>

                    <p><strong>Total: ${{ order.total.toFixed(2) }}</strong></p>

                    <div class="actions">
                        <button v-for="next in transitions[order.status] ?? []"
                                :key="next"
                                :class="`btn-${next}`"
                                :disabled="updatingId !== null"
                                @click="changeStatus(order, next)">
                            {{ actionLabels[next] ?? next }}
                        </button>
                    </div>
                </section>
            </section>
        </div>
    </main>
</template>

<style scoped>
    main {
        max-width: 1400px;
        margin: 0 auto;
        padding: 1rem;
    }

    .board {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
        gap: 1rem;
        align-items: start;
        margin-top: 1rem;
    }

    .column {
        background: #f5f5f5;
        border-radius: 8px;
        padding: 0.75rem;
    }

        .column h2 {
            margin: 0 0 0.75rem;
            font-size: 1.1rem;
        }

    .count {
        margin-left: 0.4rem;
        padding: 0 0.5rem;
        border-radius: 999px;
        background: #ddd;
        font-size: 0.85rem;
    }

    .toolbar {
        display: flex;
        align-items: center;
        justify-content: space-between;
    }

    .empty {
        color: #888;
        text-align: center;
        margin: 1rem 0;
    }

    .order {
        background: #fff;
        border: 1px solid #ddd;
        border-left: 6px solid #9e9e9e;
        border-radius: 6px;
        padding: 0.75rem 1rem;
        margin-bottom: 1rem;
    }

        .order h3 {
            margin: 0;
        }

    .status {
        font-size: 0.9rem;
        margin-left: 0.5rem;
        color: #666;
    }

    .time {
        color: #666;
        margin: 0.25rem 0;
    }

    ul {
        padding-left: 1.2rem;
    }

    .error {
        color: #b00020;
    }

    /* status colours on the card */
    .status-Received {
        border-left-color: #e65100; /* orange: needs attention */
    }

    .status-Preparing {
        border-left-color: #1565c0; /* blue: in progress */
    }

    .status-Completed {
        border-left-color: #2e7d32; /* green: done */
        opacity: 0.75;
    }

    .status-Cancelled {
        border-left-color: #9e9e9e; /* grey: closed */
        opacity: 0.6;
    }

    /* status colours on the label */
    .status-Received .status {
        color: #e65100;
    }

    .status-Preparing .status {
        color: #1565c0;
    }

    .status-Completed .status {
        color: #2e7d32;
    }

    .status-Cancelled .status {
        color: #757575;
    }

    /* action buttons */
    .actions {
        display: flex;
        flex-wrap: wrap;
        gap: 0.75rem;
        margin-top: 0.75rem;
    }

        .actions button {
            min-height: 48px;
            min-width: 120px;
            padding: 0.5rem 1.25rem;
            font-size: 1rem;
            font-weight: 600;
            border: 2px solid transparent;
            border-radius: 8px;
            color: #fff;
            cursor: pointer;
            transition: filter 0.1s, transform 0.05s;
        }

        .actions .btn-Preparing {
            background: #1565c0;
        }

        .actions .btn-Completed {
            background: #2e7d32;
        }

        /* destructive action: outlined, so it's harder to hit by habit */
        .actions .btn-Cancelled {
            background: transparent;
            color: #b00020;
            border-color: #b00020;
        }

        .actions button:hover {
            filter: brightness(1.1);
        }

        .actions button:active {
            transform: scale(0.97);
        }

        .actions button:disabled {
            opacity: 0.5;
            cursor: not-allowed;
        }
</style>