entity-effect-guidebook-adjust-traumas =
    { $chance ->
        [1]
            { $deltasign ->
                [1] Применяет
               *[-1] Удаляет
            }
       *[other]
            { $deltasign ->
                [1] применяет
               *[-1] удаляет
            }
    } { NATURALFIXED($amount, 2) } травмы типа { $traumaType }

entity-effect-guidebook-suppress-pain =
    { $chance ->
        [1] Подавляет
       *[other] подавляет
    } боль на { NATURALFIXED($amount, 2) } на { NATURALFIXED($time, 3) } { MANY("секунду", $time) } (максимум до { NATURALFIXED($maximumSuppression, 2) } подавления)

trauma-type-bone-damage = повреждение костей
trauma-type-organ-damage = повреждение органов
trauma-type-veins-damage = повреждение вен
trauma-type-nerve-damage = повреждение нервов
trauma-type-dismemberment = ампутация
trauma-type-unknown = неизвестная травма

target-body-part-head = голова
target-body-part-chest = грудь
target-body-part-groin = пах
target-body-part-left-arm = левая рука
target-body-part-left-hand = левая кисть
target-body-part-right-arm = правая рука
target-body-part-right-hand = правая кисть
target-body-part-left-leg = левая нога
target-body-part-left-foot = левая стопа
target-body-part-right-leg = правая нога
target-body-part-right-foot = правая стопа
target-body-part-left-full-arm = левая рука и кисть
target-body-part-right-full-arm = правая рука и кисть
target-body-part-left-full-leg = левая нога и стопа
target-body-part-right-full-leg = правая нога и стопа
target-body-part-hands = кисти
target-body-part-arms = руки
target-body-part-legs = ноги
target-body-part-feet = стопы
target-body-part-full-arms = полные руки
target-body-part-full-legs = полные ноги
target-body-part-body-middle = середина тела (грудь, пах, руки)
target-body-part-full-legs-groin = ноги и пах

