import { defineStore } from 'pinia'
import { ref, computed } from 'vue'

const MAX_PER_ITEM = 10

export const useCartStore = defineStore('cart', () => {
    const items = ref([])

    const totalItems = computed(() =>
        items.value.reduce((sum, i) => sum + i.quantity, 0)
    )

    const total = computed(() =>
        items.value.reduce((sum, i) => sum + i.price * i.quantity, 0)
    )

    function add(menuItem) {
        const max = menuItem.maxPerOrder ?? MAX_PER_ITEM
        const existing = items.value.find(i => i.id === menuItem.id)
        if (existing) {
            if (existing.quantity >= existing.max) return
            existing.quantity++
        } else {
            items.value.push({
                id: menuItem.id,
                name: menuItem.name,
                price: menuItem.price,
                quantity: 1,
                max,
                unavailable : false
            })
        }
    }
    // Sync cart items with the menu to update availability and max quantity
    function syncWithMenu(menu) {
        const byId = new Map(menu.flatMap(c => c.items).map(i => [i.id, i]))

        for (const line of items.value) {
            const item = byId.get(line.id)

            if (!item || !item.isAvailable) {
                line.unavailable = true
                continue
            }

            line.unavailable = false
            line.lowStock = item.lowStock
            line.max = item.maxPerOrder ?? MAX_PER_ITEM
            if (line.quantity > line.max) line.quantity = line.max
        }
    }

    function setQuantity(id, quantity) {
        if (quantity <= 0) {
            items.value = items.value.filter(i => i.id !== id)
            return
        }
        const item = items.value.find(i => i.id === id)
        if (!item || quantity > item.max) return
        item.quantity = quantity
    }

    function clear() {
        items.value = []
    }

    return { items, totalItems, total, add, setQuantity, clear, MAX_PER_ITEM, syncWithMenu }
})