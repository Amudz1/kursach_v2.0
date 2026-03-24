# AI Helper Budz — Laravel + VueJS

Полноценный веб-сайт с нейросетью: чат, подписки, профиль.

---

## Запуск за 5 шагов

### 1. Требования
- PHP 8.2+
- Composer
- Node.js 20+
- PostgreSQL 14+

### 2. Установка зависимостей
```bash
composer install
npm install
```

### 3. Настройка .env
```bash
cp .env .env.backup
```
Откройте `.env` и заполните:
```env
OPENROUTER_API_KEY=sk-or-v1-ВАШ_КЛЮЧ_ЗДЕСЬ   # https://openrouter.ai/keys
DB_PASSWORD=ваш_пароль_от_postgres
APP_KEY=                                        # заполнится автоматически
```

### 4. Инициализация
```bash
# Генерация ключа приложения
php artisan key:generate

# Создание таблиц + тарифные планы
php artisan migrate --seed

# Ссылка на хранилище файлов
php artisan storage:link
```

### 5. Запуск
Откройте два терминала:

**Терминал 1 — PHP сервер:**
```bash
php artisan serve
```

**Терминал 2 — VueJS сборка:**
```bash
npm run dev
```

Откройте браузер: **http://localhost:8000**

---

## Структура проекта

```
├── app/
│   ├── Models/
│   │   ├── User.php          — пользователь, подписка, лимиты
│   │   └── Models.php        — SubscriptionPlan, Subscription, Chat, Message...
│   ├── Services/
│   │   ├── AiService.php     — OpenRouter API
│   │   └── SubscriptionService.php
│   └── Http/Controllers/Api/
│       ├── AuthController.php
│       ├── ChatController.php
│       ├── SubscriptionController.php
│       └── ProfileController.php
├── database/
│   ├── migrations/           — схема БД
│   └── seeders/              — тарифные планы
├── resources/js/
│   ├── pages/
│   │   ├── LoginPage.vue     — вход и регистрация
│   │   ├── HomePage.vue      — главная с баннером подписки
│   │   ├── ChatPage.vue      — чат с AI (список чатов + диалог)
│   │   ├── SubscriptionPage.vue — оформление подписки
│   │   └── ProfilePage.vue   — профиль, статус, удаление аккаунта
│   ├── stores/
│   │   ├── auth.js           — Pinia: авторизация
│   │   └── chat.js           — Pinia: чаты и сообщения
│   ├── router.js             — Vue Router
│   └── App.vue               — корневой компонент + navbar
└── routes/
    ├── api.php               — REST API маршруты
    └── web.php               — SPA fallback
```

---

## Функциональность

- **Вход / Регистрация** — авторизация через Sanctum
- **Главная** — приветствие, описание AI, баннер подписки
- **Чат** — список чатов слева, диалог справа (как Telegram), markdown, файлы
- **Подписка** — 4 тарифа со скидками, валидация карты, имитация оплаты 5 сек, сохранение карты (AES-256)
- **Профиль** — статус подписки, смена пароля/email, выход, удаление аккаунта
- **Лимиты** — 12 запросов бесплатно, безлимит с подпиской
