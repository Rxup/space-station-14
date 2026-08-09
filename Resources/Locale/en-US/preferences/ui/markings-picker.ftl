markings-search = Search
-markings-selection = { $selectable ->
    [0] You have no markings remaining.
    [one] You can select one more marking.
   *[other] You can select { $selectable } more markings.
}
markings-limits = { $required ->
    [true] { $count ->
        [-1] Select at least one marking.
        [0] You cannot select any markings, but somehow, you have to? This is a bug.
        [one] Select one marking.
       *[other] Select at least one marking and up to {$count} markings. { -markings-selection(selectable: $selectable) }
    }
   *[false] { $count ->
        [-1] Select any number of markings.
        [0] You cannot select any markings.
        [one] Select up to one marking.
       *[other] Select up to {$count} markings. { -markings-selection(selectable: $selectable) }
    }
}
markings-reorder = Reorder markings

humanoid-marking-modifier-respect-limits = Respect limits
humanoid-marking-modifier-respect-group-sex = Respect group & sex restrictions
humanoid-marking-modifier-base-layers = Base layers
humanoid-marking-modifier-enable = Enable
humanoid-marking-modifier-prototype-id = Prototype id:

# Categories

markings-organ-Torso = Torso
markings-organ-Head = Head
markings-organ-ArmLeft = Left Arm
markings-organ-ArmRight = Right Arm
markings-organ-HandRight = Right Hand
markings-organ-HandLeft = Left Hand
markings-organ-LegLeft = Left Leg
markings-organ-LegRight = Right Leg
markings-organ-FootLeft = Left Foot
markings-organ-FootRight = Right Foot
markings-organ-Eyes = Eyes

markings-layer-Special = Special
markings-layer-Tail = Tail
markings-layer-Tail-Moth = Wings
markings-layer-Hair = Hair
markings-layer-FacialHair = Facial Hair
markings-layer-UndergarmentTop = Undershirt
markings-layer-UndergarmentBottom = Underpants
markings-layer-Chest = Chest
markings-layer-Head = Head
markings-layer-Snout = Snout
markings-layer-SnoutCover = Snout (Cover)
markings-layer-HeadSide = Head (Side)
markings-layer-HeadTop = Head (Top)
markings-layer-Eyes = Eyes
markings-layer-RArm = Right Arm
markings-layer-LArm = Left Arm
markings-layer-RHand = Right Hand
markings-layer-LHand = Left Hand
markings-layer-RLeg = Right Leg
markings-layer-LLeg = Left Leg
markings-layer-RFoot = Right Foot
markings-layer-LFoot = Left Foot
markings-layer-Overlay = Overlay
markings-layer-TailOverlay = Overlay
markings-used = Используемые черты
markings-unused = Неиспользуемые черты
markings-add = Добавить черту
markings-remove = Убрать черту
markings-rank-up = Вверх
markings-rank-down = Вниз
marking-points-remaining = Черт осталось: { $points }
marking-used = { $marking-name }
marking-used-forced = { $marking-name } (Принудительно)
marking-slot-add = Добавить
marking-slot-remove = Удалить
marking-slot = Слот { $number }
humanoid-marking-modifier-force = Принудительно
humanoid-marking-modifier-ignore-species = Игнорировать расу

# Categories

markings-category-Special = Специальное
markings-category-Hair = Причёска
markings-category-FacialHair = Лицевая растительность
markings-category-Head = Голова
markings-category-HeadTop = Голова (верх)
markings-category-HeadSide = Голова (бок)
markings-category-Snout = Морда
markings-category-SnoutCover = Морда (Покрытие)
markings-category-UndergarmentTop = Нижнее бельё (Верх)
markings-category-UndergarmentBottom = Нижнее бельё (Низ)
markings-category-Chest = Грудь
markings-category-Arms = Руки
markings-category-Legs = Ноги
markings-category-Tail = Хвост
markings-category-Overlay = Наложение

