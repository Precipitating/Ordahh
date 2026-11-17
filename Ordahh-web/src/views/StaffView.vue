<script setup>
import { ref, onMounted, onUnmounted, computed } from 'vue'
import * as signalR from '@microsoft/signalr'
const API_URL = 'http://localhost:5276' 

const orders = ref([])
const loading = ref(true)
const error = ref('')
const connection = new signalR.HubConnectionBuilder()
    .withUrl(`${API_URL}/hubs/orders`)
    .withAutomaticReconnect()
    .build()

let timer = null
const showClosed = ref(false)
const now = ref(Date.now())
let clockTimer = null
const rtf = new Intl.RelativeTimeFormat('en', { numeric: 'auto', style: 'short' })

const visibleOrders = computed(() => {
    if (showClosed.value) return orders.value
    return orders.value.filter(o => (transitions[o.status] ?? []).length > 0)
})

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
    connection.onreconnecting(() => {
        console.log('Reconnecting to SignalR hub...')
        clockTimer = setInterval(() => {
            now.value = Date.now()
        }, 30000)

    })

    connection.onreconnected(() => {
        console.log('Reconnected to SignalR hub')
        fetchSite() // catch up on anything missed while disconnected
    })

    connection.onclose(() => {
        console.log('Disconnected from SignalR hub')
    })

    connection.on('OrderCreated', (order) => {
        console.log('New order received', order)
        const exists = orders.value.some(o => o.id === order.id)
        if (exists) return

        orders.value.unshift(order) // add to the top of the list
    })

    connection.on('StatusUpdated', (status) => {
        console.log('Order status updated', status)
        const index = orders.value.findIndex(o => o.id === status.id)
        if (index !== -1) orders.value[index] = status


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
async function fetchSite()
{ 
    try {
        const response = await fetch(`${API_URL}/api/orders`)
        if (!response.ok) throw new Error(`Server returned ${response.status}`)
        orders.value = await response.json()
    } catch (e) {
        error.value = 'Could not load orders.'
        console.error(e)
    } finally {
        loading.value = false
    }
}

const transitions = {
    Received: ['Preparing', 'Cancelled'],
    Preparing: ['Completed'],
    Completed: [],
    Cancelled: [],
}
const updatingId = ref(null)
const statusError = ref('')

async function changeStatus(order, newStatus) {
    if (updatingId.value !== null) return

    updatingId.value = order.id
    statusError.value = ''

    try {
        const response = await fetch(`${API_URL}/api/orders/${order.id}/status`, {
            method: 'PATCH',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ status: newStatus }),
        })

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
        <label>
            <input type="checkbox" v-model="showClosed" />
            Show completed and cancelled
        </label>

        <p v-if="loading">Loading...</p>
        <p v-else-if="error">{{ error }}</p>
        <p v-else-if="visibleOrders.length === 0">No orders to show.</p>

        <section v-for="order in visibleOrders" :key="order.id" class="order">
            <h2>
                Order #{{ order.id }}
                <span class="status">{{ order.status }}</span>
            </h2>
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
                        :disabled="updatingId !== null"
                        @click="changeStatus(order, next)">
                    {{ next }}
                </button>
            </div>
        </section>
    </main>
</template>

<style scoped>
    main {
        max-width: 700px;
        margin: 0 auto;
        padding: 1rem;
    }

    .order {
        border: 1px solid #ddd;
        border-radius: 6px;
        padding: 0.75rem 1rem;
        margin-bottom: 1rem;
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
    .actions {
        display: flex;
        gap: 0.5rem;
    }
</style>