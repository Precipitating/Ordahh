import { createRouter, createWebHistory, useRouter } from 'vue-router'
import MenuView from '../views/MenuView.vue'
import StaffView from '../views/StaffView.vue'
import LoginView from '../views/LoginView.vue'
import StaffMenuView from '../views/StaffMenuView.vue'
import TrackView from '../views/TrackView.vue'
import { useAuthStore } from '../stores/auth';

const routes = [
    { path: '/', component: MenuView },
    { path: '/staff', component: StaffView, meta: { requiresAuth: true } },
    { path: '/login', component: LoginView },
    { path: '/track/:code', component: TrackView },
    { path: '/staff/menu', component: StaffMenuView, meta: { requiresAuth: true } },
]


const router = createRouter({
    history: createWebHistory(),
    routes,
})

router.beforeEach((to, from, next) => {
    const auth = useAuthStore()
    if (to.meta.requiresAuth && !auth.isLoggedIn) {
        next('/login')
    }
    else if (to.path === '/login' && auth.isLoggedIn) {
        next('/staff')
    }
    else {
        next()
    }
})



export default router