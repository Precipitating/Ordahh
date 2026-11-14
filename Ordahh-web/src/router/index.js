import { createRouter, createWebHistory } from 'vue-router'
import MenuView from '../views/MenuView.vue'

const routes = [
    { path: '/', component: MenuView },
]


const router = createRouter({
    history: createWebHistory(),
    routes,
})

export default router