import { createRouter, createWebHistory, useRouter } from 'vue-router'
import MenuView from '../views/MenuView.vue'
import StaffView from '../views/StaffView.vue'
import LoginView from '../views/LoginView.vue'
const routes = [
    { path: '/', component: MenuView },
    { path: '/staff', component: StaffView },
    { path: '/login', component: LoginView },
]


const router = createRouter({
    history: createWebHistory(),
    routes,
})

export default router