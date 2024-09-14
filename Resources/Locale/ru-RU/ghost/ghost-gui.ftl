ghost-gui-return-to-body-button = Вернуться в тело
ghost-gui-ghost-warp-button = Телепорт призрака
ghost-gui-ghost-roles-button = Роли призраков ({ $count })
ghost-gui-toggle-ghost-visibility-popup-on = Видимость призраков включена.
ghost-gui-toggle-ghost-visibility-popup-off = Видимость призраков выключена.
ghost-gui-toggle-lighting-manager-popup = Рендеринг света переключён.
ghost-gui-toggle-fov-popup = Поле зрения переключено.
ghost-gui-toggle-hearing-popup-on = Теперь вы слышите все фразы.
ghost-gui-toggle-hearing-popup-off = Теперь вы слышите только радиосвязь и фразы поблизости.
ghost-target-window-title = Телепорт призрака
ghost-target-window-current-button = Телепорт в: { $name }
ghost-target-window-warp-to-most-followed = Телепорт к самому следуемому
ghost-roles-window-title = Роли призраков
ghost-roles-window-join-raffle-button = Участвовать в лотерее
ghost-roles-window-raffle-in-progress-button =
    Участвовать в лотерее (Осталось { $time }, { $players ->
        [one] { $players } игрок
        [few] { $players } игрока
       *[other] { $players } игроков
    })
ghost-roles-window-leave-raffle-button =
    Покинуть (Осталось { $time }, { $players ->
        [one] { $players } игрок
        [few] { $players } игрока
       *[other] { $players } игроков
    })
ghost-roles-window-request-role-button = Запросить
ghost-roles-window-request-role-button-timer = Запросить ({ $time }сек.)
ghost-roles-window-follow-role-button = Следовать
ghost-roles-window-no-roles-available-label = В настоящее время нет доступных ролей призраков.
ghost-roles-window-rules-footer = Кнопка станет доступна через { $time } секунд (эта задержка нужна, чтобы убедиться, что вы прочитали правила).
ghost-return-to-body-title = Вернуться в тело
ghost-return-to-body-text = Вас воскрешают! Вернуться в своё тело?

ghost-gui-return-to-round-button = Вернуться в раунд
ghost-respawn-time-left = Минут осталось до возможности вернуться в раунд - { $time }.
ghost-respawn-max-players = Функция недоступна, игроков на сервере должно быть меньше { $players }.
ghost-respawn-window-title = Правила возвращения в раунд
ghost-respawn-window-request-button-timer = Принять ({ $time }сек.)
ghost-respawn-window-request-button = Принять
ghost-respawn-window-rules-footer = Пользуясь это функцией, вы [color=#ff7700]обязуетесь[/color] [color=#ff0000]не переносить[/color] знания своего прошлого персонажа в нового, [color=#ff0000]не метамстить[/color]. Каждый новый персонаж - [color=#ff7700]чистый уникальный лист[/color], который никак не связан с предыдущим. Поэтому не забудьте [color=#ff7700]поменять персонажа[/color] перед заходом, а также помните, что за нарушение пункта, указанного здесь, следует [color=#ff0000]бан в размере от 3ех дней[/color].
ghost-respawn-bug = Нет времени смерти. Установлено стандартное значение.
ghost-respawn-same-character = Нельзя заходить в раунд за того же персонажа. Поменяйте его в настройках персонажей.
ghost-respawn-character-almost-same = Игрок { $player } { $try ->
[true] зашёл
*[false] попытался зайти
} в раунд после респауна с похожим именем. Прошлое имя: { $oldName }, текущее: { $newName }.
ghost-respawn-same-character-slightly-changed-name = Попытка обойти запрет входа в раунд тем же персонажем. Ваши действия будут переданы администрации!
