delivery-recipient-examine = Это предназначено для {$recipient}, {$job}.
delivery-already-opened-examine = Оно уже было вскрыто.
delivery-earnings-examine = Доставка этого принесёт станции [color=yellow]{$spesos}[/color] кредитов.
delivery-recipient-no-name = Неназванный
delivery-recipient-no-job = Неизвестно

delivery-unlocked-self = Вы разблокируете {$delivery} при помощи своих отпечатков пальцев.
delivery-opened-self = Вы открываете {$delivery}.
delivery-unlocked-others = {CAPITALIZE($recipient)} разблокировал {$delivery} с помощью {POSS-ADJ($possadj)} отпечатков.
delivery-opened-others = {CAPITALIZE($recipient)} открыл {$delivery}.

delivery-unlock-verb = Разблокировать
delivery-open-verb = Открыть
delivery-slice-verb = Разрезать

delivery-teleporter-amount-examine =
    { $amount ->
        [one] Оно содержит [color=yellow]{$amount}[/color] письмо.
        *[other] It contains [color=yellow]{$amount}[/color] писем.
    }
delivery-teleporter-empty = {$entity} пуст.
delivery-teleporter-empty-verb = Взять почту


# modifiers
delivery-priority-examine = Это [color=orange]срочная {$type}[/color]. У вас осталось [color=orange]{$time}[/color] для доставки чтобы получить доплату.
delivery-priority-expired-examine = Это [color=orange]срочная {$type}[/color]. Кажется, вы не успели доставить это вовремя.

delivery-fragile-examine = Это [color=red]хрупкая {$type}[/color]. Доставьте его в целости для доплаты.
delivery-fragile-broken-examine = Это [color=red]хрупкая {$type}[/color]. Выглядит повреждённым.
