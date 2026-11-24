<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { API_URL } from '../api'

const route = useRoute()

const order = ref(null)
const loading = ref(true)
const error = ref('')

// customer-friendly wording, kept in one place
const statusLabels = {
          Received: "We've got your order",
          Preparing: 'Being prepared',
          Completed: 'Ready!',
          Cancelled: 'This order was cancelled',
}

// the normal path an order follows (Cancelled is a side exit, not a step)
const steps = [
          { status: 'Received', label: 'Received' },
          { status: 'Preparing', label: 'Preparing' },
          { status: 'Completed', label: 'Ready' },
]

const currentStep = computed(() =>
          steps.findIndex(s => s.status === order.value?.status)
)

onMounted(async () => {
          try {
            const response = await fetch(`${API_URL}/api/orders/track/${route.params.code}`)

            if (response.status === 404) {
              error.value = "We couldn't find that order. Check the link and try again."
              return
            }
            if (!response.ok) throw new Error(`Server returned ${response.status}`)

            order.value = await response.json()
          } catch (e) {
            error.value = 'Could not reach the server. Please try again.'
            console.error(e)
          } finally {
            loading.value = false
          }
})
</script>

<template>
    <main>
        <h1>Your order</h1>

        <p v-if="loading">Loading...</p>
        <p v-else-if="error" class="error">{{ error }}</p>

        <section v-else-if="order" class="order" :class="`status-${order.status}`">
            <p class="status-text">{{ statusLabels[order.status] ?? order.status }}</p>

            <ol v-if="order.status !== 'Cancelled'" class="steps">
                <li v-for="(step, index) in steps"
                    :key="step.status"
                    :class="{ done: index <= currentStep, current: index === currentStep }">
                    {{ step.label }}
                </li>
            </ol>

            <p class="placed">Placed {{ new Date(order.createdAt).toLocaleString() }}</p>

            <ul class="lines">
                <li v-for="line in order.items" :key="line.menuItemId">
                    {{ line.quantity }}x {{ line.name }}
                </li>
            </ul>

            <p class="total">Total: <strong>${{ order.total.toFixed(2) }}</strong></p>
        </section>
    </main>
</template>

<style scoped>
    main {
        max-width: 480px;
        margin: 0 auto;
        padding: 1rem;
    }

    .error {
        color: #b00020;
    }

    .order {
        border: 1px solid #ddd;
        border-left: 6px solid #9e9e9e;
        border-radius: 8px;
        padding: 1rem 1.25rem;
    }

    .status-Received {
        border-left-color: #e65100;
    }

    .status-Preparing {
        border-left-color: #1565c0;
    }

    .status-Completed {
        border-left-color: #2e7d32;
    }

    .status-Cancelled {
        border-left-color: #9e9e9e;
    }

    .status-text {
        font-size: 1.4rem;
        font-weight: 700;
        margin: 0 0 1rem;
    }

    .steps {
        display: flex;
        gap: 0.5rem;
        list-style: none;
        padding: 0;
        margin: 0 0 1rem;
    }

        .steps li {
            flex: 1;
            padding: 0.5rem 0.25rem;
            text-align: center;
            font-size: 0.9rem;
            border-bottom: 4px solid #ddd;
            color: #888;
        }

            .steps li.done {
                border-bottom-color: #1565c0;
                color: #222;
            }

            .steps li.current {
                font-weight: 700;
            }

    .placed {
        color: #666;
        margin: 0 0 0.75rem;
    }

    .lines {
        padding-left: 1.2rem;
        margin: 0 0 0.75rem;
    }

    .total {
        font-size: 1.1rem;
        margin: 0;
    }
</style>