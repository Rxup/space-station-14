reagent-effect-guidebook-chem-blood-sucker = { $chance ->
    [1] Turns
    *[other] turn
} the metabolizer into a vampire

reagent-effect-guidebook-chem-reroll-psionic = { $chance ->
    [1] Rerolls
    *[other] reroll
} psionic powers

reagent-effect-guidebook-chem-remove-psionic = { $chance ->
    [1] Removes
    *[other] remove
} psionic powers

reagent-effect-guidebook-add-moodlet = { $chance ->
    [1] Modifies
    *[other] modify
} mood by {$amount} for {NATURALFIXED($timeout, 3)} {MANY("second", $timeout)}

reagent-effect-guidebook-chem-cause-disease = { $chance ->
    [1] Causes
    *[other] cause
} the disease {$disease}

reagent-effect-guidebook-chem-miasma-pool = { $chance ->
    [1] Causes
    *[other] cause
} the current miasma pool disease

reagent-effect-guidebook-adjust-consciousness = Adjusts consciousness

reagent-effect-guidebook-change-glimmer-reaction-effect = { $chance ->
    [1] Increases
    *[other] increase
} glimmer by {$count}
