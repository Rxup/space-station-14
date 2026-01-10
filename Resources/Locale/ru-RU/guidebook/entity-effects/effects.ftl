-create-3rd-person =
    { $chance ->
        [1] Создаёт
       *[other] создаёт
    }
-cause-3rd-person =
    { $chance ->
        [1] Вызывает
       *[other] вызывает
    }
-satiate-3rd-person =
    { $chance ->
        [1] Утоляет
       *[other] утоляет
    }
entity-effect-guidebook-spawn-entity =
    { $chance ->
        [1] Создаёт
       *[other] создаёт
    } { $amount ->
        [1] { INDEFINITE($entname) }
       *[other] { $amount } { MAKEPLURAL($entname) }
    }
entity-effect-guidebook-destroy =
    { $chance ->
        [1] Уничтожает
       *[other] уничтожает
    } объект
entity-effect-guidebook-break =
    { $chance ->
        [1] Ломает
       *[other] ломает
    } объект
entity-effect-guidebook-explosion =
    { $chance ->
        [1] Вызывает
       *[other] вызывает
    } взрыв
entity-effect-guidebook-emp =
    { $chance ->
        [1] Вызывает
       *[other] вызывает
    } электромагнитный импульс
entity-effect-guidebook-flash =
    { $chance ->
        [1] Вызывает
       *[other] вызывает
    } ослепляющую вспышку
entity-effect-guidebook-foam-area =
    { $chance ->
        [1] Создаёт
       *[other] создаёт
    } большое количество пены
entity-effect-guidebook-smoke-area =
    { $chance ->
        [1] Создаёт
       *[other] создаёт
    } большое количество дыма
entity-effect-guidebook-satiate-thirst =
    { $chance ->
        [1] Утоляет
       *[other] утоляет
    } { $relative ->
        [1] жажду в среднем
       *[other] жажду со скоростью { NATURALFIXED($relative, 3) }x от среднего
    }
entity-effect-guidebook-satiate-hunger =
    { $chance ->
        [1] Утоляет
       *[other] утоляет
    } { $relative ->
        [1] голод в среднем
       *[other] голод со скоростью { NATURALFIXED($relative, 3) }x от среднего
    }
entity-effect-guidebook-health-change =
    { $chance ->
        [1]
            { $healsordeals ->
                [heals] Лечит
                [deals] Наносит
               *[both] Изменяет здоровье на
            }
       *[other]
            { $healsordeals ->
                [heals] лечит
                [deals] наносит
               *[both] изменяет здоровье на
            }
    } { $changes } { $targetPart ->
       [All]{""}
       *[other]  на {$targetPart}
    }

entity-effect-guidebook-even-health-change =
    { $chance ->
        [1]
            { $healsordeals ->
                [heals] Равномерно лечит
                [deals] Равномерно наносит
               *[both] Равномерно изменяет здоровье на
            }
       *[other]
            { $healsordeals ->
                [heals] равномерно лечит
                [deals] равномерно наносит
               *[both] равномерно изменяет здоровье на
            }
    } { $changes } { $targetPart ->
       [All]{""}
       *[other]  на {$targetPart}
    }

entity-effect-guidebook-status-effect-old =
    { $type ->
        [update]
            { $chance ->
                [1] Вызывает
               *[other] вызывает
            } { LOC($key) } минимум на { NATURALFIXED($time, 3) } { MANY("секунду", $time) } без накопления
        [add]
            { $chance ->
                [1] Вызывает
               *[other] вызывает
            } { LOC($key) } минимум на { NATURALFIXED($time, 3) } { MANY("секунду", $time) } с накоплением
        [set]
            { $chance ->
                [1] Вызывает
               *[other] вызывает
            } { LOC($key) } на { NATURALFIXED($time, 3) } { MANY("секунду", $time) } без накопления
       *[remove]
            { $chance ->
                [1] Удаляет
               *[other] удаляет
            } { NATURALFIXED($time, 3) } { MANY("секунду", $time) } { LOC($key) }
    }
entity-effect-guidebook-status-effect =
    { $type ->
        [update]
            { $chance ->
                [1] Вызывает
               *[other] вызывает
            } { LOC($key) } минимум на { NATURALFIXED($time, 3) } { MANY("секунду", $time) } без накопления
        [add]
            { $chance ->
                [1] Вызывает
               *[other] вызывает
            } { LOC($key) } минимум на { NATURALFIXED($time, 3) } { MANY("секунду", $time) } с накоплением
        [set]
            { $chance ->
                [1] Вызывает
               *[other] вызывает
            } { LOC($key) } минимум на { NATURALFIXED($time, 3) } { MANY("секунду", $time) } без накопления
       *[remove]
            { $chance ->
                [1] Удаляет
               *[other] удаляет
            } { NATURALFIXED($time, 3) } { MANY("секунду", $time) } { LOC($key) }
    } { $delay ->
        [0] немедленно
       *[other] после задержки в { NATURALFIXED($delay, 3) } { MANY("секунду", $delay) }
    }
entity-effect-guidebook-status-effect-indef =
    { $type ->
        [update]
            { $chance ->
                [1] Вызывает
               *[other] вызывает
            } постоянное { LOC($key) }
        [add]
            { $chance ->
                [1] Вызывает
               *[other] вызывает
            } постоянное { LOC($key) }
        [set]
            { $chance ->
                [1] Вызывает
               *[other] вызывает
            } постоянное { LOC($key) }
       *[remove]
            { $chance ->
                [1] Удаляет
               *[other] удаляет
            } { LOC($key) }
    } { $delay ->
        [0] немедленно
       *[other] после задержки в { NATURALFIXED($delay, 3) } { MANY("секунду", $delay) }
    }
entity-effect-guidebook-knockdown =
    { $type ->
        [update]
            { $chance ->
                [1] Вызывает
               *[other] вызывает
            } { LOC($key) } минимум на { NATURALFIXED($time, 3) } { MANY("секунду", $time) } без накопления
        [add]
            { $chance ->
                [1] Вызывает
               *[other] вызывает
            } нокдаун минимум на { NATURALFIXED($time, 3) } { MANY("секунду", $time) } с накоплением
       *[set]
            { $chance ->
                [1] Вызывает
               *[other] вызывает
            } нокдаун минимум на { NATURALFIXED($time, 3) } { MANY("секунду", $time) } без накопления
        [remove]
            { $chance ->
                [1] Удаляет
               *[other] удаляет
            } { NATURALFIXED($time, 3) } { MANY("секунду", $time) } нокдауна
    }
entity-effect-guidebook-set-solution-temperature-effect =
    { $chance ->
        [1] Устанавливает
       *[other] устанавливает
    } температуру раствора точно на { NATURALFIXED($temperature, 2) } К
entity-effect-guidebook-adjust-solution-temperature-effect =
    { $chance ->
        [1]
            { $deltasign ->
                [1] Добавляет
               *[-1] Удаляет
            }
       *[other]
            { $deltasign ->
                [1] добавляет
               *[-1] удаляет
            }
    } тепло из раствора до достижения { $deltasign ->
        [1] максимум { NATURALFIXED($maxtemp, 2) } К
       *[-1] минимум { NATURALFIXED($mintemp, 2) } К
    }
entity-effect-guidebook-adjust-reagent-reagent =
    { $chance ->
        [1]
            { $deltasign ->
                [1] Добавляет
               *[-1] Удаляет
            }
       *[other]
            { $deltasign ->
                [1] добавляет
               *[-1] удаляет
            }
    } { NATURALFIXED($amount, 2) } ед. { $reagent } { $deltasign ->
        [1] в
       *[-1] из
    } раствор
entity-effect-guidebook-adjust-reagent-group =
    { $chance ->
        [1]
            { $deltasign ->
                [1] Добавляет
               *[-1] Удаляет
            }
       *[other]
            { $deltasign ->
                [1] добавляет
               *[-1] удаляет
            }
    } { NATURALFIXED($amount, 2) } ед. реагентов группы { $group } { $deltasign ->
        [1] в
       *[-1] из
    } раствор
entity-effect-guidebook-adjust-temperature =
    { $chance ->
        [1]
            { $deltasign ->
                [1] Добавляет
               *[-1] Удаляет
            }
       *[other]
            { $deltasign ->
                [1] добавляет
               *[-1] удаляет
            }
    } { POWERJOULES($amount) } тепла { $deltasign ->
        [1] в
       *[-1] из
    } тело, в котором находится
entity-effect-guidebook-chem-cause-disease =
    { $chance ->
        [1] Вызывает
       *[other] вызывает
    } болезнь { $disease }
entity-effect-guidebook-chem-cause-random-disease =
    { $chance ->
        [1] Вызывает
       *[other] вызывает
    } болезни { $diseases }
entity-effect-guidebook-jittering =
    { $chance ->
        [1] Вызывает
       *[other] вызывает
    } дрожь
entity-effect-guidebook-clean-bloodstream =
    { $chance ->
        [1] Очищает
       *[other] очищает
    } кровоток от других химикатов
entity-effect-guidebook-cure-disease =
    { $chance ->
        [1] Лечит
       *[other] лечит
    } болезни
entity-effect-guidebook-eye-damage =
    { $chance ->
        [1]
            { $deltasign ->
                [1] Наносит
               *[-1] Лечит
            }
       *[other]
            { $deltasign ->
                [1] наносит
               *[-1] лечит
            }
    } урон глазам
entity-effect-guidebook-vomit =
    { $chance ->
        [1] Вызывает
       *[other] вызывает
    } рвоту
entity-effect-guidebook-create-gas =
    { $chance ->
        [1] Создаёт
       *[other] создаёт
    } { $moles } { $moles ->
        [1] моль
       *[other] молей
    } { $gas }
entity-effect-guidebook-drunk =
    { $chance ->
        [1] Вызывает
       *[other] вызывает
    } опьянение
entity-effect-guidebook-electrocute =
    { $chance ->
        [1] Поражает током
       *[other] поражает током
    } метаболизатор на { NATURALFIXED($time, 3) } { MANY("секунду", $time) }
entity-effect-guidebook-emote =
    { $chance ->
        [1] Заставит
       *[other] заставит
    } метаболизатор [bold][color=white]{ $emote }[/color][/bold]
entity-effect-guidebook-extinguish-reaction =
    { $chance ->
        [1] Тушит
       *[other] тушит
    } огонь
entity-effect-guidebook-flammable-reaction =
    { $chance ->
        [1] Увеличивает
       *[other] увеличивает
    } воспламеняемость
entity-effect-guidebook-ignite =
    { $chance ->
        [1] Поджигает
       *[other] поджигает
    } метаболизатор
entity-effect-guidebook-make-sentient =
    { $chance ->
        [1] Делает
       *[other] делает
    } метаболизатор разумным
entity-effect-guidebook-make-polymorph =
    { $chance ->
        [1] Превращает
       *[other] превращает
    } метаболизатор в { $entityname }
entity-effect-guidebook-modify-bleed-amount =
    { $chance ->
        [1]
            { $deltasign ->
                [1] Вызывает
               *[-1] Уменьшает
            }
       *[other]
            { $deltasign ->
                [1] вызывает
               *[-1] уменьшает
            }
    } кровотечение
entity-effect-guidebook-modify-blood-level =
    { $chance ->
        [1]
            { $deltasign ->
                [1] Увеличивает
               *[-1] Уменьшает
            }
       *[other]
            { $deltasign ->
                [1] увеличивает
               *[-1] уменьшает
            }
    } уровень крови
entity-effect-guidebook-paralyze =
    { $chance ->
        [1] Парализует
       *[other] парализует
    } метаболизатор минимум на { NATURALFIXED($time, 3) } { MANY("секунду", $time) }
entity-effect-guidebook-movespeed-modifier =
    { $chance ->
        [1] Изменяет
       *[other] изменяет
    } скорость движения на { NATURALFIXED($sprintspeed, 3) }x минимум на { NATURALFIXED($time, 3) } { MANY("секунду", $time) }
entity-effect-guidebook-reset-narcolepsy =
    { $chance ->
        [1] Временно откладывает
       *[other] временно откладывает
    } нарколепсию
entity-effect-guidebook-wash-cream-pie-reaction =
    { $chance ->
        [1] Смывает
       *[other] смывает
    } кремовый пирог с лица
entity-effect-guidebook-cure-zombie-infection =
    { $chance ->
        [1] Лечит
       *[other] лечит
    } текущую зомби-инфекцию
entity-effect-guidebook-cause-zombie-infection =
    { $chance ->
        [1] Даёт
       *[other] даёт
    } индивиду зомби-инфекцию
entity-effect-guidebook-innoculate-zombie-infection =
    { $chance ->
        [1] Лечит
       *[other] лечит
    } текущую зомби-инфекцию и обеспечивает иммунитет к будущим инфекциям
entity-effect-guidebook-reduce-rotting =
    { $chance ->
        [1] Регенерирует
       *[other] регенерирует
    } { $time } { MANY("секунду", $time) } гниения
entity-effect-guidebook-area-reaction =
    { $chance ->
        [1] Вызывает
       *[other] вызывает
    } дымовую или пенную реакцию на { NATURALFIXED($duration, 3) } { MANY("секунду", $duration) }
entity-effect-guidebook-add-to-solution-reaction =
    { $chance ->
        [1] Вызывает
       *[other] вызывает
    } добавление { $reagent } во внутренний контейнер раствора
entity-effect-guidebook-artifact-unlock =
    { $chance ->
        [1] Помогает
       *[other] помогает
    } разблокировать инопланетный артефакт.
entity-effect-guidebook-artifact-durability-restore = Восстанавливает { $restored } прочности в активных узлах инопланетного артефакта.
entity-effect-guidebook-plant-attribute =
    { $chance ->
        [1] Изменяет
       *[other] изменяет
    } { $attribute } на { $positive ->
        [true] [color=red]{ $amount }[/color]
       *[false] [color=green]{ $amount }[/color]
    }
entity-effect-guidebook-plant-cryoxadone =
    { $chance ->
        [1] Омолаживает
       *[other] омолаживает
    } растение в зависимости от возраста растения и времени роста
entity-effect-guidebook-plant-phalanximine =
    { $chance ->
        [1] Восстанавливает
       *[other] восстанавливает
    } жизнеспособность растению, ставшему нежизнеспособным из-за мутации
entity-effect-guidebook-plant-diethylamine =
    { $chance ->
        [1] Увеличивает
       *[other] увеличивает
    } продолжительность жизни и/или базовое здоровье растения с вероятностью 10% для каждого
entity-effect-guidebook-plant-robust-harvest =
    { $chance ->
        [1] Увеличивает
       *[other] увеличивает
    } эффективность растения на { $increase } до максимума { $limit }. Вызывает потерю семян растением, когда эффективность достигает { $seedlesstreshold }. Попытка добавить эффективность свыше { $limit } может вызвать уменьшение урожая с вероятностью 10%
entity-effect-guidebook-plant-seeds-add =
    { $chance ->
        [1] Восстанавливает
       *[other] восстанавливает
    } семена растения
entity-effect-guidebook-plant-seeds-remove =
    { $chance ->
        [1] Удаляет
       *[other] удаляет
    } семена растения
entity-effect-guidebook-plant-mutate-chemicals =
    { $chance ->
        [1] Мутирует
       *[other] мутирует
    } растение для производства { $name }
