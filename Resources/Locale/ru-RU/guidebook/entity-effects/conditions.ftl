entity-condition-guidebook-total-damage =
    { $max ->
        [2147483648] у него не менее { NATURALFIXED($min, 2) } общего урона
       *[other]
            { $min ->
                [0] у него не более { NATURALFIXED($max, 2) } общего урона
               *[other] у него от { NATURALFIXED($min, 2) } до { NATURALFIXED($max, 2) } общего урона
            }
    }
entity-condition-guidebook-type-damage =
    { $max ->
        [2147483648] у него не менее { NATURALFIXED($min, 2) } урона типа { $type }
       *[other]
            { $min ->
                [0] у него не более { NATURALFIXED($max, 2) } урона типа { $type }
               *[other] у него от { NATURALFIXED($min, 2) } до { NATURALFIXED($max, 2) } урона типа { $type }
            }
    }
entity-condition-guidebook-group-damage =
    { $max ->
        [2147483648] у него не менее { NATURALFIXED($min, 2) } урона группы { $type }.
       *[other]
            { $min ->
                [0] у него не более { NATURALFIXED($max, 2) } урона группы { $type }.
               *[other] у него от { NATURALFIXED($min, 2) } до { NATURALFIXED($max, 2) } урона группы { $type }
            }
    }
entity-condition-guidebook-total-hunger =
    { $max ->
        [2147483648] у цели не менее { NATURALFIXED($min, 2) } общего голода
       *[other]
            { $min ->
                [0] у цели не более { $max } общего голода
               *[other] у цели от { NATURALFIXED($min, 2) } до { NATURALFIXED($max, 2) } общего голода
            }
    }
entity-condition-guidebook-reagent-threshold =
    { $max ->
        [2147483648] не менее { NATURALFIXED($min, 2) } ед. { $reagent }
       *[other]
            { $min ->
                [0] не более { NATURALFIXED($max, 2) } ед. { $reagent }
               *[other] от { NATURALFIXED($min, 2) } до { NATURALFIXED($max, 2) } ед. { $reagent }
            }
    }
entity-condition-guidebook-mob-state-condition = моб находится в состоянии { $state }
entity-condition-guidebook-job-condition = должность цели - { $job }
entity-condition-guidebook-solution-temperature =
    температура раствора { $max ->
        [2147483648] не менее { NATURALFIXED($min, 2) } К
       *[other]
            { $min ->
                [0] не более { NATURALFIXED($max, 2) } К
               *[other] от { NATURALFIXED($min, 2) } до { NATURALFIXED($max, 2) } К
            }
    }
entity-condition-guidebook-body-temperature =
    температура тела { $max ->
        [2147483648] не менее { NATURALFIXED($min, 2) } К
       *[other]
            { $min ->
                [0] не более { NATURALFIXED($max, 2) } К
               *[other] от { NATURALFIXED($min, 2) } до { NATURALFIXED($max, 2) } К
            }
    }
entity-condition-guidebook-organ-type =
    метаболизирующий орган { $shouldhave ->
        [true] является
       *[false] не является
    } { INDEFINITE($name) } { $name } органом
entity-condition-guidebook-has-tag =
    the target { $invert ->
        [true] does not have
       *[false] has
    } the tag { $tag }
entity-condition-guidebook-this-reagent = this reagent
entity-condition-guidebook-breathing =
    the metabolizer is { $isBreathing ->
        [true] breathing normally
       *[false] suffocating
    }
entity-condition-guidebook-internals =
    the metabolizer is { $usingInternals ->
        [true] using internals
       *[false] breathing atmospheric air
    }
