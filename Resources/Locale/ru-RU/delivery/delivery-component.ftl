delivery-recipient-examine = Адресовано: {$recipient}, {$job}.
delivery-already-opened-examine = Уже вскрыто.
delivery-earnings-examine = Доставка этого принесёт станции [color=yellow]{$spesos}[/color] кредитов.
delivery-recipient-no-name = Безымянный
delivery-recipient-no-job = Неизвестно
delivery-unlocked-self = Вы разблокировали {$delivery} отпечатком пальца.
delivery-opened-self = Вы вскрываете {$delivery}.
delivery-unlocked-others = { CAPITALIZE($recipient) } { GENDER($recipient) ->
        [male] разблокировал
        [female] разблокировала
        [epicene] разблокировали
       *[neuter] разблокировало
    } { $delivery } используя свой отпечаток пальца.
delivery-opened-others = { CAPITALIZE($recipient) } { GENDER($possadj) ->
        [male] вскрыл
        [female] вскрыл
        [epicene] вскрыл
       *[neuter] вскрыл
    } { $delivery }.
delivery-unlock-verb = Разблокировать
delivery-open-verb = Вскрыть
delivery-slice-verb = Разрезать и вскрыть
delivery-teleporter-amount-examine = { $amount ->
        [one] Оно содержит [color=yellow]{ $amount }[/color] почту.
       *[other] Оно содержит [color=yellow]{ $amount }[/color] почты.
    }
delivery-teleporter-empty = { $entity } пуст.
delivery-teleporter-empty-verb = Взять почту
# modifiers
delivery-priority-examine = [color=orange]{$type} с высоким приоритетом[/color]. У вас осталось [color=orange]{$time}[/color], чтобы доставить это и получить бонус.
delivery-priority-delivered-examine = [color=orange]{$type} с высоким приоритетом[/color]. Доставлено вовремя.
delivery-priority-expired-examine = [color=orange]{$type} с высоким приоритетом[/color]. Время истекло.
delivery-fragile-examine = [color=red]{$type} имеет хрупкое содержимое[/color]. Доставьте невредимым для получения бонуса.
delivery-fragile-broken-examine = [color=red]{$type} имеет хрупкое содержимое[/color]. Выглядит сильно поврежденно.
delivery-bomb-examine = Это [color=purple]{$type}-бомба[/color]. О нет.
delivery-bomb-primed-examine = Это [color=purple]{$type}-бомба[/color]. Читать это – пустая трата вашего времени.
