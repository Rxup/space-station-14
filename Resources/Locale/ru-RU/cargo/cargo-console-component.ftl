## UI

cargo-console-menu-title = Консоль заказа грузов
cargo-console-menu-account-name-label = Имя аккаунта:{ " " }
cargo-console-menu-account-name-none-text = Нет
cargo-console-menu-account-name-format =  [bold][color={ $color }]{ $name }[/color][/bold] [font="Monospace"]\[{ $code }\][/font]
cargo-console-menu-shuttle-name-label = Название шаттла:{ " " }
cargo-console-menu-shuttle-name-none-text = Нет
cargo-console-menu-points-label = Кредиты:{ " " }
cargo-console-menu-points-amount = ${ $amount }
cargo-console-menu-shuttle-status-label = Статус шаттла:{ " " }
cargo-console-menu-shuttle-status-away-text = Отбыл
cargo-console-menu-order-capacity-label = Объём заказов:{ " " }
cargo-console-menu-call-shuttle-button = Активировать телепад
cargo-console-menu-permissions-button = Доступы
cargo-console-menu-categories-label = Категории:{ " " }
cargo-console-menu-search-bar-placeholder = Поиск
cargo-console-menu-requests-label = Запросы
cargo-console-menu-orders-label = Заказы
cargo-console-menu-order-reason-description = Причина: { $reason }
cargo-console-menu-populate-categories-all-text = Все
cargo-console-menu-populate-orders-cargo-order-row-product-name-text = { $productName } (x{ $orderAmount }) от { $orderRequester }
cargo-console-menu-cargo-order-row-approve-button = Одобрить
cargo-console-menu-cargo-order-row-cancel-button = Отменить
cargo-console-menu-tab-title-orders = Заказы
cargo-console-menu-tab-title-funds = Переводы
cargo-console-menu-account-action-transfer-limit =  [bold]Лимит перевода:[/bold] ${ $limit }
cargo-console-menu-account-action-transfer-limit-unlimited-notifier =  [color=gold](Неограничено)[/color]
cargo-console-menu-account-action-select =  [bold]Действие над Аккаунтом:[/bold]
cargo-console-menu-account-action-amount =  [bold]Размер:[/bold] $
cargo-console-menu-account-action-button = Перевести
cargo-console-menu-toggle-account-lock-button = Переключить Лимит Переводов
cargo-console-menu-account-action-option-withdraw = Вывести Средства
cargo-console-menu-account-action-option-transfer = Перевести Средства на { $code }
# Orders
cargo-console-order-not-allowed = Доступ запрещён
cargo-console-station-not-found = Нет доступной станции
cargo-console-invalid-product = Неверный ID продукта
cargo-console-too-many = Слишком много одобренных заказов
cargo-console-snip-snip = Заказ урезан до вместимости
cargo-console-insufficient-funds = Недостаточно средств (требуется { $cost })
cargo-console-unfulfilled = Нет места для выполнения заказа
cargo-console-trade-station = Отправлено на { $destination }
cargo-console-unlock-approved-order-broadcast =  [bold]Заказ на { $productName } x{ $orderAmount }[/bold], стоимостью [bold]{ $cost }[/bold], был одобрен [bold]{ $approver }[/bold]
cargo-console-fund-withdraw-broadcast =  [bold]{ $name } вывел { $amount } кредитов из { $name1 } \[{ $code1 }\]
cargo-console-fund-transfer-broadcast =  [bold]{ $name } перевёл { $amount } кредитов с { $name1 } \[{ $code1 }\] на { $name2 } \[{ $code2 }\][/bold]
cargo-console-fund-transfer-user-unknown = Неизвестно
cargo-console-paper-reason-default = Отсутствует
cargo-console-paper-approver-default = Вы
cargo-console-paper-print-name = Заказ #{ $orderNumber }
cargo-console-paper-print-text =
    Заказ #{ $orderNumber }
    Товар: { $itemName }
    Кол-во: { $orderQuantity }
    Запросил: { $requester }
    Причина: { $reason }
    Одобрил: { $approver }
# Cargo shuttle console
cargo-shuttle-console-menu-title = Консоль вызова грузового шаттла
cargo-shuttle-console-station-unknown = Неизвестно
cargo-shuttle-console-shuttle-not-found = Не найден
cargo-no-shuttle = Грузовой шаттл не найден!
cargo-shuttle-console-organics = На шаттле обнаружены органические формы жизни
# Funding allocation console
cargo-funding-alloc-console-menu-title = Консоль Финансирования
cargo-funding-alloc-console-label-account =  [bold]Аккаунт[/bold]
cargo-funding-alloc-console-label-code =  [bold] Код [/bold]
cargo-funding-alloc-console-label-balance =  [bold] Баланс [/bold]
cargo-funding-alloc-console-label-cut =  [bold] Распределение Выручки (%) [/bold]
cargo-funding-alloc-console-label-primary-cut = Срез заработка карго от заработка иными путями (%):
cargo-funding-alloc-console-label-lockbox-cut = Срез заработка карго от продажи лок-боксов (%):
cargo-funding-alloc-console-label-help-non-adjustible = Карго получает { $percent }% выручки от продажи через все источники, кроме лок-боксов. Остальное распределяется так:
cargo-funding-alloc-console-label-help-adjustible = Оставшиеся средства полученные не от продажи лок-боксов распределяются так:
cargo-funding-alloc-console-button-save = Сохранить изменения
cargo-funding-alloc-console-label-save-fail =  [bold]Ошибка распределения![/bold] [color=red]({ $pos ->
        [1] +
       *[-1] -
    }{ $val }%)[/color]
# Slip template
cargo-acquisition-slip-body =  [head=3]Asset Detail[/head]
    { "[bold]Product:[/bold]" } { $product }
    { "[bold]Description:[/bold]" } { $description }
    { "[bold]Unit cost:[/bold" }] ${ $unit }
    { "[bold]Amount:[/bold]" } { $amount }
    { "[bold]Cost:[/bold]" } ${ $cost }
    
    { "[head=3]Purchase Detail[/head]" }
    { "[bold]Orderer:[/bold]" } { $orderer }
    { "[bold]Reason:[/bold]" } { $reason }
