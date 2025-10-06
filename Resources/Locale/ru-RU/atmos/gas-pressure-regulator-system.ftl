# Examine Text
gas-pressure-regulator-system-examined =
    Клапан находится в состоянии [color={ $statusColor }]{ $open ->
        [true] открытый
       *[false] закрытый
    }[/color].
gas-pressure-regulator-examined-threshold-pressure = Пороговое давление установлено на уровне [color=lightblue]{ $threshold } кПа[/color].
gas-pressure-regulator-examined-flow-rate = Счетчик расхода показывает [color=lightblue]{ $flowRate } л/с[/color].
