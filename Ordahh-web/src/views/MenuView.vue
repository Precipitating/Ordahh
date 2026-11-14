<script setup>
import { ref, onMounted } from 'vue'
import { useCartStore } from '../stores/cart'
import MenuItem from '../components/MenuItem.vue'

const API_URL = 'http://localhost:5276' // keep your working backend port
const cart = useCartStore()

const menu = ref([])
const loading = ref(true)
const error = ref('')
const submitting = ref(false)
const orderError = ref('')
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

            <section v-for="category in menu" :key="category.id">
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
                Order #{{ confirmation.id }} placed. Total:
                <strong>${{ confirmation.total.toFixed(2) }}</strong>
            </p>
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

    @media (max-width: 700px) {
        .layout {
            grid-template-columns: 1fr;
        }
    }
</style>