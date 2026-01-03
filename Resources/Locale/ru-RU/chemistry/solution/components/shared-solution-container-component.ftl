shared-solution-container-component-on-examine-empty-container = Не содержит вещества.
shared-solution-container-component-on-examine-main-text = Содержит [color={ $color }]{ $desc }[/color] { $wordedAmount }
examinable-solution-recognized = [color={ $color }]{ $chemical }[/color]
examinable-solution-on-examine-volume = Содержащийся раствор { $fillLevel ->
    [exact] содержит [color=white]{$current}/{$max} ед.[/color].
   *[other] [bold]{ -solution-vague-fill-level(fillLevel: $fillLevel) }[/bold].
}

examinable-solution-on-examine-volume-no-max = Содержащийся раствор { $fillLevel ->
    [exact] содержит [color=white]{$current} ед.[/color].
   *[other] [bold]{ -solution-vague-fill-level(fillLevel: $fillLevel) }[/bold].
}

examinable-solution-on-examine-volume-puddle =
    Лужа { $fillLevel ->
        [exact] [color=white]{ $current } ед.[/color].
        [full] огромная и переполненная!
        [mostlyfull] огромная и переполненная!
        [halffull] глубокая и текущая.
        [halfempty] очень глубокая.
       *[mostlyempty] собирается вместе.
        [empty] образует несколько маленьких луж.
    }
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
examinable-solution-has-recognizable-chemicals = В этом растворе вы можете распознать { $recognizedString }.
examinable-solution-recognized-first = [color={ $color }]{ $chemical }[/color]
examinable-solution-recognized-next = , [color={ $color }]{ $chemical }[/color]
examinable-solution-recognized-last = и [color={ $color }]{ $chemical }[/color]
