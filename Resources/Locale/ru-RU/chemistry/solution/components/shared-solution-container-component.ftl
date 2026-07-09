shared-solution-container-component-on-examine-empty-container = Не содержит вещества.
-solution-vague-fill-level =
    { $fillLevel ->
        [full] [color=white]Полон[/color]
        [mostlyfull] [color=#DFDFDF]Почти полон[/color]
        [halffull] [color=#C8C8C8]Наполовину полон[/color]
        [halfempty] [color=#C8C8C8]Наполовину пуст[/color]
        [mostlyempty] [color=#A4A4A4]Почти пуст[/color]
       *[empty] [color=gray]Пуст[/color]
    }
shared-solution-container-component-on-examine-worded-amount-one-reagent = вещество.
shared-solution-container-component-on-examine-worded-amount-multiple-reagents = смесь веществ.
examinable-solution-has-recognizable-chemicals = Вам удаётся распознать {$recognizedString} в этом растворе.
examinable-solution-recognized-first = [color={ $color }]{ $chemical }[/color]
examinable-solution-recognized-next = , [color={ $color }]{ $chemical }[/color]
examinable-solution-recognized-last = и [color={ $color }]{ $chemical }[/color]

shared-solution-container-component-on-examine-main-text = Содержит {INDEFINITE($desc)} [color={$color}]{$desc}[/color] { $chemCount ->
    [1] химикат.
   *[other] смесь химикатов.
    }

examinable-solution-recognized = [color={$color}]{$chemical}[/color]

examinable-solution-on-examine-volume = Содержимое { $fillLevel ->
    [exact] [color=white]{$current}/{$max}u[/color].
   *[other] [bold]{ -solution-vague-fill-level(fillLevel: $fillLevel) }[/bold].
}

examinable-solution-on-examine-volume-no-max = Содержимое { $fillLevel ->
    [exact] [color=white]{$current}u[/color].
   *[other] [bold]{ -solution-vague-fill-level(fillLevel: $fillLevel) }[/bold].
}

examinable-solution-on-examine-volume-puddle = Лужа { $fillLevel ->
    [exact] [color=white]{$current}u[/color].
    [full] огромная и переполненная!
    [mostlyfull] огромная и переполненная!
    [halffull] глубокая и растекающаяся.
    [halfempty] очень глубокая.
   *[mostlyempty] собирается в лужицы.
    [empty] распадается на мелкие лужицы.
}
