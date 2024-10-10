interaction-LookAt-name = Посмотреть  
interaction-LookAt-description = Уставьтесь в пустоту и увидьте, как она смотрит на вас в ответ.  
interaction-LookAt-success-self-popup = Вы смотрите на {THE($target)}.  
interaction-LookAt-success-target-popup = Вы чувствуете, как {THE($user)} смотрит на вас...  
interaction-LookAt-success-others-popup = {THE($user)} смотрит на {THE($target)}.  

interaction-Hug-name = Обнять  
interaction-Hug-description = Одно объятие в день, может вылечить психологическую травму.  
interaction-Hug-success-self-popup = Вы обнимаете {THE($target)}.  
interaction-Hug-success-target-popup = {THE($user)} обнимает вас.  
interaction-Hug-success-others-popup = {THE($user)} обнимает {THE($target)}.  

interaction-Pet-name = Погладить  
interaction-Pet-description = Погладьте вашего коллегу, чтобы облегчить его стресс.  
interaction-Pet-success-self-popup = Вы гладите {THE($target)} по {POSS-ADJ($target)} голове.  
interaction-Pet-success-target-popup = {THE($user)} гладит вас по {POSS-ADJ($target)} голове.  
interaction-Pet-success-others-popup = {THE($user)} гладит {THE($target)}.  

interaction-KnockOn-name = Постучать  
interaction-KnockOn-description = Постучите по цели, чтобы привлечь внимание.  
interaction-KnockOn-success-self-popup = Вы стучите по {THE($target)}.  
interaction-KnockOn-success-target-popup = {THE($user)} стучит по вам.  
interaction-KnockOn-success-others-popup = {THE($user)} стучит по {THE($target)}.  

interaction-Rattle-name = Потрясти  
interaction-Rattle-success-self-popup = Вы трясете {THE($target)}.  
interaction-Rattle-success-target-popup = {THE($user)} трясет вас.  
interaction-Rattle-success-others-popup = {THE($user)} трясет {THE($target)}.  

interaction-WaveAt-name = Помахать  
interaction-WaveAt-description = Помахать цели. Если вы держите предмет, вы поможете им.  
interaction-WaveAt-success-self-popup = Вы машете {$hasUsed ->
    [false] в сторону {THE($target)}.
    *[true] своим {$used} в сторону {THE($target)}.
}  
interaction-WaveAt-success-target-popup = {THE($user)} машет {$hasUsed ->
    [false] вам.
    *[true] {POSS-PRONOUN($user)} {$used} вам.
}  
interaction-WaveAt-success-others-popup = {THE($user)} машет {$hasUsed ->
    [false] в сторону {THE($target)}.
    *[true] {POSS-PRONOUN($user)} {$used} в сторону {THE($target)}.
}  
