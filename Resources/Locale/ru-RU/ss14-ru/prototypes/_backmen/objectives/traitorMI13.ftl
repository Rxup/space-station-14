ent-BaseTraitorObjectiveMI13 = { ent-BaseObjective }
    .desc = { ent-BaseObjective.desc }
ent-EscapeShuttleObjectiveMI13 = Улететь на Центком живым и свободным.
    .desc = Один из наших агентов ожидает вас на центкоме, проследите что за вами нет хвоста.
ent-KillRandomHeadObjectiveMI13 = { ent-['BaseTraitorObjectiveMI13', 'BaseKillObjective'] }

  .desc = Нам нужно, чтобы этот глава исчез, и вы, вероятно, знаете, почему. Удачи, оперативник.
ent-BaseTraitorSocialObjectiveMI13 = { ent-['BaseTraitorObjectiveMI13', 'BaseSocialObjective'] }

  .desc = { ent-['BaseTraitorObjectiveMI13', 'BaseSocialObjective'].desc }
ent-RandomTraitorAliveObjectiveMI13 = { ent-['BaseTraitorSocialObjectiveMI13', 'BaseKeepAliveObjective'] }

  .desc = Раскрывать себя или нет — решайте сами. Нам нужно, чтобы он выжил.
ent-RandomTraitorProgressObjectiveMI13 = { ent-['BaseTraitorSocialObjectiveMI13', 'BaseHelpProgressObjective'] }

  .desc = Раскрывать себя или нет — решайте сами. Нам нужно, чтобы он преуспел.