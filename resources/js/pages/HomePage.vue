<template>
  <div class="home-page">

    <!-- Баннер подписки (навязчивый, если нет подписки) -->
    <transition name="slide-down">
      <div v-if="!auth.hasSubscription && showBanner" class="sub-banner">
        <div class="sub-banner-content">
          <span class="sub-banner-icon">🚀</span>
          <div>
            <strong>Откройте безлимитный доступ к AI!</strong>
            <p>У вас {{ auth.remainingPrompts }} бесплатных запросов. Оформите подписку от $9.99/мес</p>
          </div>
          <router-link to="/subscription" class="btn btn-primary btn--sm">Оформить сейчас</router-link>
          <button class="banner-close" @click="showBanner = false">✕</button>
        </div>
      </div>
    </transition>

    <div class="page">
      <!-- Приветствие -->
      <div class="hero">
        <div class="hero-left">
          <div class="hero-badge">
            <span v-if="auth.hasSubscription">⭐ PRO-аккаунт</span>
            <span v-else>🆓 Бесплатный план</span>
          </div>
          <h1 class="hero-title">Привет, <span class="accent">{{ auth.username }}</span>! 👋</h1>
          <p class="hero-desc">
            Добро пожаловать в <strong>AI Helper Budz</strong> — ваш персональный интеллектуальный ассистент на базе передовых языковых моделей.
          </p>

          <div class="hero-actions">
            <router-link to="/chat" class="btn btn-primary btn--lg">
              💬 Начать чат с AI
            </router-link>
            <router-link v-if="!auth.hasSubscription" to="/subscription" class="btn btn-ghost btn--lg">
              ✨ Оформить подписку
            </router-link>
          </div>
        </div>

        <div class="hero-right">
          <div class="ai-card">
            <div class="ai-card-header">
              <div class="ai-avatar">🤖</div>
              <div>
                <div class="ai-name">Budz AI</div>
                <div class="ai-status"><span class="status-dot"></span>Онлайн</div>
              </div>
            </div>
            <div class="ai-preview-msgs">
              <div class="preview-msg preview-msg--user">Объясни что такое нейронная сеть</div>
              <div class="preview-msg preview-msg--ai">Нейронная сеть — это система, вдохновлённая мозгом человека. Она состоит из слоёв узлов (нейронов), которые обучаются распознавать паттерны в данных...</div>
            </div>
          </div>
        </div>
      </div>

      <!-- Что умеет AI -->
      <section class="features">
        <h2 class="section-title">🧠 Что умеет AI Helper Budz?</h2>
        <div class="features-grid">
          <div v-for="f in features" :key="f.title" class="feature-card">
            <div class="feature-icon">{{ f.icon }}</div>
            <h3>{{ f.title }}</h3>
            <p>{{ f.desc }}</p>
          </div>
        </div>
      </section>

      <!-- Блок с планами (если нет подписки) -->
      <section v-if="!auth.hasSubscription" class="promo-section">
        <div class="promo-card">
          <div class="promo-text">
            <h2>🔥 Только {{ auth.remainingPrompts }} запросов осталось!</h2>
            <p>С подпиской PRO вы получаете неограниченное количество запросов, приоритетный доступ к лучшим моделям и историю всех чатов.</p>
          </div>
          <router-link to="/subscription" class="btn btn-primary btn--lg">
            Смотреть тарифы →
          </router-link>
        </div>
      </section>

      <!-- Статус подписки (если есть) -->
      <section v-else class="sub-status-section">
        <div class="sub-status-card">
          <div class="sub-status-icon">⭐</div>
          <div>
            <h3>Ваша подписка PRO активна</h3>
            <p>{{ auth.user?.subscription?.plan_name }} · До {{ auth.user?.subscription?.ends_at }} · Осталось {{ auth.user?.subscription?.days_remaining }} дн.</p>
          </div>
          <router-link to="/profile" class="btn btn-ghost btn--sm">Подробнее</router-link>
        </div>
      </section>
    </div>
  </div>
</template>

<script setup>
import { ref } from 'vue'
import { useAuthStore } from '../stores/auth'

const auth       = useAuthStore()
const showBanner = ref(true)

const features = [
  { icon: '💬', title: 'Умный чат',         desc: 'Общайтесь с AI на любые темы — от кулинарии до квантовой физики. Контекст беседы сохраняется на протяжении всего диалога.' },
  { icon: '📝', title: 'Работа с текстом',   desc: 'Редактирование, рерайтинг, написание с нуля, резюме, эссе, письма — AI справится с любой текстовой задачей.' },
  { icon: '💻', title: 'Помощь с кодом',     desc: 'Генерация, объяснение и отладка кода на любом языке программирования. Рефакторинг и документирование.' },
  { icon: '📎', title: 'Файлы и документы',  desc: 'Загружайте текстовые файлы и изображения. AI проанализирует содержимое и ответит на ваши вопросы.' },
  { icon: '🌍', title: 'Переводы',           desc: 'Перевод текстов на любой язык мира с сохранением контекста, стиля и нюансов оригинала.' },
  { icon: '💡', title: 'Анализ и идеи',      desc: 'Мозговой штурм, анализ данных, поиск решений, стратегическое планирование и генерация идей.' },
]
</script>

<style scoped>
.home-page { min-height: 100%; }

/* Banner */
.sub-banner {
  background: linear-gradient(90deg, #6c63ff, #a78bfa);
  padding: 12px 24px;
}
.sub-banner-content {
  max-width: 1100px;
  margin: 0 auto;
  display: flex;
  align-items: center;
  gap: 16px;
}
.sub-banner-icon  { font-size: 1.4rem; }
.sub-banner-content > div { flex: 1; }
.sub-banner-content strong { color: #fff; }
.sub-banner-content p { color: rgba(255,255,255,.8); font-size: 13px; margin-top: 2px; }
.banner-close {
  background: none; border: none; color: rgba(255,255,255,.7);
  font-size: 18px; cursor: pointer; padding: 4px;
}
.banner-close:hover { color: #fff; }
.slide-down-enter-active, .slide-down-leave-active { transition: all .3s; }
.slide-down-enter-from, .slide-down-leave-to { transform: translateY(-100%); opacity: 0; }

/* Hero */
.hero {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 48px;
  align-items: center;
  padding-bottom: 48px;
  border-bottom: 1px solid var(--border);
  margin-bottom: 48px;
}
.hero-badge {
  display: inline-block;
  background: var(--accent-glow);
  color: var(--accent);
  font-size: 12px;
  font-weight: 700;
  padding: 4px 12px;
  border-radius: 20px;
  margin-bottom: 16px;
}
.hero-title   { font-size: 2.4rem; font-weight: 800; line-height: 1.2; margin-bottom: 16px; }
.accent       { color: var(--accent); }
.hero-desc    { color: var(--text-muted); font-size: 1.05rem; margin-bottom: 28px; line-height: 1.7; }
.hero-actions { display: flex; gap: 12px; flex-wrap: wrap; }
.btn--lg      { padding: 13px 28px; font-size: 16px; }
.btn--sm      { padding: 7px 16px; font-size: 13px; }

/* AI Card preview */
.ai-card {
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: 20px;
  padding: 24px;
  box-shadow: var(--shadow);
}
.ai-card-header { display: flex; align-items: center; gap: 12px; margin-bottom: 20px; }
.ai-avatar { font-size: 2.4rem; background: var(--accent-glow); border-radius: 50%; width: 52px; height: 52px; display: flex; align-items: center; justify-content: center; }
.ai-name   { font-weight: 700; font-size: 1rem; }
.ai-status { display: flex; align-items: center; gap: 6px; font-size: 12px; color: var(--green); }
.status-dot { width: 7px; height: 7px; background: var(--green); border-radius: 50%; animation: pulse 2s infinite; }
@keyframes pulse { 0%,100%{opacity:1}50%{opacity:.4} }

.ai-preview-msgs { display: flex; flex-direction: column; gap: 10px; }
.preview-msg { padding: 10px 14px; border-radius: 12px; font-size: 13px; line-height: 1.5; max-width: 90%; }
.preview-msg--user { background: var(--accent); color: #fff; align-self: flex-end; border-bottom-right-radius: 3px; }
.preview-msg--ai   { background: var(--bg-input); align-self: flex-start; border-bottom-left-radius: 3px; }

/* Features */
.section-title { font-size: 1.5rem; font-weight: 700; margin-bottom: 24px; }
.features-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 16px;
  margin-bottom: 48px;
}
.feature-card { background: var(--bg-card); border: 1px solid var(--border); border-radius: var(--radius); padding: 22px; transition: border-color .2s; }
.feature-card:hover { border-color: var(--accent); }
.feature-icon { font-size: 1.8rem; margin-bottom: 10px; }
.feature-card h3 { font-size: 1rem; font-weight: 700; margin-bottom: 6px; }
.feature-card p  { font-size: 13px; color: var(--text-muted); line-height: 1.6; }

/* Promo */
.promo-card {
  background: linear-gradient(135deg, rgba(108,99,255,.15), rgba(167,139,250,.1));
  border: 1px solid var(--accent);
  border-radius: var(--radius);
  padding: 28px 32px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 24px;
}
.promo-text h2 { font-size: 1.3rem; margin-bottom: 8px; }
.promo-text p  { color: var(--text-muted); font-size: 14px; }

/* Sub status */
.sub-status-card {
  background: rgba(74,222,128,.05);
  border: 1px solid rgba(74,222,128,.3);
  border-radius: var(--radius);
  padding: 20px 24px;
  display: flex;
  align-items: center;
  gap: 16px;
}
.sub-status-icon { font-size: 1.8rem; }
.sub-status-card > div { flex: 1; }
.sub-status-card h3 { font-weight: 700; color: var(--green); }
.sub-status-card p  { font-size: 13px; color: var(--text-muted); margin-top: 3px; }

@media (max-width: 768px) {
  .hero { grid-template-columns: 1fr; }
  .hero-right { display: none; }
  .promo-card { flex-direction: column; }
}
</style>
