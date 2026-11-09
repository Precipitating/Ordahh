<script setup>
import { ref, onMounted } from 'vue'

const API_URL = 'http://localhost:5276' // change to YOUR API port
const menu = ref([])
const loading = ref(true)
const error = ref('')

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
</script>

<template>
    <main>
        <h1>Menu</h1>

        <p v-if="loading">Loading...</p>
        <p v-else-if="error">{{ error }}</p>

        <section v-for="category in menu" :key="category.id">
            <h2>{{ category.name }}</h2>
            <ul>
                <li v-for="item in category.items" :key="item.id">
                    <strong>{{ item.name }}</strong>
                    <span class="price">${{ item.price.toFixed(2) }}</span>
                    <p v-if="item.description">{{ item.description }}</p>
                </li>
            </ul>
        </section>
    </main>
</template>

<style scoped>
    main {
        max-width: 600px;
        margin: 0 auto;
        padding: 1rem;
    }

    ul {
        list-style: none;
        padding: 0;
    }

    li {
        padding: 0.75rem 0;
        border-bottom: 1px solid #ddd;
    }

    .price {
        float: right;
    }

    p {
        margin: 0.25rem 0 0;
        color: #666;
    }
</style>