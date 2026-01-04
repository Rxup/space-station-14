comp-kitchen-spike-deny-collect = { CAPITALIZE($this) } уже чем-то занят, сначала закончите срезать мясо!
comp-kitchen-spike-begin-hook-self-other = { CAPITALIZE(THE($victim)) } начинает насаживать { REFLEXIVE($victim) } на { THE($hook) }!
comp-kitchen-spike-begin-hook-other-self = Вы начинаете насаживать { CAPITALIZE(THE($victim)) } на { THE($hook) }!
comp-kitchen-spike-begin-hook-other = { CAPITALIZE(THE($user)) } начинает насаживать { CAPITALIZE(THE($victim)) } на { THE($hook) }!
comp-kitchen-spike-hook-self = Вы бросились на { THE($hook) }!
comp-kitchen-spike-hook-self-other = { CAPITALIZE(THE($victim)) } бросил{ GENDER($victim) ->
        [female] ась
       *[other] ся
    } на { THE($hook) }!
comp-kitchen-spike-hook-other-self = Вы бросили { CAPITALIZE(THE($victim)) } на { THE($hook) }!
comp-kitchen-spike-hook-other = { CAPITALIZE(THE($user)) } бросил { CAPITALIZE(THE($victim)) } на { THE($hook) }!
comp-kitchen-spike-begin-unhook-self = Вы начинаете снимать себя с { THE($hook) }!
comp-kitchen-spike-begin-unhook-self-other = { CAPITALIZE(THE($victim)) } начинает снимать { REFLEXIVE($victim) } с { THE($hook) }!
comp-kitchen-spike-begin-unhook-other-self = Вы начинаете снимать { CAPITALIZE(THE($victim)) } с { THE($hook) }!
comp-kitchen-spike-begin-unhook-other = { CAPITALIZE(THE($user)) } начинает снимать { CAPITALIZE(THE($victim)) } с { THE($hook) }!
comp-kitchen-spike-unhook-self = Вы сняли себя с { THE($hook) }!
comp-kitchen-spike-unhook-self-other = { CAPITALIZE(THE($victim)) } снял{ GENDER($victim) ->
        [female] ась
       *[other] ся
    } с { THE($hook) }!
comp-kitchen-spike-unhook-other-self = Вы сняли { CAPITALIZE(THE($victim)) } с { THE($hook) }!
comp-kitchen-spike-unhook-other = { CAPITALIZE(THE($user)) } снял { CAPITALIZE(THE($victim)) } с { THE($hook) }!
comp-kitchen-spike-begin-butcher-self = Вы начинаете разделывать { THE($victim) }!
comp-kitchen-spike-begin-butcher = { CAPITALIZE(THE($user)) } начинает разделывать { THE($victim) }!
comp-kitchen-spike-butcher-self = Вы разделали { THE($victim) }!
comp-kitchen-spike-butcher = { CAPITALIZE(THE($user)) } разделал { THE($victim) }!
comp-kitchen-spike-unhook-verb = Снять
comp-kitchen-spike-hooked = [color=red]{ CAPITALIZE(THE($victim)) } насажен на этот крюк![/color]
comp-kitchen-spike-deny-butcher = { CAPITALIZE($victim) } не может быть разделан на { $this }.
comp-kitchen-spike-victim-examine = [color=orange]{ CAPITALIZE(SUBJECT($target)) } выглядит довольно худым.[/color]
comp-kitchen-spike-deny-butcher-knife = { CAPITALIZE($victim) } не может быть разделан на { $this }, используйте нож для разделки.
comp-kitchen-spike-deny-not-dead =
    { CAPITALIZE($victim) } не может быть разделан. { CAPITALIZE(SUBJECT($victim)) } { GENDER($victim) ->
        [male] ещё жив
        [female] ещё жива
        [epicene] ещё живы
       *[neuter] ещё живо
    }!
comp-kitchen-spike-begin-hook-victim = { CAPITALIZE($user) } начинает насаживать вас на { $this }!
comp-kitchen-spike-begin-hook-self = Вы начинаете насаживать себя на { $this }!
comp-kitchen-spike-kill = { CAPITALIZE($user) } насаживает { $victim } на мясной крюк, тем самым убивая { SUBJECT($victim) }!
comp-kitchen-spike-suicide-other = { CAPITALIZE($victim) } бросается на мясной крюк!
comp-kitchen-spike-suicide-self = Вы бросаетесь на мясной крюк!
comp-kitchen-spike-knife-needed = Вам нужен нож для этого.
comp-kitchen-spike-remove-meat = Вы срезаете немного мяса с { $victim }.
comp-kitchen-spike-remove-meat-last = Вы срезаете последний кусок мяса с { $victim }!
comp-kitchen-spike-meat-name = мясо { $victim }
