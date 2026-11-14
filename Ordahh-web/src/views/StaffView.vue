<script setup>
import { ref, onMounted } from 'vue'

const API_URL = 'http://localhost:5276' 

const orders = ref([])
const loading = ref(true)
const error = ref('')

onMounted(async () => {
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
})

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
        <h1>Orders</h1>
        <p v-if="statusError" class="error">{{ statusError }}</p>
        <p v-if="loading">Loading...</p>
        <p v-else-if="error">{{ error }}</p>
        <p v-else-if="orders.length === 0">No orders yet.</p>

        <section v-for="order in orders" :key="order.id" class="order">
            <h2>
                Order #{{ order.id }}
                <span class="status">{{ order.status }}</span>
            </h2>
            <p class="time">{{ new Date(order.createdAt).toLocaleString() }}</p>

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