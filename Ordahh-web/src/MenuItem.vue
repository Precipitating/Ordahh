<script setup>
const props = defineProps({
      item: { type: Object, required: true }
})

const emit = defineEmits(['add'])
</script>



<template>
    <div class="item">
        <div class="info">
            <strong>{{ item.name }}</strong>
            <p v-if="item.description">{{ item.description }}</p>
        </div>
        <span class="price">${{ item.price.toFixed(2) }}</span>
        <button :disabled="!item.isAvailable" @click="emit('add', item)">
            {{ item.isAvailable ? 'Add' : 'Sold out' }}
        </button>
    </div>
</template>

<style scoped>
    .item {
        display: flex;
        align-items: center;
        gap: 0.75rem;
    }

    .info {
        flex: 1;
    }

        .info p {
            margin: 0.25rem 0 0;
            color: #666;
        }

    button {
        cursor: pointer;
    }

        button:disabled {
            cursor: not-allowed;
            opacity: 0.5;
        }
</style>