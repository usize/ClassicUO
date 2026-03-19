# run.py — current intent: survive and defend
# Claude rewrites this file. The engine hot-reloads it automatically.
#
# RULES is evaluated top-to-bottom each tick. First rule that returns True wins.
# Import helpers from lib.reflexes and lib.actions.

from lib.reflexes import defend, recover_mana, survive

RULES = [
    survive,
    defend,
    recover_mana,
]
