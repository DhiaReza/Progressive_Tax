A tax mod to reduce items price sold through the shipping bin according to 3 factors :
1. How many buildings
2. How many animals
3. How many years has passed

Each factor can be configured through MCM or stright to config.json (if you want some crazy values).

Current goal :
	change given items on each mail to reflec player's tax money
	reduce building built time based on tax

Done :
	Implement mailing for future reward system. OK
	Refactor seasonal_mail.json structur. (change quantity field to bool). and change the code to allow individual and multiple items at single mail call
	Reduce building built time

ignore :
	implement a better reward system according to tax instead of just random stuff given.

Don't want to take things too complicated. This is my first mod. Do things bit by bit.

2 main systems :
1. Tax system (considered done albeit simple one)
2. Reward system (On progress)