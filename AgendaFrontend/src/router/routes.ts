const routes = [
    {
        path: '/login',
        name: 'Login',
        component: () => import('../views/LoginView.vue'),
        meta: { requiresAuth: false }
    },
    {
        path: '/',
        redirect: '/agenda'
    },
    {
        path: '/agenda',
        name: 'Agenda',
        component: () => import('../views/index.vue'),
        meta: { requiresAuth: true }
    },
    {
        path: '/profile',
        name: 'Profile',
        component: () => import('../views/ProfileView.vue'),
        meta: { requiresAuth: true }
    },
    {
        path: '/localization',
        name: 'Localization',
        component: () => import('../views/LocalizationView.vue'),
        meta: { requiresAuth: true }
    },
    {
        path: '/calendar-settings',
        name: 'CalendarSettings',
        component: () => import('../views/CalendarSettingsView.vue'),
        meta: { requiresAuth: true }
    }
]

export default routes