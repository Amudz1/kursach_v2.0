# Скриншоты приёмки интерфейса «Фокус»

Для сравнения использован один демонстрационный аккаунт и один диалог «План подготовки к экзамену» в проекте «Учёба». В обоих вариантах показаны те же сообщения, закладка, черновик и выбранный файл `план-на-неделю.md`. Цветовые варианты сняты в пресете «Студия».

## Одна беседа в двух интерфейсах

| Компоновка | Окно, CSS px | Светлая тема | Тёмная тема |
| --- | --- | --- | --- |
| Текущий Budz, компьютер | 1 440 × 900 | [Открытый диалог](current-chat-desktop-light.png) | [Открытый диалог](current-chat-desktop-dark.png) |
| «Фокус», компьютер | 1 440 × 900 | [Открытый диалог](focus-chat-desktop-light.png) | [Открытый диалог](focus-chat-desktop-dark.png) |
| Текущий Budz, телефон | 390 × 844 | [Открытый диалог](current-chat-mobile-light.png) | [Открытый диалог](current-chat-mobile-dark.png) |
| «Фокус», телефон | 390 × 844 | [Открытый диалог](focus-chat-mobile-light.png) | [Открытый диалог](focus-chat-mobile-dark.png) |

## Экраны и состояния «Фокуса»

| Состояние | Окно, CSS px | Светлая тема | Тёмная тема |
| --- | --- | --- | --- |
| Первый экран с подсказками и «Продолжить» | 1 440 × 900 | [Снимок](focus-start-desktop-light.png) | [Снимок](focus-start-desktop-dark.png) |
| Мобильная история открыта | 390 × 844 | [Снимок](focus-history-mobile-light.png) | [Снимок](focus-history-mobile-dark.png) |
| Выбор интерфейса в «Оформлении» | 1 440 × 900 | [Снимок](focus-picker-desktop-light.png) | [Снимок](focus-picker-desktop-dark.png) |
| Запись с таймером | 390 × 844 | [Снимок](focus-recording-mobile-light.png) | [Снимок](focus-recording-mobile-dark.png) |
| Просмотр записи и команда «Расшифровать» | 390 × 844 | [Снимок](focus-preview-mobile-light.png) | [Снимок](focus-preview-mobile-dark.png) |
| Редактируемая расшифровка в черновике | 390 × 844 | [Снимок](focus-transcript-mobile-light.png) | [Снимок](focus-transcript-mobile-dark.png) |

Кадры получены в Chromium с имитацией мобильного размера окна и виртуальным микрофоном. Для расшифровки использован тестовый сервер Demo, затем текст исправлен в черновике. [Условия проверки доступности и совместимости](../../acceptance.md)

Для повторного прогона запустите `node tests/acceptance-screenshots.mjs` из каталога `frontend` при включённом `VOICE_MODE=Demo`. `BUDZ_PREVIEW` задаёт адрес сайта, а `BUDZ_CAPTURE_DIR` — каталог снимков. [Настройка голосового режима](../../voice.md)
