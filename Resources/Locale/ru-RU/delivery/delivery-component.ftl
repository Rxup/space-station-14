delivery-recipient-examine = Адресовано: { $recipient }, { $job }.
delivery-already-opened-examine = Уже вскрыто.
delivery-earnings-examine = Доставка этого принесёт станции [color=yellow]{ $spesos }[/color] кредитов.
delivery-recipient-no-name = Безымянный
delivery-recipient-no-job = Неизвестно
delivery-unlocked-self = Вы разблокировали { $delivery } отпечатком пальца.
delivery-opened-self = Вы вскрываете { $delivery }.
delivery-unlocked-others =
    { CAPITALIZE($recipient) } { GENDER($recipient) ->
        [male] разблокировал
        [female] разблокировала
        [epicene] разблокировали
       *[neuter] разблокировало
    } { $delivery } используя свой отпечаток пальца.
delivery-opened-others =
    { CAPITALIZE($recipient) } { GENDER($recipient) ->
        [male] вскрыл
        [female] вскрыл
        [epicene] вскрыл
       *[neuter] вскрыл
    } { $delivery }.
delivery-unlock-verb = Разблокировать
delivery-open-verb = Вскрыть
delivery-slice-verb = Разрезать и вскрыть
delivery-teleporter-amount-examine =
    { $amount ->
        [one] Оно содержит [color=yellow]{ $amount }[/color] почту.
       *[other] Оно содержит [color=yellow]{ $amount }[/color] почты.
    }
delivery-teleporter-empty = { $entity } пуст.
delivery-teleporter-empty-verb = Взять почту
# modifiers
delivery-priority-examine = Это [color=orange]приоритетное { $type }[/color]. У вас осталось [color=orange]{ $time }[/color] чтобы доставить это и получить бонус.
delivery-priority-delivered-examine = Это [color=orange]приоритетное { $type }[/color]. Его доставили вовремя.
delivery-priority-expired-examine = This is a [color=orange]приоритетное { $type }[/color]. Его не успели доставить вовремя.
delivery-fragile-examine = Это [color=red]хрупкое { $type }[/color]. Доставьте его в целости, чтобы получить бонус.
delivery-fragile-broken-examine = Это [color=red]хрупкое { $type }[/color]. Выглядит сильно повреждённым.
