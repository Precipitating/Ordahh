<script setup>
import { ref, onMounted, computed, watch, onUnmounted } from 'vue'
import * as signalR from '@microsoft/signalr'
import { useCartStore } from '../stores/cart'
import MenuItem from '../components/MenuItem.vue'
import { useRecentOrdersStore } from '../stores/recentOrders'
import { API_URL, readError } from '../api'
const cart = useCartStore()

const menu = ref([])
const recent = useRecentOrdersStore()
const loading = ref(true)
const error = ref('')
const submitting = ref(false)
const orderError = ref('')
const search = ref('')
const selectedCategory = ref(null)
const confirmation = ref(null)

async function loadMenu() {
    try {
        const response = await fetch(`${API_URL}/api/menu`)
        if (!response.ok) throw new Error(`Server returned ${response.status}`)
        menu.value = await response.json()
        error.value = ''
        cart.syncWithMenu(menu.value)
    } catch (e) {
        // on a background refresh, keep showing the old menu instead of an error
        if (menu.value.length === 0) error.value = 'Could not load the menu.'
        console.error(e)
    } finally {
        loading.value = false
    }
}

const connection = new signalR.HubConnectionBuilder()
    .withUrl(`${API_URL}/hubs/menu`)
    .withAutomaticReconnect()
    .build()

// several changes in a burst (a rush of orders) should cause one refetch
let refreshTimer
function scheduleRefresh() {
    clearTimeout(refreshTimer)
    refreshTimer = setTimeout(loadMenu, 500)
}

// if the selected category disappears (all its items archived), go back to "All"
watch(menu, (m) => {
    if (selectedCategory.value !== null && !m.some(c => c.id === selectedCategory.value)) {
        selectedCategory.value = null
    }
})

onMounted(async () => {
    connection.on('MenuChanged', scheduleRefresh)
    connection.onreconnected(loadMenu)   // events sent while disconnected were missed

    // connect first, then load, so a change in between can't be missed
    try {
        await connection.start()
    } catch (e) {
        console.error('Live menu unavailable', e)
    }
    await loadMenu()
})

onUnmounted(() => {
    clearTimeout(refreshTimer)
    connection.stop()
})


// Adjusting format for the POST request
const reshapeList = (list) =>
{
    console.log({ items: list.map(item => ({ menuItemId: item.id, quantity: item.quantity })) })
    return{ items: list.map(item => ({menuItemId: item.id, quantity: item.quantity})) }
}

const filteredMenu = computed(() => {
    const term = search.value.trim().toLowerCase()

    // 1. category
    const categories = selectedCategory.value === null
        ? menu.value
        : menu.value.filter(c => c.id === selectedCategory.value)

    // 2. no search text: the category result is the answer
    if (term === '') return categories

    // 3. otherwise search within those categories
    return categories
        .map(category => ({
            ...category,
            items: category.items.filter(item =>
                item.name.toLowerCase().includes(term) ||
                (item.description ?? '').toLowerCase().includes(term))
        }))
        .filter(category => category.items.length > 0)
})



async function placeOrder()
{
    if (submitting.value || cart.items.length === 0) return

    submitting.value = true
    orderError.value = ''
    confirmation.value = null

    try {
        const response = await fetch(`${API_URL}/api/orders`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(reshapeList(cart.items)),
        })

        if (!response.ok) {
            orderError.value = await readError(response, 'The order was rejected.')
            return
        }

        confirmation.value = await response.json()
        recent.add(confirmation.value)
        cart.clear()
    } catch (e) {
        orderError.value = 'Could not reach the server.'
        console.error(e)
    } finally {
        submitting.value = false
    }

}


</script>

<template>
    <div class="layout">
        <main>
            <h1>Menu</h1>

            <p v-if="loading">Loading...</p>
            <p v-else-if="error">{{ error }}</p>
            <div class="filters">
                <input v-model="search" type="search" placeholder="Search the menu" aria-label="Search the menu" />

                <label class="category-select">
                    <span class="sr-only">Category</span>
                    <select v-model="selectedCategory">
                        <option :value="null">All categories</option>
                        <option v-for="category in menu" :key="category.id" :value="category.id">
                            {{ category.name }}
                        </option>
                    </select>
                </label>
            </div>
            <section v-for="category in filteredMenu" :key="category.id">
                <h2>{{ category.name }}</h2>
                <ul>
                    <li v-for="item in category.items" :key="item.id">
                        <MenuItem :item="item" @add="cart.add" />
                    </li>
                </ul>
            </section>
        </main>

        <aside>
            <h2>Your order ({{ cart.totalItems }})</h2>

            <p v-if="cart.items.length === 0">Your cart is empty.</p>

            <ul v-else>
                <li v-for="line in cart.items" :key="line.id" class="cart-line">
                    <span>{{ line.name }}</span>
                    <span v-if="line.unavailable" class="error">No longer available. Please remove it.</span>
                    <span v-if="line.lowStock && !line.unavailable" class="low-stock">Low stock</span>

                    <div class="qty">
                        <button @click="cart.setQuantity(line.id, line.quantity - 1)">-</button>
                        <span>{{ line.quantity }}</span>
                        <button :disabled="line.quantity >= line.max" @click="cart.setQuantity(line.id, line.quantity + 1)">+</button>
                    </div>
                    <span class="price">${{ (line.price * line.quantity).toFixed(2) }}</span>
                </li>
            </ul>

            <p class="total">Total: <strong>${{ cart.total.toFixed(2) }}</strong></p>
            <button v-if="cart.items.length" @click="cart.clear()">Clear cart</button>

            <button v-if="cart.items.length" :disabled="submitting || cart.items.some(l => l.unavailable) " @click="placeOrder">
                {{ submitting ? 'Placing order...' : 'Place order' }}
            </button>

            <p v-if="orderError" class="error">{{ orderError }}</p>

            <p v-if="confirmation" class="success">
                Order placed. Total: <strong>${{ confirmation.total.toFixed(2) }}</strong>
                <br />
                <RouterLink :to="`/track/${confirmation.trackingCode}`">Track your order</RouterLink>
            </p>

            <section v-if="recent.orders.length" class="recent">
                <h3>Your recent orders</h3>
                <ul>
                    <li v-for="o in recent.orders" :key="o.code">
                        <RouterLink :to="`/track/${o.code}`">
                            ${{ Number(o.total).toFixed(2) }} · {{ new Date(o.placedAt).toLocaleString() }}
                        </RouterLink>
                    </li>
                </ul>
                <button @click="recent.clear()">Clear list</button>
            </section>
        </aside>
    </div>
</template>

<style scoped>
    .layout {
        max-width: 900px;
        margin: 0 auto;
        padding: 1rem;
        display: grid;
        grid-template-columns: 2fr 1fr;
        gap: 2rem;
    }

    ul {
        list-style: none;
        padding: 0;
    }

    li {
        padding: 0.75rem 0;
        border-bottom: 1px solid #ddd;
    }

    .cart-line {
        display: flex;
        align-items: center;
        gap: 0.75rem;
    }

        .cart-line span:first-child {
            flex: 1;
        }

    .qty {
        display: flex;
        align-items: center;
        gap: 0.4rem;
    }

    .total {
        font-size: 1.1rem;
        margin-top: 1rem;
    }

    button {
        cursor: pointer;
    }

    .error {
        color: #b00020;
    }

    .success {
        color: #008000;
    }
    .filters {
        display: flex;
        flex-wrap: wrap;
        gap: 0.75rem;
        margin: 0.75rem 0;
    }

        .filters input {
            flex: 1;
            min-width: 180px;
            padding: 0.5rem;
            font-size: 1rem;
            border: 1px solid #bbb;
            border-radius: 6px;
        }

    .category-select select {
        padding: 0.5rem;
        font-size: 1rem;
        border: 1px solid #bbb;
        border-radius: 6px;
        background: #fff;
    }

    .sr-only {
        position: absolute;
        width: 1px;
        height: 1px;
        overflow: hidden;
        clip: rect(0 0 0 0);
    }
    .low-stock {
        display: inline-block;
        margin-left: 0.4rem;
        padding: 0.1rem 0.5rem;
        border-radius: 999px;
        background: #ffe0b2;
        color: #e65100;
        font-size: 0.75rem;
        font-weight: 600;
    }
    .cart-line .error {
        display: block;
        color: #dc2626;
        font-size: 0.85rem;
        margin-top: 4px;
    }

    .recent {
        margin-top: 1.5rem;
    }

        .recent h3 {
            margin-bottom: 0.25rem;
        }

    @media (max-width: 700px) {
        .layout {
            grid-template-columns: 1fr;
        }
    }
</style>