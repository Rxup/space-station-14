ent-PresentBase = present
    .desc = A little box with incredible surprises inside.
ent-Present = { ent-PresentBase }
  .suffix = Empty
  .desc = { ent-PresentBase.desc }

ent-PresentRandomUnsafe = { ent-PresentBase }

  .suffix = Filled, any item
  .desc = { ent-PresentBase.desc }
ent-PresentRandomInsane = { ent-PresentRandomUnsafe }
    .suffix = Filled, any entity
    .desc = { ent-PresentRandomUnsafe.desc }
ent-PresentRandom = { ent-PresentBase }

  .suffix = Filled Safe
  .desc = { ent-PresentBase.desc }
ent-PresentRandomAsh = { ent-PresentBase }

  .suffix = Filled Ash
  .desc = { ent-PresentBase.desc }
ent-PresentRandomCash = { ent-PresentBase }

  .suffix = Filled Cash
  .desc = { ent-PresentBase.desc }
ent-PresentTrash = Wrapping Paper
    .desc = Carefully folded, taped, and tied with a bow. Then ceremoniously ripped apart and tossed on the floor.
