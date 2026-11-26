<template>
    <nav class="staff-nav">
        <RouterLink to="/staff">Orders</RouterLink>
        <RouterLink to="/staff/menu">Menu</RouterLink>
    </nav>
</template>

<style scoped>
    .staff-nav {
        display: flex;
        gap: 0.75rem;
        margin-bottom: 1rem;
    }

        .staff-nav a {
            padding: 0.4rem 1rem;
            border: 1px solid #bbb;
            border-radius: 999px;
            color: #222;
            text-decoration: none;
        }

            .staff-nav a.router-link-exact-active {
                background: #1565c0;
                border-color: #1565c0;
                color: #fff;
            }
</style>