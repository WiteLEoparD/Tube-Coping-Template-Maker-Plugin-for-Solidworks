# ==============================================================
# WORXLE SOLVER - OPTIMIZED FOR TI-NSPIRE CX II / CX II CAS
# Platform : TI-Python (MicroPython) / Python 3.4+
# Rules    : 6-Letter Wordle variant where the game lies on
#            exactly ONE tile per round.
# ==============================================================
import math
import time

# Precomputed optimal 6-letter openers for WorXle
BEST_OPENER = "slater"
BEST_OPENER_ENTROPY = 8.53
WIN_CODE = 728  # 2 + 2*3 + 2*9 + 2*27 + 2*81 + 2*243
INV_LN2 = 1.4426950408889634  # 1 / ln(2)

# Powers of 3 for base-3 encoding: [1, 3, 9, 27, 81, 243]
POWERS = (1, 3, 9, 27, 81, 243)

# --- Fast Base-3 Feedback Calculation (Zero Allocation) ---
def get_feedback_code(g, t):
    """Returns feedback as integer: 0..728 (sum_{i} c_i * 3^i)."""
    g0, g1, g2, g3, g4, g5 = g[0], g[1], g[2], g[3], g[4], g[5]
    t0, t1, t2, t3, t4, t5 = t[0], t[1], t[2], t[3], t[4], t[5]
    f0 = f1 = f2 = f3 = f4 = f5 = 0
    
    # Green pass (exact matches)
    if g0 == t0: f0 = 2; t0 = ""
    if g1 == t1: f1 = 2; t1 = ""
    if g2 == t2: f2 = 2; t2 = ""
    if g3 == t3: f3 = 2; t3 = ""
    if g4 == t4: f4 = 2; t4 = ""
    if g5 == t5: f5 = 2; t5 = ""
    
    # Yellow pass (misplaced matches)
    if not f0:
        if g0 == t0: f0 = 1; t0 = ""
        elif g0 == t1: f0 = 1; t1 = ""
        elif g0 == t2: f0 = 1; t2 = ""
        elif g0 == t3: f0 = 1; t3 = ""
        elif g0 == t4: f0 = 1; t4 = ""
        elif g0 == t5: f0 = 1; t5 = ""
    if not f1:
        if g1 == t0: f1 = 1; t0 = ""
        elif g1 == t1: f1 = 1; t1 = ""
        elif g1 == t2: f1 = 1; t2 = ""
        elif g1 == t3: f1 = 1; t3 = ""
        elif g1 == t4: f1 = 1; t4 = ""
        elif g1 == t5: f1 = 1; t5 = ""
    if not f2:
        if g2 == t0: f2 = 1; t0 = ""
        elif g2 == t1: f2 = 1; t1 = ""
        elif g2 == t2: f2 = 1; t2 = ""
        elif g2 == t3: f2 = 1; t3 = ""
        elif g2 == t4: f2 = 1; t4 = ""
        elif g2 == t5: f2 = 1; t5 = ""
    if not f3:
        if g3 == t0: f3 = 1; t0 = ""
        elif g3 == t1: f3 = 1; t1 = ""
        elif g3 == t2: f3 = 1; t2 = ""
        elif g3 == t3: f3 = 1; t3 = ""
        elif g3 == t4: f3 = 1; t4 = ""
        elif g3 == t5: f3 = 1; t5 = ""
    if not f4:
        if g4 == t0: f4 = 1; t0 = ""
        elif g4 == t1: f4 = 1; t1 = ""
        elif g4 == t2: f4 = 1; t2 = ""
        elif g4 == t3: f4 = 1; t3 = ""
        elif g4 == t4: f4 = 1; t4 = ""
        elif g4 == t5: f4 = 1; t5 = ""
    if not f5:
        if g5 == t0: f5 = 1; t0 = ""
        elif g5 == t1: f5 = 1; t1 = ""
        elif g5 == t2: f5 = 1; t2 = ""
        elif g5 == t3: f5 = 1; t3 = ""
        elif g5 == t4: f5 = 1; t4 = ""
        elif g5 == t5: f5 = 1; t5 = ""
        
    return f0 + 3*f1 + 9*f2 + 27*f3 + 81*f4 + 243*f5

def get_neighbors(p):
    """Returns the 12 patterns at Hamming distance 1 from pattern p."""
    neigh = []
    temp = p
    for w in POWERS:
        orig = temp % 3
        temp //= 3
        if orig == 0:
            neigh.append(p + w)
            neigh.append(p + 2 * w)
        elif orig == 1:
            neigh.append(p - w)
            neigh.append(p + w)
        else:  # orig == 2
            neigh.append(p - 2 * w)
            neigh.append(p - w)
    return neigh

def parse_feedback(s):
    """Converts 6-char feedback string (e.g. GGY00B) into pattern code."""
    s = s.strip().upper().replace(" ", "")
    if len(s) != 6:
        return None
    mapping = {"0": 0, "B": 0, "_": 0, "Y": 1, "1": 1, "G": 2, "2": 2}
    code = 0
    for i in range(6):
        c = s[i]
        if c not in mapping:
            return None
        code += mapping[c] * POWERS[i]
    return code

def filter_words(guess, obs_code, possible_targets):
    """Filters candidate targets consistent with 1-lie observed feedback."""
    if obs_code == WIN_CODE:
        return [guess]
    obs_neigh = set(get_neighbors(obs_code))
    survivors = []
    for t in possible_targets:
        if t == guess:
            continue
        true_fb = get_feedback_code(guess, t)
        if true_fb in obs_neigh:
            survivors.append(t)
    return survivors

def calculate_entropy(guess, possible_targets, target_set):
    """Calculates Shannon entropy in bits for guess against target pool."""
    N = len(possible_targets)
    if N <= 1:
        return 0.0
    
    fb_counts = {}
    for t in possible_targets:
        if t != guess:
            fb = get_feedback_code(guess, t)
            fb_counts[fb] = fb_counts.get(fb, 0) + 1
    
    lie_counts = {}
    for fb, count in fb_counts.items():
        for nbr in get_neighbors(fb):
            lie_counts[nbr] = lie_counts.get(nbr, 0) + count
    
    entropy = 0.0
    norm = 1.0 / (12.0 * N)
    for c in lie_counts.values():
        p = c * norm
        entropy -= p * (math.log(p) * INV_LN2)
    
    if guess in target_set:
        p_win = 1.0 / N
        if WIN_CODE in lie_counts:
            p_lie = lie_counts[WIN_CODE] * norm
            entropy += p_lie * (math.log(p_lie) * INV_LN2)
            p_tot = p_lie + p_win
            entropy -= p_tot * (math.log(p_tot) * INV_LN2)
        else:
            entropy -= p_win * (math.log(p_win) * INV_LN2)
    
    return entropy

def get_best_guess(pool, possible_targets):
    """Finds optimal guess from candidate pool maximizing entropy."""
    target_set = set(possible_targets)
    best_guess = pool[0]
    best_entropy = -1.0
    
    total = len(pool)
    show_prog = total > 50
    
    for idx, guess in enumerate(pool):
        if show_prog and idx > 0 and idx % 50 == 0:
            print(" ...evaluating " + str(idx) + "/" + str(total))
        e = calculate_entropy(guess, possible_targets, target_set)
        if e > best_entropy:
            best_entropy = e
            best_guess = guess
    
    return best_guess, best_entropy

# --- Embedded Target Word Bank (3,326 Official Solutions) ---
WORDS_DATA = (
    "abroad abrupt absent absorb absurd abused abuser abuses accent accept "
    "access accord accuse aching acidic across acting action active actors "
    "actual addict adding adhere adjust admire admits adopts adored adrift "
    "adults advert advice advise aerial affair affect affirm afford afloat "
    "afraid ageing agency agenda agents agreed agrees aiding ailing aiming "
    "airing airmen airway aisles alarms albeit albino albums alerts aliens "
    "alison allege alleys allied allies allows alloys allure almond almost "
    "alpine alters alumni always amazed amazes ambush amends amidst amount "
    "amused analog anchor anemia angels angled angles animal ankles annals "
    "annoys annual answer anthem antics antifa anyhow anyone anyway aortic "
    "apathy apiece appeal appear apples arabic arcade arched archer arches "
    "arctic ardent arenas argued argues arisen arises armada armies arming "
    "armory armour around arrays arrest arrive arrows artery artist ascend "
    "ascent ashore asking asleep aspect aspire assent assert assess assets "
    "assign assist assume assure asthma astral astray astute asylum atomic "
    "atrium attach attack attain attend attest attire auburn audits august "
    "auntie aurora author autism autumn avatar avenge avenue averse avoids "
    "awaits awaken awards awhile azalea babies backed backup badass badger "
    "badges bagels bagged bailed bakers bakery baking ballad ballet ballot "
    "bamboo banana banded bandit banged banger banish banker banned banner "
    "banter barbed barber barbie barely barges barker barley barons barred "
    "barrel barren barter basalt bashed basics basing basins basket bathed "
    "batted batter battle bazaar beacon beaded beagle beards bearer beasts "
    "beaten beater beauty beaver became become beetle before beggar begged "
    "begins behalf behave behind behold beings belief belong bender bengal "
    "benign beside betray better beware beyond biased biases bibles biceps "
    "bidder bigger biggie bigots bikers biking bikini billed binary binder "
    "biopic biopsy birdie births bishop bistro bitchy biting bitten bitter "
    "blacks blades blamed blames blanco blanks blasts blazer bleach bleeds "
    "blends blight blinds blocks blokes blonde bloods bloody blooms blouse "
    "blower blurry boards boasts bodied bodies bodily boiled boiler boldly "
    "bolted bombed bomber bonded bonnet bonnie boogie booing booked booker "
    "boomer boosts booted booths border boring borrow bosses botany bother "
    "bottle bottom bought bounce bouncy bounds bounty bourne bovine bowels "
    "bowers bowing bowled bowler bowman bowser boxers boxing braced braces "
    "braids brains brakes branch brands brandy braves brazen brazil breach "
    "breaks breath breeds breeze breezy brewed brewer bribed bribes bricks "
    "bridal brides bridge briefs bright brings broken broker bronze brooch "
    "brooks browns browse bruins bruise brunch brutal bubble bubbly bucket "
    "buckle budget buffer buffet bugged builds bullet bummed bummer bumped "
    "bumper bundle bunker burden bureau burger burial buried burned burner "
    "burrow bursts bushes busted buster butler butter button buyers buying "
    "buyout buzzed buzzer bylaws bypass cabins cables cactus cadets cairns "
    "called caller calmed calmer calmly calves camels camera camped camper "
    "campus canals canary cancel cancer candid candle canine canned cannon "
    "cannot canoes canons canopy canton canvas canyon capita capped carbon "
    "cardio career carers caring carney carpet carrot cartel carter carton "
    "carved carver cashed casing casino casket caster castes castle castor "
    "casual catchy caters cattle caucus caught causal caused causes caveat "
    "cavern caviar cavity cayman ceased ceases celebs celery cellar cement "
    "censor census center centre cereal chains chairs chakra chalet champs "
    "chance change chants chapel charge charms charts chased chaser chases "
    "cheats checks cheeks cheeky cheers cheery cheese cheesy cheque cherry "
    "chests chewed chicks chiefs childs chilli chills chilly chimes choice "
    "choked choose choral chords chores chorus chosen chrome chubby chunks "
    "chunky church cigars cinema cipher circle circus cities citing citrus "
    "claims clamps classy clause cleans clears clergy cleric clerks clever "
    "cliche clicks client cliffs climax climbs clinch clinic clique clocks "
    "cloned clones closed closer closes closet cloths clouds cloudy clover "
    "cloves clowns clumsy clutch coarse coasts coated cobalt cocoon coding "
    "coffee coffin cohort coined colder collar colony colors colour column "
    "combat comedy comets comics coming commit common compel comply concur "
    "condom condos confer consul contra convex convey convoy cooked cooker "
    "cookie cooled cooler copied copies coping copper corals cordon corner "
    "corona corpse corpus corral corset cortex cosmic cosmos costly cotton "
    "cougar coughs counts county couple coupon course courts cousin covers "
    "covert coward cowboy coyote cracks cradle crafts crafty cramps cranes "
    "cranky crappy crater crates craven crayon crazed creams creamy crease "
    "create credit creeks creeps creepy creole crimes cringe crises crisis "
    "crisps crispy critic crooks crores crotch crouch crowds crowns cruise "
    "crumbs crunch crusty crying crypto cuckoo cuddle cuddly curate curfew "
    "curing curled curran cursed curses cursor curved curves custom cutest "
    "cutoff cutter cyborg cycles cyclic cystic dagger dainty damage dammit "
    "damned danced dancer dances danger daring darker dashed dating dawned "
    "dazzle deacon deadly dealer dearly deaths debate debris debtor debuts "
    "decade deceit decent decide decked decode decree deduct deemed deepen "
    "deeper deeply defeat defect defend defied defies define degree delays "
    "delete deluge deluxe demand demise demons denial denied denies denote "
    "dental depart depend depict deploy deport depths deputy derail derive "
    "desert design desire desist detail detain detect detour device devils "
    "devise devoid devote devour devout dharma dialed dialog diaper diesel "
    "dieter differ digest digger digits dilute diners dining dinner dipped "
    "direct disarm dishes dismal dismay dispel distal divers divert divide "
    "divine diving doable docked doctor dodged dodger dollar domain domino "
    "donate donkey donors donuts doodle doomed doping dorado dorsal dosage "
    "dotted double doubly doubts douche downed downer dozens drafts dragon "
    "drains dramas draped draper drawer dreams dreamt dreamy dreary dredge "
    "drills drinks driven driver drives drones drying dubbed ducked dugout "
    "dumber dumped duplex duress during dusted duties dwarfs dynamo eagles "
    "earned earths earthy easier easily easing eaters eating echoed echoes "
    "eczema edging edible edited editor effect effort eighth eighty either "
    "elbows elders eldest elects eleven elicit elites elixir emails embark "
    "emblem embody embryo emerge empire employ enable enamel encore ending "
    "endure energy engage engine enigma enjoys enlist enmity enough enrich "
    "enroll ensign ensued ensues ensure entail enters entice entire entity "
    "enzyme equals equate equine equity erased eroded erotic errand errors "
    "escape escort escrow essays estate esteem etched ethics ethnic eulogy "
    "eureka evenly events evoked evokes evolve exceed except excess excise "
    "excite excuse exempt exhale exiled exiles exists exited exodus exotic "
    "expand expect expert expire export expose extant extend extent extras "
    "eyeing eyelid fabric facade facets facial facing factor fading failed "
    "fairer fairly faiths faking falcon fallen family famine famous fandom "
    "farley farmed farmer faster father fathom fatter faucet faults faulty "
    "favors favour feared fedora feeble feeder feisty feline fellas fellow "
    "felony female fenced fences fender ferret fervor fetish feudal fewest "
    "fiance fiasco fibers fibres fickle fiddle fields fierce fiesta fifths "
    "fights figure filing filled filler filmed filter filthy finale finals "
    "finder finely finest finger finish finite firing firmly fiscal fished "
    "fisher fishes fitted fitter fixing flakes flames flanks flared flares "
    "flashy flavor flawed fleece fleets flicks flight flimsy flirty floats "
    "flocks floods floors floppy floral flowed flower fluent fluffy fluids "
    "flurry flyers flying fodder folded folder follow fondly foodie fooled "
    "footed footer forage forbid forced forces forest forged forget forgot "
    "formal format formed former forums fossil foster fought fouled fourth "
    "fowler framed frames franco francs franks frauds freaks freaky freely "
    "freeze french frenzy fresco fresno fridge friend fright fringe fronts "
    "frosty frozen frugal fruits fruity frying fueled fulfil fulham fuller "
    "fumble funded fungal fungus funnel fusion futile future gadget gained "
    "gaines galaxy galley gallon gallop galore gambit gamble gamers gaming "
    "gaping garage garden garlic garner garnet garvey gassed gather gators "
    "gauges gazing geared gender genera geneva genius genome genres gentle "
    "gently gentry german ghetto ghosts giants gifted giggle gilded ginger "
    "giving gladly glance glands glazed glider glitch global globes gloomy "
    "glossy glover gloves gluten goalie goblin golden golfer google gospel "
    "gossip gotcha gotten govern graces graded grader grades grains grange "
    "granny grants grapes graphs grassy grated gravel graves grease greasy "
    "greats greedy greens greets grieve grille grimes gritty grocer groove "
    "groovy ground groups grouse groves grower growth grudge grumpy grunge "
    "guards guests guided guides guilds guilty guinea guitar gunman gunmen "
    "gunned gunner gurney gutted gutter habits hacked hacker hailed haired "
    "halted halves hamlet hammer hamper handed handle hangar hanged hanger "
    "happen harass harbor harden harder hardly harman harmed harper harrow "
    "hassle haters hating hatred hauled haunts having hazard headed header "
    "healed healer health hearth hearts hearty heated heater heaven hectic "
    "hector hedges height helium heller helmet helped helper herald herbal "
    "hereby herein heresy hermit heroes heroic heroin herpes heyday hiatus "
    "hickey hidden hiding higher highly hijack hikers hiking hinder hinged "
    "hinges hinted hippie hiring hitter hobbit hockey hodges holden holder "
    "hollow homage homers honest honors honour hooded hoodie hooked hooker "
    "hookup hooper hooray hoover hoping hopped hopper hordes horned horner "
    "hornet horrid horror horses hosted hostel hotels hotter hottie hounds "
    "hourly housed houses huddle hugely hugged humane humans humble humbly "
    "humour hunger hungry hunted hunter hurdle hurled hurley hustle hybrid "
    "iconic ideals idiots ignite ignore images immune impact impala impart "
    "impede import impose improv incest inches incite income indeed indies "
    "indigo indoor induce infant infect influx inform inhale inject injure "
    "injury inland inmate innate inning inputs insane insect insert inside "
    "insist insult insure intact intake intend intent intern invade invent "
    "invest invite invoke inward iodine ironic isabel island issued issuer "
    "issues itself jacked jacket jagged jagger jaguar jailed jammed jargon "
    "jasper jersey jewels jigsaw jingle jockey johnny joined joints joking "
    "joyful joyous judged juggle juices jumped jumper jungle junior junkie "
    "juries jurors kaiser karate keenly keeper kelvin kennel kernel kettle "
    "kicked kicker kidnap kidney killed killer kinase kinder kindle kindly "
    "kissed kisses kitten knicks knight knives knocks kosher labels labour "
    "lacked ladder ladies lagoon laguna lakers lambda lament lancet landed "
    "lander laptop larger larvae lasers lashed lashes lasted lastly lately "
    "latent latest latter lauded laughs launch laurel lavish lawful lawyer "
    "layers laying layout leader league leaked leaned learns learnt leased "
    "leases leaves ledger legacy legend legged legion lemons lender length "
    "lenses lesion lessen lesser lesson lethal letter levant levels levers "
    "levied levies liable libido licked lifted ligand lights likely liking "
    "lilies limits linden linear liners lineup linger lining linked liquid "
    "liquor listed listen lister liters litres litter little lively livery "
    "living lizard lizzie loaded loader loaned loathe locals locate locked "
    "locker lodged lodges logged lonely longed longer looked loosen looted "
    "losers losing losses lotion louder loudly lounge louvre lovely lovers "
    "loving lowers lowest lumbar lumber luxury lyrics madame madman madras "
    "magnet magnum magnus maiden mailed mainly majors makers makeup making "
    "malice mammal manage maniac manila manned manner mantis mantle mantra "
    "manual manure mapped marble marcel margin marina marine marked marker "
    "market markup marlin maroon marred marrow martyr marvel mascot mashed "
    "masked masons masses master mating matrix matron matter mature mayhem "
    "mayors mcleod meadow meager medals medial median medics medina medium "
    "medley medusa meetup mellon mellow melody melted member memoir memory "
    "menace mental mentor mercer merely merged merger merits messed messes "
    "metals meteor meters method methyl metric midday middle midget midway "
    "mighty milder mildly miller milner mimics minded miners mingle mining "
    "minion minors minute mirage mirror misery misled missed misses mister "
    "misuse mixers mixing mobile mocked models modern modest modify module "
    "molded molten moment monies monkey months mooney morale morals morbid "
    "morgan morgue morons morris morrow mortal mortar mosaic mosque mostly "
    "mother motifs motion motive motley motors mounds mounts mouths movers "
    "movies moving mowing muffin mullen muller murals murder muscle museum "
    "muster mutant mutiny mutton mutual muzzle myriad myrtle myself mystic "
    "mythic nailed namely naming napkin narrow nation native nature nausea "
    "nearby nearer nearly neatly nebula nectar needed needle nellie nelson "
    "nephew nerves nested netted neural neuron newbie newest newton nicely "
    "nicest niches nickel nieces nights nimble ninety ninjas nipple nobles "
    "nobody nodded noises noodle normal notary notice notify noting notion "
    "novels novice nozzle nuance nuclei nudity nugget number nurses nutmeg "
    "obeyed object oblige obtain obtuse occult occupy occurs oceans octane "
    "octave oculus offend offers office offset oilers oldest oldies olives "
    "onions online onward opaque opened opener openly operas opioid oppose "
    "optics opting option oracle orally orange orbits orchid ordeal orders "
    "organs orient origin ornate orphan others otters ounces ousted outage "
    "outcry outfit outing outlaw outlet output outset overly owners owning "
    "oxygen oyster pacers pacing packed packer packet padded paddle padres "
    "paints paired palace palate pallet pandas panels panned pantry papacy "
    "papers parade parcel pardon parent parish parity parked parker parlor "
    "parody parole parrot parted partly passed passer passes pasted pastel "
    "pastor pastry patent patrol patron paused pauses paving payday payers "
    "paying payoff payout peaked peanut pearls pebble pedals peeing peeled "
    "pegged pellet pelvic pelvis pencil penned people pepper perils period "
    "perish permit person petals peters petite petrol pharma phased phases "
    "phobia phoned phones photon photos phrase picked picket pickle pickup "
    "picnic pieces pierce pigeon piling pillar pillow pilots pinned piping "
    "piracy pirate pistol piston pitted pixels pizzas placed places placid "
    "plague plains planar planes planet planks plants plaque plasma plated "
    "plates played player pleads please pledge plenty plight plough plunge "
    "plural pocket podium poetic poetry pointe points pointy poised poison "
    "poking police policy polish polite polled pollen ponder ponies poodle "
    "poorer poorly poplar popped porous portal ported porter posing postal "
    "posted poster potato potent potion potter pounds poured powder powers "
    "praise pranks prayed prayer preach prefer prefix pretty priced prices "
    "pricey priest primal primed primer prince prints priory prison prized "
    "prizes probes profit promos prompt proofs propel proper proton proved "
    "proven proves pseudo psyche psycho public puddle pulled pulpit pulses "
    "pumped pundit punish punter pupils puppet purely purest purged purify "
    "purity purple purses pursue pushed pushes puzzle python quaint quaker "
    "quarry quartz queens quests queues quinoa quirky quorum quotas quoted "
    "quotes rabbis rabbit rabble rabies racers racial racing racism racist "
    "racked racket radial radios radius raffle ragged raging raided raider "
    "rained raised raises raisin rammed rancho random ranged ranger ranges "
    "ranked ransom rapids raping rapist rapper raptor rarely rarest rarity "
    "rascal rather rating ration ratios rattle ravens ravine raving reacts "
    "reader really realms realty reaper reared reason rebate rebels reboot "
    "reborn rebuke recall recent recess recipe recite reckon recoil record "
    "rector redeem reduce reeves refers refill refine reflex reform refuge "
    "refund refuse refute regain regard regent reggae regime regina region "
    "regret reigns reject rejoin relate relays relics relied relief relies "
    "relish relive reload remade remain remake remark remedy remind remote "
    "remove rename render renown rental rented reopen repaid repair repeal "
    "repeat repent replay report repost resale rescue resent reside resign "
    "resist resort rested result resume retail retain retake retina retire "
    "return reused revamp reveal revere revert review revise revive revoke "
    "revolt reward rewind rhinos rhymes rhythm ribbon richer riches richly "
    "ridden riddle riders ridges riding rifles rigged rights ringer ripped "
    "ripper ripple rising risked ritual rivals rivers roared robbed robber "
    "robins robots robust rocked rocker rocket rodent rogers rolled roller "
    "rookie rooted rosary roster rotary rotate rotten rounds routed router "
    "routes rovers rowing royale royals rubbed rubber rubble rubles rudder "
    "rugged ruined rulers ruling rumble rumors rumour runner runoff runway "
    "rupees rushed rushes russel rustic sabres sacked sacred saddle safari "
    "safely safest safety sailed sailor saints salads salary saline saliva "
    "salmon salons saloon salted salute sample sanity santos satire saucer "
    "sauces savage saving savior saying scaled scales scarce scared scares "
    "scenes scenic scents schema scheme schism school scored scorer scores "
    "scotch scouts scrape scraps scream screen screws scribe script scroll "
    "scrubs sealed seaman search season seated seater second secret sector "
    "secure seduce seeded seeing seeker seemed seized seldom select selfie "
    "seller selves senate sender senior sensed sensei senses sensor sentry "
    "septic sequel serene serial series sermon served server serves sesame "
    "settle sevens severe sewage sewers sewing sexism sexist sexual shabby "
    "shaded shades shadow shafts shaggy shaken shaker shakes shaman shamed "
    "shaped shapes shards shared shares sharia sharif sharks shaved sheath "
    "sheets sheikh shells shelly sherry shield shifts shines shirts shiver "
    "shocks shoddy shoots shores shorts shorty should shouts shoved shovel "
    "showed shower shreds shrewd shrimp shrine shrink shroud shrubs shrugs "
    "shrunk sickle sickly siding sighed sights signal signed silent silica "
    "silver simmer simple simply sinful singer single sinned sinner sirens "
    "sister sitcom sitter sixers sizing skater skates sketch skewed skiers "
    "skiing skills skinny skirts skulls slalom slated slater slaves slayer "
    "sleazy sledge sleeps sleepy sleeve sliced slices slider slides slight "
    "slogan slopes sloppy slough slowed slower slowly sludge slutty smacks "
    "smells smelly smiled smiles smiley smiths smoked smoker smokes smokey "
    "smooth snacks snails snakes snappy sneaks sneaky sneeze sniper snitch "
    "soaked soared soccer social socket sodium soften softer softly solace "
    "solder solely solemn solids solved solves somber sonata sooner soothe "
    "sorely sorrow sorted sought sounds source soviet spaced spaces spades "
    "spared spares sparks sparse speaks spears speech speeds speedy spells "
    "spends sphere spiced spices spider spiked spikes spills spinal spines "
    "spiral spirit splash spleen splits spoils spoilt spoken sponge spooky "
    "spoons spores sports sporty spouse sprang sprays spread spring sprint "
    "sprite sprout spruce sprung spying squads square squash squats squire "
    "squirt stable stacks staffs staged stages stains stairs stakes stalks "
    "stalls stamps stance stands stanza staple starch stared stares starry "
    "starts starve stated states static statue status stayed steady steaks "
    "steals steamy stench stereo sticks sticky stigma stiles stills stings "
    "stingy stinks stinky stitch stocks stoked stokes stolen stoned stoner "
    "stones stools stored stores storey storms stormy stoves strain strait "
    "strand straps straws streak stream street stress strewn strict stride "
    "strife strike string stripe strips strive stroke stroll strong struck "
    "strung studio stuffs stuffy stumps stunts stupid sturdy styled styles "
    "stylus subbed subdue submit subset subtle subtly suburb subway sucked "
    "sucker sudden suffer suffix sugars sugary suited suites sulfur sultan "
    "summed summer summit summon sunken sunset superb supper supply surely "
    "surfer surges surrey survey suture swamps swayed swears sweats sweaty "
    "sweeps sweets swells swings switch swords symbol syntax system tables "
    "tablet tackle tactic tagged tailed tailor takers taking talent talked "
    "taller tamara tandem tangle tanker tanned tanner taping tapped target "
    "tariff tarmac tasked tasted tastes tattoo taught tavern taxing teamed "
    "teased teaser techno teller temper temple tenant tended tender tendon "
    "tenets tennis tensor tenure termed terror tested tester texted thanks "
    "theirs themed themes thence theory theres theses thesis thighs things "
    "thingy thinks thinly thirds thirst thirty thorns though thrash thread "
    "threat threes thrice thrift thrill thrive throat throne thrown throws "
    "thrust thumbs thwart ticked ticker ticket tickle tiered tigers tights "
    "tilted timber timely timers timing tinder tinged tinker tinted tipped "
    "tiring tissue titans titled titles todays toggle toilet tokens tomato "
    "tongue tonnes topics topped topper torque tossed totals touchy toured "
    "touted toward towels towers towing toxins traced tracer traces tracks "
    "tracts traded trader trades tragic trails trains traits trance trashy "
    "trauma travel travis treats treaty treble tremor trench trends trendy "
    "trials tribal tribes tricks tricky triple tripod trivia trolls troops "
    "tropes trophy trough troupe trucks trumps trunks trusts trusty truths "
    "trying tubing tucked tucker tumble tumors tumour tundra tuning tunnel "
    "turkey turned turner turret turtle tutors tuxedo tweaks tweets twelve "
    "twenty twists twitch tycoon typing tyrant ulcers umpire unable unborn "
    "uncles undead undone uneasy uneven unfair unfold unholy unions unique "
    "unison united unites unjust unless unlike unload unlock unpack unpaid "
    "unreal unrest unruly unsafe unseen unsure untold untrue unused unveil "
    "unwell unwind unwise upbeat update upheld uphill uphold upkeep upland "
    "uplift upload uproar upsets upside uptake uptown upward urgent urging "
    "usable useful uterus utmost utopia vacant vacate vacuum valley valued "
    "values valves vanish vanity vaping vapour varied varies vastly vaults "
    "vector vegans veggie veiled velvet vendor veneer venues verbal verify "
    "versed verses versus vertex vessel vested vetoed vetted viable victim "
    "victor videos viewed viewer villas violet violin virgin virtue vision "
    "visits visual vocals voiced voices volley volume voodoo vortex voters "
    "voting vowels voyage vulgar wading waffle waging wagons waited waiter "
    "waived waiver waking walked walker walled wallet walnut wander waning "
    "wanted warden warmed warmer warmly warmth warned warner warped warren "
    "warsaw washed washer washes wasted wastes waters watery waving waxing "
    "weaken weaker weakly wealth weapon wearer weasel weaver webcam wedges "
    "weekly weighs weight weiner weirdo welded whales wheels whence whilst "
    "whisky whites wholly whoops whores wicked wicket widely widest widows "
    "wiener wiggle wilder wildly willed willow window winery winged winger "
    "winner winter wiping wiring wisdom wisely wished wishes wither within "
    "wizard wobble wolves wonder wooded wooden woolly worded worked worker "
    "worlds worsen worthy wounds wraith wreath wrecks wrench wright wrists "
    "writer writes wrongs yachts yakuza yearly yelled yellow yields yogurt "
    "youths zenith zipper zodiac zombie zoning"
)
ALL_WORDS = WORDS_DATA.split()
VALID_WORDS_SET = set(ALL_WORDS)

# --- Interactive CLI for TI-Nspire ---
def play():
    print("===============================")
    print("      WORXLE SOLVER (TI-N)")
    print("===============================")
    print("Target pool: " + str(len(ALL_WORDS)) + " words")
    print("Colors: G=Green, Y=Yellow, 0/B=Gray")
    print("Note: Game lies on 1 tile per turn")
    
    possible_targets = list(ALL_WORDS)
    turn = 1
    
    while len(possible_targets) > 1:
        print("\n--- Turn " + str(turn) + " ---")
        print("Remaining words: " + str(len(possible_targets)))
        
        if len(possible_targets) <= 12:
            print("Candidates:")
            print(", ".join(possible_targets))
        
        if turn == 1:
            best_g = BEST_OPENER
            print("Optimal opener: " + BEST_OPENER.upper())
            print("(Entropy: " + str(BEST_OPENER_ENTROPY) + " bits)")
        else:
            t0 = time.time()
            # Evaluate remaining candidates (fast & direct win chance)
            pool = possible_targets
            best_g, entropy = get_best_guess(pool, possible_targets)
            elapsed = time.time() - t0
            print("Suggested guess: '" + best_g.upper() + "'")
            print("(Entropy: " + str(round(entropy, 2)) + " b, " + str(round(elapsed, 2)) + "s)")
        
        # Prompt user for actual guess played
        while True:
            prompt = "Played (Enter for '" + best_g.upper() + "'): "
            ug = input(prompt).strip().lower()
            if not ug:
                user_guess = best_g
                break
            if len(ug) == 6:
                user_guess = ug
                break
            print("Must be 6 letters!")
        
        # Prompt user for feedback from game
        while True:
            fb_str = input("Feedback for " + user_guess.upper() + ": ").strip()
            code = parse_feedback(fb_str)
            if code is not None:
                break
            print("Invalid! Use G, Y, 0 (e.g. GGY00B)")
        
        if code == WIN_CODE:
            print("\n*** SOLVED IN " + str(turn) + " TURNS! ***")
            print("Target word: " + user_guess.upper())
            return
        
        # Filter remaining candidate solutions
        possible_targets = filter_words(user_guess, code, possible_targets)
        turn += 1
    
    if len(possible_targets) == 1:
        print("\n*** SOLVED! ***")
        print("Target word: " + possible_targets[0].upper())
    else:
        print("\n0 words left! Feedback was entered incorrectly.")

if __name__ == "__main__":
    play()
