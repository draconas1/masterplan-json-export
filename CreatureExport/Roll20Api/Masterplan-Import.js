var MasterplanImport = MasterplanImport || (function() {
    const bookName = "Book About Badgers";
    const importCommand = "import-masterplan"

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

                macroStr += "{{range=" + power.Range +"}}"
                macroStr += "{{attack=[[1d20+" + power.Attack.Bonus +"]] vs **" + power.Attack.Defence + "**}}";
                macroStr += "{{damage=" + power.Details + "}}";

                powerDesc += power.Range + ", " + power.Attack.Bonus + " vs " + power.Attack.Defence
                // note got to go back to the holder
                if (powerHolder.Damage) {
                    let damage = powerHolder.Damage;
                    let critDamage = damage.NumDice * damage.DiceSize + damage.Bonus;
                    macroStr += (powerHolder.Damage) ? "{{critical=" + critDamage + "}}" : "";
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

    // event handler
    // Based off https://app.roll20.net/forum/post/1113190/script-d-and-d-4e-character-importer/?pageforid=1113190#post-1113190
    const handleMessage = (msg) => {
        // Exit if not an api command
        if (msg.type != "api") return;
        // Split the message into command and argument(s)
        let command = MasterplanCommon.parseCommand(msg).command

        if(command === importCommand){
            if (MasterplanCommon.shouldExitIfNotGM(msg)) {
                return;
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
                    MasterplanCommon.debugLog("Stripping formatting from GM Notes")
                    let parsed = MasterplanCommon.decodeEditorText(gmnotes)
                    MasterplanCommon.debugLog(parsed)
                    MasterplanCommon.debugLog("Running JSON.parse on GM Notes")
                    let overallObject = JSON.parse(parsed);
                    _.each(overallObject, function(encounter) {
                        MasterplanCommon.msgGM("I'm looking at encounter: " + encounter.Name);

                        _.each(encounter.Creatures, function(creatureData) {
                            // see if the creature already exists
                            MasterplanCommon.debugOutput("Checking for: " + creatureData.Name)
                            let sourceList = findObjs({ type: 'character', name: creatureData.Name });
                            if (sourceList.length > 0) {
                                MasterplanCommon.msgGM( creatureData.Name + " already exists, so I won't be recreating it");
                            }
                            else {
                                MasterplanCommon.debugOutput("Creating: " + creatureData.Name)
                                let creature = createObj('character', {
                                    name: creatureData.Name,
                                    archived: false
                                });
                                createCharacterSheet(encounter.Name, creatureData, creature);
                                MasterplanCommon.msgGM( "I created " + creatureData.Name);
                            }
                        })
                    });
                    MasterplanCommon.chatOutput("My book was full of things, but I don't think they were very nice.  Good luck!");
                }
                else {
                    MasterplanCommon.chatOutput("My book was empty.  Thee were no badgers!");
                }
            })
        }
    }

    return {
        handleMessage
    };
}());

on('chat:message', MasterplanImport.handleMessage);