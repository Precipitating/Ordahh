import { createRouter, createWebHistory } from 'vue-router'
import MenuView from '../views/MenuView.vue'
import StaffView from '../views/StaffView.vue'

const routes = [
    { path: '/', component: MenuView },
    { path: '/staff', component: StaffView },
]


const router = createRouter({
    history: createWebHistory(),
    routes,
})

export default router