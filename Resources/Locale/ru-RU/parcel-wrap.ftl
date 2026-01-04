parcel-wrap-verb-wrap = Упаковать
parcel-wrap-verb-unwrap = Распаковать
parcel-wrap-popup-parcel-destroyed = Упаковка, содержащая { THE($contents) }, уничтожена!
parcel-wrap-popup-being-wrapped = { CAPITALIZE(THE($user)) } пытается упаковать вас!
parcel-wrap-popup-being-wrapped-self = Вы начинаете упаковывать себя.
# Shown when parcel wrap is examined in details range
parcel-wrap-examine-detail-uses =
    { $uses ->
        [one] Осталось [color={ $markupUsesColor }]{ $uses }[/color] использований
       *[other] Осталось [color={ $markupUsesColor }]{ $uses }[/color] использований
    }.
