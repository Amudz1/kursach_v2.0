<template>
  <div class="app-root">
    <!-- Navbar (только для авторизованных) -->
    <nav v-if="auth.isLoggedIn" class="navbar">
      <div class="navbar-brand">
        <span class="brand-icon">🤖</span>
        <span class="brand-name">AI Helper Budz</span>
      </div>

      <div class="navbar-links">
        <router-link to="/"            class="nav-link" active-class="active">🏠 Главная</router-link>
        <router-link to="/chat"        class="nav-link" active-class="active">💬 Чат с AI</router-link>
        <router-link to="/subscription" class="nav-link" active-class="active">
          ✨ Подписка
          <span v-if="!auth.hasSubscription" class="nav-badge">FREE</span>
        </router-link>
        <router-link to="/profile"     class="nav-link" active-class="active">👤 Профиль</router-link>
      </div>

      <div class="navbar-user">
        <span class="user-greeting">{{ auth.username }}</span>
        <span v-if="auth.hasSubscription" class="sub-chip sub-chip--pro">PRO ✓</span>
        <span v-else class="sub-chip sub-chip--free">FREE</span>
        <button class="btn-logout" @click="handleLogout">Выйти</button>
      </div>
    </nav>

    <!-- Основной контент -->
    <main class="main-content" :class="{ 'no-navbar': !auth.isLoggedIn }">
      <router-view v-slot="{ Component }">
        <transition name="fade" mode="out-in">
          <component :is="Component" />
        </transition>
      </router-view>
    </main>
  </div>
</template>

<script setup>
import { useAuthStore } from './stores/auth'
import { useRouter } from 'vue-router'

const auth   = useAuthStore()
const router = useRouter()

async function handleLogout() {
  await auth.logout()
  router.push('/login')
}
</script>

<style>
/* ── CSS Variables (тёмная тема) ────────────────────────── */
:root {
  --bg:          #0f0f17;
  --bg-card:     #1a1a2e;
  --bg-input:    #16213e;
  --bg-hover:    #1f2b47;
  --border:      #2a3a5c;
  --accent:      #6c63ff;
  --accent-dark: #5a52d5;
  --accent-glow: rgba(108,99,255,.25);
  --green:       #4ade80;
  --red:         #f87171;
  --orange:      #fb923c;
  --yellow:      #fbbf24;
  --text:        #e2e8f0;
  --text-muted:  #94a3b8;
  --radius:      12px;
  --radius-sm:   8px;
  --shadow:      0 4px 24px rgba(0,0,0,.4);
}

* { box-sizing: border-box; margin: 0; padding: 0; }

html, body { height: 100%; }

body {
  font-family: 'Inter', 'Segoe UI', system-ui, sans-serif;
  background: var(--bg);
  color: var(--text);
  font-size: 15px;
  line-height: 1.6;
  -webkit-font-smoothing: antialiased;
}

a { color: var(--accent); text-decoration: none; }

/* ── Navbar ─────────────────────────────────────────────── */
.navbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 28px;
  height: 64px;
  background: var(--bg-card);
  border-bottom: 1px solid var(--border);
  position: sticky;
  top: 0;
  z-index: 100;
  backdrop-filter: blur(12px);
}

.navbar-brand {
  display: flex;
  align-items: center;
  gap: 10px;
  text-decoration: none;
}
.brand-icon { font-size: 1.6rem; }
.brand-name {
  font-size: 1.1rem;
  font-weight: 700;
  background: linear-gradient(135deg, #6c63ff, #a78bfa);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
}

.navbar-links { display: flex; gap: 4px; }

.nav-link {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 8px 14px;
  border-radius: var(--radius-sm);
  color: var(--text-muted);
  font-size: 14px;
  font-weight: 500;
  transition: all .2s;
  position: relative;
}
.nav-link:hover  { background: var(--bg-hover); color: var(--text); }
.nav-link.active { background: var(--accent-glow); color: var(--accent); }

.nav-badge {
  background: var(--orange);
  color: #000;
  font-size: 10px;
  font-weight: 700;
  padding: 1px 5px;
  border-radius: 10px;
  margin-left: 2px;
}

.navbar-user {
  display: flex;
  align-items: center;
  gap: 10px;
}
.user-greeting {
  font-weight: 600;
  font-size: 14px;
  color: var(--text-muted);
}

.sub-chip {
  font-size: 11px;
  font-weight: 700;
  padding: 3px 8px;
  border-radius: 20px;
}
.sub-chip--pro  { background: rgba(108,99,255,.2); color: var(--accent); }
.sub-chip--free { background: rgba(251,146,60,.15); color: var(--orange); }

.btn-logout {
  padding: 6px 14px;
  background: transparent;
  border: 1px solid var(--border);
  border-radius: var(--radius-sm);
  color: var(--text-muted);
  font-size: 13px;
  cursor: pointer;
  transition: all .2s;
}
.btn-logout:hover { border-color: var(--red); color: var(--red); }

/* ── Main content ───────────────────────────────────────── */
.main-content {
  min-height: calc(100vh - 64px);
}
.main-content.no-navbar { min-height: 100vh; }

/* ── Transitions ────────────────────────────────────────── */
.fade-enter-active, .fade-leave-active { transition: opacity .2s; }
.fade-enter-from, .fade-leave-to       { opacity: 0; }

/* ── Global utils ───────────────────────────────────────── */
.btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  padding: 10px 22px;
  border: none;
  border-radius: var(--radius-sm);
  font-size: 15px;
  font-weight: 600;
  cursor: pointer;
  transition: all .2s;
}
.btn:disabled { opacity: .5; cursor: not-allowed; }
.btn-primary  { background: var(--accent); color: #fff; }
.btn-primary:hover:not(:disabled) { background: var(--accent-dark); transform: translateY(-1px); box-shadow: 0 4px 16px var(--accent-glow); }
.btn-ghost    { background: transparent; border: 1px solid var(--border); color: var(--text-muted); }
.btn-ghost:hover:not(:disabled) { border-color: var(--accent); color: var(--accent); }
.btn-danger   { background: transparent; border: 1px solid var(--border); color: var(--red); }
.btn-danger:hover:not(:disabled) { background: rgba(248,113,113,.1); }
.btn-success  { background: var(--green); color: #000; }
.btn-success:hover:not(:disabled) { opacity: .9; }

.input {
  width: 100%;
  padding: 11px 16px;
  background: var(--bg-input);
  border: 1px solid var(--border);
  border-radius: var(--radius-sm);
  color: var(--text);
  font-size: 15px;
  outline: none;
  transition: border-color .2s;
}
.input:focus          { border-color: var(--accent); }
.input.input--error   { border-color: var(--red); }
.input-error-msg      { color: var(--red); font-size: 12px; margin-top: 4px; }

.card {
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  padding: 24px;
}

.alert { padding: 12px 16px; border-radius: var(--radius-sm); font-size: 14px; margin-bottom: 16px; }
.alert-success { background: rgba(74,222,128,.1); border: 1px solid rgba(74,222,128,.3); color: var(--green); }
.alert-error   { background: rgba(248,113,113,.1); border: 1px solid rgba(248,113,113,.3); color: var(--red); }
.alert-warn    { background: rgba(251,146,60,.1);  border: 1px solid rgba(251,146,60,.3);  color: var(--orange); }
.alert-info    { background: rgba(108,99,255,.1);  border: 1px solid rgba(108,99,255,.3);  color: var(--accent); }

.page { max-width: 1100px; margin: 0 auto; padding: 32px 24px; }
.page-title { font-size: 1.8rem; font-weight: 700; margin-bottom: 8px; }
.page-subtitle { color: var(--text-muted); margin-bottom: 28px; }

/* Spinner */
.spinner {
  width: 20px; height: 20px;
  border: 2px solid var(--border);
  border-top-color: var(--accent);
  border-radius: 50%;
  animation: spin .7s linear infinite;
  display: inline-block;
}
@keyframes spin { to { transform: rotate(360deg); } }

/* Scrollbar */
::-webkit-scrollbar       { width: 6px; height: 6px; }
::-webkit-scrollbar-track { background: var(--bg); }
::-webkit-scrollbar-thumb { background: var(--border); border-radius: 3px; }
::-webkit-scrollbar-thumb:hover { background: var(--accent); }
</style>
