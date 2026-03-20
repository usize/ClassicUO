# run.py — current intent: survive and be social; idle training
# Claude rewrites this file. The engine hot-reloads it automatically.
#
# RULES is evaluated top-to-bottom each tick. First rule that returns True wins.
# Priority order (highest first):
#   survive          — heal / flee on low HP
#   respond_to_player — pause task if a human speaks; escalates for Claude to reply
#   defend           — attack nearest hostile if HP ok
#   recover_mana     — meditate if MP low and no threat
#   <task rules>     — whatever Claude is currently working on

from lib.reflexes import defend, recover_mana, respond_to_player, survive

RULES = [
    survive,
    respond_to_player,
    defend,
    recover_mana,
    # Claude adds task rules below here, e.g.:
    # train_magery,
    # find_vendor,
]
