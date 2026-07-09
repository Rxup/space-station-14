whitelist-not-whitelisted = Вас нет в вайтлисте.
# proper handling for having a min/max or not
whitelist-playercount-invalid = { $min ->
        [0] Вайтлист для этого сервера применяется только для числа игроков ниже { $max }.
       *[other]
            Вайтлист для этого сервера применяется только для числа игроков выше { $min } { $max ->
                [2147483647] ->  так что, возможно, вы сможете присоединиться позже.
               *[other] ->  и ниже { $max } игроков, так что, возможно, вы сможете присоединиться позже.
            }
    }
whitelist-not-whitelisted-rp = Вас нет в вайтлисте. Чтобы попасть в вайтлист, посетите наш Discord (ссылку можно найти по адресу https://backmen.ru).
cmd-whitelistadd-desc = Добавить игрока в вайтлист сервера.
cmd-whitelistadd-help = Использование: whitelistadd <username или  User ID>
cmd-whitelistadd-existing = { $username } уже находится в белом списке!
cmd-whitelistadd-added = { $username } добавлен в белый список
cmd-whitelistadd-not-found = Не удалось найти игрока '{$username}'
cmd-whitelistadd-arg-player = [player]
cmd-whitelistremove-desc = Удалить игрока с белого списка сервера.
cmd-whitelistremove-help = Использование: whitelistremove <username или  User ID>
cmd-whitelistremove-existing = { $username } не находится в белом списке!
cmd-whitelistremove-removed = { $username } удалён из белого списка
cmd-whitelistremove-not-found = Не удалось найти игрока '{$username}'
cmd-whitelistremove-arg-player = [player]
cmd-kicknonwhitelisted-desc = Кикнуть всех игроков не в белом списке с сервера.
cmd-kicknonwhitelisted-help = Использование: kicknonwhitelisted
ban-banned-permanent = Этот бан можно только обжаловать. Для этого посетите { $link }.
ban-banned-permanent-appeal = Этот бан можно только обжаловать. Для этого посетите {$link}.
ban-expires = Вы получили бан на {$duration} минут, и он истечёт {$time} по UTC (для московского времени добавьте 3 часа).
ban-banned-1 = Вам, или другому пользователю этого компьютера или соединения, запрещено здесь играть.
ban-banned-2 = Причина бана: "{$reason}"
ban-banned-3 = Попытки обойти этот бан, например, путём создания нового аккаунта, будут фиксироваться.
soft-player-cap-full = Сервер заполнен!
panic-bunker-account-denied = Этот сервер находится в режиме "Бункер", часто используемом в качестве меры предосторожности против рейдов. Новые подключения от аккаунтов, не соответствующих определённым требованиям, временно не принимаются. Повторите попытку позже
panic-bunker-account-denied-reason = Этот сервер находится в режиме "Бункер", часто используемом в качестве меры предосторожности против рейдов. Новые подключения от аккаунтов, не соответствующих определённым требованиям, временно не принимаются. Повторите попытку позже Причина: "{$reason}"
panic-bunker-account-reason-account = Ваш аккаунт Space Station 14 слишком новый. Он должен быть старше {$minutes} минут
baby-jail-account-reason-overall = Наигранное Вами время на сервере должно быть больше {$minutes} {$minutes}.

whitelist-playtime = У вас недостаточно наигранного времени для входа на этот сервер. Нужно минимум {$minutes} минут.
whitelist-player-count = Сервер сейчас не принимает игроков. Попробуйте позже.
whitelist-notes = У вас слишком много админ-заметок для входа на этот сервер. Проверьте их командой /adminremarks в чате.
whitelist-manual = Вас нет в вайтлисте этого сервера.
whitelist-blacklisted = Вы в чёрном списке этого сервера.
whitelist-always-deny = Вам запрещено заходить на этот сервер.
whitelist-fail-prefix = Нет в вайтлисте: {$msg}

cmd-blacklistadd-desc = Добавляет игрока с указанным именем в чёрный список сервера.
cmd-blacklistadd-help = Использование: blacklistadd <username>
cmd-blacklistadd-existing = {$username} уже в чёрном списке!
cmd-blacklistadd-added = {$username} добавлен в чёрный список
cmd-blacklistadd-not-found = Не удалось найти '{$username}'
cmd-blacklistadd-arg-player = [player]

cmd-blacklistremove-desc = Удаляет игрока с указанным именем из чёрного списка сервера.
cmd-blacklistremove-help = Использование: blacklistremove <username>
cmd-blacklistremove-existing = {$username} не в чёрном списке!
cmd-blacklistremove-removed = {$username} удалён из чёрного списка
cmd-blacklistremove-not-found = Не удалось найти '{$username}'
cmd-blacklistremove-arg-player = [player]

baby-jail-account-denied = Это новичковый сервер для новых игроков и тех, кто им помогает. Аккаунты, которые слишком старые или не в вайтлисте, не принимаются. Попробуйте другие серверы — в SS14 много интересного!
baby-jail-account-denied-reason = Это новичковый сервер для новых игроков и тех, кто им помогает. Аккаунты, которые слишком старые или не в вайтлисте, не принимаются. Попробуйте другие серверы! Причина: "{$reason}"
baby-jail-account-reason-account = Ваш аккаунт Space Station 14 слишком старый. Он должен быть моложе {$minutes} минут

panic-bunker-account-reason-overall = Общее наигранное время на сервере должно быть больше {$minutes} минут

generic-misconfigured = Сервер настроен неправильно и не принимает игроков. Свяжитесь с владельцем сервера и попробуйте позже.

ipintel-server-ratelimited = Сервер использует внешнюю проверку подключений, но достиг лимита запросов к внешнему сервису. Свяжитесь с администрацией или попробуйте позже.
ipintel-unknown = Сервер использует внешнюю проверку подключений, но при проверке вашего соединения произошла ошибка. Свяжитесь с администрацией или попробуйте позже.
ipintel-suspicious = Похоже, вы подключаетесь через дата-центр, прокси или VPN. Такие подключения не допускаются. Отключите VPN и попробуйте снова или свяжитесь с администрацией.

hwid-required = Клиент отказался отправить hardware id. Свяжитесь с администрацией для помощи.
