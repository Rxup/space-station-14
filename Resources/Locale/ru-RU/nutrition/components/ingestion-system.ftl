### Interaction Messages


# System


## When trying to ingest without the required utensil... but you gotta hold it

ingestion-you-need-to-hold-utensil = Вам нужно держать { INDEFINITE($utensil) } { $utensil }, чтобы съесть это!
ingestion-try-use-is-empty = { CAPITALIZE(THE($entity)) } пуст!
ingestion-try-use-wrong-utensil = Вы не можете { $verb } { THE($food) } с помощью { INDEFINITE($utensil) } { $utensil }.
ingestion-remove-mask = Сначала нужно снять { $entity }.

## Failed Ingestion

ingestion-you-cannot-ingest-any-more = Вы больше не можете { $verb }!
ingestion-other-cannot-ingest-any-more = { CAPITALIZE(SUBJECT($target)) } больше не может { $verb }!
ingestion-cant-digest = Вы не можете переварить { THE($entity) }!
ingestion-cant-digest-other = { CAPITALIZE(SUBJECT($target)) } не может переварить { THE($entity) }!

## Action Verbs, not to be confused with Verbs

ingestion-verb-food = Есть
ingestion-verb-drink = Пить

# Edible Component

edible-nom = Ням. { $flavors }
edible-nom-other = Ням.
edible-slurp = Хлюп. { $flavors }
edible-slurp-other = Хлюп.
edible-swallow = Вы проглатываете { THE($food) }
edible-gulp = Глоток. { $flavors }
edible-gulp-other = Глоток.
edible-has-used-storage = Вы не можете { $verb } { THE($food) } с предметом внутри.

## Nouns

edible-noun-edible = съедобное
edible-noun-food = еда
edible-noun-drink = напиток
edible-noun-pill = таблетка

## Verbs

edible-verb-edible = поглотить
edible-verb-food = съесть
edible-verb-drink = выпить
edible-verb-pill = проглотить

## Force feeding

edible-force-feed = { CAPITALIZE(THE($user)) } пытается заставить вас { $verb } что-то!
edible-force-feed-success = { CAPITALIZE(THE($user)) } заставил вас { $verb } что-то! { $flavors }
edible-force-feed-success-user = Вы успешно накормили { THE($target) }
