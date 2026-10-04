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
