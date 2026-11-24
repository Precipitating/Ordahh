<script setup>
import { ref, onMounted, computed } from 'vue'
import { useCartStore } from '../stores/cart'
import MenuItem from '../components/MenuItem.vue'
import { useRecentOrdersStore } from '../stores/recentOrders'
import { API_URL } from '../api'
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

onMounted(async () => {
      try {
        const response = await fetch(`${API_URL}/api/menu`)
        if (!response.ok) throw new Error(`Server returned ${response.status}`)
        menu.value = await response.json()
      } catch (e) {
        error.value = 'Could not load the menu.'
        console.error(e)
      } finally {
        loading.value = false
      }
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

        if (!response.ok)
        {
            const contentType = response.headers.get('content-type') ?? ''
            if (contentType.includes('json'))
            {
                // validation failures from [ApiController] arrive as JSON
                const problem = await response.json()
                orderError.value = Object.values(problem.errors).flat().join(' ') || problem.title
            } else
            {
                // your own BadRequest("...") messages arrive as plain text
                orderError.value = await response.text()
            }
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
                    <div class="qty">
                        <button @click="cart.setQuantity(line.id, line.quantity - 1)">-</button>
                        <span>{{ line.quantity }}</span>
                        <button :disabled="line.quantity >= cart.MAX_PER_ITEM" @click="cart.setQuantity(line.id, line.quantity + 1)">+</button>
                    </div>
                    <span class="price">${{ (line.price * line.quantity).toFixed(2) }}</span>
                </li>
            </ul>

            <p class="total">Total: <strong>${{ cart.total.toFixed(2) }}</strong></p>
            <button v-if="cart.items.length" @click="cart.clear()">Clear cart</button>
            <button v-if="cart.items.length" :disabled="submitting" @click="placeOrder">
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