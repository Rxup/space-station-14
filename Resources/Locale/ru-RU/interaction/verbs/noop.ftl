interaction-LookAt-name = Осмотреть
interaction-LookAt-description = Смотри в пустоту и смотри, как она смотрит в ответ..
interaction-LookAt-success-self-popup = Вы смотрите на { THE($target) }.
interaction-LookAt-success-target-popup = Вы чуствуете как { THE($user) } смотрит на...
interaction-LookAt-success-others-popup = { THE($user) } смотрит на { THE($target) }.
interaction-Hug-name = Обнять
interaction-Hug-description = Одно объятие в день избавляет от психологических ужасов, которые находятся за пределами вашего понимания.
interaction-Hug-success-self-popup = Вы обняли { THE($target) }.
interaction-Hug-success-target-popup = { THE($user) } обнял вас.
interaction-Hug-success-others-popup = { THE($user) } обнял { THE($target) }.
interaction-Pet-name = Погладить
interaction-Pet-description = Погладьте своего коллегу, чтобы облегчить его стресс.
interaction-Pet-success-self-popup = Вы гладите { THE($target) } по { POSS-ADJ($target) } голове.
interaction-Pet-success-target-popup = { THE($user) } гладит вас по { POSS-ADJ($target) } головке.
interaction-Pet-success-others-popup = { THE($user) } гладит { THE($target) }.
interaction-PetAnimal-name = { interaction-Pet-name }
interaction-PetAnimal-description = Погладить существо.
interaction-PetAnimal-success-self-popup = { interaction-Pet-success-self-popup }
interaction-PetAnimal-success-target-popup = { interaction-Pet-success-target-popup }
interaction-PetAnimal-success-others-popup = { interaction-Pet-success-others-popup }
interaction-KnockOn-name = Постучать
interaction-KnockOn-description = Постучите по цели, чтобы привлечь к себе внимание.
interaction-KnockOn-success-self-popup = Вы постучали по { THE($target) }.
interaction-KnockOn-success-target-popup = { THE($user) } стучит по тебе.
interaction-KnockOn-success-others-popup = { THE($user) } стучит по { THE($target) }.
interaction-Rattle-name = Трясти
interaction-Rattle-success-self-popup = Вы трясёте { THE($target) }.
interaction-Rattle-success-target-popup = { THE($user) } трясёт вас.
interaction-Rattle-success-others-popup = { THE($user) } трясёт { THE($target) }.
# The below includes conditionals for if the user is holding an item
interaction-WaveAt-name = Помахать
interaction-WaveAt-description = Помашите в сторону знакомого. Если у вас в руках какой-либо предмет, он его увидят.
interaction-WaveAt-success-self-popup =
    Вы машете { $hasUsed ->
        [false] { THE($target) }.
       *[true] вашей { $used } { THE($target) }.
    }
interaction-WaveAt-success-target-popup =
    { THE($user) } машет { $hasUsed ->
        [false] вам.
       *[true] { POSS-PRONOUN($user) } { $used } вам.
    }
interaction-WaveAt-success-others-popup =
    { THE($user) } машет { $hasUsed ->
        [false] { THE($target) }.
       *[true] { POSS-PRONOUN($user) } { $used } { THE($target) }.
    }
