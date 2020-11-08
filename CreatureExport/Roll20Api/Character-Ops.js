var CharacterOps = CharacterOps || (function () {
    const knowledgeCommand = "know";
    const spendingHealingSurgeCommand = "heal"
    const warforgedResolveCommand = "wf-resolve"
    const longRestCommand = "longrest"
    const shortRestCommand = "shortrest"

    let difficultClass = {
        1: {easy: 8, moderate: 12, hard: 19},
        2: {easy: 9, moderate: 13, hard: 20},
        3: {easy: 9, moderate: 13, hard: 21},
        4: {easy: 10, moderate: 14, hard: 21},
        5: {easy: 10, moderate: 15, hard: 22},
        6: {easy: 11, moderate: 15, hard: 23},
        7: {easy: 11, moderate: 16, hard: 23},
        8: {easy: 12, moderate: 16, hard: 24},
        9: {easy: 12, moderate: 17, hard: 25},
        10: {easy: 13, moderate: 18, hard: 26},
        11: {easy: 13, moderate: 19, hard: 27},
        12: {easy: 14, moderate: 20, hard: 28},
        13: {easy: 14, moderate: 20, hard: 29},
        14: {easy: 15, moderate: 21, hard: 29},
        15: {easy: 15, moderate: 22, hard: 30},
        16: {easy: 16, moderate: 22, hard: 31},
        17: {easy: 16, moderate: 23, hard: 31},
        18: {easy: 17, moderate: 23, hard: 32},
        19: {easy: 17, moderate: 24, hard: 33},
        20: {easy: 18, moderate: 25, hard: 34},
        21: {easy: 19, moderate: 26, hard: 35},
        22: {easy: 20, moderate: 27, hard: 36},
        23: {easy: 20, moderate: 27, hard: 37},
        24: {easy: 21, moderate: 28, hard: 37},
        25: {easy: 21, moderate: 29, hard: 38},
        26: {easy: 22, moderate: 29, hard: 39},
        27: {easy: 22, moderate: 30, hard: 39},
        28: {easy: 23, moderate: 30, hard: 40},
        29: {easy: 23, moderate: 31, hard: 41},
        30: {easy: 24, moderate: 32, hard: 42}
    };

    const knowledges = {
        arcana: ["elemental", "fey", "shadow", "construct", "warforged"],
        dungeoneering: ["aberrant", "mournland"],
        nature: ["natural"],
        religion: ["immortal", "undead"]
    };
    const knowledgesLookup = {};
    _.each(_.pairs(knowledges), function (skillAndOrigins) {
        let skill = skillAndOrigins[0];
        let origins = skillAndOrigins[1];
        _.each(origins, function (origin) {
            knowledgesLookup[origin] = skill;
        });
    });

    const loopOverSelected = (msg, func) => {
        TokenOps.loopOverSelected(msg, function(token, index) {
            let charId = token.get("represents")
            if (charId) {
                let character = getObj("character", charId)
                func(token, character, index)
            }
            else {
                MasterplanCommon.debugOutput(token.get("name") + " was not linked to a character", msg.who)
            }
        })
    }

    const determineKnowledge = (msg) => {
        if (MasterplanCommon.shouldExitIfNotGM(msg)) {
            return;
        }

        if (MasterplanCommon.shouldExitIfNotSelected(msg)) {
            return;
        }

        let output = ""
        loopOverSelected(msg, function(token, character) {
            let charId = character.id
            let level = getAttrByName(charId, "level")
            let race = getAttrByName(charId, "race")

            let knowledge = undefined
            if (race) {
                let raceLower = race.toLowerCase();
                knowledge = _.find(_.keys(knowledgesLookup), function(creatureType) {
                    return raceLower.includes(creatureType);
                });
                if (knowledge) {
                    knowledge = knowledgesLookup[knowledge];
                }
                else {
                    knowledge = race;
                }
            }

            let name = character.get("name")
            if (level) {
                let diffData = difficultClass[level]
                if (diffData) {
                    output += "{{" + name + " = " + diffData.moderate +  "/" + diffData.hard + (knowledge !== undefined ? " (" + knowledge + ")" : "") + "}}"
                }
            }
        });
        MasterplanCommon.msgGM( "&{template:default}{{name=Knowledges}}" + output);
    }

    const spendHealingSurge = (msg, command) => {
        if (MasterplanCommon.shouldExitIfNotSelected(msg)) {
            return;
        }

        let bonus = 0
        if (command.options.length > 0) {
            bonusStr = command.options.shift()
            bonus = parseInt(bonusStr)
            if (isNaN(bonus)) {
                MasterplanCommon.chatOutput("I couldn't parse the bonus HP into a number.  I tried to parse: '" + bonusStr + "'")
                return;
            }
        }

        loopOverSelected(msg, function(token, character) {
            let charId = character.id
            let name = character.get("name")
            let surgesAttrList = findObjs({ type: 'attribute', characterid: charId, name: "surges"})
            let surgeValue = getAttrByName(charId, "surge-value")
            if (surgesAttrList.length > 0 && surgeValue) {
                let surgesAttr = surgesAttrList[0]
                let surges = surgesAttr.get("current")
                if (surges > 0) {
                    let hp = TokenOps.value(token, HP_BAR, false)
                    let hpMax = TokenOps.value(token, HP_BAR, true)
                    MasterplanCommon.debugOutput(name + ": HP/Max, Surges/Surge Value " + hp + "/" + hpMax + ", " + surges + "/" + surgeValue, msg.who)
                    if (hp < hpMax) {
                        // + is concatenate as soon a a string gets involved
                        let surgeInt = parseInt(surgeValue)
                        let hpInt = parseInt(hp)
                        let newHP = Math.min(hpInt + surgeInt + bonus, hpMax)
                        let newSurges = surges - 1

                        token.set(HP_BAR_VALUE, newHP)
                        surgesAttr.set("current", newSurges)
                        MasterplanCommon.chatOutput(name + " is now at " + newHP + "HP with " + newSurges + " healing surges remaining", msg.who)
                        TokenOps.applyBloodiedDeadEffect(token)
                    }
                    else {
                        MasterplanCommon.chatOutput(name + " is at max HP", msg.who)
                    }
                }
                else {
                    MasterplanCommon.chatOutput(name + " has no healing surges remaining", msg.who)
                }
            }
            else {
                MasterplanCommon.chatOutput("Could not find one of 'surges' or 'surge-value' for " + name, msg.who)
            }
        });
    }

    const warforgeResolve = (msg) => {
        if (MasterplanCommon.shouldExitIfNotSelected(msg)) {
            return;
        }

        loopOverSelected(msg, function(token, character) {
            let hp = TokenOps.value(token, HP_BAR, false)
            let hpMax = TokenOps.value(token, HP_BAR, true)
            let tempHP = TokenOps.value(token, TMP_HP_BAR, false)
            let level = parseInt(getAttrByName(character.id, "level"))
            let gains = 3 + Math.floor(level/2)
            if (tempHP < gains) {
                token.set(TMP_HP_BAR_VALUE, gains)
            }
            if (hp <= (hpMax / 2)) {
                let newHp = parseInt(hp) + gains
                token.set(HP_BAR_VALUE, Math.min(newHp, hpMax))
                TokenOps.applyBloodiedDeadEffect(token)
            }
        });
    }
    
    const findAttr = (charId, attrName) => {
        let result = findObjs({ type: 'attribute', characterid: charId, name: attrName})
        if (result.length > 0) {
            return result[0]
        }
        else return undefined
    }

    const setAttr = (charId, attr, value) => {
        MasterplanCommon.debugLog(attr + " setting")
        let currentAttr = findAttr(charId, attr)
        if (currentAttr) {
            MasterplanCommon.debugLog(attr + " found, setting now")
            currentAttr.set("current", value)
            return currentAttr;
        }
        else {
            MasterplanCommon.debugLog(attr + " found, setting now")
            return createObj("attribute", {
                name: attr,
                current: value,
                characterid: charId
            });
        }
    }

    const longrest = (msg) => {
        if (MasterplanCommon.shouldExitIfNotSelected(msg)) {
            return;
        }

        loopOverSelected(msg, function(token, character) {
            let charId = character.id
            let name = character.get("name")
            // Sort HP via the token
            let hpMax = TokenOps.value(token, HP_BAR, true)
            MasterplanCommon.debugOutput(name + ": resetting HP to " + hpMax + " and temp hp to 0", msg.who)
            token.set(HP_BAR_VALUE, hpMax)
            token.set(TMP_HP_BAR_VALUE, 0)            

            // reset surges
            let surgesAttr = findAttr(charId, "surges")
            let surgesMax = getAttrByName(character.id, 'surges', 'max')
            if (surgesAttr && surgesMax) {
                MasterplanCommon.debugOutput(name + ": resetting surges to " + surgesMax, msg.who)
                surgesAttr.set("current", surgesMax)
            }
            else {
                MasterplanCommon.debugOutput(name + ": Can't reset healing surges, could not find one of 'surges' or 'surges|max'", msg.who)
            }
        
            //reset action points
            MasterplanCommon.debugOutput(name + ": Action points reset to 1", msg.who)
            setAttr(charId, "action-points", 1);
            
            // reset powers
            let i = 1;
            let skipped = 0
            let final = 1;
            MasterplanCommon.debugOutput(name + ": Resetting powers", msg.who)
            for (i = 1; i <= 99; i++) {
                let powerExistsAttr = findAttr(charId, "power-" + i + "-name")
                if (powerExistsAttr) {
                    MasterplanCommon.debugLog(name + ": power " + i + " exists")
                    setAttr(charId, "power-" + i + "-used", 0);
                    skipped = 0;
                }
                else {
                    MasterplanCommon.debugLog(name + ": power " + i + " does not exist")
                    skipped++;    
                }
                if (skipped > 3) {
                    MasterplanCommon.debugLog(name + ": at index " + i + " have skipped 3 powers, so giving up")
                    final = i-3;
                    break;
                }
            }
            TokenOps.applyBloodiedDeadEffect(token)
            MasterplanCommon.chatOutput(character.get("name") + " has had a long rest, I reset powers up to " + final, msg.who)
        });
    }


    const shortRest = (msg) => {
        if (MasterplanCommon.shouldExitIfNotSelected(msg)) {
            return;
        }

        loopOverSelected(msg, function(token, character) {
            let charId = character.id
            let name = character.get("name")
            // Sort HP via the token
            token.set(TMP_HP_BAR_VALUE, 0)

            // reset surges
           
            // reset powers
            let i = 1;
            let skipped = 0
            let final = 1;
            MasterplanCommon.debugOutput(name + ": Resetting powers", msg.who)
            for (i = 1; i <= 99; i++) {
                let powerExistsAttr = findAttr(charId, "power-" + i + "-name")
                if (powerExistsAttr) {
                    MasterplanCommon.debugLog(name + ": power " + i + " exists")
                    let powerType = getAttrByName(charId, "power-" + i + "-useage")
                    if (powerType === "Encounter") {
                        MasterplanCommon.debugLog(name + ": power " + i + " is an encounter power - resetting")
                        setAttr(charId, "power-" + i + "-used", 0);
                    }
                    else {
                        MasterplanCommon.debugLog(name + ": skipping power " + i + " as is not an encounter power")
                    }
                    skipped = 0;
                }
                else {
                    MasterplanCommon.debugLog(name + ": power " + i + " does not exist")
                    skipped++;
                }
                if (skipped > 3) {
                    MasterplanCommon.debugLog(name + ": at index " + i + " have skipped 3 powers, so giving up")
                    final = i-3;
                    break;
                }
            }
            TokenOps.applyBloodiedDeadEffect(token)
            MasterplanCommon.chatOutput(character.get("name") + " has had a short rest, I reset powers up to " + final, msg.who)
        });
    }

    const handleMessage = (msg) => {
        // Exit if not an api command
        if (msg.type != "api") return;
        // Split the message into command and argument(s)
        let commandInfo =  MasterplanCommon.parseCommand(msg)
        let command = commandInfo.command

        if (command === knowledgeCommand) {
            determineKnowledge(msg);
        }

        if (command === spendingHealingSurgeCommand) {
            spendHealingSurge(msg, commandInfo);
        }

        if (command === warforgedResolveCommand) {
            warforgeResolve(msg);
        }
        
        if (command === longRestCommand) {
            longrest(msg);
        }
        
        if (command == shortRestCommand) {
            shortRest(msg)
        }
    }


    return {
        handleMessage,
        loopOverSelected
    };
})();

on('chat:message', CharacterOps.handleMessage);
