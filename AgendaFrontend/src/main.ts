import { createApp } from 'vue'
import { createRouter, createWebHistory } from 'vue-router'
import App from './App.vue'
import routes from "./router/routes"

const router = createRouter({
    history: createWebHistory(import.meta.env.BASE_URL),
    routes,
})

// Add authentication guard
router.beforeEach((to, from, next) => {
    const accessToken = localStorage.getItem('accessToken');
    const isAuthenticated = !!accessToken;

    if (to.meta.requiresAuth && !isAuthenticated) {
        // Redirect to login if route requires auth and user is not authenticated
        next({ name: 'Login' });
    } else if (to.name === 'Login' && isAuthenticated) {
        // Redirect to agenda if user is already logged in and tries to access login page
        next({ name: 'Agenda' });
    } else {
        next();
    }
});

createApp(App)
.use(router)
.mount('#app')
