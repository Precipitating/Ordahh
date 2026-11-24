import { defineStore } from 'pinia'
import { ref } from 'vue'

const KEY = 'ordahh.recentOrders'
const MAX = 5

function load() {
    try {
        const parsed = JSON.parse(localStorage.getItem(KEY) ?? '[]')
        return Array.isArray(parsed) ? parsed : []
    } catch {
        return [] // corrupt or edited value: start clean
    }
}

export const useRecentOrdersStore = defineStore('recentOrders', () => {
    const orders = ref(load())

    function add(order) {
        orders.value = [
            { code: order.trackingCode, total: order.total, placedAt: order.createdAt },
            ...orders.value.filter(o => o.code !== order.trackingCode),
        ].slice(0, MAX)
        localStorage.setItem(KEY, JSON.stringify(orders.value))
    }

    function clear() {
        orders.value = []
        localStorage.removeItem(KEY)
    }

    return { orders, add, clear }
})