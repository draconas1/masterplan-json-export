var MasterplanImport = MasterplanImport || (function() {
    const bookName = "Book About Badgers";
    const importCommand = "import-masterplan"
    const numberOfMultiAttacks = 6  //1-20

    // Utility Functions
    const addAttribute = (charid, attr, value) => {
        return createObj("attribute", {
            name: attr,
            current: value,
            characterid: charid
        });
    }

    const addAttributeWithMax = (charid, attr, value) => {
        return createObj("attribute", {
            name: attr,
            current: value,
            max: value,
            characterid: charid
        });
    }

    const maybeCreateLine = (context, maybeString) => {
        if (maybeString && maybeString !== "") {
            return context + ": " + maybeString + "\n";
        }
        return "";
    }

    const mapAction = (action) => {
        switch(action) {
            case "Standard":
                return "Standard";
            case "Move":
                return "Move";
            case "Minor":
                return "Minor";
            case "Free":
                return "Free Action";
            case "Reaction":
                return "Immediate Reaction";
            case "Interrupt":
                return "Immediate interrupt";
            case "Opportunity":
                return "Opportunity Action";
            default:
                return "No Action";
        }
    }

    const mapUseage = (useage) => {
        switch(useage) {
            case "Basic":
                return "At-Will";
            case "AtWill":
                return "At-Will";
            case "Encounter":
                return "Encounter";
            default:
                return "Daily";
        }
    }

    const mapDefence = (defence) => {
        switch(defence) {
            case "Fortitude" : return "Fort";
            case "Reflex" : return "Ref";
            default: return defence;
        }
    }


    // Main Character sheet populator function
    const createCharacterSheet = (encounterName, characterData, characterSheet) => {
        let id = characterSheet.get('id');
        let charAttr = (attr, value) => addAttribute(id, attr, value);

        // can't access tags in the api, so have to make my own
        charAttr("chr-type", "monster")
        charAttr("encounter", encounterName)

        //charAttr("character_name", characterData.Name);
        charAttr("level", characterData.Level);
        charAttr("class", characterData.Role);
        charAttr("race", characterData.FullTypeDesc);
        addAttributeWithMax(id, "hp", characterData.HP);
        charAttr("hp-bloodied", Math.floor(characterData.HP / 2));
        charAttr("alignment", characterData.Alignment);
        charAttr("size", characterData.Size);
        charAttr("savebonus", characterData.SavingThrowMod);
        charAttr("surge-value", Math.floor(characterData.HP / 4));
        addAttributeWithMax(id, "surges", 1);

        let resistances = "";
        resistances += maybeCreateLine("Immune", characterData.Immune);
        resistances += maybeCreateLine("Resist", characterData.Resist);
        resistances += maybeCreateLine("Vulnerable", characterData.Vulnerable);
        resistances += maybeCreateLine("Regeneration", characterData.Regeneration);
        charAttr("resistances", resistances);

        let halfLevel = Math.floor(characterData.Level / 2);
        let tenHalf = 10 + halfLevel;
        charAttr("ac-class", characterData.AC - tenHalf);
        charAttr("fort-class", characterData.Fortitude - tenHalf);
        charAttr("ref-class", characterData.Reflex - tenHalf);
        charAttr("will-class", characterData.Will - tenHalf);
        charAttr("ac-raw", characterData.AC)

        // initative
        let sheetCalculatedInit = halfLevel + characterData.Dexterity.Modifier
        charAttr("init-misc", characterData.Initiative - sheetCalculatedInit);

        // attributes
        charAttr("strength", characterData.Strength.Score);
        charAttr("constitutiion", characterData.Constitution.Score);
        charAttr("dexterity", characterData.Dexterity.Score);
        charAttr("intelligence", characterData.Intelligence.Score);
        charAttr("wisdom", characterData.Wisdom.Score);
        charAttr("charisma", characterData.Charisma.Score);

        // skills
        _.each(characterData.Skills, function(skill) {
            let skillName = skill.Name.toLowerCase();
            charAttr(skillName + "-trained", 1);
        })


        // other misc
        charAttr("init-special-senses", characterData.Senses);

        if (characterData.MovementDist) { //optional, may not have been parsed correctly
            charAttr("speed-base", characterData.MovementDist);
        }

        charAttr("lang", characterData.Languages);

        let knowledgeDesc = ""
        let templateDesc = (key, value) => "{{" + key + "=" + value + "}}"

        // auruas
        _.each(characterData.Auras, function(aura, index) {
            charAttr("aura-" + index + "-range", aura.Bonus);
            charAttr("aura-" + index + "-name", aura.Name);

            knowledgeDesc += templateDesc("Aura: " + aura.Name, aura.Desc)
        });

        //traits
        let maxTraitIndex = 0;
        _.each(characterData.CreatureTraits, function(trait, index) {
            let traitDetails = trait.PowerCard;
            let traitAttr = "**" + traitDetails.Name + "**: " + traitDetails.Details;
            charAttr("repeating_class-feats_" + index + "_class-feat", traitAttr);
            knowledgeDesc += templateDesc(traitDetails.Name, traitDetails.Details)
            maxTraitIndex = index
        });

        // powers
        _.each(characterData.CreaturePowers, function(powerHolder, index) {
            let powerDesc = "";
            let power = powerHolder.PowerCard;
            let powerAttr = (attr, value) => charAttr("power-" + (index + 1) + "-"  + attr, value);
            powerAttr("toggle", "on");
            powerAttr("name", power.Name);

            // action must exist else it goes into traits
            let action = power.Action;
            powerAttr("action", mapAction(action.Action));
            powerAttr("useage", mapUseage(action.Use));
            powerAttr("range", power.Range);

            powerDesc += mapAction(action.Action) + ", " + mapUseage(action.Use) + ", ";

            let macroStr = "&{template:dnd4epower} "
            if (action.Use == "Encounter") {
                macroStr += "{{encounter=1}}"
            }
            else {
                macroStr += "{{atwill=1}}"
            }
            macroStr += "{{name=" + power.Name + "}}";
            macroStr += "{{action=" + mapAction(action.Action) +" ♦ }}";
            macroStr += "{{trigger=" + action.Trigger + "}}";
            if (action.Trigger != "") {
                powerDesc += "(" + action.Trigger + ") ";
            }

            if (power.Attack) {
                powerAttr("def", mapDefence(power.Attack.Defence));
                
                let conditionDesc = ""
                if (power.Condition != "") {
                    macroStr += "{{requirement=" + power.Condition + "}}";
                    conditionDesc = " (" + power.Condition + ")"
                }

                macroStr += "{{range=" + power.Range +"}}"
                let multiAttack = false
                if (power.Range) {
                    let lc = power.Range.toLowerCase()
                    multiAttack = lc.includes("burst") || lc.includes("blast")
                }
                
                if (multiAttack) {
                    macroStr += "{{multiattacktoggle=[[" + numberOfMultiAttacks + "]]}}"
                    macroStr += "{{attack=[ ]([[1d1]])[[1d20cs>20+" + power.Attack.Bonus +"]] vs **" + power.Attack.Defence + "**}}"
                    let i
                    for (i = 2; i <= numberOfMultiAttacks; i++) {
                        macroStr += "{{multiattack" + i + "=[[1d20cs>20+" + power.Attack.Bonus +"]] vs **" + power.Attack.Defence + "**}}"
                    }
                }
                else {
                    macroStr += "{{attack=[[1d20+" + power.Attack.Bonus +"]] vs **" + power.Attack.Defence + "**}}"
                }
                macroStr += "{{damage=" + power.Details + "}}";

                powerDesc += power.Range + ", " + power.Attack.Bonus + " vs " + power.Attack.Defence + conditionDesc
                // note got to go back to the holder
                if (powerHolder.Damage) {
                    let damage = powerHolder.Damage;
                    let critDamage = damage.NumDice * damage.DiceSize + damage.Bonus;
                    macroStr += "{{critical=" + critDamage + "}}";
                }
            }
            else {
                // triggering actions sometimes end up with descriptions in the range field
                if (!power.Details || power.Details == "") {
                    macroStr += "{{effect=" + power.Range +"}}";
                    powerDesc += power.Range;
                }
                else {
                    macroStr += "{{effect=" + power.Details +"}}";
                    powerDesc += power.Details;
                }
            }


            powerAttr("macro", macroStr);
            createObj("ability", {
                name: power.Name,
                description: "",
                action: "%{selected|-power-" + (index + 1) + "}",
                istokenaction: true,
                characterid: id
            });
            knowledgeDesc += templateDesc(power.Name, powerDesc)
        });

        if (characterData.Tactics) {
            knowledgeDesc += templateDesc("Tactics", characterData.Tactics)
            let traitAttr = "**Tactics**: " + characterData.Tactics;
            let newIndex = maxTraitIndex + 1
            log(maxTraitIndex)
            log(newIndex)
            charAttr("repeating_class-feats_" + newIndex + "_class-feat", traitAttr);
        }

        charAttr("power-knowledge", knowledgeDesc)

        //settings
        charAttr("init-tie", 0.01)
    }

    // Main Trap sheet populator function
    const createTrapSheet = (encounterName, trapData, characterSheet) => {
        let id = characterSheet.get('id');
        let charAttr = (attr, value) => addAttribute(id, attr, value);
        let nullableAttr = (attr, value) => {if (value && value !== "" && !isNaN(value)) {charAttr(attr, value)}};

        // can't access tags in the api, so have to make my own
        charAttr("chr-type", "trap")
        charAttr("encounter", encounterName)
        
        charAttr("level", trapData.Level);
        charAttr("class", trapData.Role);

        // so some traps have defences and HP, and it is possible that i will manually edit the json
        // more likely that i will just manually edit the sheet, but here for completeness
        // the parser will automatically set them based off the description provided the description is written int his format:
        // HP=123
        // AC=123
        // other=123
        // fort=123
        // ref=123
        // will=123
        // 
        // HP=123 must be the first line of the description to trigger this
        // all other lines optional. other=123 will set all non-ac defences that are not set more specifically        
        let halfLevel = Math.floor(trapData.Level / 2);
        let tenHalf = 10 + halfLevel;
        if (trapData.HP) {
            addAttributeWithMax(id, "hp", trapData.HP);
            charAttr("hp-bloodied", Math.floor(trapData.HP / 2));
        }
        nullableAttr("ac-class", trapData.AC - tenHalf);
        nullableAttr("fort-class", trapData.Fortitude - tenHalf);
        nullableAttr("ref-class", trapData.Reflex - tenHalf);
        nullableAttr("will-class", trapData.Will - tenHalf);
        nullableAttr("ac-raw", trapData.AC)
        
        // initative
        let sheetCalculatedInit = halfLevel
        charAttr("init-misc", trapData.Initiative - sheetCalculatedInit);
       
        let knowledgeDesc = ""
        let templateDesc = (key, value) => "{{" + key + "=" + value + "}}"

        charAttr("resistances", trapData.Details);
        charAttr("lang", trapData.Trigger);

        charAttr("trap-details", templateDesc("Details", trapData.Details))
        charAttr("trap-trigger", templateDesc("Trigger", trapData.Trigger))
        
        createObj("ability", {
            name: "Trap Details",
            description: "",
            action: "/w gm &{template:default} {{name=" + trapData.Name + "}} @{selected|trap-details} @{selected|trap-trigger}",
            istokenaction: true,
            characterid: id
        });
        
        // skills
        let trapSkills = "";
        _.each(trapData.Skills, function(skill, index) {
            trapSkills += templateDesc(skill.Name + " " + skill.Bonus, skill.Desc)
            let traitAttr = "**" + skill.Name + "**: DC " + skill.Bonus + ": " + skill.Desc;
            charAttr("repeating_race-feats_" + index + "_race-feat", traitAttr);
        })
        charAttr("trap-skills", trapSkills);
        createObj("ability", {
            name: "Relevant Skills",
            description: "",
            action: "/w gm &{template:default} {{name=Relevant Skills}} @{selected|trap-skills}",
            istokenaction: true,
            characterid: id
        });
        
        //countermeasures
        let trapCountermeasures = ""
        _.each(trapData.Countermeasures, function(countermeasure, index) {
            charAttr("repeating_class-feats_" + index + "_class-feat", countermeasure);
            trapCountermeasures += templateDesc(index + 1, countermeasure)
        });
        charAttr("trap-counters", trapCountermeasures);
        createObj("ability", {
            name: "Countermeasures",
            description: "",
            action: "/w gm &{template:default} {{name=Countermeasures}} @{selected|trap-counters}",
            istokenaction: true,
            characterid: id
        });

        // Attacks
        _.each(trapData.Attacks, function(attackHolder, index) {
            let powerDesc = "";
            let power = attackHolder.Attack;
            let powerAttr = (attr, value) => charAttr("power-" + (index + 1) + "-"  + attr, value);
            powerAttr("toggle", "on");
            powerAttr("name", power.Name);
        
            let action = power.Action;
            powerAttr("action", mapAction(action));
            powerAttr("useage", "At-Will");
            powerAttr("range", power.Range);

            powerDesc += mapAction(action) + ", " + "At-Will" + ", ";

            let macroStr = "&{template:dnd4epower} {{atwill=1}}"
           
            macroStr += "{{name=" + power.Name + "}}";
            macroStr += "{{action=" + mapAction(action) +" ♦ }}";

            if (power.Attack) {
                powerAttr("def", mapDefence(power.Attack.Defence));

                macroStr += "{{range=" + power.Range +"}}"
                macroStr += "{{target=" + power.Target +"}}"
                macroStr += "{{attack=[[1d20+" + power.Attack.Bonus +"]] vs **" + power.Attack.Defence + "**}}";
                macroStr += "{{damage=" + power.OnHit + "}}";
                macroStr += "{{miss=" + power.OnMiss + "}}";
                macroStr += "{{effect=" + power.Effect + "}}";

                powerDesc += power.Range + ", " + power.Attack.Bonus + " vs " + power.Attack.Defence
                // note need to go back to the holder
                if (attackHolder.Damage) {
                    let damage = attackHolder.Damage;
                    let critDamage = damage.NumDice * damage.DiceSize + damage.Bonus;
                    macroStr += "{{critical=" + critDamage + "}}";
                }
            }
            else {
                if (power.Effect || power.Effect != "") {
                    macroStr += "{{effect=" + power.Effect +"}}";
                    powerDesc += power.Effect;
                }
            }


            powerAttr("macro", macroStr);
            createObj("ability", {
                name: power.Name,
                description: "",
                action: "%{selected|-power-" + (index + 1) + "}",
                istokenaction: true,
                characterid: id
            });
            knowledgeDesc += templateDesc(power.Name, powerDesc)
        });

        //settings
        charAttr("init-tie", 0.01)
    }

    // event handler
    // Based off https://app.roll20.net/forum/post/1113190/script-d-and-d-4e-character-importer/?pageforid=1113190#post-1113190
    const handleMessage = (msg) => {
        // Exit if not an api command
        if (msg.type != "api") return;
        // Split the message into command and argument(s)
        let command = MasterplanCommon.parseCommand(msg)

        if(command.command === importCommand){
            if (MasterplanCommon.shouldExitIfNotGM(msg)) {
                return;
            }
            
            let override = false
            if (command.options && command.options.length > 0) {
                if (command.options[0] === "override") {
                    override = true
                }
            }

            MasterplanCommon.chatOutput("It's " + new Date().toISOString() + " and the GM has asked me to look in my book!  Hopefully it's full of badgers.");

            let sourceList = findObjs({ type: 'handout', name: bookName });
            if (sourceList.length < 1) {
                MasterplanCommon.msgGM("I can't find my '" + bookName + "'");
                return;
            }

            let source = sourceList[0];
            MasterplanCommon.debugLog("Fetching the gmnotes")
            source.get("gmnotes", function(gmnotes) {
                MasterplanCommon.debugLog(gmnotes)
                if (gmnotes && gmnotes !== "null") {
                    let parsed = null
                    let overallObject = null
                    try {
                        MasterplanCommon.debugLog("Stripping formatting from GM Notes")
                        parsed = MasterplanCommon.decodeEditorText(gmnotes)
                        MasterplanCommon.debugLog(parsed)
                        MasterplanCommon.debugLog("Running JSON.parse on GM Notes")
                        overallObject = JSON.parse(parsed);
                    } catch (e) {
                        log("Error parsing json: " + e)
                        log("Original GM Notes")
                        log(gmnotes)
                        log("-------")
                        log("GM Notes after the we have stripped html")
                        log(parsed)
                        log("There are some steps on methods to work around it here:")
                        log("https://github.com/draconas1/masterplan-json-export/wiki/Roll20#problems-reading-the-json")
                        MasterplanCommon.chatOutput("Oh Dear.  There was a problem parsing the json from the badger book!  I have logged details in the API Console");
                        return
                    }
                    _.each(overallObject, function(encounter) {
                        MasterplanCommon.msgGM("I'm looking at encounter: " + encounter.Name);

                        _.each(encounter.Creatures, function(creatureData) {
                            // see if the creature already exists
                            MasterplanCommon.debugOutput("Checking for: " + creatureData.Name)
                            let sourceList = findObjs({ type: 'character', name: creatureData.Name });
                            if (sourceList.length > 0 && !override) {
                                MasterplanCommon.msgGM( creatureData.Name + " already exists, so I won't be recreating it");
                            }
                            else {
                                let creature = null;
                                if (sourceList.length > 0) {
                                    creature = sourceList[0]
                                    const toBeDeleted = ['attribute', 'ability']
                                    const elements = _.map(toBeDeleted, function (deleteType) {
                                        return findObjs({
                                            type: deleteType,
                                            characterid: creature.get('id'),
                                        }, {});
                                    })
                                    _.each(elements, function(elementList) {
                                        _.each(elementList, function(element) {
                                            MasterplanCommon.debugLog( "Deleting " + element.get('name') + ' from ' + creatureData.Name);
                                            element.remove();
                                        })
                                    })
                                }
                                else {
                                    MasterplanCommon.debugOutput("Creating: " + creatureData.Name)
                                    creature = createObj('character', {
                                        name: creatureData.Name,
                                        archived: false
                                    });
                                }                               
                                createCharacterSheet(encounter.Name, creatureData, creature);
                                MasterplanCommon.msgGM( "I created " + creatureData.Name);
                            }
                        })
                        if (encounter.Traps) {
                            _.each(encounter.Traps, function(trapData) {
                                // see if the creature already exists
                                MasterplanCommon.debugOutput("Checking for: " + trapData.Name)
                                let sourceList = findObjs({ type: 'character', name: trapData.Name });
                                if (sourceList.length > 0) {
                                    MasterplanCommon.msgGM( trapData.Name + " already exists, so I won't be recreating it");
                                }
                                else {
                                    MasterplanCommon.debugOutput("Creating: " + trapData.Name)
                                    let creature = createObj('character', {
                                        name: trapData.Name,
                                        archived: false
                                    });
                                    createTrapSheet(encounter.Name, trapData, creature);
                                    MasterplanCommon.msgGM( "I created " + trapData.Name);
                                }
                            })
                        }
                    });
                    MasterplanCommon.chatOutput("My book was full of things, but I don't think they were very nice.  Good luck!");
                }
                else {
                    MasterplanCommon.chatOutput("My book was empty.  There were no badgers!");
                }
            })
        }
    }

    return {
        handleMessage
    };
}());

on('chat:message', MasterplanImport.handleMessage);