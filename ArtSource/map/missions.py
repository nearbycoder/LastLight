"""The twelve nights of Last Light. Writes Assets/Resources/Data/missions.json.

Each night: which hazards exist, the ship schedule (time, type, route, name, captain, hail),
fog banks, storm, wreckers, scripted radio and onboarding hints. Routes and sites are defined in
merrow_bay.py.

    python3 ArtSource/map/missions.py
"""
import json
import os

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Assets", "Resources", "Data", "missions.json")


def ship(t, type_, route, name, captain="", hail="", damaged=False):
    return dict(t=t, type=type_, route=route, name=name, captain=captain, hail=hail, damaged=damaged)


def radio(who, text, t=-1, on=""):
    return dict(t=t, on=on, who=who, text=text)


def hint(id_, t=-1, on=""):
    return dict(id=id_, t=t, on=on)


MISSIONS = []

# ---------------------------------------------------------------- Night 1
MISSIONS.append(dict(
    id="first_watch", night=1, title="First Watch", date="Monday, 2nd October",
    briefing="Three boats out of Porthkell tonight. Clear sky, easy sea. Keep them in your light and they'll keep their heads.",
    newThing="aim", allowedWrecks=1, drainScale=0.85,
    ships=[
        ship(5, "trawler", "n2_harbor", "Little Auk", "maren", "Gannet Head, Little Auk. Coming home with a full hold. Light us in, keeper?"),
        ship(34, "trawler", "harbor_e2", "Kittiwake", "", "Kittiwake leaving Porthkell. Heading east for the morning."),
        ship(52, "trawler", "w2_e2", "Saint Brannoc", "", "Saint Brannoc passing along the coast. Evening, Gannet Head."),
        ship(80, "trawler", "e2_harbor", "Dunlin", "", "Dunlin, inbound from Corley. Long night."),
    ],
    radio=[
        radio("ianto", "Evening, keeper. Ianto at the harbour. Quiet one to start you off.", t=1.5),
        radio("board", "LETTER FROM THE BOARD OF LIGHTS: Gannet Head Light will be discontinued at the close of the season. A radio beacon will replace it. Twelve nights remain.", t=15),
        radio("ianto", "Twelve nights. Well. Let's make them count.", t=26),
        radio("ianto", "A boat in the dark loses its nerve. Get your light on her.", on="firstLost"),
        radio("ianto", "That's the lot. Porthkell sleeps sound. Goodnight, keeper.", on="end"),
    ],
    hints=[hint("aim", t=2), hint("ships", on="firstEntered")],
))

# ---------------------------------------------------------------- Night 2
MISSIONS.append(dict(
    id="the_teeth", night=2, title="The Teeth", date="Tuesday, 3rd October",
    briefing="Boats coming down past the Merrow Teeth. The reefs are under the water; sweep your light ahead of them so the captains can see the rocks in time.",
    newThing="chart", allowedWrecks=1, drainScale=0.9, reefGroups=["teeth"],
    ships=[
        ship(4, "trawler", "n3_harbor", "Little Auk", "maren", "Little Auk again, keeper. Coming down past the Teeth. Mind them for me?"),
        ship(30, "trawler", "e1_harbor", "Guillemot", "", "Guillemot inbound from the east."),
        ship(56, "trawler", "n3_e1", "Fulmar", "", "Fulmar, bound for Corley the long way round."),
        ship(78, "steamer", "e1_harbor", "SS Calloway", "pryce", "Gannet Head, this is the Calloway. Pryce. I know these rocks better than you do, but light them anyway."),
        ship(100, "trawler", "n3_harbor", "Shearwater", "", "Shearwater coming down from the north."),
    ],
    radio=[
        radio("ianto", "The Teeth are out there, keeper. Underwater. A captain only steers round what he can see.", t=1.5),
        radio("maren", "Thanks, keeper! I can see the white water now.", on="firstCharted"),
        radio("ianto", "Far out? Hold your light steady. It'll reach further.", on="firstFar"),
        radio("pryce", "Hm. Not bad. Not bad at all.", on="end"),
    ],
    hints=[hint("chart", on="firstEntered"), hint("focus", t=70)],
))

# ---------------------------------------------------------------- Night 3
MISSIONS.append(dict(
    id="lantern_row", night=3, title="Lantern Row", date="Thursday, 5th October",
    briefing="Black Hen throws a long shadow your light can't reach. The channel buoys have lamps: light them, and they'll guide the boats a while on their own.",
    newThing="buoy", allowedWrecks=1, reefGroups=["teeth", "hens"],
    buoys=["hen_bell", "sands_n", "fairway", "teeth_bell"],
    ships=[
        ship(4, "trawler", "n1_harbor", "Little Auk", "maren", "Auk here. Coming down behind Black Hen, keeper. Can't see your light from here!"),
        ship(26, "trawler", "w1_harbor", "Razorbill", "", "Razorbill from the west, inbound."),
        ship(44, "steamer", "e1_harbor", "SS Calloway", "pryce", "Calloway. Same rocks as last night. Same keeper, I hope."),
        ship(66, "trawler", "n1_harbor", "Petrel", "", "Petrel, on the Hen's side."),
        ship(84, "trawler", "n3_harbor", "Shearwater", "", "Shearwater coming down past the Teeth."),
        ship(104, "steamer", "w1_e1", "SS Maud Ellen", "", "Maud Ellen, through traffic, eastbound."),
    ],
    radio=[
        radio("ianto", "Light the Hen Bell before the Auk gets there. A burning buoy keeps a boat steady.", t=2),
        radio("maren", "Ha! The bell's lit. I'm alright now, keeper.", on="firstBuoy"),
        radio("maren", "You're the only one awake round here, aren't you?", t=95),
        radio("ianto", "Lantern Row's all lit. Nicely done.", on="end"),
    ],
    hints=[hint("buoy", t=3)],
))


STORM = dict(enabled=True, cx=0.45, cz=-0.55, rain=0.8, lightningMin=9, lightningMax=15)

# ---------------------------------------------------------------- Night 4
MISSIONS.append(dict(
    id="deep_water", night=4, title="Deep Water", date="Friday, 6th October",
    briefing="Colliers tonight. A steamer draws four fathom, and the Long Sands will take her keel where a trawler floats over. Light the sands for the big ships.",
    newThing="shoal", allowedWrecks=1, reefGroups=["teeth", "hens"], shoals=["longsands"],
    buoys=["sands_n", "sands_s", "fairway", "hen_bell"],
    ships=[
        ship(4, "steamer", "n2_harbor", "SS Calloway", "pryce", "Calloway again. Coming down for Porthkell across the Sands. Mind them for me, keeper. I can't."),
        ship(24, "trawler", "w1_harbor", "Razorbill", "", "Razorbill inbound from the west."),
        ship(40, "trawler", "harbor_n2", "Kittiwake", "", "Kittiwake heading out. Fair night for it."),
        ship(58, "steamer", "e1_harbor", "SS Maud Ellen", "", "Maud Ellen, collier, inbound from the east."),
        ship(80, "trawler", "n1_harbor", "Little Auk", "maren", "Auk's coming home behind the Hen. Wave the light, keeper!"),
        ship(102, "steamer", "w1_harbor", "SS Penhallow", "", "Penhallow, loaded deep. Making for Porthkell."),
        ship(124, "trawler", "e2_harbor", "Dunlin", "", "Dunlin coming in along the coast."),
    ],
    radio=[
        radio("ianto", "Long Sands are shifting again. Shallow enough to wade at low water. Light them up before a steamer gets there.", t=2),
        radio("pryce", "Sands lit. Good. I'll go round.", on="firstShoal"),
        radio("ianto", "Steamers turn like a church. Give them warning early, keeper.", on="firstWreck"),
        radio("pryce", "Hm. You'll make a keeper yet.", on="end"),
    ],
    hints=[hint("shoal", on="firstEntered")],
))

# ---------------------------------------------------------------- Night 5
MISSIONS.append(dict(
    id="sea_fret", night=5, title="Sea Fret", date="Saturday, 7th October",
    briefing="Sea fret rolling in off the water. Your light won't carry through it unless you hold it narrow, and the boats will lose their nerve quick. Sound the horn if they're lost.",
    newThing="fog", allowedWrecks=1, foghorn=True, haze=1.5, reefGroups=["teeth", "collar"], buoys=["collar", "teeth_bell"],
    fog=[dict(x=-60, z=70, r=34, density=1.0, vx=1.4, vz=0.0), dict(x=30, z=50, r=30, density=1.0, vx=1.1, vz=0.2),
         dict(x=95, z=85, r=36, density=0.9, vx=1.0, vz=-0.1), dict(x=-110, z=20, r=28, density=0.9, vx=1.3, vz=0.1)],
    ships=[
        ship(4, "trawler", "n3_harbor", "Little Auk", "maren", "Auk here. Can't see my own bow, keeper. Keep that light coming."),
        ship(22, "trawler", "e1_harbor", "Shearwater", "", "Shearwater in the fret, coming down from the east."),
        ship(40, "steamer", "w1_e1", "SS Hesper", "", "Hesper, eastbound. Thick as porridge out here."),
        ship(62, "trawler", "harbor_e2", "Kittiwake", "", "Kittiwake going out. Lord knows why."),
        ship(80, "trawler", "n2_harbor", "Guillemot", "", "Guillemot, inbound, blind as a mole."),
        ship(100, "steamer", "e1_harbor", "SS Calloway", "pryce", "Calloway in the fret. I'll take your horn over your light tonight, keeper."),
        ship(126, "trawler", "n3_e1", "Fulmar", "", "Fulmar, passing north of the Teeth."),
    ],
    radio=[
        radio("ianto", "Fret's thick. Hold the light narrow to punch through it. And when they lose you, give them the horn.", t=2),
        radio("maren", "I heard you! I know where I am now!", on="firstLost"),
        radio("ianto", "Silhouettes in the fog. That's a sight. Good work.", on="end"),
    ],
    hints=[hint("focus", t=5)],
))

# ---------------------------------------------------------------- Night 6
MISSIONS.append(dict(
    id="evening_star", night=6, title="The Evening Star", date="Sunday, 8th October",
    briefing="The Evening Star's running a late crossing: forty passengers and the Porthkell brass band. Bring her in with everyone else, and don't let her near the Ledge.",
    newThing="ferry", allowedWrecks=1, reefGroups=["teeth", "widow", "collar"], shoals=["longsands"],
    buoys=["widow", "fairway", "collar", "sands_n"],
    ships=[
        ship(4, "ferry", "e2_harbor", "Evening Star", "dot", "Evening Star, out of Corley with forty souls and a brass band! Evening, Gannet Head!"),
        ship(22, "trawler", "n1_harbor", "Little Auk", "maren", "Auk again. Is that the band I can hear?"),
        ship(40, "steamer", "harbor_e2", "SS Maud Ellen", "", "Maud Ellen departing east."),
        ship(58, "trawler", "w2_e2", "Saint Brannoc", "", "Saint Brannoc along the coast, eastbound."),
        ship(76, "trawler", "e1_harbor", "Puffin", "", "Puffin inbound from the east."),
        ship(96, "ferry", "harbor_e2", "Evening Star", "dot", "Evening Star again, going back out! The band wants to play for the lighthouse!"),
        ship(116, "steamer", "n2_harbor", "SS Calloway", "pryce", "Calloway. Will someone tell that ferry to stop playing the trombone."),
        ship(138, "trawler", "e2_w2", "Curlew", "", "Curlew, westbound along the shore."),
    ],
    radio=[
        radio("ianto", "Ferry's worth more than the rest put together, keeper. In every way.", t=2),
        radio("dot", "Everyone wave at the lighthouse! Go on!", t=30),
        radio("dot", "Ooh, we're all lit up! Lovely!", on="firstCharted"),
        radio("dot", "Home safe. Three cheers for the keeper! Hip hip!", on="end"),
    ],
    hints=[hint("buoy", t=60)],
))

# ---------------------------------------------------------------- Night 7
MISSIONS.append(dict(
    id="mayday", night=7, title="Mayday", date="Tuesday, 10th October",
    briefing="The Calloway's dynamo has failed: no lamps, and she's shipping water somewhere off the Teeth. You'll not see her till your light finds her. Watch for the flares.",
    newThing="damaged", allowedWrecks=1, haze=1.2, reefGroups=["teeth", "widow", "hens"], shoals=["longsands"],
    buoys=["teeth_bell", "hen_bell", "fairway"],
    fog=[dict(x=60, z=70, r=26, density=0.6, vx=0.6, vz=0.0)],
    ships=[
        ship(4, "steamer", "n3_harbor", "SS Calloway", "pryce", "Gannet Head, Calloway. We've lost the dynamo. No lamps, taking water. North of the Teeth, I think. Find us.", damaged=True),
        ship(20, "trawler", "w1_harbor", "Razorbill", "", "Razorbill inbound. Is that the Calloway in trouble?"),
        ship(38, "trawler", "harbor_n2", "Little Auk", "maren", "Auk going out to stand by the Calloway. Don't lose me too!"),
        ship(60, "trawler", "e1_harbor", "Dunlin", "", "Dunlin inbound from the east."),
        ship(80, "ferry", "w1_e1", "Evening Star", "dot", "Evening Star, passing. Thinking of you, Calloway!"),
        ship(102, "trawler", "e2_harbor", "Guillemot", "", "Guillemot. Something dark on the water off Widow's Ledge, keeper. No lamps at all.", damaged=True),
        ship(124, "steamer", "w2_e2", "SS Hesper", "", "Hesper, eastbound along the coast."),
        ship(146, "trawler", "n1_harbor", "Petrel", "", "Petrel inbound behind the Hen."),
    ],
    radio=[
        radio("ianto", "No lamps on her, keeper. Sweep the dark till you find her, and keep her lit.", t=3),
        radio("pryce", "That's a flare gone up. Look for it, keeper!", on="firstFlare"),
        radio("pryce", "...Obliged to you. Truly.", on="end"),
    ],
    hints=[hint("flare", t=8)],
))

# ---------------------------------------------------------------- Night 8
MISSIONS.append(dict(
    id="squall", night=8, title="Squall", date="Thursday, 12th October",
    briefing="Glass is falling fast. The current runs hard onto the rocks in a blow, and the rain will cut your reach. The lightning's your friend tonight: it shows the reefs.",
    newThing="storm", allowedWrecks=2, haze=1.3, storm=STORM, foghorn=True,
    reefGroups=["teeth", "widow", "collar", "hens"], shoals=["longsands"], buoys=["collar", "hen_bell", "teeth_bell", "fairway"],
    ships=[
        ship(4, "trawler", "n2_harbor", "Little Auk", "maren", "This is no night for a little boat, keeper. Running for home!"),
        ship(18, "trawler", "e2_harbor", "Kittiwake", "", "Kittiwake running for shelter!"),
        ship(34, "steamer", "e1_harbor", "SS Maud Ellen", "", "Maud Ellen, making for Porthkell, heavy weather."),
        ship(52, "trawler", "w1_harbor", "Razorbill", "", "Razorbill in from the west, taking it green over the bow."),
        ship(70, "ferry", "harbor_e2", "Evening Star", "dot", "Evening Star must sail, keeper, mail and all. Hold on to your hats!"),
        ship(92, "trawler", "n3_harbor", "Shearwater", "", "Shearwater coming down past the Teeth. Can't see a thing between the flashes."),
        ship(112, "steamer", "w2_e2", "SS Penhallow", "", "Penhallow eastbound. The current's pushing us shoreward."),
        ship(132, "trawler", "e1_harbor", "Fulmar", "", "Fulmar inbound."),
        ship(150, "steamer", "n1_harbor", "SS Calloway", "pryce", "Calloway, coming home through the worst of it. Light on, keeper."),
    ],
    radio=[
        radio("ianto", "Storm's on us. The current will push them onto the rocks if they lose their way.", t=2),
        radio("ianto", "Every flash of lightning shows the reefs. Use it.", on="firstWreck"),
        radio("maren", "I saw the Teeth in the lightning! Ugly things.", t=50),
        radio("pryce", "Hell of a night. Hell of a light.", on="end"),
    ],
    hints=[hint("storm", t=5)],
))

# ---------------------------------------------------------------- Night 9
MISSIONS.append(dict(
    id="false_light", night=9, title="False Light", date="Saturday, 14th October",
    briefing="Word from Corley: lanterns seen on the cliffs at night. The Corley brothers, wrecking again. A captain will steer for any light that turns like yours. Put your beam on theirs and they'll douse it.",
    newThing="wrecker", allowedWrecks=1, reefGroups=["widow", "teeth", "collar"], shoals=["cocklebank"],
    buoys=["widow", "teeth_bell", "collar"],
    wreckers=[dict(sites=["corley", "corley"], start=34, relight=24, sweep=16)],
    ships=[
        ship(4, "trawler", "e2_harbor", "Dunlin", "", "Dunlin inbound along the east coast."),
        ship(22, "trawler", "n3_harbor", "Little Auk", "maren", "Auk here. Someone's playing silly devils on the Corley cliffs, keeper."),
        ship(40, "steamer", "e2_w2", "SS Hesper", "", "Hesper, westbound along the coast."),
        ship(58, "trawler", "e1_harbor", "Puffin", "", "Puffin inbound from the east."),
        ship(76, "ferry", "e2_harbor", "Evening Star", "dot", "Evening Star coming in from Corley. Which of those lights is yours, keeper?"),
        ship(98, "trawler", "harbor_e2", "Kittiwake", "", "Kittiwake going east."),
        ship(118, "steamer", "e2_harbor", "SS Calloway", "pryce", "Calloway. I've heard about the Corleys. Keep your eye on them."),
        ship(138, "trawler", "w2_e2", "Saint Brannoc", "", "Saint Brannoc, eastbound."),
        ship(156, "trawler", "e2_w2", "Morwenna", "", "Morwenna, westbound."),
    ],
    radio=[
        radio("ianto", "Watch the cliffs at Corley Cove. If a light shows that isn't yours, hold your beam on it.", t=2),
        radio("ianto", "There! A false light at Corley. Put your beam on it!", on="firstWrecker"),
        radio("ianto", "That's sent them running. They'll be back, mind.", on="firstDoused"),
        radio("maren", "They want us on the rocks for the salvage. Wicked.", on="firstLured"),
        radio("ianto", "The Corleys went home empty-handed. Well done, keeper.", on="end"),
    ],
    hints=[],
))

# ---------------------------------------------------------------- Night 10
MISSIONS.append(dict(
    id="two_lights", night=10, title="Two Lights", date="Monday, 16th October",
    briefing="Fret again, and the Corleys have learnt to move. Black Hen one hour, West Point the next. And a letter from the Board, for what it's worth.",
    newThing="", allowedWrecks=2, foghorn=True, haze=1.4, reefGroups=["hens", "teeth", "collar", "outer"], shoals=["longsands"],
    buoys=["hen_bell", "sands_n", "fairway", "collar"],
    fog=[dict(x=-30, z=80, r=30, density=0.9, vx=1.2, vz=0.0), dict(x=60, z=40, r=28, density=0.9, vx=1.0, vz=0.1), dict(x=-120, z=40, r=26, density=0.8, vx=1.4, vz=0.0)],
    wreckers=[dict(sites=["blackhen", "westpoint"], start=26, relight=18, sweep=18)],
    ships=[
        ship(4, "trawler", "n1_harbor", "Little Auk", "maren", "Auk, behind the Hen again. Two lights out here tonight. Which is you?"),
        ship(18, "trawler", "w1_harbor", "Razorbill", "", "Razorbill in from the west."),
        ship(34, "steamer", "w1_e1", "SS Maud Ellen", "", "Maud Ellen, eastbound, far out."),
        ship(52, "trawler", "n2_harbor", "Guillemot", "", "Guillemot inbound."),
        ship(68, "ferry", "w2_e2", "Evening Star", "dot", "Evening Star, along the coast. The passengers love the fog, keeper. I don't."),
        ship(86, "trawler", "harbor_n2", "Kittiwake", "", "Kittiwake out past the Hen."),
        ship(104, "steamer", "n3_harbor", "SS Calloway", "pryce", "Calloway, coming down past the Teeth. Two lights. I'll trust the one I know."),
        ship(122, "trawler", "w1_n3", "Petrel", "", "Petrel, passing north."),
        ship(140, "trawler", "e1_harbor", "Shearwater", "", "Shearwater inbound."),
        ship(158, "steamer", "w1_harbor", "SS Penhallow", "", "Penhallow making for harbour from the west."),
    ],
    radio=[
        radio("board", "The Board notes reports of 'false lights' on the Merrow coast. Such matters will be moot once the radio beacon is commissioned.", t=6),
        radio("ianto", "Moot, he says. Tell that to the Auk.", t=20),
        radio("ianto", "Now West Point. They've a boat to row between them.", on="firstDoused"),
        radio("ianto", "Two lights, and they followed the right one. That's you, keeper.", on="end"),
    ],
    hints=[],
))

# ---------------------------------------------------------------- Night 11
MISSIONS.append(dict(
    id="the_mimic", night=11, title="The Mimic", date="Wednesday, 18th October",
    briefing="The Corleys have a bigger lantern now, on the Sentinels' cliff, and they turn it round and round, just like yours. The captains can't tell you apart unless you're on them.",
    newThing="mimic", allowedWrecks=2, haze=1.1, reefGroups=["teeth", "widow", "collar", "hens", "outer"], shoals=["longsands", "cocklebank"],
    buoys=["collar", "widow", "teeth_bell", "hen_bell", "fairway"],
    wreckers=[dict(sites=["sentinels", "sentinels"], start=20, relight=20, sweep=26, mimic=True), dict(sites=["corley", "blackhen"], start=90, relight=26, sweep=16)],
    ships=[
        ship(4, "trawler", "e2_harbor", "Little Auk", "maren", "Two lights turning, keeper. Swing yours my way so I know."),
        ship(16, "trawler", "w2_e2", "Saint Brannoc", "", "Saint Brannoc, eastbound."),
        ship(30, "steamer", "e1_harbor", "SS Calloway", "pryce", "Calloway. I've rounded this Head four thousand times. I know your light, keeper."),
        ship(46, "trawler", "harbor_e2", "Kittiwake", "", "Kittiwake going east."),
        ship(60, "ferry", "e2_w2", "Evening Star", "dot", "Evening Star, westbound along the cliffs. Ooh, two lighthouses!"),
        ship(76, "trawler", "n3_harbor", "Shearwater", "", "Shearwater coming down past the Teeth."),
        ship(92, "steamer", "e2_harbor", "SS Hesper", "", "Hesper inbound along the coast."),
        ship(108, "trawler", "n2_harbor", "Guillemot", "", "Guillemot inbound."),
        ship(124, "trawler", "e2_w2", "Curlew", "", "Curlew, westbound."),
        ship(140, "steamer", "w1_e1", "SS Maud Ellen", "", "Maud Ellen, eastbound."),
        ship(156, "trawler", "e1_harbor", "Fulmar", "", "Fulmar inbound."),
        ship(172, "ferry", "harbor_e2", "Evening Star", "dot", "Evening Star going back out. Keep the real one shining!"),
    ],
    radio=[
        radio("ianto", "The false one turns like yours. Don't chase it every time. Stay with your ships.", t=3),
        radio("board", "The Board advises the radio beacon will be commissioned at the close of the season, as planned.", t=90),
        radio("pryce", "Told you. I know your light.", on="end"),
    ],
    hints=[],
))

# ---------------------------------------------------------------- Night 12
MISSIONS.append(dict(
    id="last_light", night=12, title="Last Light", date="Friday, 20th October",
    briefing="Last night of the season, keeper. And of course it's the worst one: a full gale, the Corleys out for one last wreck, and every boat in Porthkell at sea. Bring them home.",
    newThing="finale", allowedWrecks=2, haze=1.3, storm=STORM, foghorn=True, finale=True,
    reefGroups=["teeth", "widow", "collar", "hens", "outer"], shoals=["longsands"],
    buoys=["collar", "hen_bell", "teeth_bell", "fairway", "widow"],
    wreckers=[dict(sites=["corley", "blackhen", "westpoint"], start=40, relight=18, sweep=18)],
    ships=[
        ship(4, "trawler", "n2_harbor", "Little Auk", "maren", "Last night, keeper! Auk's coming home, and she's staying home."),
        ship(16, "trawler", "e2_harbor", "Kittiwake", "", "Kittiwake running for shelter."),
        ship(30, "ferry", "e1_harbor", "Evening Star", "dot", "Evening Star! We've lost the lamps and we're taking water! Forty souls aboard, keeper!", damaged=True),
        ship(46, "trawler", "w1_harbor", "Razorbill", "", "Razorbill in from the west."),
        ship(60, "steamer", "n3_harbor", "SS Maud Ellen", "", "Maud Ellen coming down past the Teeth."),
        ship(76, "trawler", "harbor_n2", "Little Auk", "maren", "Auk going back out. Someone has to stand by the Star.", ),
        ship(92, "steamer", "w2_e2", "SS Penhallow", "", "Penhallow eastbound."),
        ship(108, "trawler", "n1_harbor", "Petrel", "", "Petrel behind the Hen."),
        ship(124, "trawler", "e2_w2", "Dunlin", "", "Dunlin westbound."),
        ship(140, "steamer", "w1_e1", "SS Hesper", "", "Hesper, far out."),
        ship(156, "trawler", "n3_harbor", "Shearwater", "", "Shearwater coming down."),
        ship(176, "steamer", "e2_harbor", "SS Calloway", "pryce", "Calloway, coming home for the last time on your light, keeper. Let's do it properly."),
    ],
    radio=[
        radio("ianto", "Everyone's out, keeper. Last night, and the worst one. Bring them home.", t=2),
        radio("dot", "We can see your light! Keep it on us!", on="firstFlare"),
        radio("corley", "One last wreck, lads. The beacon won't light the cliffs for him.", t=38),
        radio("ianto", "That's the gale blowing itself out. Dawn's coming.", on="end"),
    ],
    hints=[],
))


if __name__ == "__main__":
    for m in MISSIONS:
        for k, v in dict(reefGroups=[], shoals=[], buoys=[], fog=[], wreckers=[], radio=[], hints=[], foghorn=False,
                          haze=1.0, drainScale=1.0, finale=False, newThing="",
                          storm=dict(enabled=False, cx=0, cz=0, rain=0, lightningMin=10, lightningMax=18)).items():
            m.setdefault(k, v)
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w") as f:
        json.dump(dict(missions=MISSIONS), f, indent=1)
    print("wrote", OUT, len(MISSIONS), "nights")
