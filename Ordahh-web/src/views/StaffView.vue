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
</script>

<template>
    <main>
        <h1>Orders</h1>

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
</style>